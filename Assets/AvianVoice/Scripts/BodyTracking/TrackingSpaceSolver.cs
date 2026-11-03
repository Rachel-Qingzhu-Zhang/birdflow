using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// Solves the rigid (yaw + translation) transform from the body tracking space to Unity world space
    /// using points that exist in both: the head (headset) and, when available, the wrists (controllers or
    /// tracked hands). Both spaces are gravity aligned, so pitch and roll are never solved.
    /// </summary>
    public sealed class TrackingSpaceSolver
    {
        /// <summary>Exponential smoothing rate for the yaw estimate (per second).</summary>
        public float YawSmoothing = 3f;

        /// <summary>Minimum horizontal head-to-hand distance before a hand is used to solve yaw.</summary>
        public float MinBaselineMeters = 0.12f;

        float yawEstimate;
        bool hasYaw;

        public Pose TrackingToWorld { get; private set; } = Pose.identity;

        public bool HasSolution { get; private set; }

        public float YawDegrees => yawEstimate;

        public void Reset()
        {
            yawEstimate = 0f;
            hasYaw = false;
            HasSolution = false;
            TrackingToWorld = Pose.identity;
        }

        public void Update(
            BodyPoseFrame frame,
            Pose cameraWorld,
            Vector3? leftHandWorld,
            Vector3? rightHandWorld,
            float deltaTime)
        {
            if (frame == null || !frame.Has(BodyJoint.Head))
            {
                HasSolution = false;
                return;
            }

            var headTracking = frame[BodyJoint.Head].position;
            var measured = float.NaN;
            var bestBaseline = 0f;
            Accumulate(frame, BodyJoint.LeftWrist, leftHandWorld, headTracking, cameraWorld.position, ref measured, ref bestBaseline);
            Accumulate(frame, BodyJoint.RightWrist, rightHandWorld, headTracking, cameraWorld.position, ref measured, ref bestBaseline);

            if (!float.IsNaN(measured))
            {
                if (!hasYaw)
                {
                    yawEstimate = measured;
                    hasYaw = true;
                }
                else
                {
                    var t = 1f - Mathf.Exp(-YawSmoothing * Mathf.Max(0f, deltaTime));
                    yawEstimate = Mathf.LerpAngle(yawEstimate, measured, t);
                }
            }

            var rotation = Quaternion.Euler(0f, yawEstimate, 0f);
            var position = cameraWorld.position - rotation * headTracking;
            TrackingToWorld = new Pose(position, rotation);
            HasSolution = true;
        }

        void Accumulate(
            BodyPoseFrame frame,
            BodyJoint wrist,
            Vector3? handWorld,
            Vector3 headTracking,
            Vector3 headWorld,
            ref float measured,
            ref float bestBaseline)
        {
            if (!handWorld.HasValue || !frame.Has(wrist))
                return;

            var trackingBaseline = frame[wrist].position - headTracking;
            var worldBaseline = handWorld.Value - headWorld;
            trackingBaseline.y = 0f;
            worldBaseline.y = 0f;

            var length = Mathf.Min(trackingBaseline.magnitude, worldBaseline.magnitude);
            if (length < MinBaselineMeters || length <= bestBaseline)
                return;

            bestBaseline = length;
            measured = Vector3.SignedAngle(trackingBaseline, worldBaseline, Vector3.up);
        }

        public static Pose Compose(Pose space, Pose local)
        {
            return new Pose(space.position + space.rotation * local.position, space.rotation * local.rotation);
        }

        public static Vector3 TransformPoint(Pose space, Vector3 local)
        {
            return space.position + space.rotation * local;
        }

        public static Vector3 TransformDirection(Pose space, Vector3 local)
        {
            return space.rotation * local;
        }

        public static float YawOf(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                return 0f;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }
    }
}
