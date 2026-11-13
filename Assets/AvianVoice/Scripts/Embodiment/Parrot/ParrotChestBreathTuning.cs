using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Shared chest scale tuning for parrots. Values come from the AvianVoiceSettings asset
    /// (Teacher Breath section) so they can be tuned live in the Inspector.
    /// </summary>
    public static class ParrotChestBreathTuning
    {
        const float DefaultChestInhaleScale = 0.40f;
        const float DefaultBellyInhaleScale = 0.28f;
        const float DefaultBreathMotionScale = 0.65f;

        public static float ChestInhaleScale
        {
            get
            {
                var settings = AvianVoiceSettings.FindLoaded();
                return settings != null ? Mathf.Max(0f, settings.chestInhaleScale) : DefaultChestInhaleScale;
            }
        }

        public static float SecondaryChestInhaleScale
        {
            get
            {
                var settings = AvianVoiceSettings.FindLoaded();
                return settings != null ? Mathf.Max(0f, settings.bellyInhaleScale) : DefaultBellyInhaleScale;
            }
        }

        /// <summary>
        /// Share of the breathing motion that is shown on the bird (chest, belly and breath wings), 0..1.
        /// Applies to the breath only, not to the singing (performance) poses.
        /// </summary>
        public static float BreathMotionScale
        {
            get
            {
                var settings = AvianVoiceSettings.FindLoaded();
                return settings != null ? Mathf.Clamp01(settings.breathMotionScale) : DefaultBreathMotionScale;
            }
        }

        public static float DepthToEmphasis(float depth)
        {
            var settings = AvianVoiceSettings.FindLoaded();
            var power = settings != null ? Mathf.Max(0.1f, settings.breathCurvePower) : 1f;
            return Mathf.Pow(Mathf.Clamp01(depth), power);
        }

        public static float ComputeChestFactor(float inhaleScale, float emphasis) =>
            1f + emphasis * inhaleScale;
    }
}
