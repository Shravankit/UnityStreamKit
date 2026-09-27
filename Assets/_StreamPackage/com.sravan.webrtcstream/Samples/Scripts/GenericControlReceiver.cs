using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using WebRTCStreamPackage.Runtime;
using WebRTCStreamSDK;

namespace WebRTCStream
{
    [Serializable]
    public class ControlEvent : UnityEvent<string> { } // invoked with command.value

    /// <summary>
    /// A domain-agnostic receiver for StreamSDK.OnControlReceived — no vehicle,
    /// no NWH, no assumption about what a "target" means. Any other script can
    /// register interest in a specific target string at runtime and get called
    /// back with just that command's value, without this file ever needing to
    /// know that target exists.
    ///
    /// Two ways to react to a command:
    ///
    ///   1. From code — register a handler for a target:
    ///        receiver.RegisterHandler("throttleOverride", value =>
    ///        {
    ///            float throttle = float.Parse(value);
    ///            myVehicle.input.Vertical = throttle;
    ///        });
    ///
    ///   2. From the inspector — add an entry under "Inspector Bound Targets"
    ///      with the target name, then drag in a method (e.g. a UnityEvent<string>)
    ///      the same way you'd wire a Button.onClick.
    ///
    /// Both can be used at once; every matching handler and inspector event
    /// fires for a given target. Unhandled targets are logged as warnings so
    /// you notice a typo instead of silently dropping commands.
    ///
    /// Setup:
    ///   1. Add this component next to your StreamSDK component.
    ///   2. Assign streamSDK in the inspector.
    ///   3. Call RegisterHandler() from whatever script needs to react, or
    ///      wire the inspector list.
    /// </summary>
    [AddComponentMenu("WebRTC Stream SDK/Samples/Generic Control Receiver")]
    public class GenericControlReceiver : MonoBehaviour
    {
        [Serializable]
        private class InspectorBinding
        {
            public string target;
            public ControlEvent onReceived;
        }

        [SerializeField] private StreamSDK streamSDK;

        [Tooltip("Optional: wire specific target names to UnityEvents here, the same way you'd wire a Button.onClick. Fires alongside any code-registered handlers for the same target.")]
        [SerializeField] private List<InspectorBinding> inspectorBoundTargets = new List<InspectorBinding>();

        /// <summary>Fires for every command received, regardless of target — useful for logging/debugging.</summary>
        public event Action<ControlCommand> OnAnyCommand;

        private readonly Dictionary<string, Action<string>> _handlers = new Dictionary<string, Action<string>>();

        private void OnEnable()
        {
            if (streamSDK != null) streamSDK.OnControlReceived += HandleCommand;
        }

        private void OnDisable()
        {
            if (streamSDK != null) streamSDK.OnControlReceived -= HandleCommand;
        }

        /// <summary>Bind to a StreamSDK from code instead of the inspector.</summary>
        public void Bind(StreamSDK sdk)
        {
            if (streamSDK != null && enabled) streamSDK.OnControlReceived -= HandleCommand;
            streamSDK = sdk;
            if (streamSDK != null && enabled) streamSDK.OnControlReceived += HandleCommand;
        }

        /// <summary>
        /// Register a callback for a specific command target. Multiple handlers
        /// can be registered for the same target — all of them fire.
        /// </summary>
        public void RegisterHandler(string target, Action<string> handler)
        {
            if (_handlers.TryGetValue(target, out var existing))
                _handlers[target] = existing + handler;
            else
                _handlers[target] = handler;
        }

        /// <summary>Remove a previously registered callback for a target.</summary>
        public void UnregisterHandler(string target, Action<string> handler)
        {
            if (!_handlers.TryGetValue(target, out var existing)) return;
            existing -= handler;
            if (existing == null) _handlers.Remove(target);
            else _handlers[target] = existing;
        }

        private void HandleCommand(ControlCommand command)
        {
            OnAnyCommand?.Invoke(command);

            bool handled = false;

            if (_handlers.TryGetValue(command.target, out var handler))
            {
                handler.Invoke(command.value);
                handled = true;
            }

            for (int i = 0; i < inspectorBoundTargets.Count; i++)
            {
                var binding = inspectorBoundTargets[i];
                if (binding.target == command.target)
                {
                    binding.onReceived?.Invoke(command.value);
                    handled = true;
                }
            }

            if (!handled)
                Debug.LogWarning($"[GenericControlReceiver] No handler registered for target '{command.target}' (value: '{command.value}').");
        }
    }
}