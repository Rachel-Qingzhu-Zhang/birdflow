using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// Backend-agnostic body joints. Values mirror the Meta Quest body tracking joint set
    /// (upper body + legs when full-body / IOBT is available).
    /// </summary>
    public enum BodyJoint
    {
        Root,
        Hips,
        SpineLower,
        SpineMiddle,
        SpineUpper,
        Chest,
        Neck,
        Head,
        LeftShoulder,
        LeftUpperArm,
        LeftLowerArm,
        LeftWrist,
        RightShoulder,
        RightUpperArm,
        RightLowerArm,
        RightWrist,
        LeftUpperLeg,
        LeftLowerLeg,
        LeftFoot,
        RightUpperLeg,
        RightLowerLeg,
        RightFoot,
        Count
    }

    /// <summary>
    /// One frame of body tracking. Poses are in the backend tracking space
    /// (Unity axes, floor-level origin). Reused between frames to avoid allocation.
    /// </summary>
    public sealed class BodyPoseFrame
    {
        public bool IsValid;
        public float Confidence;
        public bool IsFullBody;
        public double Time;

        public readonly Pose[] Joints = new Pose[(int)BodyJoint.Count];
        public readonly bool[] JointValid = new bool[(int)BodyJoint.Count];

        public Pose this[BodyJoint joint] => Joints[(int)joint];

        public bool Has(BodyJoint joint) => IsValid && JointValid[(int)joint];

        public void Set(BodyJoint joint, Vector3 position, Quaternion rotation)
        {
            Joints[(int)joint] = new Pose(position, rotation);
            JointValid[(int)joint] = true;
        }

        public void Clear()
        {
            IsValid = false;
            Confidence = 0f;
            IsFullBody = false;
            for (var i = 0; i < JointValid.Length; i++)
            {
                JointValid[i] = false;
                Joints[i] = Pose.identity;
            }
        }
    }
}
