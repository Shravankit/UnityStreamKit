using System;
using UnityEngine;
using Unity.WebRTC;
using System.Collections;
using WebRTCStreamSDK.Internal;
using System.Collections.Generic;
using WebRTCStreamPackage.Runtime;
using WebRTCStreamPackage.Runtime.SO;
using WebRTCStreamPackage.Runtime.Internal;

namespace WebRTCStreamSDK
{
    [AddComponentMenu("WebRTC Stream SDK/Stream SDK")]
    public class StreamSDK : MonoBehaviour
    {
        private static readonly List<StreamSDK> _all = new List<StreamSDK>();

        /// <summary>All active StreamSDK instances — use this when running multiple players at once.</summary>
        public static IReadOnlyList<StreamSDK> AllInstances => _all;

        /// <summary>Convenience accessor for the single-player case. The first instance created. Prefer direct references when running more than one player.</summary>
        public static StreamSDK Instance { get; private set; }

        [SerializeField] private StreamConfig config;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        public event Action<RTCPeerConnectionState> OnConnectionStateChanged;

        /// <summary>Fired whenever a control command arrives on this player's data channel from the remote/dashboard side.</summary>
        public event Action<ControlCommand> OnControlReceived;

        public bool IsStreaming { get; private set; }

        /// <summary>The signaling-assigned id for this player's current session, once registered.</summary>
        public string PlayerId => _playerId;

        /// <summary>Whether incoming control commands from the remote/website side are currently accepted for this player.</summary>
        public bool IsRemoteControlEnabled { get; private set; }

        private readonly List<IStreamTelemetryProvider> _telemetryProviders = new List<IStreamTelemetryProvider>();

        private SignalingClient _signaling;
        private WebRTCSession _session;
        private RenderTexture _rt;
        private MediaStream _mediaStream;
        private VideoStreamTrack _videoTrack;
        private string _playerId;
        private string _resolvedPlayerName;
        private Coroutine _webrtcUpdateRoutine;
        private Coroutine _telemetryRoutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            _all.Add(this);
        }

        private void Update()
        {
            _signaling?.Tick();
        }

        private void OnDestroy()
        {
            _all.Remove(this);
            if (Instance == this) Instance = _all.Count > 0 ? _all[0] : null;
            StopStreaming();
        }

        /// <summary>Begin streaming the given camera. Uses the inspector-assigned config unless overrideConfig is supplied.</summary>
        public void StartStreaming(Camera streamCamera, StreamConfig overrideConfig = null)
        {
            if (IsStreaming)
            {
                Debug.LogWarning("[WebRTCStreamSDK] Already streaming.");
                return;
            }
            if (streamCamera == null)
            {
                RaiseError("No camera assigned.");
                return;
            }

            var cfg = overrideConfig != null ? overrideConfig : config;
            if (cfg == null)
            {
                RaiseError("No StreamConfig assigned.");
                return;
            }

            IsStreaming = true;
            StartCoroutine(StreamRoutine(streamCamera, cfg));
        }

        public void StopStreaming()
        {
            if (!IsStreaming) return;
            IsStreaming = false;

            if (_webrtcUpdateRoutine != null) StopCoroutine(_webrtcUpdateRoutine);
            if (_telemetryRoutine != null) StopCoroutine(_telemetryRoutine);
            _telemetryRoutine = null;

            _session?.Dispose();
            _session = null;

            _videoTrack?.Dispose();
            _mediaStream?.Dispose();

            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }

            _signaling?.Close();
            _signaling = null;

