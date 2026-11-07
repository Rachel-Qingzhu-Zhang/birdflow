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
    }
}
#endif
