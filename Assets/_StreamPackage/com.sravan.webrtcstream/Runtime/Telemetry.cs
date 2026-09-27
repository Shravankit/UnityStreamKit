using System;
using System.Collections.Generic;

namespace WebRTCStreamPackage.Runtime
{
    [Serializable]
    public struct TelemetryField
    {
        public string name;
        public string value;

        public TelemetryField(string name, string value)
        {
            this.name = name;
            this.value = value;
        }
    }

    //interface 
    public interface IStreamTelemetryProvider
    {
        IEnumerable<TelemetryField> GetTelemetryFields();
    }

    public struct ControlCommand
    {
        public string target;
        public string value;
    }
}