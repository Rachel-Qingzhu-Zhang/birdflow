using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Breath energy on a parrot whose pack animator is off: folded, open, then a light flap.
    /// Angles stay small so the skinned wing does not tear.
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
                spread = 18f;
                var rate = inhaling ? 7.2f : 3.2f;
                flap = Mathf.Sin(time * rate) * 10f * energy;
            }
            else if (energy >= 0.28f)
            {
                var open = Mathf.InverseLerp(0.28f, 0.7f, energy);
                spread = Mathf.Lerp(5f, 16f, open);
            }

            var pitch = energy >= 0.28f ? -5f * energy : 0f;
            ApplyOne(leftWing, leftRest, pitch, spread + flap, true);
            ApplyOne(rightWing, rightRest, pitch, spread + flap, false);
        }

        public static void Restore(Transform leftWing, Transform rightWing, Quaternion leftRest, Quaternion rightRest)
        {
            if (leftWing != null)
                leftWing.localRotation = leftRest;
            if (rightWing != null)
                rightWing.localRotation = rightRest;
        }

        static void ApplyOne(Transform wing, Quaternion rest, float pitch, float spread, bool isLeft)
        {
            if (wing == null)
                return;

            var roll = (isLeft ? 1f : -1f) * spread;
            wing.localRotation = rest * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
