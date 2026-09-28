# Unity WebRTC Stream Dashboard (MERN)

A dashboard for watching video + live telemetry from any number of Unity apps
running `com.sravan.webrtcstream`, and sending control commands back to each
one — no per-vehicle assumptions, works with whatever fields/targets your
Unity side reports.

- **Server** (Node/Express + `ws` + Mongoose): the WebRTC *signaling* relay
  — it only shuttles SDP offers/answers and ICE candidates between each Unity
  app and whichever browser tab is watching it. Once a peer connection is up,
  video and the telemetry/control data channel go **peer-to-peer** — the
  server never touches that traffic. Mongo is optional and only used to log
  a connect/disconnect history; the live dashboard works without it.
- **Client** (React + Vite): lists connected Unity apps in real time, lets
  you "Watch" any number of them at once (each gets its own video tile),
  shows their live telemetry table, and has a form to send `{target, value}`
  control commands down that stream's data channel.

## How the pieces line up with the Unity package

| This project | Unity package |
|---|---|
| `server.js` WebSocket at `/signaling` | `StreamConfig.serverUrl` (or `serverIP`/`serverPort`) — point it at `ws://<this-server-host>:<PORT>` |
| `register_player` / `registered` | `SignalingClient.Register()` |
| `offer` / `answer` / `ice_candidate` | `WebRTCSession` SDP + ICE exchange |
| Data channel `{type:"telemetry", playerId, fields}` | `GenericTelemetryProvider` / `TelemetryPacket` |
| Data channel `{type:"control", target, value}` (client → Unity) | `GenericControlReceiver` / `ControlPacket` |

Because signaling only relays SDP/ICE, you don't need to change anything in
the Unity-side `SignalingClient`/`StreamConfig` you already have — just make
sure `ResolvedUrl` points at this server.

## Run it

**Server:**
```bash
cd server
cp .env.example .env   # edit PORT / MONGODB_URI if needed
npm install
npm run dev             # or: npm start
```

**Client:**
```bash
cd client
cp .env.example .env   # point VITE_SIGNALING_URL at the server above
npm install
npm run dev
```

Open the printed Vite URL (usually `http://localhost:5173`). As soon as a
Unity app calls `StartStreaming(...)` against this server's `ws://` address,
it'll show up in the "Connected Unity apps" list — click **Watch** to bring
up its video + telemetry + control panel.

## Known limitations (inherited from the Unity SDK's design)

- **One viewer per stream at a time.** Each Unity `StreamSDK` instance is one
  peer connection — if a second browser tab tries to `watch` the same
  player while another is already attached, the new one takes over the
  `viewerWs` slot server-side, but the *existing* browser tab's peer
  connection is left dangling (it'll just stop receiving further ICE/answers
  routed its way). If you need true multi-viewer fan-out per stream, that's
  an SFU-shaped problem — this relay design doesn't do it.
- **No reconnect logic in the server.** If a Unity app's WebSocket drops, it
  disappears from the list on next `broadcastPlayerList()` — reconnecting
  Unity-side (e.g. via `GenericStreamController`'s `autoReconnect`) will
  register as a *new* `playerId`, not resume the old one.
- **Control commands aren't validated against what Unity expects.** The
  dashboard will happily send any `target`/`value` string pair — whether
  Unity's `GenericControlReceiver` has a handler registered for that target
  is entirely up to your Unity-side code.
