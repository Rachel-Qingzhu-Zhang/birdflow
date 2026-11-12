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

        [MenuItem("Avian Voice/Embodiment/Debug/Hold Human Breathing: Full Inhale (Play Mode)")]
        static void HoldInhale() => Hold(1f, "held at full inhale");

        [MenuItem("Avian Voice/Embodiment/Debug/Hold Human Breathing: Exhale (Play Mode)")]
        static void HoldExhale() => Hold(0f, "held at exhale");

        [MenuItem("Avian Voice/Embodiment/Debug/Hold Human Breathing: Off, Follow the Breath (Play Mode)")]
        static void HoldOff() => Hold(-1f, "follows the breath signal again");

        static void Hold(float emphasis, string what)
        {
            var breathing = Object.FindAnyObjectByType<HumanBreathEmbodiment>();
            if (breathing == null)
            {
                Debug.Log("[AvianVoice] Human breathing: no human avatar with HumanBreathEmbodiment (switch to Human first)");
                return;
            }

            breathing.SetPreviewEmphasis(emphasis);
            Debug.Log("[AvianVoice] Human breathing " + what + ".");
        }
    }
}
#endif
