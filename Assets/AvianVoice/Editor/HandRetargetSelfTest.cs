#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AvianVoice.BodyTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Checks the hand / finger retargeting (<see cref="HandFrameRetargeting"/>) without a headset.
    ///
    /// One avatar plays the tracked skeleton: its bones have their own axes, and its reference ("bind") pose has the
    /// palm turned another way than the other avatar's T-pose (forearm rotated) and slightly more curled fingers,
    /// the way a tracker's bind pose differs from an avatar's T-pose. The other avatar is driven from it bone by
    /// bone exactly as AvianHumanoidRetargeter does at runtime (avatarBone = sourceBone * correction), once with the
    /// full-frame corrections and once with the SDK's direction-only corrections. For a set of hand poses the test
    /// measures how far each driven segment points away from the source segment and how far the palm normals
    /// differ (degrees). Pairs: MakeHuman -> MakeHuman (control), Armature -> MakeHuman, MakeHuman -> Armature,
    /// Armature -> Rocketbox male, Armature -> Rocketbox female.
    ///
    /// Menu: Avian Voice / Embodiment / Debug / Hand Retarget Self-Test (log only, or with images in Temp/HandRetargetTest).
    /// Batch: Unity -batchmode -projectPath . -executeMethod AvianVoice.Editor.HandRetargetSelfTest.Batch -handTestOut &lt;folder&gt;
    /// </summary>
    public static class HandRetargetSelfTest
    {
        const float PassDegrees = 2f;

        /// <summary>
        /// Below the scene, so the test can also run inside an open scene; close enough to the origin that float
        /// positions keep sub-micrometre resolution (at 5000 m they resolve only ~0.5 mm, several degrees on a
        /// 2 cm finger segment).
        /// </summary>
        internal static readonly Vector3 TestOrigin = new(0f, -100f, 0f);

        internal sealed class Rig
        {
            public GameObject go;
            public Animator animator;
            public HumanPoseHandler handler;
            public HumanPose pose;
            public Vector3 bodyPosition;
            public Quaternion bodyRotation;
            public Dictionary<HumanBodyBones, Vector3> fingertips;
        }

        sealed class PoseSpec
        {
            public string name;
            public Action<float[]> set;
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Hand Retarget Self-Test")]
        public static void RunMenu()
        {
            Run(null);
        }

        [MenuItem("Avian Voice/Embodiment/Debug/Hand Retarget Self-Test (with images)")]
        public static void RunMenuWithImages()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AvianVoice] HAND TEST: stop Play mode first.");
                return;
            }

            Run(Path.GetFullPath("Temp/HandRetargetTest"));
        }

        /// <summary>
        /// Skin check for each human avatar: the left hand as imported, in Unity's muscle-zero pose, and with every
        /// finger joint (thumb excepted) bent 60 degrees one way and the other about its geometric flexion axis. A
        /// broken skin shows here regardless of any retargeting (e.g. joints that sit outside the finger mesh, which
        /// happened when the MakeHuman body-shape targets were exported as blend shapes Unity drops).
        /// Writes Temp/HandRetargetTest/hand_states.png (one row per avatar).
        /// </summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Render Avatar Hand States")]
        public static void RenderHandStates()
        {
            var tiles = new List<Texture2D>();
            foreach (var path in AvianEmbodimentSceneSetup.HumanAvatarAssetPaths)
            {
                var resources = AvianEmbodimentSceneSetup.ToResourcesPath(path);
                var prefab = Resources.Load<GameObject>(resources);
                if (prefab == null)
                    continue;

                var go = UnityEngine.Object.Instantiate(prefab);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetPositionAndRotation(TestOrigin, Quaternion.identity);
                var raw = new Rig { go = go, animator = go.GetComponent<Animator>(), fingertips = new Dictionary<HumanBodyBones, Vector3>() };
                tiles.Add(RenderHand(raw, new[] { raw }));

                var rig = Load(resources, TestOrigin + new Vector3(2f, 0f, 0f));
                tiles.Add(RenderHand(rig, new[] { rig }));
                Destroy(rig);

                BendLeftFingers(raw, 60f);
                tiles.Add(RenderHand(raw, new[] { raw }));
                BendLeftFingers(raw, -120f);
                tiles.Add(RenderHand(raw, new[] { raw }));
                UnityEngine.Object.DestroyImmediate(go);
            }

            SaveGrid(tiles, 4, Path.Combine(Path.GetFullPath("Temp/HandRetargetTest"), "hand_states.png"));
            foreach (var t in tiles)
                UnityEngine.Object.DestroyImmediate(t);
        }

        /// <summary>
        /// Turning the wrist (palm towards the floor / the ceiling) on each human body: once with all the twist at the
        /// wrist, as Meta's body skeleton delivers it (its forearm joint does not turn; the twist is in its wrist-twist
        /// joint), once shared out along the forearm as at runtime (<see cref="HandFrameRetargeting.DistributeForearmTwist"/>,
        /// share 0.5). The Armature in its T-pose plays the tracked arm. Rows = bodies, columns = palm turned -80 deg
        /// as tracked / shared, +80 deg as tracked / shared. Writes Temp/HandRetargetTest/wrist_twist.png.
        /// </summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Render Wrist Twist")]
        public static void RenderWristTwist()
        {
            var sourcePath = AvianEmbodimentSceneSetup.ToResourcesPath(AvianEmbodimentSceneSetup.ArmatureAvatarAssetPath);
            var targets = new[]
            {
                AvianEmbodimentSceneSetup.MakeHumanAvatarAssetPath, AvianEmbodimentSceneSetup.RocketboxMaleAvatarAssetPath,
                AvianEmbodimentSceneSetup.RocketboxFemaleAvatarAssetPath,
            };
            const float share = 0.5f;
            var tiles = new List<Texture2D>();
            var sb = new StringBuilder("[AvianVoice] WRIST TWIST (left arm, palm turned about the forearm; total twist / forearm part after sharing, deg):");
            foreach (var asset in targets)
            {
                var source = Load(sourcePath, TestOrigin);
                var target = Load(AvianEmbodimentSceneSetup.ToResourcesPath(asset), TestOrigin + new Vector3(2f, 0f, 0f));
                if (source == null || target == null)
                {
                    Destroy(source, target);
                    continue;
                }

                try
                {
                    HumanoidTPose.Apply(source.animator);
                    HumanoidTPose.Apply(target.animator);
                    var bind = new HandFrameRetargeting.Snapshot();
                    var rest = new HandFrameRetargeting.Snapshot();
                    Snap(source, bind);
                    HandFrameRetargeting.SnapshotFromSkinBindPose(target.animator, target.go.transform,
                        HandFrameRetargeting.SnapshotBones, target.fingertips, rest);
                    var frames = new HandFrameRetargeting();
                    if (!frames.Calibrate(bind, rest, includeForearm: true, includeFingers: true))
                        continue;
                    (bool, Quaternion) Frame(HumanBodyBones bone) => (frames.TryGetCorrection(bone, out var c), c);

                    var upper = target.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    var lower = target.animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    var hand = target.animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    Drive(source, target, Frame);
                    var elbowRest = Quaternion.Inverse(upper.rotation) * lower.rotation;
                    var wristRest = Quaternion.Inverse(lower.rotation) * hand.rotation;

                    var sourceLower = source.animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    var sourceHand = source.animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    var sourceHandRest = sourceHand.rotation;
                    sb.Append("\n  ").Append(Name(AvianEmbodimentSceneSetup.ToResourcesPath(asset)).PadRight(16));
                    foreach (var turn in new[] { -80f, 80f })
                    {
                        var axis = (sourceHand.position - sourceLower.position).normalized;
                        sourceHand.rotation = Quaternion.AngleAxis(turn, axis) * sourceHandRest;
                        Drive(source, target, Frame);
                        tiles.Add(RenderForearm(target, new[] { source, target }));
                        var total = HandFrameRetargeting.DistributeForearmTwist(upper, lower, hand, elbowRest, wristRest, share);
                        var forearmPart = HandFrameRetargeting.TwistAngle(
                            Quaternion.Inverse(elbowRest) * (Quaternion.Inverse(upper.rotation) * lower.rotation),
                            (Quaternion.Inverse(lower.rotation) * (hand.position - lower.position)).normalized);
                        tiles.Add(RenderForearm(target, new[] { source, target }));
                        sb.Append("  turn ").Append(turn.ToString("+0;-0")).Append(": total ").Append(total.ToString("0"))
                          .Append(" / forearm ").Append(forearmPart.ToString("0"));
                    }

                    sourceHand.rotation = sourceHandRest;
                }
                finally
                {
                    Destroy(source, target);
                }
            }

            Debug.Log(sb.ToString());
            if (tiles.Count > 0)
            {
                var path = Path.Combine(Path.GetFullPath("Temp/HandRetargetTest"), "wrist_twist.png");
                SaveGrid(tiles, 4, path, "[AvianVoice] WRIST TWIST image: " + path +
                                         " (rows = neutral / male / female, columns = palm -80 deg as tracked / shared, +80 deg as tracked / shared)");
            }

            foreach (var t in tiles)
                UnityEngine.Object.DestroyImmediate(t);
        }

        /// <summary>The left forearm and hand from the front and a little above.</summary>
        static Texture2D RenderForearm(Rig rig, Rig[] all)
        {
            var lower = rig.animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position;
            var hand = rig.animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
            var center = Vector3.Lerp(lower, hand, 0.6f);
            var camPosition = center + new Vector3(0f, 0.16f, 0.5f);
            return RenderView(rig, all, camPosition, Quaternion.LookRotation(center - camPosition), 40f, Tile, 0.02f);
        }

        /// <summary>Rotate each left finger joint (thumb excepted) about the axis perpendicular to its segment and the palm normal.</summary>
        static void BendLeftFingers(Rig rig, float degrees)
        {
            var snap = new HandFrameRetargeting.Snapshot();
            Snap(rig, snap);
            if (!HandFrameRetargeting.TryPalmNormal(snap, true, out var normal, out _))
                return;

            foreach (var bone in HandFrameRetargeting.ApplyOrder)
            {
                if (!HandFrameRetargeting.IsFinger(bone) || !HandFrameRetargeting.IsLeft(bone) || HandFrameRetargeting.IsThumb(bone))
                    continue;
                var t = rig.animator.GetBoneTransform(bone);
                var next = HandFrameRetargeting.IsDistal(bone) ? null : rig.animator.GetBoneTransform(HandFrameRetargeting.NextInChain(bone));
                var previous = rig.animator.GetBoneTransform(bone - 1);
                if (t == null || (next == null && previous == null))
                    continue;
                var segment = next != null ? (next.position - t.position).normalized : (t.position - previous.position).normalized;
                t.rotation = Quaternion.AngleAxis(degrees, Vector3.Cross(segment, normal).normalized) * t.rotation;
            }
        }

        public static void Batch()
        {
            string outDir = null;
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-handTestOut")
                    outDir = args[i + 1];
            }

            var ok = false;
            try
            {
                foreach (var path in AvianEmbodimentSceneSetup.HumanAvatarAssetPaths)
                {
                    if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                        (importer.animationType != ModelImporterAnimationType.Human || !importer.isReadable))
                    {
                        Debug.Log("[AvianVoice] HAND TEST: reimporting " + path + " as a readable Humanoid.");
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }

                if (outDir != null)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ok = Run(outDir);
            }
            catch (Exception e)
            {
                Debug.LogError("[AvianVoice] HAND TEST EXCEPTION: " + e);
            }

            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run(string imageDir)
        {
            var makeHuman = AvianEmbodimentSceneSetup.ToResourcesPath(AvianEmbodimentSceneSetup.MakeHumanAvatarAssetPath);
            var armature = AvianEmbodimentSceneSetup.ToResourcesPath(AvianEmbodimentSceneSetup.ArmatureAvatarAssetPath);
            var male = AvianEmbodimentSceneSetup.ToResourcesPath(AvianEmbodimentSceneSetup.RocketboxMaleAvatarAssetPath);
            var female = AvianEmbodimentSceneSetup.ToResourcesPath(AvianEmbodimentSceneSetup.RocketboxFemaleAvatarAssetPath);
            var ok = RunPair(makeHuman, makeHuman, imageDir); // control: same rig
            ok &= RunPair(armature, makeHuman, imageDir);
            ok &= RunPair(makeHuman, armature, imageDir);
            ok &= RunPair(armature, male, imageDir);
            ok &= RunPair(armature, female, imageDir);
            Debug.Log("[AvianVoice] HAND TEST " + (ok ? "PASSED" : "FAILED") + " (frame-aligned segment direction and palm normal within " +
                      PassDegrees + " deg of the source in every pose)");
            return ok;
        }

        static bool RunPair(string sourcePath, string targetPath, string imageDir)
        {
            var source = Load(sourcePath, TestOrigin);
            var target = Load(targetPath, TestOrigin + new Vector3(2f, 0f, 0f));
            var targetOld = imageDir != null ? Load(targetPath, TestOrigin + new Vector3(4f, 0f, 0f)) : null;
            if (source == null || target == null || (imageDir != null && targetOld == null))
            {
                Debug.LogError("[AvianVoice] HAND TEST: avatar missing or not Humanoid (" + sourcePath + " / " + targetPath + ")");
                Destroy(source, target, targetOld);
                return false;
            }

            try
            {
                var bind = new HandFrameRetargeting.Snapshot();
                var rest = new HandFrameRetargeting.Snapshot();
                SetMuscles(source, BindPose);
                Snap(source, bind);
                // The avatar's reference: the pose its mesh was skinned in, not Unity's muscle-zero T-pose.
                SetMuscles(target, m => { });
                HandFrameRetargeting.SnapshotFromSkinBindPose(target.animator, target.go.transform,
                    HandFrameRetargeting.SnapshotBones, target.fingertips, rest);

                var frames = new HandFrameRetargeting();
                if (!frames.Calibrate(bind, rest, includeForearm: true, includeFingers: true))
                {
                    Debug.LogError("[AvianVoice] HAND TEST: calibration failed for " + sourcePath + " -> " + targetPath);
                    return false;
                }

                var directionOnly = new Dictionary<HumanBodyBones, Quaternion>();
                foreach (var bone in HandFrameRetargeting.ApplyOrder)
                {
                    if (HandFrameRetargeting.TryDirectionOnlyCorrection(bind, rest, bone, out var c))
                        directionOnly[bone] = c;
                }

                (bool, Quaternion) Frame(HumanBodyBones bone) => (frames.TryGetCorrection(bone, out var c), c);
                (bool, Quaternion) Sdk(HumanBodyBones bone) => (directionOnly.TryGetValue(bone, out var c), c);

                var sb = new StringBuilder();
                sb.Append("[AvianVoice] HAND TEST ").Append(Name(sourcePath)).Append(" -> ").Append(Name(targetPath))
                  .Append(": ").Append(frames.BoneCount).Append(" bones, fingertips from mesh ")
                  .Append(source.fingertips.Count).Append('/').Append(target.fingertips.Count)
                  .Append(". Max error in degrees (segment direction / palm normal):");

                var pass = true;
                var images = new List<Texture2D>();
                foreach (var spec in Poses)
                {
                    SetMuscles(source, spec.set);
                    SetMuscles(target, spec.set);
                    Drive(source, target, Frame);
                    var (newDir, newPalm, worst) = Measure(source, target);

                    SetMuscles(target, spec.set);
                    Drive(source, target, Sdk);
                    var (oldDir, oldPalm, _) = Measure(source, target);

                    sb.Append("\n  ").Append(spec.name.PadRight(12))
                      .Append(" frame-aligned ").Append(newDir.ToString("0.0")).Append(" / ").Append(newPalm.ToString("0.0"))
                      .Append(" (").Append(worst).Append(')')
                      .Append("   SDK direction-only ").Append(oldDir.ToString("0.0")).Append(" / ").Append(oldPalm.ToString("0.0"));
                    pass &= newDir < PassDegrees && newPalm < PassDegrees;

                    if (imageDir != null)
                    {
                        SetMuscles(target, spec.set);
                        Drive(source, target, Frame);
                        SetMuscles(targetOld, spec.set);
                        Drive(source, targetOld, Sdk);
                        images.Add(RenderHand(source, new[] { source, target, targetOld }));
                        images.Add(RenderHand(target, new[] { source, target, targetOld }));
                        images.Add(RenderHand(targetOld, new[] { source, target, targetOld }));
                    }
                }

                Debug.Log(sb.ToString());
                if (imageDir != null && images.Count > 0)
                    SaveGrid(images, 3, Path.Combine(imageDir, "hand_retarget_" + Name(sourcePath) + "_to_" + Name(targetPath) + ".png"));
                foreach (var t in images)
                    UnityEngine.Object.DestroyImmediate(t);
                return pass;
            }
            finally
            {
                Destroy(source, target, targetOld);
            }
        }

        // ------------------------------------------------------------------ poses

        /// <summary>
        /// The source's reference pose: neutral (no wrist bend), but the palm faces another way (forearm rotated)
        /// and the fingers are a little more curled than the target's T-pose.
        /// </summary>
        static void BindPose(float[] m)
        {
            Set(m, "Forearm Twist", 0.7f);
            SetFingers(m, "Stretched", -0.15f);
        }

        static readonly PoseSpec[] Poses =
        {
            new() { name = "rest", set = m => { } },
            new() { name = "bind", set = BindPose },
            new() { name = "fist", set = m => { SetFingers(m, "Stretched", -1f); SetFinger(m, "Thumb", "Stretched", -0.6f); } },
            new()
            {
                name = "point", set = m =>
                {
                    SetFingers(m, "Stretched", -1f);
                    SetFinger(m, "Index", "Stretched", 1f);
                    SetFinger(m, "Thumb", "Stretched", -0.7f);
                }
            },
            new()
            {
                name = "thumbs-up", set = m =>
                {
                    SetFingers(m, "Stretched", -1f);
                    SetFinger(m, "Thumb", "Stretched", 1f);
                    Set(m, "Thumb Spread", 1f);
                    Set(m, "Forearm Twist", -0.8f);
                }
            },
            new() { name = "spread", set = m => { SetFingers(m, "Stretched", 1f); SetFingers(m, "Spread", 1f); } },
            new()
            {
                name = "wrist+elbow", set = m =>
                {
                    Set(m, "Hand Down-Up", 0.8f);
                    Set(m, "Hand In-Out", -0.6f);
                    Set(m, "Forearm Twist", 0.9f);
                    Set(m, "Forearm Stretch", -0.6f);
                    Set(m, "Arm Down-Up", -0.4f);
                    SetFingers(m, "Stretched", -0.3f);
                }
            },
        };

        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Little" };

        static void SetFingers(float[] m, string kind, float value)
        {
            foreach (var finger in FingerNames)
                SetFinger(m, finger, kind, value);
        }

        static void SetFinger(float[] m, string finger, string kind, float value)
        {
            SetWhere(m, n => n.Contains(finger) && n.Contains(kind), finger + " " + kind, value);
        }

        /// <summary>Set a muscle on both sides by a part of its name (e.g. "Hand Down-Up", "Forearm Twist").</summary>
        static void Set(float[] m, string part, float value)
        {
            SetWhere(m, n => n.Contains(part), part, value);
        }

        static void SetWhere(float[] m, Func<string, bool> match, string label, float value)
        {
            var names = HumanTrait.MuscleName;
            var found = 0;
            for (var i = 0; i < names.Length; i++)
            {
                var n = names[i];
                if ((n.StartsWith("Left") || n.StartsWith("Right")) && match(n))
                {
                    m[i] = value;
                    found++;
                }
            }

            if (found == 0)
                Debug.LogWarning("[AvianVoice] HAND TEST: no muscle named like '" + label + "'");
        }

        // ------------------------------------------------------------------ rigs

        internal static Rig Load(string resourcesPath, Vector3 position)
        {
            var prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab == null)
                return null;

            var go = UnityEngine.Object.Instantiate(prefab);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var animator = go.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                UnityEngine.Object.DestroyImmediate(go);
                return null;
            }

            var rig = new Rig
            {
                go = go, animator = animator,
                handler = new HumanPoseHandler(animator.avatar, go.transform), pose = new HumanPose()
            };
            rig.handler.GetHumanPose(ref rig.pose);
            rig.bodyPosition = rig.pose.bodyPosition;
            rig.bodyRotation = rig.pose.bodyRotation;
            SetMuscles(rig, m => { });
            rig.fingertips = HandFrameRetargeting.ComputeFingertips(animator);
            return rig;
        }

        /// <summary>
        /// Muscle pose with the body position / rotation read once at load (a GetHumanPose / SetHumanPose round trip
        /// drifts the body when the root is away from the origin).
        /// </summary>
        internal static void SetMuscles(Rig rig, Action<float[]> set)
        {
            for (var i = 0; i < rig.pose.muscles.Length; i++)
                rig.pose.muscles[i] = 0f;
            set(rig.pose.muscles);
            rig.pose.bodyPosition = rig.bodyPosition;
            rig.pose.bodyRotation = rig.bodyRotation;
            rig.handler.SetHumanPose(ref rig.pose);
        }

        internal static void Destroy(params Rig[] rigs)
        {
            foreach (var rig in rigs)
            {
                if (rig == null)
                    continue;
                rig.handler?.Dispose();
                if (rig.go != null)
                    UnityEngine.Object.DestroyImmediate(rig.go);
            }
        }

        static readonly HumanBodyBones[] PalmBones =
        {
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal,
        };

        internal static void Snap(Rig rig, HandFrameRetargeting.Snapshot snapshot)
        {
            snapshot.Clear();
            var root = rig.go.transform;
            var inv = Quaternion.Inverse(root.rotation);
            void Add(HumanBodyBones bone)
            {
                var t = rig.animator.GetBoneTransform(bone);
                if (t == null)
                    return;
                snapshot.Set(bone, root.InverseTransformPoint(t.position), inv * t.rotation);
                if (rig.fingertips.TryGetValue(bone, out var localTip))
                    snapshot.tips[bone] = root.InverseTransformPoint(t.TransformPoint(localTip));
            }

            foreach (var bone in HandFrameRetargeting.ApplyOrder)
                Add(bone);
            foreach (var bone in PalmBones)
                Add(bone);
        }

        /// <summary>The runtime rule: avatarBone = sourceBone * correction (root-local rotations), parents first.</summary>
        internal static void Drive(Rig source, Rig target, Func<HumanBodyBones, (bool ok, Quaternion correction)> correction)
        {
            var sourceRootInv = Quaternion.Inverse(source.go.transform.rotation);
            var targetRoot = target.go.transform.rotation;
            foreach (var bone in HandFrameRetargeting.ApplyOrder)
            {
                var (ok, c) = correction(bone);
                var s = source.animator.GetBoneTransform(bone);
                var t = target.animator.GetBoneTransform(bone);
                if (ok && s != null && t != null)
                    t.rotation = targetRoot * (sourceRootInv * s.rotation * c);
            }
        }

        static (float direction, float palm, string worst) Measure(Rig source, Rig target)
        {
            var s = new HandFrameRetargeting.Snapshot();
            var t = new HandFrameRetargeting.Snapshot();
            Snap(source, s);
            Snap(target, t);
            var maxDir = 0f;
            var worst = "";
            foreach (var bone in HandFrameRetargeting.ApplyOrder)
            {
                // Where each segment points (fingertips from the meshes). The roll of a finger segment about its own
                // axis has no exact definition from joint positions, so it is not part of the criterion.
                if (!HandFrameRetargeting.TryDirection(s, bone, out var ds) || !HandFrameRetargeting.TryDirection(t, bone, out var dt))
                    continue;
                var angle = Vector3.Angle(ds, dt);
                if (angle > maxDir)
                {
                    maxDir = angle;
                    worst = bone.ToString();
                }
            }

            var maxPalm = 0f;
            foreach (var left in new[] { true, false })
            {
                if (HandFrameRetargeting.TryPalmNormal(s, left, out var ns, out _) && HandFrameRetargeting.TryPalmNormal(t, left, out var nt, out _))
                    maxPalm = Mathf.Max(maxPalm, Vector3.Angle(ns, nt));
            }

            return (maxDir, maxPalm, worst);
        }

        static string Name(string resourcesPath) => Path.GetFileName(resourcesPath);

        // ------------------------------------------------------------------ images

        const int Tile = 320;

        /// <summary>
        /// Close-up of the left hand. The skinned meshes are baked first: within one editor frame a skinned mesh
        /// keeps the skin of its last render, so it would not show a pose set just before.
        /// </summary>
        static Texture2D RenderHand(Rig rig, Rig[] all)
        {
            var hand = rig.animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var middle = rig.animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            var center = middle != null ? Vector3.Lerp(hand.position, middle.position, 0.8f) : hand.position;
            // In front of the avatar (Humanoid faces +Z), a little above the hand.
            var camPosition = center + new Vector3(0.05f, 0.18f, 0.42f);
            return RenderView(rig, all, camPosition, Quaternion.LookRotation(center - camPosition), 32f, Tile, 0.01f);
        }

        /// <summary>
        /// Renders only <paramref name="rig"/> (all rigs' skinned meshes hidden, this rig's baked copy shown) from a
        /// camera pose. The skinned meshes are baked first: within one editor frame a skinned mesh keeps the skin of
        /// its last render, so it would not show a pose set just before.
        /// </summary>
        internal static Texture2D RenderView(Rig rig, Rig[] all, Vector3 camPosition, Quaternion camRotation, float fieldOfView,
            int size, float nearClip)
        {
            var temporary = new List<UnityEngine.Object>();
            var hidden = new List<SkinnedMeshRenderer>();
            foreach (var r in all)
            {
                foreach (var skinned in r.go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skinned.enabled)
                    {
                        skinned.enabled = false;
                        hidden.Add(skinned);
                    }
                }
            }

            foreach (var skinned in rig.go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // useScale false gives world-size vertices for both rigs here (MakeHuman's renderer sits under a
                // 100x scaled node; baking "with scale" shrinks it to 1/100).
                var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                skinned.quality = SkinQuality.Bone4; // as AvatarEmbodimentController sets it at runtime
                skinned.BakeMesh(mesh, false);
                var copy = new GameObject("HandTestBaked") { hideFlags = HideFlags.HideAndDontSave };
                copy.transform.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
                copy.AddComponent<MeshFilter>().sharedMesh = mesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = skinned.sharedMaterials;
                temporary.Add(mesh);
                temporary.Add(copy);
            }

            var camGo = new GameObject("HandTestCamera") { hideFlags = HideFlags.HideAndDontSave };
            var lightGo = new GameObject("HandTestLight") { hideFlags = HideFlags.HideAndDontSave };
            temporary.Add(camGo);
            temporary.Add(lightGo);
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            try
            {
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.64f, 0.66f);
                cam.fieldOfView = fieldOfView;
                cam.nearClipPlane = nearClip;
                cam.farClipPlane = 6f;
                cam.targetTexture = rt;
                camGo.transform.SetPositionAndRotation(camPosition, camRotation);
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                return tex;
            }
            finally
            {
                foreach (var o in temporary)
                    UnityEngine.Object.DestroyImmediate(o);
                foreach (var skinned in hidden)
                    skinned.enabled = true;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        static void SaveGrid(List<Texture2D> tiles, int columns, string path)
        {
            SaveGrid(tiles, columns, path, "[AvianVoice] HAND TEST image: " + path +
                                           " (rows = poses, columns = tracked source / frame-aligned / SDK direction-only)");
        }

        /// <summary>Square tiles of equal size, first row at the top.</summary>
        internal static void SaveGrid(List<Texture2D> tiles, int columns, string path, string logLine)
        {
            var size = tiles[0].width;
            var rows = (tiles.Count + columns - 1) / columns;
            var grid = new Texture2D(columns * size, rows * size, TextureFormat.RGB24, false);
            for (var i = 0; i < tiles.Count; i++)
            {
                var x = (i % columns) * size;
                var y = (rows - 1 - i / columns) * size;
                grid.SetPixels(x, y, size, size, tiles[i].GetPixels());
            }

            grid.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllBytes(path, grid.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(grid);
            Debug.Log(logLine);
        }
    }
}
#endif
