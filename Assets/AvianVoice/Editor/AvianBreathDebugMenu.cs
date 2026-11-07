#if UNITY_EDITOR
using AvianVoice.Embodiment.Avatars;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>Debug menus for the human avatar's chest breathing.</summary>
    static class AvianBreathDebugMenu
    {
        [MenuItem("Avian Voice/Embodiment/Debug/Load Task 2 Breathing (Play Mode)")]
        static void LoadBreathing()
        {
            var manager = Object.FindAnyObjectByType<AvianVoice.Tasks.TaskManager>();
            if (manager != null)
                manager.LoadTaskById("breath_sync");
            else
                Debug.LogWarning("[AvianVoice] No TaskManager in the running scene.");
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Measure Human Breathing (Play Mode)")]
        static void Measure()
        {
            var breathing = Object.FindAnyObjectByType<HumanBreathEmbodiment>();
            Debug.Log("[AvianVoice] Human breathing: " + (breathing != null ? breathing.Measure() : "no human avatar with HumanBreathEmbodiment (switch to Human first)"));
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Log Human Breathing Now (Play Mode)")]
        static void LogNow()
        {
            var breathing = Object.FindAnyObjectByType<HumanBreathEmbodiment>();
            if (breathing == null || breathing.ChestBone == null)
            {
                Debug.Log("[AvianVoice] Human breathing: not active");
                return;
            }

            Debug.Log("[AvianVoice] Human breathing now: emphasis " + breathing.Emphasis.ToString("0.00") + " chest scale " + breathing.ChestBone.localScale.ToString("F3"));
        }
    }
}
#endif
