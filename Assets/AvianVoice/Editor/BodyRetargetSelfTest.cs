#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AvianVoice.BodyTracking;
using UnityEditor;
using UnityEngine;
using Rig = AvianVoice.Editor.HandRetargetSelfTest.Rig;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Whole-body check of the human avatar retargeting without a headset.
    ///
    /// The Armature plays the tracked body (its muscle T-pose is the "bind pose"). For a set of everyday poses the
    /// MakeHuman body is posed twice:
    /// <list type="bullet">
    /// <item>reference: the same Humanoid muscle values (Unity's own retargeting, which keeps every bone's twist);</item>
    /// <item>runtime: the way it is driven in the headset: torso, neck, head, shoulders and upper arms by the Meta SDK
    /// rule (each bone's start-to-end direction matched once at the bind pose, then the tracked bone's rotation
    /// times that fixed correction; OVRUnityHumanoidSkeletonRetargeter.ComputeOffsetsUsingSkeletonComponent),
    /// forearms, wrists and fingers by <see cref="HandFrameRetargeting"/>.</item>
    /// </list>
    /// The log lists, per pose, how far each runtime bone is turned away from the reference (degrees) and how far
    /// the shoulder joints / wrists end up (cm). Images (Temp/BodyRetargetTest): one row per pose, columns =
    /// tracked body / MakeHuman reference / MakeHuman as driven at runtime; front view (as in the mirror) and the
    /// first-person view from the eyes.
    /// </summary>
    public static class BodyRetargetSelfTest
    {
        const int TileSize = 260;

        /// <summary>Bones the SDK drives (AvianHumanoidRetargeter: hips, back, neck, head, arms, legs), parents first.</summary>
        static readonly HumanBodyBones[] SdkBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        /// <summary>Bones AvianHumanoidRetargeter.keepAvatarTorsoShape drives as changes from the bind pose.</summary>
        static readonly HumanBodyBones[] TorsoBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
        };

        /// <summary>Joint pairs from OVRHumanBodyBonesMappings.BoneToJointPair: the direction the SDK matches per bone.</summary>
        static readonly Dictionary<HumanBodyBones, (HumanBodyBones start, HumanBodyBones end)> Pairs = new()
        {
            { HumanBodyBones.Hips, (HumanBodyBones.Hips, HumanBodyBones.Spine) },
            { HumanBodyBones.Spine, (HumanBodyBones.Spine, HumanBodyBones.Chest) },
            { HumanBodyBones.Chest, (HumanBodyBones.Chest, HumanBodyBones.UpperChest) },
            { HumanBodyBones.UpperChest, (HumanBodyBones.UpperChest, HumanBodyBones.Neck) },
            { HumanBodyBones.Neck, (HumanBodyBones.Neck, HumanBodyBones.Head) },
            { HumanBodyBones.Head, (HumanBodyBones.Neck, HumanBodyBones.Head) },
            { HumanBodyBones.LeftShoulder, (HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm) },
            { HumanBodyBones.LeftUpperArm, (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm) },
            { HumanBodyBones.RightShoulder, (HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm) },
            { HumanBodyBones.RightUpperArm, (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm) },
            { HumanBodyBones.LeftUpperLeg, (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg) },
            { HumanBodyBones.LeftLowerLeg, (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot) },
            { HumanBodyBones.LeftFoot, (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes) },
            { HumanBodyBones.RightUpperLeg, (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg) },
            { HumanBodyBones.RightLowerLeg, (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot) },
            { HumanBodyBones.RightFoot, (HumanBodyBones.RightFoot, HumanBodyBones.RightToes) },
        };

        static readonly HumanBodyBones[] MeasuredBones =
        {
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
        };

        sealed class PoseSpec
        {
            public string name;
            public Action<float[]> set;
        }

        /// <summary>Changes on top of the Avatar T-pose muscle values (absolute values for the muscles named).</summary>
        static readonly PoseSpec[] Poses =
        {
            new() { name = "T-pose", set = m => { } },
            new() { name = "arms down", set = m => { Both(m, "Arm Down-Up", -0.9f); } },
            new() { name = "arms forward", set = m => { Both(m, "Arm Front-Back", -0.75f); } },
            new() { name = "arms up 45", set = m => { Both(m, "Arm Down-Up", 0.75f); } },
            new() { name = "arms overhead", set = m => { Both(m, "Arm Down-Up", 1f); Both(m, "Shoulder Down-Up", 0.6f); } },
            new()
            {
                name = "hands to chest", set = m =>
                {
                    Both(m, "Arm Down-Up", -0.6f);
                    Both(m, "Arm Front-Back", -0.4f);
                    Both(m, "Forearm Stretch", -0.9f);
                }
            },
            new() { name = "shrug", set = m => { Both(m, "Arm Down-Up", -0.9f); Both(m, "Shoulder Down-Up", 1f); } },
            new() { name = "reach forward", set = m => { Both(m, "Arm Front-Back", -0.75f); Both(m, "Shoulder Front-Back", 1f); } },
            new()
            {
                name = "lean + twist", set = m =>
                {
                    Both(m, "Arm Down-Up", -0.9f);
                    One(m, "Spine Front-Back", 0.6f);
                    One(m, "Spine Twist Left-Right", 0.6f);
                    One(m, "Chest Twist Left-Right", 0.4f);
                }
            },
            new()
            {
                name = "head turn", set = m =>
                {
                    Both(m, "Arm Down-Up", -0.9f);
                    One(m, "Head Turn Left-Right", 0.7f);
                    One(m, "Neck Nod Down-Up", 0.5f);
                }
            },
        };

        [MenuItem("Avian Voice/Embodiment/Debug/Body Retarget Self-Test (with images)")]
        public static void RunMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AvianVoice] BODY TEST: stop Play mode first.");
                return;
            }

            Run(Path.GetFullPath("Temp/BodyRetargetTest"));
        }

        public static void Run(string imageDir)
        {
            var sourcePath = AvianEmbodimentSceneSetup.ToResourcesPath(AvianEmbodimentSceneSetup.HumanAvatarAssetPath);
            var targetPath = AvianEmbodimentSceneSetup.ToResourcesPath(AvianEmbodimentSceneSetup.MakeHumanAvatarAssetPath);
            var origin = HandRetargetSelfTest.TestOrigin;
            var source = HandRetargetSelfTest.Load(sourcePath, origin);
            var reference = HandRetargetSelfTest.Load(targetPath, origin + new Vector3(2.5f, 0f, 0f));
            var muscleRest = HandRetargetSelfTest.Load(targetPath, origin + new Vector3(5f, 0f, 0f));
            var tPoseRest = HandRetargetSelfTest.Load(targetPath, origin + new Vector3(7.5f, 0f, 0f));
            var rigs = new[] { source, reference, muscleRest, tPoseRest };
            if (Array.Exists(rigs, r => r == null))
            {
                Debug.LogError("[AvianVoice] BODY TEST: avatar missing or not Humanoid.");
                HandRetargetSelfTest.Destroy(rigs);
                return;
            }

            try
            {
                var sb = new StringBuilder("[AvianVoice] BODY TEST Armature (tracked body, bind = its T-pose) -> MakeHuman.");

                // Rest poses. Load left every rig at muscle zero, which is what HumanFirstPersonDriver used to hand
                // the SDK as the "T-pose"; the tracker's bind pose is a real T-pose.
                HeadRests.Clear();
                HeadRests[muscleRest] = Local(muscleRest, HumanBodyBones.Head).rotation;
                foreach (var rig in new[] { source, tPoseRest })
                    HumanoidTPose.Apply(rig.animator);
                HeadRests[source] = Local(source, HumanBodyBones.Head).rotation;
                HeadRests[tPoseRest] = Local(tPoseRest, HumanBodyBones.Head).rotation;
                HeadRests[reference] = HeadRests[tPoseRest];
                sb.Append("\n  head bone, muscle zero vs Avatar T-pose: ")
                  .Append(Quaternion.Angle(HeadRests[muscleRest], HeadRests[tPoseRest]).ToString("0.0")).Append(" deg");

                var tMuscles = TPoseMuscles(source);
                sb.Append("\n  Avatar T-pose in muscle values:");
                foreach (var name in new[]
                         {
                             "Left Shoulder Down-Up", "Left Shoulder Front-Back", "Left Arm Down-Up", "Left Arm Front-Back",
                             "Left Arm Twist In-Out", "Left Forearm Stretch", "Left Forearm Twist In-Out", "Left Upper Leg Front-Back",
                             "Left Lower Leg Stretch"
                         })
                {
                    var i = Array.IndexOf(HumanTrait.MuscleName, name);
                    if (i >= 0)
                        sb.Append(' ').Append(name.Replace("Left ", "")).Append('=').Append(tMuscles[i].ToString("0.00"));
                }

                var oldCorrections = Corrections(source, muscleRest, false, sb, "before (muscle-zero rest, every bone direction-matched)");
                var newCorrections = Corrections(source, tPoseRest, true, sb, "after (Avatar T-pose rest, torso keeps the avatar's shape)");

                var bind = new HandFrameRetargeting.Snapshot();
                var rest = new HandFrameRetargeting.Snapshot();
                HandRetargetSelfTest.Snap(source, bind);
                HandFrameRetargeting.SnapshotFromSkinBindPose(tPoseRest.animator, tPoseRest.go.transform,
                    HandFrameRetargeting.SnapshotBones, tPoseRest.fingertips, rest);
                var frames = new HandFrameRetargeting();
                if (!frames.Calibrate(bind, rest, includeForearm: true, includeFingers: true))
                    Debug.LogWarning("[AvianVoice] BODY TEST: hand frame calibration failed; forearms and hands stay on the muscle pose.");

                var front = new List<Texture2D>();
                var eyes = new List<Texture2D>();
                sb.Append("\n  per pose, runtime vs reference: largest bone turn (deg), upper arm turn, shoulder joint / wrist offset L,R (cm); before -> after:");
                foreach (var spec in Poses)
                {
                    var muscles = (float[])tMuscles.Clone();
                    spec.set(muscles);
                    foreach (var rig in rigs)
                        HandRetargetSelfTest.SetMuscles(rig, m => Array.Copy(muscles, m, m.Length));
                    DriveBody(source, muscleRest, oldCorrections);
                    DriveBody(source, tPoseRest, newCorrections);
                    foreach (var driven in new[] { muscleRest, tPoseRest })
                        HandRetargetSelfTest.Drive(source, driven, bone => (frames.TryGetCorrection(bone, out var c), c));

                    sb.Append("\n  ").Append(spec.name.PadRight(15)).Append(Compare(reference, muscleRest))
                      .Append("  ->  ").Append(Compare(reference, tPoseRest));

                    if (imageDir != null)
                    {
                        foreach (var rig in rigs)
                        {
                            front.Add(RenderFront(rig, rigs));
                            eyes.Add(RenderEyes(rig, rigs, source));
                        }
                    }
                }

                Debug.Log(sb.ToString());
                if (imageDir != null)
                {
                    const string columns = " (rows = poses, columns = tracked body / MakeHuman reference / runtime before / runtime after (T-pose rest, torso keeps the avatar shape))";
                    var frontPath = Path.Combine(imageDir, "body_front.png");
                    var eyesPath = Path.Combine(imageDir, "body_first_person.png");
                    HandRetargetSelfTest.SaveGrid(front, rigs.Length, frontPath, "[AvianVoice] BODY TEST image: " + frontPath + columns);
                    HandRetargetSelfTest.SaveGrid(eyes, rigs.Length, eyesPath, "[AvianVoice] BODY TEST image: " + eyesPath + columns);
                    foreach (var t in front)
                        UnityEngine.Object.DestroyImmediate(t);
                    foreach (var t in eyes)
                        UnityEngine.Object.DestroyImmediate(t);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[AvianVoice] BODY TEST EXCEPTION: " + e);
            }
            finally
            {
                HeadRests.Clear();
                HandRetargetSelfTest.Destroy(rigs);
            }
        }

        /// <summary>
        /// Corrections for a target rest pose against the source's current (bind) pose: the SDK's direction match
        /// (avatar bone = tracked bone * inverse(bind) * shortest turn of the avatar's rest direction onto the
        /// tracked one * avatar rest), or, with <paramref name="keepTorso"/>, for the torso bones without that turn
        /// (AvianHumanoidRetargeter.keepAvatarTorsoShape).
        /// </summary>
        static Dictionary<HumanBodyBones, Quaternion> Corrections(Rig source, Rig target, bool keepTorso, StringBuilder sb, string label)
        {
            var corrections = new Dictionary<HumanBodyBones, Quaternion>();
            sb.Append("\n  ").Append(label).Append(": rest turn per bone at the bind pose (deg):");
            foreach (var bone in SdkBones)
            {
                if (!TryDirection(source, bone, out var sourceDir) || !TryDirection(target, bone, out var targetDir))
                    continue;
                var s = Local(source, bone);
                var t = Local(target, bone);
                var turn = keepTorso && Array.IndexOf(TorsoBones, bone) >= 0
                    ? Quaternion.identity
                    : Quaternion.FromToRotation(targetDir, sourceDir);
                corrections[bone] = Quaternion.Inverse(s.rotation) * turn * t.rotation;
                sb.Append(' ').Append(Short(bone)).Append('=').Append(Quaternion.Angle(Quaternion.identity, turn).ToString("0"));
            }

            sb.Append("; clavicle elevation / forward: tracked ").Append(Describe(source, HumanBodyBones.LeftShoulder))
              .Append(", MakeHuman ").Append(Describe(target, HumanBodyBones.LeftShoulder));
            return corrections;
        }

        static string Compare(Rig reference, Rig runtime)
        {
            var worst = 0f;
            var worstBone = "";
            foreach (var bone in MeasuredBones)
            {
                if (reference.animator.GetBoneTransform(bone) == null || runtime.animator.GetBoneTransform(bone) == null)
                    continue;
                var angle = Quaternion.Angle(Local(reference, bone).rotation, Local(runtime, bone).rotation);
                if (angle > worst)
                {
                    worst = angle;
                    worstBone = Short(bone);
                }
            }

            var upperArm = Quaternion.Angle(Local(reference, HumanBodyBones.LeftUpperArm).rotation, Local(runtime, HumanBodyBones.LeftUpperArm).rotation);
            return worst.ToString("0").PadLeft(3) + " (" + worstBone + ") upArm " + upperArm.ToString("0") +
                   " sh " + Offset(reference, runtime, HumanBodyBones.LeftUpperArm) + "," + Offset(reference, runtime, HumanBodyBones.RightUpperArm) +
                   " wr " + Offset(reference, runtime, HumanBodyBones.LeftHand) + "," + Offset(reference, runtime, HumanBodyBones.RightHand);
        }

        /// <summary>Muscle values of the rig's current pose (call with the rig in its Avatar T-pose).</summary>
        static float[] TPoseMuscles(Rig rig)
        {
            using var handler = new HumanPoseHandler(rig.animator.avatar, rig.go.transform);
            var pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            return (float[])pose.muscles.Clone();
        }

        /// <summary>The SDK rule: avatarBone = trackedBone * correction (root-local rotations), parents first.</summary>
        static void DriveBody(Rig source, Rig target, Dictionary<HumanBodyBones, Quaternion> corrections)
        {
            var sourceRootInv = Quaternion.Inverse(source.go.transform.rotation);
            var targetRoot = target.go.transform.rotation;
            foreach (var bone in SdkBones)
            {
                var s = source.animator.GetBoneTransform(bone);
                var t = target.animator.GetBoneTransform(bone);
                if (s != null && t != null && corrections.TryGetValue(bone, out var c))
                    t.rotation = targetRoot * (sourceRootInv * s.rotation * c);
            }
        }

        /// <summary>
        /// Bone pose in the rig's root frame, positions measured from the hips (the muscle pose places a rig's
        /// skeleton away from its root object when that is not at the world origin).
        /// </summary>
        static (Vector3 position, Quaternion rotation) Local(Rig rig, HumanBodyBones bone)
        {
            var root = rig.go.transform;
            var t = rig.animator.GetBoneTransform(bone);
            var hips = rig.animator.GetBoneTransform(HumanBodyBones.Hips);
            return (Quaternion.Inverse(root.rotation) * (t.position - hips.position), Quaternion.Inverse(root.rotation) * t.rotation);
        }

        static bool TryDirection(Rig rig, HumanBodyBones bone, out Vector3 direction)
        {
            direction = default;
            if (!Pairs.TryGetValue(bone, out var pair))
                return false;
            var a = rig.animator.GetBoneTransform(pair.start);
            var b = rig.animator.GetBoneTransform(pair.end);
            if (a == null || b == null || (b.position - a.position).sqrMagnitude < 1e-10f)
                return false;
            var root = rig.go.transform;
            direction = (root.InverseTransformPoint(b.position) - root.InverseTransformPoint(a.position)).normalized;
            return true;
        }

        static string Describe(Rig rig, HumanBodyBones bone)
        {
            if (!TryDirection(rig, bone, out var d))
                return "n/a";
            var elevation = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
            var forward = Mathf.Atan2(d.z, Mathf.Abs(d.x)) * Mathf.Rad2Deg;
            return elevation.ToString("0") + " / " + forward.ToString("0");
        }

        static string Offset(Rig a, Rig b, HumanBodyBones bone)
        {
            var ta = a.animator.GetBoneTransform(bone);
            var tb = b.animator.GetBoneTransform(bone);
            if (ta == null || tb == null)
                return "n/a";
            return (100f * Vector3.Distance(Local(a, bone).position, Local(b, bone).position)).ToString("0.0");
        }

        static string Short(HumanBodyBones bone)
        {
            var s = bone.ToString();
            return s.Replace("Left", "L").Replace("Right", "R").Replace("Upper", "Up").Replace("Lower", "Lo").Replace("Shoulder", "Sh");
        }

        // ------------------------------------------------------------------ muscles

        static void Both(float[] m, string part, float value)
        {
            Set(m, "Left " + part, value);
            Set(m, "Right " + part, value);
        }

        static void One(float[] m, string name, float value)
        {
            Set(m, name, value);
        }

        static void Set(float[] m, string name, float value)
        {
            var i = Array.IndexOf(HumanTrait.MuscleName, name);
            if (i < 0)
            {
                Debug.LogWarning("[AvianVoice] BODY TEST: no muscle named '" + name + "'");
                return;
            }

            m[i] = value;
        }

        // ------------------------------------------------------------------ images

        /// <summary>From the front, as the participant sees the body in the mirror (Humanoid faces +Z).</summary>
        static Texture2D RenderFront(Rig rig, Rig[] all)
        {
            var root = rig.go.transform;
            var hips = rig.animator.GetBoneTransform(HumanBodyBones.Hips).position;
            var target = hips + root.rotation * new Vector3(0f, 0.35f, 0f);
            var position = hips + root.rotation * new Vector3(0.35f, 0.5f, 2.3f);
            return HandRetargetSelfTest.RenderView(rig, all, position, Quaternion.LookRotation(target - position), 42f, TileSize, 0.05f);
        }

        /// <summary>
        /// From the avatar's eye point (where the headset camera is put), looking the way the tracked head looks and
        /// 60 degrees down, as when one looks at one's own body (the head is clipped by the near plane, as it is
        /// hidden in the headset).
        /// </summary>
        static Texture2D RenderEyes(Rig rig, Rig[] all, Rig tracked)
        {
            var root = rig.go.transform;
            var head = rig.animator.GetBoneTransform(HumanBodyBones.Head);
            var eye = head.position + HeadTurn(rig) * (root.rotation * new Vector3(0f, 0.04f, 0.085f));
            var look = HeadTurn(tracked) * root.rotation * Quaternion.Euler(60f, 0f, 0f);
            return HandRetargetSelfTest.RenderView(rig, all, eye, look, 100f, TileSize, 0.07f);
        }

        /// <summary>World rotation of the head bone away from its T-pose.</summary>
        static Quaternion HeadTurn(Rig rig)
        {
            var root = rig.go.transform;
            var headLocal = Quaternion.Inverse(root.rotation) * rig.animator.GetBoneTransform(HumanBodyBones.Head).rotation;
            return root.rotation * (headLocal * Quaternion.Inverse(HeadRest(rig))) * Quaternion.Inverse(root.rotation);
        }

        /// <summary>Head bone rotation (root frame) in the muscle T-pose, recorded per rig right after loading.</summary>
        static readonly Dictionary<Rig, Quaternion> HeadRests = new();

        static Quaternion HeadRest(Rig rig) => HeadRests.TryGetValue(rig, out var q) ? q : Quaternion.identity;
    }
}
#endif
