using System;
using System.Collections;
using UnityEngine;
using Unity.WebRTC;

namespace WebRTCStreamSDK.Internal
{
    /// <summary>
    /// Wraps RTCPeerConnection creation, offer/answer exchange and bitrate control.
    /// Needs a MonoBehaviour "runner" purely to host coroutines.
    /// </summary>
    internal class WebRTCSession
    {
        public event Action<RTCIceCandidate> OnIceCandidate;
        public event Action<RTCPeerConnectionState> OnConnectionStateChange;
        public event Action OnDataChannelOpen;
        public event Action OnDataChannelClose;
        public event Action<string> OnDataChannelMessage;

        private readonly MonoBehaviour _runner;
        private RTCPeerConnection _pc;
        private RTCDataChannel _dataChannel;

        public bool IsDataChannelOpen => _dataChannel != null && _dataChannel.ReadyState == RTCDataChannelState.Open;

        public WebRTCSession(MonoBehaviour runner)
        {
            _runner = runner;
        }

        public void CreateAndSendOffer(MediaStream stream, string[] stunServers, int bitrateKbps, int targetFPS, Action<string> onOfferReady)
        {
            _runner.StartCoroutine(CreateOfferRoutine(stream, stunServers, bitrateKbps, targetFPS, onOfferReady));
        }

        public void SetRemoteAnswer(string sdp)
        {
            _runner.StartCoroutine(SetRemoteAnswerRoutine(sdp));
        }

        public void AddRemoteIce(string candidate, string sdpMid, int sdpMLineIndex)
        {
            if (_pc == null) return;
            _pc.AddIceCandidate(new RTCIceCandidate(new RTCIceCandidateInit
            {
                candidate = candidate,
                sdpMid = sdpMid,
                sdpMLineIndex = (ushort?)sdpMLineIndex
            }));
        }

        /// <summary>Sends a string over the "telemetry" data channel, if it's open.</summary>
        public void SendData(string payload)
        {
            if (IsDataChannelOpen) _dataChannel.Send(payload);
        }

        public void Dispose()
        {
            _dataChannel?.Close();
            _dataChannel = null;
            _pc?.Dispose();
            _pc = null;
        }

        // NOTE: RTCSessionDescriptionAsyncOperation / RTCSetSessionDescriptionAsyncOperation
        // do NOT implement GetAwaiter() — always "yield return", never "await".
        private IEnumerator CreateOfferRoutine(MediaStream stream, string[] stunServers, int bitrateKbps, int targetFPS, Action<string> onOfferReady)
        {
            var config = new RTCConfiguration
            {
                iceServers = new[] { new RTCIceServer { urls = stunServers } }
            };
            _pc = new RTCPeerConnection(ref config);

            _pc.OnIceCandidate = candidate =>
            {
                if (candidate == null || string.IsNullOrEmpty(candidate.Candidate)) return;
                OnIceCandidate?.Invoke(candidate);
            };
            _pc.OnConnectionStateChange = state => OnConnectionStateChange?.Invoke(state);

            // Per-player control/telemetry channel, negotiated as part of the initial offer —
            // must be created before CreateOffer() so it's included in the SDP.
            _dataChannel = _pc.CreateDataChannel("telemetry");
            _dataChannel.OnOpen += () => OnDataChannelOpen?.Invoke();
            _dataChannel.OnClose += () => OnDataChannelClose?.Invoke();
            _dataChannel.OnMessage += bytes => OnDataChannelMessage?.Invoke(System.Text.Encoding.UTF8.GetString(bytes));

            foreach (var track in stream.GetTracks())
            {
                RTCRtpSender sender = _pc.AddTrack(track, stream);
                _runner.StartCoroutine(SetBitrateRoutine(sender, bitrateKbps, targetFPS));
            }

            RTCSessionDescriptionAsyncOperation offerOp = _pc.CreateOffer();
            yield return offerOp;
            if (offerOp.IsError)
            {
                Debug.LogError($"[WebRTCStreamSDK] CreateOffer failed: {offerOp.Error.message}");
                yield break;
            }

            RTCSessionDescription offerDesc = offerOp.Desc;
            RTCSetSessionDescriptionAsyncOperation setLocalOp = _pc.SetLocalDescription(ref offerDesc);
            yield return setLocalOp;
            if (setLocalOp.IsError)
            {
                Debug.LogError($"[WebRTCStreamSDK] SetLocalDescription failed: {setLocalOp.Error.message}");
                yield break;
            }

            onOfferReady?.Invoke(offerDesc.sdp);
        }

        private IEnumerator SetRemoteAnswerRoutine(string sdp)
        {
            if (_pc == null) yield break;

            var desc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = sdp };
            RTCSetSessionDescriptionAsyncOperation op = _pc.SetRemoteDescription(ref desc);
            yield return op;

            if (op.IsError)
                Debug.LogError($"[WebRTCStreamSDK] SetRemoteDescription failed: {op.Error.message}");
        }

        // NOTE: sender.SetParameters() returns an RTCError *struct* —
        // compare error.errorType to RTCErrorType.None, never the struct itself.
        private IEnumerator SetBitrateRoutine(RTCRtpSender sender, int bitrateKbps, int targetFPS)
        {
            yield return new WaitForSeconds(0.5f);

            RTCRtpSendParameters parameters = sender.GetParameters();
            if (parameters.encodings == null || parameters.encodings.Length == 0) yield break;

            foreach (var enc in parameters.encodings)
            {
                enc.maxBitrate = (ulong)(bitrateKbps * 1000);
                enc.minBitrate = (ulong)(bitrateKbps * 700);
                enc.maxFramerate = (uint)targetFPS;
            }

            RTCError error = sender.SetParameters(parameters);
            if (error.errorType != RTCErrorType.None)
                Debug.LogWarning($"[WebRTCStreamSDK] SetParameters: {error.message}");
        }
    }
}
