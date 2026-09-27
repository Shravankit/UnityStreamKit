using System;
using UnityEngine;
using NativeWebSocket;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using WebRTCStreamPackage.Runtime.SO;

namespace WebRTCStreamPackage.Runtime.Internal
{
    internal class SignalingClient
    {
        public event Action OnOpen;
        public event Action<string> OnRegistered;
        public event Action<string> OnAnswer;
        public event Action<string, string, int> OnIce;
        public event Action<string> OnError;
        public event Action OnClosed;

        WebSocket webSocket;

        readonly Queue<string> _msgQueue = new Queue<string>();
        readonly object _queueLock = new object();

        public bool IsOpen => webSocket != null && webSocket.State == WebSocketState.Open;

        /// <summary>
        /// Connects using the resolved base URL from a StreamConfig (either its
        /// explicit serverUrl override, or serverIP:serverPort built together) —
        /// this is the "change it directly in the config, client just uses it"
        /// path. "/signaling" is appended here so callers never need to know
        /// the path.
        /// </summary>
        public IEnumerator Connect(StreamConfig cfg) => Connect(cfg.ResolvedUrl);

        /// <summary>
        /// Connects to an explicit base URL, e.g. "ws://192.168.1.50:3000" or
        /// just "192.168.1.50:3000" (ws:// is added automatically if missing).
        /// </summary>
        public IEnumerator Connect(string baseUrl)
        {
            string url = baseUrl.Trim().TrimEnd('/');
            if (!url.Contains("://")) url = "ws://" + url;

            webSocket = new WebSocket($"{url}/signaling");

            webSocket.OnOpen += () => OnOpen?.Invoke();
            webSocket.OnMessage += bytes =>
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                lock (_queueLock) { _msgQueue.Enqueue(json); }
            };

            webSocket.OnError += e => OnError?.Invoke(e);
            webSocket.OnClose += _ => OnClosed?.Invoke();

            webSocket.Connect();
            Debug.Log("Connected");
            yield return null;
        }

        public void Tick()
        {
            webSocket?.DispatchMessageQueue();

            lock (_queueLock)
            {
                while (_msgQueue.Count > 0)
                    Route(_msgQueue.Dequeue());
            }
        }

        public void Register(string playerName) => Send(JsonUtility.ToJson(new RegisterMsg { type = "register_player", name = playerName }));

        public void SendOffer(string playerId, string sdp) => Send(JsonUtility.ToJson(new SdpMsg { type = "offer", playerId = playerId, sdp = sdp }));

        public void SendIce(string playerId, string candidate, string sdpMid, int sdpMLineIndex) =>
            Send(JsonUtility.ToJson(new IceMsg
            {
                type = "ice_candidate",
                playerId = playerId,
                candidate = candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = sdpMLineIndex
            }));


        public async Task Close()
        {
            if (webSocket != null) await webSocket.Close();
        }

        private void Send(string json)
        {
            if (IsOpen) webSocket.SendText(json);
        }

        private void Route(string json)
        {
            var msg = JsonUtility.FromJson<BaseMsg>(json);
            if (msg == null) return;

            switch (msg.type)
            {
                case "registered":
                    OnRegistered?.Invoke(JsonUtility.FromJson<RegisteredMsg>(json).playerId);
                    break;
                case "answer":
                    OnAnswer?.Invoke(JsonUtility.FromJson<SdpMsg>(json).sdp);
                    break;
                case "ice_candidate":
                    var ice = JsonUtility.FromJson<IceMsg>(json);
                    OnIce?.Invoke(ice.candidate, ice.sdpMid, ice.sdpMLineIndex);
                    break;
            }
        }
    }
}