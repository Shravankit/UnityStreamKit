using UnityEngine;

namespace WebRTCStreamPackage.Runtime.SO
{
    [CreateAssetMenu(fileName = "StreamConfig", menuName = "WebRTC Stream SDK/Stream Config")]
    public class StreamConfig : ScriptableObject
    {
        [Header("Signaling Server")]
        public string serverIP = "server.ip";
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
    }
}