const mongoose = require('mongoose');

// Purely a history log for "who connected/disconnected when" — the live
// dashboard state (who's online right now) lives in memory in server.js,
// not in Mongo, so the WebSocket signaling path works even if Mongo is down.
const sessionLogSchema = new mongoose.Schema({
  playerId: { type: String, required: true },
  playerName: { type: String, required: true },
  event: { type: String, enum: ['connected', 'disconnected'], required: true },
  timestamp: { type: Date, default: Date.now },
});

module.exports = mongoose.model('SessionLog', sessionLogSchema);
