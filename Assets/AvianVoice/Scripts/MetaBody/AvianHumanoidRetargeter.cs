#if AVIAN_META_XR_CORE
using System;
using System.Collections.Generic;
using UnityEngine;
using BodySection = OVRUnityHumanoidSkeletonRetargeter.OVRHumanBodyBonesMappings.BodySection;

namespace AvianVoice.MetaBody
{
    /// <summary>
    /// <see cref="OVRUnityHumanoidSkeletonRetargeter"/> configured for a stable first-person avatar:
    /// <list type="bullet">
    /// <item>full-body joint set (IOBT / full body tracking);</item>
    /// <item>rotation-only retargeting: only the hips take the tracked position, every other bone keeps the
    /// avatar's own bone length (the SDK default writes the tracked world position into almost every joint,
    /// which stretches and collapses a body whose proportions differ from the user's);</item>
    /// <item>fingers are not driven: they keep the rest pose and follow the wrist (driving them from the
    /// tracked hand joints crumples hands whose finger axes differ from Meta's);</item>
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

        [Tooltip("Also retarget the finger bones from the tracked hands (off: fingers keep the rest pose).")]
        public bool driveFingers;

        [Tooltip("Write the tracked hips position (crouching, leaning). Off: only rotations; the owner places the root.")]
        public bool positionHips = true;

        public bool RestPoseCaptured { get; private set; }

        protected override void Awake()
        {
            _skeletonType = SkeletonType.FullBody;
            _adjustments = Array.Empty<JointAdjustment>();

            var upper = new List<BodySection>
            {
                BodySection.Hips, BodySection.Back, BodySection.Neck, BodySection.Head,
                BodySection.LeftArm, BodySection.RightArm
            };
            if (driveFingers)
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
            base.Start();
            RestPoseCaptured = true;
            Debug.Log("[AvianVoice] Humanoid retargeter on '" + name + "': rest pose captured (rotation-only"
                      + (positionHips ? " + hips position" : "") + ", fingers " + (driveFingers ? "driven" : "kept") + ").");
        }
    }
}
#endif