            OnDisconnected?.Invoke();
        }

        /// <summary>
        /// Register a component whose values should be streamed to the remote
        /// viewer over this player's data channel (e.g. a VehicleTelemetryProvider).
        /// Safe to call before or after StartStreaming.
        /// </summary>
        public void RegisterTelemetryProvider(IStreamTelemetryProvider provider)
        {
            if (provider != null && !_telemetryProviders.Contains(provider))
                _telemetryProviders.Add(provider);
        }

        public void UnregisterTelemetryProvider(IStreamTelemetryProvider provider)
        {
            _telemetryProviders.Remove(provider);
        }

        /// <summary>Send a one-off control command to the remote/dashboard side (rarely needed — control usually flows the other way).</summary>
        public void SendControl(string target, string value)
        {
            var packet = new ControlPacket { type = "control", target = target, value = value };
            _session?.SendData(JsonUtility.ToJson(packet));
        }

        /// <summary>
        /// Enable or disable incoming website/dashboard control for this player at
        /// runtime — e.g. flip this off mid-session if a player disputes control,
        /// or gate it behind your own auth check. Overrides the StreamConfig default
        /// until StartStreaming is called again.
        /// </summary>
        public void SetRemoteControlEnabled(bool enabled)
        {
            IsRemoteControlEnabled = enabled;
        }

        private IEnumerator StreamRoutine(Camera streamCamera, StreamConfig cfg)
        {
            IsRemoteControlEnabled = cfg.allowRemoteControl;

            _rt = new RenderTexture(cfg.captureWidth, cfg.captureHeight, 0, RenderTextureFormat.BGRA32) { antiAliasing = 2 };
            _rt.Create();
            streamCamera.targetTexture = _rt;

            _videoTrack = new VideoStreamTrack(_rt);
            _mediaStream = new MediaStream();
            _mediaStream.AddTrack(_videoTrack);

            _webrtcUpdateRoutine = StartCoroutine(WebRTC.Update());

            _session = new WebRTCSession(this);
            _session.OnIceCandidate += candidate =>
                _signaling.SendIce(_playerId, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex ?? 0);
            _session.OnConnectionStateChange += state =>
            {
                OnConnectionStateChanged?.Invoke(state);
                if (state == RTCPeerConnectionState.Connected) OnConnected?.Invoke();
                if (state == RTCPeerConnectionState.Disconnected || state == RTCPeerConnectionState.Failed)
                    OnDisconnected?.Invoke();
            };
            _session.OnDataChannelOpen += () =>
            {
                if (_telemetryRoutine != null) StopCoroutine(_telemetryRoutine);
                _telemetryRoutine = StartCoroutine(TelemetryRoutine(cfg));
            };
            _session.OnDataChannelClose += () =>
            {
                if (_telemetryRoutine != null) StopCoroutine(_telemetryRoutine);
                _telemetryRoutine = null;
            };
            _session.OnDataChannelMessage += HandleDataChannelMessage;

            _signaling = new SignalingClient();
            _signaling.OnOpen += () => _signaling.Register(ResolvePlayerName(cfg));
            _signaling.OnRegistered += playerId =>
            {
                _playerId = playerId;
                _session.CreateAndSendOffer(_mediaStream, cfg.stunServers, cfg.bitrateKbps, cfg.targetFPS,
                    sdp => _signaling.SendOffer(_playerId, sdp));
            };
            _signaling.OnAnswer += sdp => _session.SetRemoteAnswer(sdp);
            _signaling.OnIce += (candidate, sdpMid, sdpMLineIndex) => _session.AddRemoteIce(candidate, sdpMid, sdpMLineIndex);
            _signaling.OnError += err => RaiseError(err);
            _signaling.OnClosed += () => OnDisconnected?.Invoke();

            yield return _signaling.Connect(cfg);
        }

        // Sends each registered provider's fields over the data channel at cfg.telemetryRateHz,
        // tagged with this player's id so a dashboard watching several players can tell them apart.
        private IEnumerator TelemetryRoutine(StreamConfig cfg)
        {
            var wait = new WaitForSeconds(1f / Mathf.Max(0.1f, cfg.telemetryRateHz));
            var packet = new TelemetryPacket { playerId = _playerId };

            while (true)
            {
                if (_telemetryProviders.Count > 0)
                {
                    packet.fields.Clear();
                    foreach (var provider in _telemetryProviders)
                    {
                        foreach (var field in provider.GetTelemetryFields())
                            packet.fields.Add(field);
                    }
                    _session.SendData(JsonUtility.ToJson(packet));
                }
                yield return wait;
            }
        }

        private void HandleDataChannelMessage(string json)
        {
            ControlPacket packet;
            try
            {
                packet = JsonUtility.FromJson<ControlPacket>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[WebRTCStreamSDK] Malformed data channel message: {e.Message}");
                return;
            }

            if (packet == null || packet.type != "control") return;

            if (!IsRemoteControlEnabled)
            {
                Debug.Log($"[WebRTCStreamSDK] Ignored control command '{packet.target}' — remote control is disabled for this player.");
                return;
            }

            OnControlReceived?.Invoke(new ControlCommand { target = packet.target, value = packet.value });
        }

        // Falls back to a per-instance generated name rather than the raw device id,
        // so multiple players on the same device/build don't collide with each other.
        private string ResolvePlayerName(StreamConfig cfg)
        {
            if (!string.IsNullOrEmpty(cfg.playerName)) return cfg.playerName;

            if (string.IsNullOrEmpty(_resolvedPlayerName))
                _resolvedPlayerName = $"{gameObject.name}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

            return _resolvedPlayerName;
        }

        private void RaiseError(string message)
        {
            Debug.LogError($"[WebRTCStreamSDK] {message}");
            OnError?.Invoke(message);
        }
    }
}
