require("dotenv").config();

const express = require("express");
const http = require("http");
const cors = require("cors");
const mongoose = require("mongoose");
const { WebSocketServer, WebSocket } = require("ws");
const { randomUUID } = require("crypto");

const SessionLog = require("./models/SessionLog");

const PORT = process.env.PORT || 3000;
const MONGODB_URI = process.env.MONGODB_URI || "";

const app = express();
app.use(cors());
app.use(express.json());

// ── In-memory registries — this is what actually drives the dashboard ──────
// in real time. Mongo (below) is only for a persistent history log; the
// signaling path works fine even if Mongo is never configured.
const players = new Map(); // playerId -> { ws, name, lastOffer, viewerWs }
const viewers = new Set(); // connected browser dashboard sockets

function getPlayerList() {
  return Array.from(players.entries()).map(([playerId, p]) => ({
    playerId,
    name: p.name,
  }));
}

function broadcastPlayerList() {
  const payload = JSON.stringify({ type: "players", players: getPlayerList() });
  for (const viewerWs of viewers) {
    if (viewerWs.readyState === WebSocket.OPEN) viewerWs.send(payload);
  }
}

async function logEvent(playerId, name, event) {
  if (mongoose.connection.readyState !== 1) return;
  try {
    await SessionLog.create({ playerId, playerName: name, event });
  } catch (err) {
    console.error("[Mongo] failed to log session event:", err.message);
  }
}

// ── REST ─────────────────────────────────────────────────────────────────
app.get("/api/players", (req, res) => {
  res.json(getPlayerList());
});

app.get("/api/history", async (req, res) => {
  if (mongoose.connection.readyState !== 1) return res.json([]);
  const logs = await SessionLog.find().sort({ timestamp: -1 }).limit(100);
  res.json(logs);
});

app.get("/api/health", (req, res) => {
  res.json({
    ok: true,
    playersConnected: players.size,
    viewersConnected: viewers.size,
  });
});

// ── Signaling (WebRTC handshake relay only — video/telemetry/control all ──
// travel peer-to-peer over WebRTC once connected; the server never sees them)
const server = http.createServer(app);
const wss = new WebSocketServer({ server, path: "/signaling" });

wss.on("connection", (ws) => {
  ws.role = null; // 'player' | 'viewer', set on first relevant message
  ws.playerId = null;

  ws.on("message", (raw) => {
    let msg;
    try {
      msg = JSON.parse(raw.toString());
    } catch {
      return; // ignore malformed frames
    }

    switch (msg.type) {
      // ── Unity player side ──────────────────────────────────────────
      case "register_player": {
        const playerId = randomUUID();
        ws.role = "player";
        ws.playerId = playerId;
        players.set(playerId, {
          ws,
          name: msg.name || playerId,
          lastOffer: null,
          viewerWs: null,
        });
        ws.send(JSON.stringify({ type: "registered", playerId }));
        broadcastPlayerList();
        logEvent(playerId, msg.name || playerId, "connected");
        break;
      }

      case "offer": {
        const player = players.get(msg.playerId);
        if (!player) return;
        player.lastOffer = msg.sdp; // buffered so a viewer connecting later still gets it
        if (player.viewerWs && player.viewerWs.readyState === WebSocket.OPEN) {
          player.viewerWs.send(
            JSON.stringify({
              type: "offer",
              playerId: msg.playerId,
              sdp: msg.sdp,
            }),
          );
        }
        break;
      }

      case "ice_candidate": {
        // Relay verbatim to whichever side didn't send it.
        const player = players.get(msg.playerId);
        if (!player) return;
        if (
          ws.role === "player" &&
          player.viewerWs &&
          player.viewerWs.readyState === WebSocket.OPEN
        ) {
          player.viewerWs.send(JSON.stringify(msg));
        } else if (
          ws.role === "viewer" &&
          player.ws &&
          player.ws.readyState === WebSocket.OPEN
        ) {
          player.ws.send(JSON.stringify(msg));
        }
        break;
      }

      // ── Browser dashboard side ─────────────────────────────────────
      case "viewer_hello": {
        ws.role = "viewer";
        viewers.add(ws);
        ws.send(JSON.stringify({ type: "players", players: getPlayerList() }));
        break;
      }

      case "watch": {
        const player = players.get(msg.playerId);
        if (!player) {
          ws.send(
            JSON.stringify({
              type: "error",
              playerId: msg.playerId,
              message: `Player ${msg.playerId} is not connected.`,
            }),
          );
          return;
        }
        player.viewerWs = ws;
        if (player.lastOffer) {
          ws.send(
            JSON.stringify({
              type: "offer",
              playerId: msg.playerId,
              sdp: player.lastOffer,
            }),
          );
        }
        break;
      }

      case "answer": {
        const player = players.get(msg.playerId);
        if (player?.ws && player.ws.readyState === WebSocket.OPEN) {
          player.ws.send(
            JSON.stringify({
              type: "answer",
              playerId: msg.playerId,
              sdp: msg.sdp,
            }),
          );
        }
        break;
      }

      case "unwatch": {
        const player = players.get(msg.playerId);
        if (player && player.viewerWs === ws) player.viewerWs = null;
        break;
      }

      default:
        console.warn("[signaling] unknown message type:", msg.type);
    }
  });

  ws.on("close", () => {
    if (ws.role === "player" && ws.playerId) {
      const player = players.get(ws.playerId);
      players.delete(ws.playerId);
      broadcastPlayerList();
      logEvent(ws.playerId, player?.name || ws.playerId, "disconnected");
    }
    if (ws.role === "viewer") {
      viewers.delete(ws);
      // Free up any player this viewer was watching so someone else can attach.
      for (const player of players.values()) {
        if (player.viewerWs === ws) player.viewerWs = null;
      }
    }
  });
});

async function start() {
  if (MONGODB_URI) {
    try {
      await mongoose.connect(MONGODB_URI);
      console.log("[Mongo] connected");
    } catch (err) {
      console.error(
        "[Mongo] connection failed, continuing without history logging:",
        err.message,
      );
    }
  } else {
    console.log("[Mongo] MONGODB_URI not set — skipping history logging.");
  }

  server.listen(PORT, () => {
    console.log(`Signaling + API server listening on :${PORT}`);
    console.log(`  Unity StreamConfig.serverUrl -> ws://<this-host>:${PORT}`);
    console.log(`  Dashboard REST base          -> http://<this-host>:${PORT}`);
  });
}

start();
