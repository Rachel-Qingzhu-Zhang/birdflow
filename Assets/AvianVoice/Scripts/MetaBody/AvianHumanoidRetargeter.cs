#if AVIAN_META_XR_CORE
using System;
using System.Collections.Generic;
using System.Text;
using AvianVoice.BodyTracking;
using UnityEngine;
using BodySection = OVRUnityHumanoidSkeletonRetargeter.OVRHumanBodyBonesMappings.BodySection;
using BoneId = OVRSkeleton.BoneId;

namespace AvianVoice.MetaBody
{
    /// <summary>
    /// <see cref="OVRUnityHumanoidSkeletonRetargeter"/> configured for a stable first-person avatar:
    /// <list type="bullet">
    /// <item>full-body joint set (IOBT / full body tracking);</item>
    /// <item>rotation-only retargeting: only the hips take the tracked position, every other bone keeps the
    /// avatar's own bone length (the SDK default writes the tracked world position into almost every joint,
    /// which stretches and collapses a body whose proportions differ from the user's);</item>
    /// <item>forearms, wrists and fingers are retargeted with full bone frames (<see cref="HandFrameRetargeting"/>):
    /// the SDK matches bones by direction only, so on rigs whose rest palm faces another way than Meta's bind pose
    /// the hands come out rotated and the fingers bend about the wrong axes (crumpled hands);</item>
    /// <item>no default pelvis tilt (the SDK ships a 60 degree Hips rotation tweak meant for its own sample
    /// rig, which bends other rigs' torsos the wrong way);</item>
    /// <item>the owner re-poses the rig into its T-pose right before the SDK takes its one-time snapshot
    /// (<see cref="BeforeCaptureRestPose"/>), so a fallback pose applied earlier cannot poison the offsets.</item>
    /// </list>
    /// </summary>
    public sealed class AvianHumanoidRetargeter : OVRUnityHumanoidSkeletonRetargeter
    {
        /// <summary>Invoked right before the SDK records the target rig's T-pose (once, in Start).</summary>
        public Action BeforeCaptureRestPose;

        [Tooltip("Retarget forearms, wrists and fingers with full bone frames (direction + palm normal) instead of the SDK's " +
                 "direction-only match. Off: the SDK drives the wrists and the fingers keep the rest pose (or follow driveFingers).")]
        public bool frameAlignedHands = true;

        [Tooltip("Drive the finger bones from the tracked hand joints. Off: fingers keep the rest pose and follow the wrist.")]
        public bool driveFingers = true;

        [Tooltip("Write the tracked hips position (crouching, leaning). Off: only rotations; the owner places the root.")]
        public bool positionHips = true;

        public bool RestPoseCaptured { get; private set; }

        /// <summary>Hand frames calibrated against the current bind pose (data arrived at least once).</summary>
        public bool HandsCalibrated => hands.IsCalibrated && calibratedForCount == SkeletonChangedCount;

        readonly HandFrameRetargeting hands = new();
        readonly HandFrameRetargeting.Snapshot targetRest = new();
        readonly HandFrameRetargeting.Snapshot sourceBind = new();
        readonly Dictionary<HumanBodyBones, int> sourceBoneIndex = new();
        readonly Dictionary<HumanBodyBones, Transform> targetBones = new();
        Dictionary<HumanBodyBones, Vector3> targetFingertips = new();
        Animator targetAnimator;
        int calibratedForCount = -1;
        string handSummary = "not calibrated";

        /// <summary>Tracked fingertip joints, keyed by the Humanoid distal bone they end.</summary>
        static readonly Dictionary<BoneId, HumanBodyBones> TipToDistal = new()
        {
            { BoneId.FullBody_LeftHandThumbTip, HumanBodyBones.LeftThumbDistal },
            { BoneId.FullBody_LeftHandIndexTip, HumanBodyBones.LeftIndexDistal },
            { BoneId.FullBody_LeftHandMiddleTip, HumanBodyBones.LeftMiddleDistal },
            { BoneId.FullBody_LeftHandRingTip, HumanBodyBones.LeftRingDistal },
            { BoneId.FullBody_LeftHandLittleTip, HumanBodyBones.LeftLittleDistal },
            { BoneId.FullBody_RightHandThumbTip, HumanBodyBones.RightThumbDistal },
            { BoneId.FullBody_RightHandIndexTip, HumanBodyBones.RightIndexDistal },
            { BoneId.FullBody_RightHandMiddleTip, HumanBodyBones.RightMiddleDistal },
            { BoneId.FullBody_RightHandRingTip, HumanBodyBones.RightRingDistal },
            { BoneId.FullBody_RightHandLittleTip, HumanBodyBones.RightLittleDistal },
        };

