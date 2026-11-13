#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using AvianVoice.Embodiment.Avatars;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>Debug menus for the human avatar's chest breathing.</summary>
    static class AvianBreathDebugMenu
    {
        [MenuItem("Avian Voice/Embodiment/Debug/Load Task 1 Meet (Play Mode)")]
        static void LoadMeet() => LoadTask("meet");

        [MenuItem("Avian Voice/Embodiment/Debug/Load Task 2 Breathing (Play Mode)")]
        static void LoadBreathing() => LoadTask("breath_sync");

        [MenuItem("Avian Voice/Embodiment/Debug/Load Task 4 Rest (Play Mode)")]
        static void LoadRest() => LoadTask("reflection");

        static void LoadTask(string taskId)
        {
            var manager = Object.FindAnyObjectByType<AvianVoice.Tasks.TaskManager>();
            if (manager != null)
                manager.LoadTaskById(taskId);
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

            Debug.Log("[AvianVoice] Human breathing now: " + breathing.DescribeNow());
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

        /// <summary>
        /// Every human body standing with the arms down, at exhale and at full inhale, from the front (as in the
        /// mirror) and from the side, with the measured sizes in the log. Works outside Play, without the headset.
        /// Writes Temp/HumanBreathingTest/breathing.png (rows = bodies, columns = front exhale / front inhale /
        /// side exhale / side inhale).
        /// </summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Render Human Breathing (exhale and full inhale)")]
        static void RenderBreathing()
        {
            const int tileSize = 400;
            var tiles = new List<Texture2D>();
            var sb = new StringBuilder("[AvianVoice] HUMAN BREATHING (columns: front exhale / front inhale / side exhale / side inhale):");
            foreach (var asset in AvianEmbodimentSceneSetup.HumanAvatarAssetPaths)
            {
                var rig = HandRetargetSelfTest.Load(AvianEmbodimentSceneSetup.ToResourcesPath(asset), HandRetargetSelfTest.TestOrigin);
                if (rig == null)
                    continue;
                try
                {
                    HandRetargetSelfTest.SetMuscles(rig, m =>
                    {
                        foreach (var side in new[] { "Left", "Right" })
                        {
                            SetMuscle(m, side + " Arm Down-Up", -0.9f);
                            SetMuscle(m, side + " Arm Front-Back", 0.3f);
                            SetMuscle(m, side + " Forearm Stretch", 1f);
                            SetMuscle(m, side + " Upper Leg Front-Back", 0.6f);
                            SetMuscle(m, side + " Lower Leg Stretch", 1f);
                        }
                    });

                    var breathing = rig.go.AddComponent<HumanBreathEmbodiment>();
                    if (!breathing.PoseNow(0f))
                    {
                        sb.Append("\n  ").Append(rig.go.name).Append(": no Humanoid neck / arms");
                        continue;
                    }

                    sb.Append("\n  ").Append(rig.go.name).Append(": ").Append(breathing.Measure());
                    var hips = rig.animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    var target = hips + new Vector3(0f, 0.42f, 0f);
                    var front = hips + new Vector3(0f, 0.5f, 1.7f);
                    var fromSide = hips + new Vector3(1.7f, 0.5f, 0f);
                    foreach (var camera in new[] { front, fromSide })
                    {
                        foreach (var emphasis in new[] { 0f, 1f })
                        {
                            breathing.PoseNow(emphasis);
                            tiles.Add(HandRetargetSelfTest.RenderView(rig, new[] { rig }, camera, Quaternion.LookRotation(target - camera), 38f, tileSize, 0.05f));
                        }
                    }

                    breathing.PoseNow(0f);
                }
                finally
                {
                    HandRetargetSelfTest.Destroy(rig);
                }
            }

            Debug.Log(sb.ToString());
            if (tiles.Count == 0)
                return;
            var path = Path.Combine(Path.GetFullPath("Temp/HumanBreathingTest"), "breathing.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            HandRetargetSelfTest.SaveGrid(tiles, 4, path, "[AvianVoice] HUMAN BREATHING image: " + path);
            foreach (var t in tiles)
                Object.DestroyImmediate(t);
        }

        static void SetMuscle(float[] muscles, string name, float value)
        {
            var i = System.Array.IndexOf(HumanTrait.MuscleName, name);
            if (i >= 0)
                muscles[i] = value;
        }
    }
}
#endif
