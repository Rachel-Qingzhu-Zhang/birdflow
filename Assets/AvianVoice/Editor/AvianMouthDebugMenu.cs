#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using AvianVoice.BodyTracking;
using AvianVoice.Embodiment.Avatars;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>Debug menus for the human avatar's mouth, which opens with the participant's voice.</summary>
    static class AvianMouthDebugMenu
    {
        [MenuItem("Avian Voice/Embodiment/Debug/Log Human Mouth Now (Play Mode)")]
        static void LogNow()
        {
            var mouth = Object.FindAnyObjectByType<HumanMouthEmbodiment>();
            Debug.Log("[AvianVoice] Human mouth now: " + (mouth != null ? mouth.DescribeNow() : "no human avatar shown (switch to Human first)"));
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Hold Human Mouth: Full Voice (Play Mode)")]
        static void HoldOpen() => Hold(1f, "held open (full voice)");

        [MenuItem("Avian Voice/Embodiment/Debug/Hold Human Mouth: Closed (Play Mode)")]
        static void HoldClosed() => Hold(0f, "held closed");

        [MenuItem("Avian Voice/Embodiment/Debug/Hold Human Mouth: Off, Follow the Voice (Play Mode)")]
        static void HoldOff() => Hold(-1f, "follows the voice again");

        static void Hold(float amount, string what)
        {
            var mouth = Object.FindAnyObjectByType<HumanMouthEmbodiment>();
            if (mouth == null)
            {
                Debug.Log("[AvianVoice] Human mouth: no human avatar shown (switch to Human first)");
                return;
            }

            mouth.SetPreview(amount);
            Debug.Log("[AvianVoice] Human mouth " + what + ".");
        }

        /// <summary>
        /// Every human body with the mouth closed, at the quietest voiced sound and at full voice: close-ups from
        /// the front and from the side, and the upper body from 1.5 m as the mirror shows it. Works outside Play,
        /// without the headset. Writes Temp/HumanMouthTest/mouth.png (rows = bodies; columns = front closed / soft /
        /// full, side closed / soft / full, mirror distance closed / full).
        /// </summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Render Human Mouth (closed, soft voice, full voice)")]
        static void RenderMouth()
        {
            const int tileSize = 360;
            var tiles = new List<Texture2D>();
            var sb = new StringBuilder("[AvianVoice] HUMAN MOUTH (columns: front closed / soft / full, side closed / soft / full, mirror distance closed / full):");
            foreach (var asset in AvianEmbodimentSceneSetup.HumanAvatarAssetPaths)
            {
                var rig = HandRetargetSelfTest.Load(AvianEmbodimentSceneSetup.ToResourcesPath(asset), HandRetargetSelfTest.TestOrigin);
                if (rig == null)
                    continue;

                HumanMouthEmbodiment mouth = null;
                try
                {
                    // The rest pose of the study (head and face from the skin bind pose), arms as loaded.
                    HumanoidTPose.Apply(rig.animator);
                    mouth = rig.go.AddComponent<HumanMouthEmbodiment>();
                    if (!mouth.PoseNow(0f))
                    {
                        sb.Append("\n  ").Append(rig.go.name).Append(": no jaw bone, the mouth stays closed");
                        continue;
                    }

                    var soft = mouth.SoftVoiceShare;
                    // One throw-away render: the first bake after the mouth interior was split off (a new shared
                    // mesh) comes back empty within the same editor frame.
                    Object.DestroyImmediate(HandRetargetSelfTest.RenderView(rig, new[] { rig }, rig.go.transform.position + Vector3.forward,
                        Quaternion.identity, 24f, 16, 0.05f));
                    var head = rig.animator.GetBoneTransform(HumanBodyBones.Head);
                    var jaw = mouth.JawBone;
                    // Around the mouth: below the head bone's eye line, at the face front.
                    var target = new Vector3(head.position.x, Mathf.Lerp(jaw.position.y, head.position.y, 0.5f) - 0.01f, head.position.z + 0.08f);
                    var front = target + new Vector3(0f, 0f, 0.5f);
                    var side = target + new Vector3(0.38f, 0f, 0.33f);
                    foreach (var camera in new[] { front, side })
                    {
                        foreach (var amount in new[] { 0f, soft, 1f })
                        {
                            mouth.PoseNow(amount);
                            tiles.Add(HandRetargetSelfTest.RenderView(rig, new[] { rig }, camera, Quaternion.LookRotation(target - camera), 24f, tileSize, 0.05f));
                        }
                    }

                    var mirror = target + new Vector3(0f, 0f, 1.5f);
                    foreach (var amount in new[] { 0f, 1f })
                    {
                        mouth.PoseNow(amount);
                        tiles.Add(HandRetargetSelfTest.RenderView(rig, new[] { rig }, mirror, Quaternion.LookRotation(target - mirror), 24f, tileSize, 0.05f));
                    }

                    mouth.PoseNow(1f);
                    sb.Append("\n  ").Append(rig.go.name).Append(": full voice = jaw ").Append(mouth.FullJawDegrees.ToString("0.0"))
                      .Append(" deg (lips ").Append((mouth.LipOpeningMeters * 1000f).ToString("0")).Append(" mm), soft voice = ")
                      .Append((soft * 100f).ToString("0")).Append("% of that; ").Append(mouth.DescribeNow());
                }
                finally
                {
                    if (mouth != null)
                        mouth.Release();
                    HandRetargetSelfTest.Destroy(rig);
                }
            }

            Debug.Log(sb.ToString());
            if (tiles.Count == 0)
                return;
            var path = Path.Combine(Path.GetFullPath("Temp/HumanMouthTest"), "mouth.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            HandRetargetSelfTest.SaveGrid(tiles, 8, path, "[AvianVoice] HUMAN MOUTH image: " + path);
            foreach (var t in tiles)
                Object.DestroyImmediate(t);
        }
    }
}
#endif
