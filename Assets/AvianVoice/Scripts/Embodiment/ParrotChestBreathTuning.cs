using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Shared chest scale tuning for player and friend parrots.
    /// </summary>
    public static class ParrotChestBreathTuning
    {
        public const float AmplitudeMultiplier = 2.25f;
        public const float ChestInhaleScale = 0.22f;
        public const float SecondaryChestInhaleScale = 0.15f;
        public const float EmphasisPower = 0.55f;

        public static float DepthToEmphasis(float depth) =>
            Mathf.Pow(Mathf.Clamp01(depth), EmphasisPower);

        public static float ComputeChestFactor(float inhaleScale, float emphasis) =>
            1f + emphasis * inhaleScale * AmplitudeMultiplier;
    }
}
