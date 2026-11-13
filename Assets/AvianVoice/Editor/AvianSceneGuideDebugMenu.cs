#if UNITY_EDITOR
using System.Text.RegularExpressions;
using AvianVoice.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// What the participant is shown as text, for checks without the headset: where the guide card is (above the
    /// mirror or on the camera), and whether any text in the headset view names a note or a frequency.
    /// </summary>
    public static class AvianSceneGuideDebugMenu
    {
        // B3, F#4, Bb3 ... and "247 Hz".
        static readonly Regex NoteOrHz = new(@"(?<![A-Za-z])[A-G](#|b|♯|♭)?[0-8](?![0-9A-Za-z])|\d\s*Hz", RegexOptions.Compiled);

        [MenuItem("Avian Voice/Embodiment/Debug/Log Scene Guide (Play Mode)")]
        public static void LogSceneGuide()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AvianVoice] Log Scene Guide: enter Play mode first.");
                return;
            }

            var sb = new System.Text.StringBuilder("[AvianVoice] Scene guide: ");
            sb.Append(SceneGuide.DescribeState());
            var cam = Camera.main;
            if (cam != null)
                sb.Append(" | camera pos=").Append(cam.transform.position.ToString("F3")).Append(" euler=").Append(cam.transform.eulerAngles.ToString("F1"))
                  .Append(" uiLayerDrawn=").Append((cam.cullingMask & (1 << AvianVoice.Embodiment.BirdSceneRoles.UiLayer)) != 0);

            // Every text that is active in the scene (the headset shows the UI layer and world texts alike).
            var shown = 0;
            var withNote = 0;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (text == null || !text.enabled || string.IsNullOrEmpty(text.text))
                    continue;
                var canvas = text.GetComponentInParent<Canvas>();
                if (canvas != null && !canvas.enabled)
                    continue;
                shown++;
                if (!NoteOrHz.IsMatch(text.text))
                    continue;
                withNote++;
                sb.Append(" | NOTE NAME OR HZ in '").Append(Path(text.transform)).Append("': \"").Append(text.text.Replace("\n", " | ")).Append('"');
            }

            foreach (var text in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (text == null || !text.enabled || string.IsNullOrEmpty(text.text))
                    continue;
                var canvas = text.GetComponentInParent<Canvas>();
                if (canvas != null && !canvas.enabled)
                    continue;
                shown++;
                if (!NoteOrHz.IsMatch(text.text))
                    continue;
                withNote++;
                sb.Append(" | NOTE NAME OR HZ in '").Append(Path(text.transform)).Append("': \"").Append(text.text.Replace("\n", " | ")).Append('"');
            }

            sb.Append(" | textsShown=").Append(shown).Append(" textsWithNoteOrHz=").Append(withNote);
            Debug.Log(sb.ToString());
        }

        static bool viewerMoved;
        static Vector3 savedRigPosition;
        static Quaternion savedRigRotation;

        /// <summary>
        /// Without the headset: move and turn the XR rig (first call), put it back (second call), and log the guide
        /// card's world pose each time. A card fixed in the scene keeps its pose; a head-locked one moves with the rig.
        /// </summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Scene Guide - Move Viewer Test (Play Mode)")]
        public static void MoveViewerTest()
        {
            var poses = Application.isPlaying ? Object.FindAnyObjectByType<AvianVoice.Embodiment.XrRigTaskStartPoses>() : null;
            if (poses == null)
            {
                Debug.LogWarning("[AvianVoice] Move Viewer Test: enter Play mode in Avian_Lab_Embodiment first.");
                return;
            }

            var rig = poses.transform;
            if (!viewerMoved)
            {
                savedRigPosition = rig.position;
                savedRigRotation = rig.rotation;
                // Half a metre to the left, 0.3 m back, turned 25 degrees to the right, looking up 12 degrees.
                poses.ApplyPose(savedRigPosition + savedRigRotation * new Vector3(-0.5f, 0f, -0.3f), savedRigRotation * Quaternion.Euler(-12f, 25f, 0f));
                viewerMoved = true;
            }
            else
            {
                poses.ApplyPose(savedRigPosition, savedRigRotation);
                viewerMoved = false;
            }

            var cam = Camera.main;
            Debug.Log("[AvianVoice] Scene guide move-viewer test: viewer " + (viewerMoved ? "MOVED" : "restored")
                      + " rig pos=" + rig.position.ToString("F3") + " euler=" + rig.eulerAngles.ToString("F1")
                      + (cam != null ? " camera pos=" + cam.transform.position.ToString("F3") + " euler=" + cam.transform.eulerAngles.ToString("F1") : "")
                      + " | " + SceneGuide.DescribeState());
        }

        static string Path(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }
    }
}
#endif
