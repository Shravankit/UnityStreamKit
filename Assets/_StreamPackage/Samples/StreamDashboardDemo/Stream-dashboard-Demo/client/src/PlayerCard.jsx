import { useEffect, useRef, useState } from "react";

const ICE_SERVERS = [{ urls: "stun:stun.l.google.com:19302" }];

export default function PlayerCard({
  playerId,
  name,
  sendSignal,
  registerHandler,
  onClose,
}) {
  const videoRef = useRef(null);
  const pcRef = useRef(null);
  const dataChannelRef = useRef(null);
  const [connectionState, setConnectionState] = useState("connecting");
  const [telemetry, setTelemetry] = useState({});
  const [target, setTarget] = useState("throttleOverride");
  const [value, setValue] = useState("");
  const [stats, setStats] = useState({
    fps: null,
    latencyMs: null,
    rttMs: null,
  });

  useEffect(() => {
    const pc = new RTCPeerConnection({ iceServers: ICE_SERVERS });
    pcRef.current = pc;

    pc.ontrack = (event) => {
      if (videoRef.current) videoRef.current.srcObject = event.streams[0];
    };

    pc.onicecandidate = (event) => {
      if (event.candidate) {
        sendSignal({
          type: "ice_candidate",
          playerId,
          candidate: event.candidate.candidate,
          sdpMid: event.candidate.sdpMid,
          sdpMLineIndex: event.candidate.sdpMLineIndex,
        });
      }
    };

    pc.onconnectionstatechange = () => setConnectionState(pc.connectionState);

    // Unity is always the offerer and opens the "telemetry" data channel
    // itself — the browser just receives it here, it never creates one.
    pc.ondatachannel = (event) => {
      const channel = event.channel;
      dataChannelRef.current = channel;
      channel.onmessage = (msgEvent) => {
        try {
          const packet = JSON.parse(msgEvent.data);
          if (packet.type === "telemetry") {
            const fields = {};
            for (const f of packet.fields) fields[f.name] = f.value;
            setTelemetry(fields);
          }
        } catch {
          /* ignore malformed packets */
        }
      };
    };

    const unregister = registerHandler(playerId, async (msg) => {
      if (msg.type === "offer") {
        await pc.setRemoteDescription({ type: "offer", sdp: msg.sdp });
        const answer = await pc.createAnswer();
        await pc.setLocalDescription(answer);
        sendSignal({ type: "answer", playerId, sdp: answer.sdp });
      } else if (msg.type === "ice_candidate") {
        try {
          await pc.addIceCandidate({
            candidate: msg.candidate,
            sdpMid: msg.sdpMid,
            sdpMLineIndex: msg.sdpMLineIndex,
          });
        } catch (err) {
          console.warn("Failed to add ICE candidate", err);
        }
      } else if (msg.type === "error") {
        console.error(`[${playerId}]`, msg.message);
      }
    });

    // Ask the server for this player's stream — triggers the buffered offer
    // (or the next one Unity sends) to come back to us.
    sendSignal({ type: "watch", playerId });

    // ── Live FPS / latency, read straight from the peer connection ────────
    // FPS: browser-reported framesPerSecond on the video inbound-rtp report,
    // falling back to a manual delta of framesDecoded over time if a browser
    // doesn't expose it.
    // Latency: two numbers, since "latency" means different things —
    //   - rttMs: network round-trip time from the active ICE candidate pair
    //   - latencyMs: average jitter-buffer delay per frame, i.e. roughly how
    //     long video sits buffered before it's shown — closer to what you'd
    //     perceive as "glass to glass" delay than raw RTT is.
    let prevFrame = { timestamp: 0, framesDecoded: 0 };

    const pollStats = async () => {
      if (pc.connectionState === "closed") return;
      let fps = null;
      let latencyMs = null;
      let rttMs = null;

      try {
        const report = await pc.getStats();
        report.forEach((entry) => {
          if (entry.type === "inbound-rtp" && entry.kind === "video") {
            if (typeof entry.framesPerSecond === "number") {
              fps = Math.round(entry.framesPerSecond);
            } else if (prevFrame.timestamp) {
              const dtSeconds = (entry.timestamp - prevFrame.timestamp) / 1000;
              const dFrames = entry.framesDecoded - prevFrame.framesDecoded;
              if (dtSeconds > 0) fps = Math.round(dFrames / dtSeconds);
            }
            if (
              entry.jitterBufferDelay != null &&
              entry.jitterBufferEmittedCount
            ) {
              latencyMs = Math.round(
                (entry.jitterBufferDelay / entry.jitterBufferEmittedCount) *
                  1000,
              );
            }
            prevFrame = {
              timestamp: entry.timestamp,
              framesDecoded: entry.framesDecoded,
            };
          }
          if (
            entry.type === "candidate-pair" &&
            entry.state === "succeeded" &&
            entry.currentRoundTripTime != null
          ) {
            rttMs = Math.round(entry.currentRoundTripTime * 1000);
          }
        });
      } catch {
        /* getStats can throw briefly right after close() during teardown */
      }

      setStats({ fps, latencyMs, rttMs });
    };

    const statsInterval = setInterval(pollStats, 1000);

    return () => {
      clearInterval(statsInterval);
      unregister();
      pc.close();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [playerId]);

  const sendControl = (e) => {
    e.preventDefault();
    const channel = dataChannelRef.current;
    if (channel && channel.readyState === "open") {
      channel.send(JSON.stringify({ type: "control", target, value }));
    } else {
      console.warn(
        "Data channel not open yet — the stream may still be connecting.",
      );
    }
  };

  return (
    <div className="player-card">
      <div className="player-card-header">
        <strong>{name}</strong>
        <span className={`pill ${connectionState}`}>{connectionState}</span>
        <button onClick={onClose} title="Stop watching">
          ✕
        </button>
      </div>

      <video
        ref={videoRef}
        autoPlay
        playsInline
        muted
        className="stream-video"
      />

      <div className="stream-stats">
        <span title="Decoded frames per second">FPS: {stats.fps ?? "—"}</span>
        <span title="Average time a frame sits in the jitter buffer before being shown">
          Latency: {stats.latencyMs != null ? `${stats.latencyMs} ms` : "—"}
        </span>
        <span title="Network round-trip time on the active ICE candidate pair">
          RTT: {stats.rttMs != null ? `${stats.rttMs} ms` : "—"}
        </span>
      </div>

      <div className="telemetry">
        <h4>Telemetry</h4>
        {Object.keys(telemetry).length === 0 ? (
          <p className="empty">No telemetry received yet.</p>
        ) : (
          <table>
            <tbody>
              {Object.entries(telemetry).map(([k, v]) => (
                <tr key={k}>
                  <td>{k}</td>
                  <td>{v}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <form className="control-form" onSubmit={sendControl}>
        <h4>Send control</h4>
        <input
          value={target}
          onChange={(e) => setTarget(e.target.value)}
          placeholder="target, e.g. throttleOverride"
        />
        <input
          value={value}
          onChange={(e) => setValue(e.target.value)}
          placeholder="value, e.g. 0.75"
        />
        <button type="submit">Send</button>
      </form>
    </div>
  );
}
