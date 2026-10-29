using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Shared voice-driven motion: how strongly the mic is speaking, and a small vertical bob.
    /// </summary>
    public static class BirdPresenceMotion
    {
        public static float VocalEnergy(VoiceFrame voice)
        {
            var loud = Mathf.Clamp01(voice.loudness);
            if (loud < 0.05f)
                return 0f;
            if (voice.voiced > 0.5f)
                return loud;
            return loud * 0.55f;
        }

        public static float FloatHeight(float energy, float time, float liftMeters, float bobMeters, float bobHz)
        {
            var e = Mathf.Clamp01(energy);
            if (e < 0.01f)
                return 0f;
            return e * liftMeters + Mathf.Sin(time * bobHz * Mathf.PI * 2f) * bobMeters * e;
        }
    }
}
