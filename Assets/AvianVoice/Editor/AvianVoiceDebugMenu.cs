#if UNITY_EDITOR
using AvianVoice.Audio;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>Debug: push a synthetic voice with a known pitch into the analyzer to compare the bird and human feedback voices.</summary>
    static class AvianVoiceDebugMenu
    {
        [MenuItem("Avian Voice/Embodiment/Debug/Test Voice Pitch: steady 330 Hz 1.5 s (Play Mode)")]
        static void Steady() => Inject(330f, 330f, 1.5f);

        [MenuItem("Avian Voice/Embodiment/Debug/Test Voice Pitch: speech-like glide 120-240 Hz 1.5 s (Play Mode)")]
        static void Glide() => Inject(120f, 240f, 1.5f);

        [MenuItem("Avian Voice/Embodiment/Debug/Test Voice Pitch: sung glide 330-494 Hz 1.5 s (Play Mode)")]
        static void SungGlide() => Inject(330f, 494f, 1.5f);

        static void Inject(float from, float to, float seconds)
        {
            var mapper = BirdVoiceMapper.Find();
            if (!Application.isPlaying || mapper == null)
            {
                Debug.LogWarning("[AvianVoice] Enter Play mode first.");
                return;
            }

            mapper.DebugInjectVoice(from, to, seconds);
            Debug.Log("[AvianVoice] Voice pitch test: " + from + " -> " + to + " Hz for " + seconds + " s (feedback " + mapper.Feedback + ").");
        }
    }
}
#endif
