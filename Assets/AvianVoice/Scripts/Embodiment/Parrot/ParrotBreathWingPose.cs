using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Breath wing motion for the pack parrot. The left and right humerus bones share nearly the
    /// same local Z axis in world space (both point along the bird's right), so Euler Z mirroring
    /// twists one wing. Spread is applied around the bird's forward axis instead.
    /// </summary>
    public static class ParrotBreathWingPose
    {
        public static void Apply(
            Transform leftWing,
            Transform rightWing,
            Quaternion leftRest,
            Quaternion rightRest,
            float depth,
            bool inhaling,
            float time)
        {
            var energy = Mathf.Clamp01(depth);
            var spread = 0f;
            var flap = 0f;

            if (energy >= 0.7f)
            {
                spread = 14f;
                var rate = inhaling ? 6.5f : 3.0f;
                flap = Mathf.Sin(time * rate) * 6f * energy;
            }
            else if (energy >= 0.28f)
            {
                var open = Mathf.InverseLerp(0.28f, 0.7f, energy);
                spread = Mathf.Lerp(4f, 12f, open);
            }

            var lift = energy >= 0.28f ? 5f * energy : 0f;
            var frame = ResolveBirdFrame(leftWing, rightWing);
            ApplyOne(leftWing, leftRest, lift, spread + flap, true, frame);
            ApplyOne(rightWing, rightRest, lift, spread + flap, false, frame);
        }

        /// <summary>
        /// Open flapping pose for the flight reflection. Spread stays wide so the pond
        /// shows a flying bird even when the glide clip tucks the wings.
        /// </summary>
        public static void ApplyFlight(
            Transform leftWing,
            Transform rightWing,
            Quaternion leftRest,
            Quaternion rightRest,
            float time)
        {
            const float spread = 46f;
            var flap = Mathf.Sin(time * 7.5f) * 16f;
            const float lift = 10f;
            var frame = ResolveBirdFrame(leftWing, rightWing);
            ApplyOne(leftWing, leftRest, lift, spread + flap, true, frame);
            ApplyOne(rightWing, rightRest, lift, spread + flap, false, frame);
        }

        public static void Restore(Transform leftWing, Transform rightWing, Quaternion leftRest, Quaternion rightRest)
        {
            if (leftWing != null)
                leftWing.localRotation = leftRest;
            if (rightWing != null)
                rightWing.localRotation = rightRest;
        }

        static void ApplyOne(
            Transform wing,
            Quaternion rest,
            float liftDeg,
            float spreadDeg,
            bool isLeft,
            BirdFrame frame)
        {
            if (wing == null || wing.parent == null)
                return;

            // Parent-space extra keeps L/R as mirrors of the rest pose.
            // Open around bird forward with opposite signs so both wings lift out.
            // Lift around the shoulder line uses the SAME sign so both wings pitch together.
            var parent = wing.parent;
            var openAxis = parent.InverseTransformDirection(frame.forward);
            var liftAxis = parent.InverseTransformDirection(frame.right);
            if (openAxis.sqrMagnitude < 0.0001f || liftAxis.sqrMagnitude < 0.0001f)
                return;

            openAxis.Normalize();
            liftAxis.Normalize();
            var extra = Quaternion.AngleAxis((isLeft ? -1f : 1f) * spreadDeg, openAxis)
                        * Quaternion.AngleAxis(liftDeg, liftAxis);
            wing.localRotation = extra * rest;
        }

        struct BirdFrame
        {
            public Vector3 forward;
            public Vector3 right;
        }

        static BirdFrame ResolveBirdFrame(Transform leftWing, Transform rightWing)
        {
            var root = leftWing != null ? leftWing.root : rightWing != null ? rightWing.root : null;
            Vector3 across;
            if (leftWing != null && rightWing != null)
            {
                across = rightWing.position - leftWing.position;
                across.y = 0f;
            }
            else
            {
                across = root != null ? root.right : Vector3.right;
                across.y = 0f;
            }

            if (across.sqrMagnitude < 0.0001f)
                across = Vector3.right;
            across.Normalize();

            var forward = Vector3.Cross(across, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            var nose = root != null ? root.forward : Vector3.forward;
            nose.y = 0f;
            if (nose.sqrMagnitude > 0.0001f && Vector3.Dot(forward, nose) < 0f)
                forward = -forward;

            return new BirdFrame
            {
                forward = forward,
                right = across
            };
        }
    }
}
