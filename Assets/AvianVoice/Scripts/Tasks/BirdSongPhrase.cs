using UnityEngine;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// Shared melody for singing and calling friends. Values are human Hz.
    /// The bird synth transposes them with the same ratio as the player's voice,
    /// so the note you hear is the note you are asked to sing.
    /// </summary>
    public static class BirdSongPhrase
    {
        public static readonly float[] Notes = { 330f, 392f, 440f, 494f };

        public const float GuideSeconds = 0.7f;
        public const float GapAfterGuide = 0.28f;
        public const float PauseBetweenNotes = 0.45f;

        public static float ListenSeconds => GuideSeconds + GapAfterGuide;

        /// <summary>
        /// 1 when sungHz is the same pitch class as the target, within toleranceHz at the target octave.
        /// Any octave counts, so a bird-register guide can be answered in the singer's own range.
        /// </summary>
        public static float Match(float sungHz, float targetHz, float toleranceHz)
        {
            if (sungHz <= 0f || targetHz <= 0f)
                return 0f;

            var semis = Mathf.Abs(12f * Mathf.Log(sungHz / targetHz, 2f));
            var wrapped = Mathf.Repeat(semis, 12f);
            if (wrapped > 6f)
                wrapped = 12f - wrapped;

            var window = 12f * Mathf.Log((targetHz + Mathf.Max(1f, toleranceHz)) / targetHz, 2f);
            return Mathf.Clamp01(1f - wrapped / Mathf.Max(0.05f, window));
        }

        public static string NoteName(float hz)
        {
            if (hz < 360f) return "E4";
            if (hz < 420f) return "G4";
            if (hz < 470f) return "A4";
            if (hz < 510f) return "B4";
            if (hz < 555f) return "C5";
            if (hz < 620f) return "D5";
            return "E5";
        }
    }
}
