import { useEffect, useRef, useState, useCallback } from 'react';
import PlayerCard from './PlayerCard.jsx';

const SIGNALING_URL = import.meta.env.VITE_SIGNALING_URL || 'ws://localhost:3000/signaling';

export default function App() {
  const [players, setPlayers] = useState([]); // [{ playerId, name }]
  const [watching, setWatching] = useState(new Set()); // playerIds currently rendered as a card
  const [connected, setConnected] = useState(false);
  const wsRef = useRef(null);
  const messageHandlersRef = useRef(new Map()); // playerId -> callback for offer/ice/error routing

  useEffect(() => {
    const ws = new WebSocket(SIGNALING_URL);
    wsRef.current = ws;

    ws.onopen = () => {
      setConnected(true);
      ws.send(JSON.stringify({ type: 'viewer_hello' }));
    };

    ws.onclose = () => setConnected(false);
    ws.onerror = () => setConnected(false);

    ws.onmessage = (event) => {
      const msg = JSON.parse(event.data);
      if (msg.type === 'players') {
        setPlayers(msg.players);
        return;
      }
      // offer / ice_candidate / error all carry a playerId — route to that card.
      const handler = messageHandlersRef.current.get(msg.playerId);
      if (handler) handler(msg);
    };

    return () => ws.close();
  }, []);

  const sendSignal = useCallback((payload) => {
    if (wsRef.current?.readyState === WebSocket.OPEN) {
      wsRef.current.send(JSON.stringify(payload));
    }
  }, []);

  const registerHandler = useCallback((playerId, cb) => {
    messageHandlersRef.current.set(playerId, cb);
    return () => messageHandlersRef.current.delete(playerId);
  }, []);

  const startWatching = (playerId) => {
    setWatching((prev) => new Set(prev).add(playerId));
  };

  const stopWatching = (playerId) => {
    sendSignal({ type: 'unwatch', playerId });
    setWatching((prev) => {
      const next = new Set(prev);
      next.delete(playerId);
      return next;
    });
  };

  return (
    <div className="dashboard">
      <header>
        <h1>Unity Stream Dashboard</h1>
        <span className={`status ${connected ? 'ok' : 'down'}`}>
          {connected ? 'Signaling connected' : 'Signaling disconnected'}
        </span>
      </header>

      <section className="player-list">
        <h2>Connected Unity apps ({players.length})</h2>
        {players.length === 0 && <p className="empty">No Unity apps connected yet.</p>}
        <ul>
          {players.map((p) => (
            <li key={p.playerId}>
              <span>{p.name}</span>
              {watching.has(p.playerId) ? (
                <button onClick={() => stopWatching(p.playerId)}>Stop watching</button>
              ) : (
                <button onClick={() => startWatching(p.playerId)}>Watch</button>
              )}
            </li>
          ))}
        </ul>
      </section>

      <section className="stream-grid">
        {[...watching].map((playerId) => {
          const player = players.find((p) => p.playerId === playerId);
          if (!player) return null; // disconnected while being watched
          return (
            <PlayerCard
              key={playerId}
              playerId={playerId}
              name={player.name}
              sendSignal={sendSignal}
              registerHandler={registerHandler}
              onClose={() => stopWatching(playerId)}
            />
          );
        })}
      </section>
    </div>
  );
}