        protected override void Awake()
        {
            _skeletonType = SkeletonType.FullBody;
            _adjustments = Array.Empty<JointAdjustment>();

            // The SDK never drives the fingers here: with frameAlignedHands they are driven below; without it they
            // would only be retargeted by direction, which crumples hands of rigs whose finger axes differ.
            var upper = new List<BodySection>
            {
                BodySection.Hips, BodySection.Back, BodySection.Neck, BodySection.Head,
                BodySection.LeftArm, BodySection.RightArm
            };
            if (driveFingers && !frameAlignedHands)
            {
                upper.Add(BodySection.LeftHand);
                upper.Add(BodySection.RightHand);
            }

            var full = new List<BodySection>(upper)
            {
                BodySection.LeftLeg, BodySection.LeftFoot, BodySection.RightLeg, BodySection.RightFoot
            };

            _bodySectionsToAlign = upper.ToArray();
            _fullBodySectionsToAlign = full.ToArray();
            var positions = positionHips ? new[] { BodySection.Hips } : Array.Empty<BodySection>();
            _bodySectionToPosition = positions;
            _fullBodySectionToPosition = positions;
            _updateType = UpdateType.UpdateOnly;
            base.Awake();
        }

        protected override void Start()
        {
            BeforeCaptureRestPose?.Invoke();
            CaptureTargetRest();
            base.Start();
            RestPoseCaptured = true;
            Debug.Log("[AvianVoice] Humanoid retargeter on '" + name + "': rest pose captured (rotation-only"
                      + (positionHips ? " + hips position" : "") + ", hands "
                      + (frameAlignedHands ? "frame-aligned" : "SDK") + ", fingers "
                      + (driveFingers ? "driven" : "kept") + ", " + targetRest.joints.Count + " hand/arm bones on the rig).");
        }

        protected override void Update()
        {
            base.Update();
            if (frameAlignedHands && ShouldRunUpdateThisFrame())
                ApplyHandFrames();
        }

        /// <summary>One line for the diagnostics menus.</summary>
        public string DescribeHands()
        {
            return "hands=" + (frameAlignedHands ? "frame-aligned" : "SDK") + " fingers=" + (driveFingers ? "driven" : "kept")
                   + " " + (HandsCalibrated ? handSummary : "not calibrated (no tracking data yet)");
        }

        /// <summary>
        /// Live check: rotation between each tracked bone's frame and the avatar bone's frame (direction + roll), and
        /// between the palm normals (degrees). Near 0 when the hands follow; a crumpled or rotated hand shows tens of degrees.
        /// </summary>
        public string MeasureHandError()
        {
            if (!HandsCalibrated || Bones == null)
                return "hand error: not calibrated";

            var source = new HandFrameRetargeting.Snapshot();
            var target = new HandFrameRetargeting.Snapshot();
            FillSourceSnapshot(source, Bones);
            FillTargetSnapshot(target);

            var sb = new StringBuilder("hand error (deg):");
            foreach (var left in new[] { true, false })
            {
                var maxDir = 0f;
                var worst = "";
                foreach (var bone in HandFrameRetargeting.ApplyOrder)
                {
                    if (HandFrameRetargeting.IsLeft(bone) != left || !hands.TryGetCorrection(bone, out _))
                        continue;
                    if (!HandFrameRetargeting.TryFrame(source, bone, out var fs) ||
                        !HandFrameRetargeting.TryFrame(target, bone, out var ft))
                        continue;
                    var angle = Quaternion.Angle(fs, ft);
                    if (angle > maxDir)
                    {
                        maxDir = angle;
                        worst = bone.ToString();
                    }
                }

                var palm = HandFrameRetargeting.TryPalmNormal(source, left, out var ns, out _) &&
                           HandFrameRetargeting.TryPalmNormal(target, left, out var nt, out _)
                    ? Vector3.Angle(ns, nt).ToString("0.0")
                    : "n/a";
                sb.Append(left ? " L" : " R").Append(" palm=").Append(palm)
                  .Append(" maxBone=").Append(maxDir.ToString("0.0")).Append(worst.Length > 0 ? " (" + worst + ")" : "");
            }

            return sb.ToString();
        }

        void CaptureTargetRest()
        {
            targetAnimator = GetComponent<Animator>();
            targetBones.Clear();
            if (targetAnimator == null || targetAnimator.avatar == null || !targetAnimator.avatar.isHuman)
                return;

            // Forearms, wrists, fingers, and the palm reference joints (normal + across) even when fingers are not driven.
            foreach (var bone in HandFrameRetargeting.SnapshotBones)
                AddTargetBone(bone);

            targetFingertips = HandFrameRetargeting.ComputeFingertips(targetAnimator);
            // Reference = the pose the mesh was skinned in, not the Humanoid muscle-zero T-pose (which may turn
            // finger bones about their own axis, invisible to joint positions).
            var fromBind = HandFrameRetargeting.SnapshotFromSkinBindPose(targetAnimator, transform,
                HandFrameRetargeting.SnapshotBones, targetFingertips, targetRest);
            if (fromBind == 0)
                FillTargetSnapshot(targetRest);
        }

