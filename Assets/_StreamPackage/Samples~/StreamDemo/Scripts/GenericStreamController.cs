using UnityEngine;
using WebRTCStreamSDK;
using UnityEngine.Events;
using System.Collections;
using WebRTCStreamPackage.Runtime.SO;

namespace WebRTCStream
{
    [AddComponentMenu("WebRTC Stream SDK/Samples/Generic Stream Controller")]
    [RequireComponent(typeof(StreamSDK))]
    public class GenericStreamController : MonoBehaviour
    {
        [Header("Required")]
        [SerializeField] private StreamSDK streamSDK;
        [SerializeField] private Camera streamCamera;

        [Header("Optional override (leave null to use StreamSDK's inspector-assigned config)")]
        [SerializeField] private StreamConfig configOverride;

        [Header("Behavior")]
        [Tooltip("Start streaming automatically when this component becomes enabled.")]
        [SerializeField] private bool autoStartOnEnable = false;

        [Tooltip("If the connection drops or fails, keep retrying instead of just sitting disconnected.")]
        [SerializeField] private bool autoReconnect = false;

        [Tooltip("Seconds to wait between reconnect attempts.")]
        [SerializeField] private float reconnectDelaySeconds = 3f;

        [Header("Events (wire up UI, logging, anything)")]
        public UnityEvent onConnected;
        public UnityEvent onDisconnected;
        public UnityEvent<string> onError;

        /// <summary>True once the underlying StreamSDK reports a live connection.</summary>
        public bool IsConnected { get; private set; }

        /// <summary>True while a stream session is active (connecting or connected).</summary>
        public bool IsStreaming => streamSDK != null && streamSDK.IsStreaming;

        private Coroutine _reconnectRoutine;
        private bool _stoppedIntentionally;

        private void OnEnable()
        {
            if (streamSDK != null)
            {
                streamSDK.OnConnected += HandleConnected;
                streamSDK.OnDisconnected += HandleDisconnected;
                streamSDK.OnError += HandleError;
            }

            if (autoStartOnEnable) StartCoroutine(AutoStartNextFrame());
        }

        private void OnDisable()
        {
            _stoppedIntentionally = true;
            if (_reconnectRoutine != null) StopCoroutine(_reconnectRoutine);

            if (streamSDK != null)
            {
                streamSDK.OnConnected -= HandleConnected;
                streamSDK.OnDisconnected -= HandleDisconnected;
                streamSDK.OnError -= HandleError;
            }
        }

        /// <summary>Bind to a StreamSDK/Camera from code instead of the inspector.</summary>
        public void Bind(StreamSDK sdk, Camera cam)
        {
            if (streamSDK != null)
            {
                streamSDK.OnConnected -= HandleConnected;
                streamSDK.OnDisconnected -= HandleDisconnected;
                streamSDK.OnError -= HandleError;
            }

            streamSDK = sdk;
            streamCamera = cam;

            if (streamSDK != null && enabled)
            {
                streamSDK.OnConnected += HandleConnected;
                streamSDK.OnDisconnected += HandleDisconnected;
                streamSDK.OnError += HandleError;
            }
        }

        /// <summary>Start streaming the assigned camera. Safe to call from a UI button / UnityEvent.</summary>
        public void StartStream()
        {
            _stoppedIntentionally = false;

            if (streamSDK == null)
            {
                Debug.LogError("[GenericStreamController] No StreamSDK assigned.");
                return;
            }
            if (streamCamera == null)
            {
                Debug.LogError("[GenericStreamController] No Camera assigned.");
                return;
            }

            streamSDK.StartStreaming(streamCamera, configOverride);
        }

        /// <summary>Stop streaming. Safe to call from a UI button / UnityEvent.</summary>
        public void StopStream()
        {
            _stoppedIntentionally = true;
            if (_reconnectRoutine != null)
            {
                StopCoroutine(_reconnectRoutine);
                _reconnectRoutine = null;
            }
            streamSDK?.StopStreaming();
        }

        /// <summary>Swap the camera being streamed — stops and restarts the session.</summary>
        public void SetCamera(Camera cam)
        {
            streamCamera = cam;
            if (IsStreaming)
            {
                StopStream();
                StartStream();
            }
        }

        private void HandleConnected()
        {
            IsConnected = true;
            onConnected?.Invoke();
        }

        private void HandleDisconnected()
        {
            IsConnected = false;
            onDisconnected?.Invoke();

            if (autoReconnect && !_stoppedIntentionally && isActiveAndEnabled)
            {
                if (_reconnectRoutine != null) StopCoroutine(_reconnectRoutine);
                _reconnectRoutine = StartCoroutine(ReconnectAfterDelay());
            }
        }

        private void HandleError(string message)
        {
            onError?.Invoke(message);
        }

        private IEnumerator ReconnectAfterDelay()
        {
            yield return new WaitForSeconds(reconnectDelaySeconds);
            if (!_stoppedIntentionally && !IsStreaming)
                StartStream();
        }

        private IEnumerator AutoStartNextFrame()
        {
            yield return null; // let the scene finish activating first
            StartStream();
        }
    }
}