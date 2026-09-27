using UnityEngine;

namespace WebRTCStreamPackage.Runtime.SO
{
    [CreateAssetMenu(fileName = "StreamConfig", menuName = "WebRTC Stream SDK/Stream Config")]
    public class StreamConfig : ScriptableObject
    {
        [Header("Signaling Server")]
        [Tooltip("Full override, e.g. \"192.168.1.50:3000\" or \"ws://192.168.1.50:3000\". If set, this is used as-is and serverIP/serverPort below are ignored entirely. Leave blank to build the URL from serverIP + serverPort instead.")]
        public string serverUrl = "";

        [Tooltip("Used only when Server Url above is left blank.")]
        public string serverIP = "127.0.0.1";
        public int serverPort = 3000; //port

        [Tooltip("Leave blank to auto-generate from SystemInfo.deviceUniqueIdentifier")]
        public string playerName = "";

        [Header("Stream Quality")]
        public int captureWidth = 1920;
        public int captureHeight = 1080;
        public int targetFPS = 60;

        [Tooltip("Target bitrate in kbps, e.g. 8000 = 8 Mbps")]
        public int bitrateKbps = 8000;

        [Header("ICE Servers")]
        public string[] stunServers = { "stun:stun.l.google.com:19302" };

        [Header("Per-Player Telemetry / Control")]

        [Tooltip("How often to send registered telemetry providers' values over the data channel, in updates per second.")]
        public float telemetryRateHz = 10f;

        [Tooltip("If unchecked, incoming control commands from the remote/website side are ignored — telemetry still streams out either way. Can also be toggled at runtime via StreamSDK.SetRemoteControlEnabled().")]
        public bool allowRemoteControl = true;

        /// <summary>
        /// The base "ws://host:port" the client should actually connect to —
        /// serverUrl verbatim (normalized) if you set one, otherwise built from
        /// serverIP + serverPort. SignalingClient appends the "/signaling" path
        /// itself, so this does NOT include it.
        /// </summary>
        public string ResolvedUrl =>
            string.IsNullOrWhiteSpace(serverUrl)
                ? $"ws://{serverIP}:{serverPort}"
                : NormalizeUrl(serverUrl);

        private static string NormalizeUrl(string url)
        {
            url = url.Trim().TrimEnd('/');
            if (!url.Contains("://")) url = "ws://" + url;
            return url;
        }
    }
}