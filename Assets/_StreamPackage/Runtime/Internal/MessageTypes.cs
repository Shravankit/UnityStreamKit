using System;
using System.Collections.Generic;
using UnityEngine;

namespace WebRTCStreamPackage.Runtime.Internal
{
    [Serializable] internal class BaseMsg { public string type; }
    [Serializable] internal class RegisterMsg { public string type; public string name; }
    [Serializable] internal class RegisteredMsg { public string type; public string playerId; }
    [Serializable] internal class SdpMsg { public string type; public string playerId; public string sdp; }

    [Serializable]
    internal class IceMsg
    {
        public string type;
        public string playerId;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    [Serializable]
    internal class TelemetryPacket
    {
        public string type = "telemetry";
        public string playerId;
        public List<TelemetryField> fields = new List<TelemetryField>();
    }

    [Serializable]
    internal class ControlPacket
    {
        public string type; // expected "control"
        public string target;
        public string value;
    }
}