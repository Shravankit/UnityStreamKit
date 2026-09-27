using System;
using UnityEngine;
using NativeWebSocket;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        public IEnumerator Connect(string ip, int port)
        {
            webSocket = new WebSocket($"ws://{ip}:{port}/signaling");

            webSocket.OnOpen += () => OnOpen?.Invoke();
            webSocket.OnMessage += bytes =>
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                lock (_queueLock) { _msgQueue.Enqueue(json); }
            };

            webSocket.OnError += e => OnError?.Invoke(e);
            webSocket.OnClose += _ => OnClosed?.Invoke();

            webSocket.Connect();
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