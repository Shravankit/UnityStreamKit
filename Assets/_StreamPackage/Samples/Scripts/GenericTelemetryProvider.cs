using System;
using System.Collections.Generic;
using UnityEngine;
using WebRTCStreamPackage.Runtime;
using WebRTCStreamSDK;

namespace WebRTCStream
{
    ///   2. Assign streamSDK in the inspector (or leave it and call
    ///      Bind(streamSDK) from code if you'd rather wire it up yourself).
    ///   3. From any other script: GetComponent<GenericTelemetryProvider>().SetField(...)
    /// </summary>
    [AddComponentMenu("WebRTC Stream SDK/Samples/Generic Telemetry Provider")]
    public class GenericTelemetryProvider : MonoBehaviour, IStreamTelemetryProvider
    {
        [SerializeField] private StreamSDK streamSDK;

        // Static values set via SetField().
        private readonly Dictionary<string, string> _fields = new Dictionary<string, string>();

        // Live values re-evaluated every telemetry tick.
        private readonly Dictionary<string, Func<string>> _fieldSources = new Dictionary<string, Func<string>>();

        private void OnEnable()
        {
            if (streamSDK != null) streamSDK.RegisterTelemetryProvider(this);
        }

        private void OnDisable()
        {
            if (streamSDK != null) streamSDK.UnregisterTelemetryProvider(this);
        }

        /// <summary>Bind to a StreamSDK from code instead of the inspector.</summary>
        public void Bind(StreamSDK sdk)
        {
            if (streamSDK != null && enabled) streamSDK.UnregisterTelemetryProvider(this);
            streamSDK = sdk;
            if (streamSDK != null && enabled) streamSDK.RegisterTelemetryProvider(this);
        }

        /// <summary>Set/overwrite a static value. Call this whenever the value changes.</summary>
        public void SetField(string name, string value) => _fields[name] = value;

        /// <summary>Stop reporting a static field.</summary>
        public void RemoveField(string name) => _fields.Remove(name);

        /// <summary>
        /// Register a live getter for a field — called every telemetry tick so
        /// the reported value is always current. Use this for anything that
        /// changes continuously so you don't have to call SetField() yourself.
        /// </summary>
        public void SetFieldSource(string name, Func<string> getter) => _fieldSources[name] = getter;

        /// <summary>Stop reporting a live field.</summary>
        public void RemoveFieldSource(string name) => _fieldSources.Remove(name);

        /// <summary>Clear everything (static and live) — e.g. on scene reset.</summary>
        public void ClearAll()
        {
            _fields.Clear();
            _fieldSources.Clear();
        }

        public IEnumerable<TelemetryField> GetTelemetryFields()
        {
            foreach (var kvp in _fields)
                yield return new TelemetryField(kvp.Key, kvp.Value);

            foreach (var kvp in _fieldSources)
            {
                string value;
                try { value = kvp.Value(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GenericTelemetryProvider] Getter for '{kvp.Key}' threw: {e.Message}");
                    continue;
                }
                yield return new TelemetryField(kvp.Key, value);
            }
        }
    }

}