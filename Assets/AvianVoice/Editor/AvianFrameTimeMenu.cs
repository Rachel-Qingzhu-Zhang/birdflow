#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>Debug: log frame-time statistics of the running scene (smooth vs. stuttering).</summary>
    static class AvianFrameTimeMenu
    {
        [MenuItem("Avian Voice/Embodiment/Debug/Log Frame Times 3 s (Play Mode)")]
        static void LogFrameTimes()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AvianVoice] Enter Play mode first.");
                return;
            }

            AvianVoice.Study.FrameTimeProbe.Run(3f);
        }

        /// <summary>Test: stop mirroring the headset view into the Game view (saves a blit and the Game view draw each frame).</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Game View Mirror Off (test, Play Mode)")]
        static void GameViewMirrorOff()
        {
            UnityEngine.XR.XRSettings.gameViewRenderMode = UnityEngine.XR.GameViewRenderMode.None;
            Debug.Log("[AvianVoice] Game view mirror: None");
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Game View Mirror Left Eye (Play Mode)")]
        static void GameViewMirrorOn()
        {
            UnityEngine.XR.XRSettings.gameViewRenderMode = UnityEngine.XR.GameViewRenderMode.LeftEye;
            Debug.Log("[AvianVoice] Game view mirror: LeftEye");
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Mirror Off (test, Play Mode)")]
        static void MirrorOff()
        {
            var mirror = Object.FindAnyObjectByType<AvianVoice.Embodiment.MirrorBodySetup>();
            if (mirror != null)
                mirror.SetMirrorActive(false);
            Debug.Log("[AvianVoice] Mirror: " + (mirror != null ? "off" : "no MirrorBodySetup"));
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Mirror On (Play Mode)")]
        static void MirrorOn()
        {
            var mirror = Object.FindAnyObjectByType<AvianVoice.Embodiment.MirrorBodySetup>();
            if (mirror != null)
                mirror.SetMirrorActive(true);
            Debug.Log("[AvianVoice] Mirror: " + (mirror != null ? "on" : "no MirrorBodySetup"));
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Eye Resolution 0.7 (test, Play Mode)")]
        static void EyeResolutionLow()
        {
            UnityEngine.XR.XRSettings.eyeTextureResolutionScale = 0.7f;
            Debug.Log("[AvianVoice] Eye texture resolution scale: 0.7");
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Eye Resolution 1.0 (Play Mode)")]
        static void EyeResolutionFull()
        {
            UnityEngine.XR.XRSettings.eyeTextureResolutionScale = 1f;
            Debug.Log("[AvianVoice] Eye texture resolution scale: 1.0");
        }

        /// <summary>Test: the Game view's own VSync (editor present, monitor refresh) can pace the whole editor loop below the headset rate.</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Game View VSync Off (test)")]
        static void GameViewVSyncOff() => SetGameViewVSync(false);

        [MenuItem("Avian Voice/Embodiment/Debug/Game View VSync On (test)")]
        static void GameViewVSyncOn() => SetGameViewVSync(true);

        static void SetGameViewVSync(bool enabled)
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (type == null)
            {
                Debug.LogWarning("[AvianVoice] UnityEditor.GameView type not found.");
                return;
            }

            var property = type.GetProperty("vSyncEnabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var count = 0;
            foreach (var window in Resources.FindObjectsOfTypeAll(type))
            {
                if (property == null)
                    break;
                var before = property.GetValue(window);
                property.SetValue(window, enabled);
                Debug.Log("[AvianVoice] Game view '" + window.name + "' vSyncEnabled: " + before + " -> " + property.GetValue(window));
                count++;
            }

            if (property == null)
                Debug.LogWarning("[AvianVoice] GameView.vSyncEnabled not found in this Unity version.");
            else if (count == 0)
                Debug.Log("[AvianVoice] No Game view open.");
        }

        /// <summary>Test: maximize the Game view so no other editor window (Scene view, Inspector) repaints during Play.</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Maximize Game View (test)")]
        static void MaximizeGameView() => SetGameViewMaximized(true);

        [MenuItem("Avian Voice/Embodiment/Debug/Restore Game View (test)")]
        static void RestoreGameView() => SetGameViewMaximized(false);

        static void SetGameViewMaximized(bool maximized)
        {
            Debug.Log("[AvianVoice] Game view maximized: " + (AvianGameViewUtility.SetMaximized(maximized) ? maximized.ToString() : "no Game view"));
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Log Mirror State (Play Mode)")]
        static void LogMirrorState()
        {
            var mirror = Object.FindAnyObjectByType<AvianVoice.Embodiment.MirrorBodySetup>();
            var sb = new System.Text.StringBuilder("[AvianVoice] Mirror state: " + (mirror != null ? mirror.DescribeReflection() : "no MirrorBodySetup"));
            foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (cam == null)
                    continue;
                sb.Append("\n  camera '").Append(cam.name).Append("' enabled=").Append(cam.enabled).Append(" active=").Append(cam.gameObject.activeInHierarchy)
                  .Append(" scene=").Append(cam.gameObject.scene.name).Append(" hide=").Append(cam.hideFlags).Append(" far=").Append(cam.farClipPlane)
                  .Append(" cull0=").Append(cam.layerCullDistances[0]).Append(" type=").Append(cam.cameraType).Append(" target=").Append(cam.targetTexture != null ? cam.targetTexture.name : "-");
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>Test: close the experimenter panel to see whether its repaint costs frames (reopen from the menu).</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Close Experimenter Panel (test)")]
        static void ClosePanel()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<AvianExperimenterWindow>())
                window.Close();
            Debug.Log("[AvianVoice] Experimenter panel closed (Avian Voice / Experimenter Panel reopens it).");
        }
    }
}
#endif
