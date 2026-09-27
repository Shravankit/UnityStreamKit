using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace WebRTCStreamSDK.Editor
{
    [InitializeOnLoad]
    internal static class DependencyInstaller
    {
        private const string SessionFlag = "WebRTCStreamSDK.DependencyCheckDone";

        private static readonly (string id, string source)[] Required =
        {
            ("com.unity.webrtc", "com.unity.webrtc"),
            ("com.endel.nativewebsocket", "https://github.com/endel/NativeWebSocket.git#upm"),
        };

        private static ListRequest _listRequest;
        private static AddRequest _addRequest;
        private static Queue<string> _toInstall;

        static DependencyInstaller()
        {
            if (SessionState.GetBool(SessionFlag, false)) return;
            SessionState.SetBool(SessionFlag, true);
            EditorApplication.delayCall += BeginCheck;
        }
        private static void BeginCheck()
        {
            _listRequest = Client.List(true, false); // offline list of what's already installed
            EditorApplication.update += PollList;
        }

        private static void PollList()
        {
            if (_listRequest == null || !_listRequest.IsCompleted) return;
            EditorApplication.update -= PollList;

            var installedIds = new HashSet<string>();
            if (_listRequest.Status == StatusCode.Success)
            {
                foreach (var pkg in _listRequest.Result)
                    installedIds.Add(pkg.name);
            }
            else
            {
                Debug.LogWarning("[WebRTCStreamSDK] Could not read installed packages list; skipping auto-dependency check.");
                return;
            }

            _toInstall = new Queue<string>();
            foreach (var (id, source) in Required)
            {
                if (!installedIds.Contains(id))
                    _toInstall.Enqueue(source);
            }

            InstallNext();
        }

        // Package Manager only reliably handles one Add request at a time,
        // so missing dependencies are installed one after another, not in parallel.
        private static void InstallNext()
        {
            if (_toInstall == null || _toInstall.Count == 0) return;

            var source = _toInstall.Dequeue();
            Debug.Log($"[WebRTCStreamSDK] Installing required dependency: {source}");
            _addRequest = Client.Add(source);
            EditorApplication.update += PollAdd;
        }

        private static void PollAdd()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;
            EditorApplication.update -= PollAdd;

            if (_addRequest.Status == StatusCode.Failure)
            {
                Debug.LogWarning(
                    $"[WebRTCStreamSDK] Auto-install failed for a required dependency: {_addRequest.Error?.message}. " +
                    "Please add it manually via Window > Package Manager.");
            }

            InstallNext();
        }
    }
}