using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// Selects and plays bird call clips by pitch. Generates procedural chirps if no clips assigned.
    /// </summary>
    public class BirdCallLibrary : MonoBehaviour
    {
        [System.Serializable]
        public class PitchedClip
        {
            public string label;
            public AudioClip clip;
            public float basePitchHz = 440f;
        }

        [SerializeField]
        PitchedClip[] clips = new PitchedClip[0];

        [SerializeField]
        float proceduralDuration = 0.15f;

        AudioClip[] proceduralCache;
        float lastPlayTime = -1f;
        float smoothedPitchHz;

        void Awake()
        {
            if (clips == null || clips.Length == 0)
                BuildProceduralClips();
        }

        void BuildProceduralClips()
        {
            var baseHz = new[] { 220f, 330f, 440f, 554f, 659f, 880f };
            clips = new PitchedClip[baseHz.Length];
            proceduralCache = new AudioClip[baseHz.Length];
            for (var i = 0; i < baseHz.Length; i++)
            {
                proceduralCache[i] = CreateChirpClip(baseHz[i], proceduralDuration);
                clips[i] = new PitchedClip
                {
                    label = $"Chirp_{baseHz[i]:0}",
                    clip = proceduralCache[i],
                    basePitchHz = baseHz[i]
                };
            }
        }

        /// <summary>Cooldown until the next Transform chirp can fire (matches clip duration when enabled).</summary>
        public float GetTransformCooldown(float targetHz, AvianVoiceSettings settings)
        {
            if (settings == null || clips == null || clips.Length == 0 || targetHz <= 0f)
                return 0.12f;

            if (!settings.transformCooldownUsesClipLength)
            {
                return Mathf.Clamp(
                    settings.transformCooldown,
                    settings.minTransformCooldown,
                    settings.maxTransformCooldown);
            }

            var mappedHz = GetSmoothedPitchForSelection(targetHz, settings);
            var entry = FindNearestEntry(mappedHz);
            if (entry?.clip == null)
            {
                return Mathf.Clamp(
                    settings.transformCooldown,
                    settings.minTransformCooldown,
                    settings.maxTransformCooldown);
            }

            var ratio = ComputePlaybackPitchRatio(mappedHz, entry, settings);
            var effectiveSeconds = entry.clip.length / Mathf.Max(ratio, 0.01f);
            effectiveSeconds += settings.transformClipGapSeconds;
            return Mathf.Clamp(
                effectiveSeconds,
                settings.minTransformCooldown,
                settings.maxTransformCooldown);
        }

        /// <summary>Task / manual playback without transform gating.</summary>
        public void PlayNearest(AudioSource source, float targetHz, float loudness, float cooldown)
        {
            PlayInternal(source, targetHz, loudness, cooldown, null);
        }

        public void PlayNearest(AudioSource source, float targetHz, float loudness, AvianVoiceSettings settings)
        {
            if (settings == null)
            {
                PlayNearest(source, targetHz, loudness, 0.12f);
                return;
            }

            var cooldown = GetTransformCooldown(targetHz, settings);
            PlayInternal(source, targetHz, loudness, cooldown, settings);
        }

        void PlayInternal(
            AudioSource source,
            float targetHz,
            float loudness,
            float cooldown,
            AvianVoiceSettings settings)
        {
            if (source == null || clips == null || clips.Length == 0 || targetHz <= 0f)
                return;

            if (Time.time - lastPlayTime < cooldown)
                return;

            if (loudness < 0.05f)
                return;

            if (settings != null && settings.transformBlockOverlap && source.isPlaying)
                return;

            var mappedHz = GetSmoothedPitchForSelection(targetHz, settings);
            var entry = FindNearestEntry(mappedHz);
            if (entry == null || entry.clip == null)
                return;

            var minPitch = settings != null ? settings.transformMinPitch : 0.5f;
            var maxPitch = settings != null ? settings.transformMaxPitch : 2f;
            source.pitch = Mathf.Clamp(ComputePlaybackPitchRatio(mappedHz, entry, settings), minPitch, maxPitch);

            var volume = Mathf.Pow(Mathf.Clamp01(loudness), 0.6f);
            if (settings != null)
                volume = Mathf.Lerp(settings.transformVolumeMin, settings.transformVolumeMax, volume);
            else
                volume = Mathf.Clamp01(volume);

            source.volume = volume;
            source.PlayOneShot(entry.clip);
            lastPlayTime = Time.time;
        }

        float GetSmoothedPitchForSelection(float targetHz, AvianVoiceSettings settings)
        {
            if (settings == null || settings.pitchSmooth <= 0f)
                return targetHz;

            var dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            var smooth = 1f - Mathf.Exp(-settings.pitchSmooth * dt);
            smoothedPitchHz = smoothedPitchHz <= 0f
                ? targetHz
                : Mathf.Lerp(smoothedPitchHz, targetHz, smooth);
            return smoothedPitchHz;
        }

        PitchedClip FindNearestEntry(float mappedHz)
        {
            var best = 0;
            var bestDelta = float.MaxValue;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i].clip == null)
                    continue;
                var delta = Mathf.Abs(clips[i].basePitchHz - mappedHz);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = i;
                }
            }

            return clips[best];
        }

        static float ComputePlaybackPitchRatio(float mappedHz, PitchedClip entry, AvianVoiceSettings settings)
        {
            if (settings != null && settings.transformPitchFromReference && settings.referencePitchHz > 0f)
                return mappedHz / settings.referencePitchHz;
            return mappedHz / Mathf.Max(entry.basePitchHz, 1f);
        }

        static AudioClip CreateChirpClip(float frequency, float duration)
        {
            const int sampleRate = 48000;
            var sampleCount = Mathf.Max(64, Mathf.RoundToInt(sampleRate * duration));
            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var env = Mathf.Exp(-t * 18f);
                var tone = Mathf.Sin(2f * Mathf.PI * frequency * t);
                var noise = (Random.value * 2f - 1f) * 0.08f;
                data[i] = (tone + noise) * env;
            }

            var clip = AudioClip.Create($"BirdChirp_{frequency:0}", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