        void AddTargetBone(HumanBodyBones bone)
        {
            var t = targetAnimator.GetBoneTransform(bone);
            if (t != null)
                targetBones[bone] = t;
        }

        void FillTargetSnapshot(HandFrameRetargeting.Snapshot snapshot)
        {
            snapshot.Clear();
            var root = transform;
            var rootInv = Quaternion.Inverse(root.rotation);
            foreach (var pair in targetBones)
            {
                var t = pair.Value;
                snapshot.Set(pair.Key, root.InverseTransformPoint(t.position), rootInv * t.rotation);
                // Fingertips from the mesh (MakeHuman's mixamo rig has no fingertip joints).
                if (targetFingertips.TryGetValue(pair.Key, out var localTip))
                    snapshot.tips[pair.Key] = root.InverseTransformPoint(t.TransformPoint(localTip));
            }
        }

        void FillSourceSnapshot(HandFrameRetargeting.Snapshot snapshot, IList<OVRBone> bones)
        {
            snapshot.Clear();
            var root = transform;
            var rootInv = Quaternion.Inverse(root.rotation);
            var mapping = BodyBoneMappingsInterface.GetFullBodyBoneIdToHumanBodyBone;
            for (var i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone?.Transform == null)
                    continue;

                var position = root.InverseTransformPoint(bone.Transform.position);
                if (TipToDistal.TryGetValue(bone.Id, out var distal))
                {
                    snapshot.tips[distal] = position;
                    continue;
                }

                if (mapping.TryGetValue(bone.Id, out var human) && targetBones.ContainsKey(human))
                    snapshot.Set(human, position, rootInv * bone.Transform.rotation);
            }
        }

        void CalibrateHands()
        {
            calibratedForCount = SkeletonChangedCount;
            sourceBoneIndex.Clear();
            FillSourceSnapshot(sourceBind, BindPoses);

            var mapping = BodyBoneMappingsInterface.GetFullBodyBoneIdToHumanBodyBone;
            for (var i = 0; i < Bones.Count; i++)
            {
                if (Bones[i] != null && mapping.TryGetValue(Bones[i].Id, out var human) && targetBones.ContainsKey(human))
                    sourceBoneIndex[human] = i;
            }

            if (!hands.Calibrate(sourceBind, targetRest, includeForearm: true, includeFingers: driveFingers))
            {
                handSummary = "calibration failed (source " + sourceBind.joints.Count + " / avatar " + targetRest.joints.Count + " joints)";
                Debug.LogWarning("[AvianVoice] Hand retargeting: " + handSummary + "; the SDK keeps driving the wrists.");
                return;
            }

            var sb = new StringBuilder();
            sb.Append(hands.BoneCount).Append(" bones; SDK direction-only roll error at bind:");
            foreach (var bone in new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand })
            {
                if (hands.DirectionOnlyRollError.TryGetValue(bone, out var deg))
                    sb.Append(' ').Append(bone).Append('=').Append(deg.ToString("0"));
            }

            var maxFinger = 0f;
            foreach (var pair in hands.DirectionOnlyRollError)
            {
                if (HandFrameRetargeting.IsFinger(pair.Key))
                    maxFinger = Mathf.Max(maxFinger, pair.Value);
            }

            sb.Append(" fingersMax=").Append(maxFinger.ToString("0"));
            handSummary = sb.ToString();
            Debug.Log("[AvianVoice] Hand retargeting calibrated on '" + name + "': " + handSummary);
        }

        void ApplyHandFrames()
        {
            if (!IsInitialized || !IsDataValid || Bones == null || BindPoses == null || BindPoses.Count == 0 ||
                targetBones.Count == 0)
                return;

            if (calibratedForCount != SkeletonChangedCount)
                CalibrateHands();
            if (!hands.IsCalibrated)
                return;

            foreach (var bone in HandFrameRetargeting.ApplyOrder)
            {
                if (!hands.TryGetCorrection(bone, out var correction) ||
                    !sourceBoneIndex.TryGetValue(bone, out var index) ||
                    !targetBones.TryGetValue(bone, out var target))
                    continue;

                var source = Bones[index];
                if (source?.Transform == null)
                    continue;

                target.rotation = source.Transform.rotation * correction;
            }
        }
    }
}
#endif
