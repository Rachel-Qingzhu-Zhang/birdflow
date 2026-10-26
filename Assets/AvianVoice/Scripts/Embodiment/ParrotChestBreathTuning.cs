using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Shared chest scale tuning for player and friend parrots.
    /// </summary>
    public static class ParrotChestBreathTuning
    {
        // Linear map: depth 0.5 → +20% chest, depth 1 → +40% chest.
        // Belly uses the same breath signal at a slightly smaller amount.
        public const float AmplitudeMultiplier = 1f;
        public const float ChestInhaleScale = 0.40f;
        public const float SecondaryChestInhaleScale = 0.28f;
        public const float EmphasisPower = 1f;

        public static float DepthToEmphasis(float depth) =>
            Mathf.Pow(Mathf.Clamp01(depth), EmphasisPower);

        public static float ComputeChestFactor(float inhaleScale, float emphasis) =>
            1f + emphasis * inhaleScale * AmplitudeMultiplier;
    }
}
