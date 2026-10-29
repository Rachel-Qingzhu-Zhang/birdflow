using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// Real-time bird whistle synthesizer driven by the singer's pitch and loudness.
    /// Human pitch is mapped log-linearly into the bird range; the tone gets a fast trill,
    /// an upward onset glide, a soft 2nd harmonic and a little breathy noise.
    /// Parameters are written on the main thread and read on the audio thread.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class BirdSongSynth : MonoBehaviour
    {
        const float TwoPi = Mathf.PI * 2f;

        AvianVoiceSettings settings;
        AudioSource source;

        // Main thread -> audio thread.
        volatile float targetHz = 3000f;
        volatile float targetGate;
        volatile float targetLevel;
        volatile float trillRate = 22f;
        volatile float trillDepthSemis = 1.2f;
        volatile float glideSemis = 5f;
        volatile float glideSeconds = 0.12f;
        volatile float attackSeconds = 0.02f;
        volatile float releaseSeconds = 0.12f;
        volatile float harmonic2 = 0.18f;
        volatile float noiseLevel = 0.06f;
        volatile float volume = 0.5f;
        volatile float pitchSmoothSeconds = 0.04f;

        // Audio thread state.
        int sampleRate = 48000;
        double phase;
        double trillPhase;
        float currentHz = 3000f;
        float envelope;
        float glideRemaining;
        bool gateWasOpen;
        float svfLow;
        float svfBand;
        uint noiseState = 0x9E3779B9u;

        public bool IsSounding => targetGate > 0.5f;

        public static BirdSongSynth Ensure(Transform parent, AudioSource template, AvianVoiceSettings voiceSettings)
        {
            if (parent == null)
                return null;

            var existing = parent.GetComponentInChildren<BirdSongSynth>(true);
            if (existing != null)
            {
                var existingSource = existing.GetComponent<AudioSource>();
                if (existingSource != null)
                {
                    existingSource.spatialBlend = 0f;
                    existingSource.spatialize = false;
                    existingSource.volume = 1f;
                }

                existing.Configure(voiceSettings);
                return existing;
            }

            var go = new GameObject("BirdSongSynth");
            go.transform.SetParent(parent, false);
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.clip = null;
            audio.spatialBlend = 0f;
            audio.spatialize = false;
            audio.dopplerLevel = 0f;
            audio.volume = 1f;
            if (template != null)
                audio.outputAudioMixerGroup = template.outputAudioMixerGroup;

            var synth = go.AddComponent<BirdSongSynth>();
            synth.Configure(voiceSettings);
            return synth;
        }

        /// <summary>Log-linear map: human [minHz, maxHz] onto bird [minHz, maxHz].</summary>
        public static float MapHumanToBirdHz(float humanHz, AvianVoiceSettings s)
        {
            var hMin = s != null ? Mathf.Max(20f, s.synthHumanMinHz) : 90f;
            var hMax = s != null ? Mathf.Max(hMin + 1f, s.synthHumanMaxHz) : 900f;
            var bMin = s != null ? Mathf.Max(100f, s.synthBirdMinHz) : 1800f;
            var bMax = s != null ? Mathf.Max(bMin + 1f, s.synthBirdMaxHz) : 6000f;
            var h = Mathf.Clamp(humanHz, hMin, hMax);
            var t = Mathf.Log(h / hMin) / Mathf.Log(hMax / hMin);
            return bMin * Mathf.Pow(bMax / bMin, t);
        }

        public void Configure(AvianVoiceSettings voiceSettings)
        {
            settings = voiceSettings;
            PushSettings();
        }

        /// <summary>Call every frame with the latest voice analysis.</summary>
        public void SetVoice(bool voiced, float humanPitchHz, float loudness)
        {
            if (voiced && humanPitchHz > 0f)
            {
                targetHz = MapHumanToBirdHz(humanPitchHz, settings);
                var loud = Mathf.Clamp01(loudness);
                targetLevel = Mathf.Lerp(0.35f, 1f, loud);
                var minRate = settings != null ? settings.synthTrillRateMin : 18f;
                var maxRate = settings != null ? settings.synthTrillRateMax : 28f;
                trillRate = Mathf.Lerp(minRate, maxRate, loud);
                targetGate = 1f;
            }
            else
            {
                targetGate = 0f;
            }

            EnsurePlaying();
        }

        public void Silence() => targetGate = 0f;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            if (source != null && !source.isPlaying && isActiveAndEnabled)
                source.Play();
        }

        void OnEnable()
        {
            EnsurePlaying();
        }

        void OnDisable()
        {
            targetGate = 0f;
        }

        void Update()
        {
            // Re-read every frame so Inspector edits apply live.
            PushSettings();
        }

        void EnsurePlaying()
        {
            if (source == null)
                source = GetComponent<AudioSource>();
            if (source != null && !source.isPlaying && isActiveAndEnabled)
                source.Play();
        }

        void PushSettings()
        {
            if (settings == null)
                return;

            trillDepthSemis = Mathf.Max(0f, settings.synthTrillDepthSemitones);
            glideSemis = Mathf.Max(0f, settings.synthOnsetGlideSemitones);
            glideSeconds = Mathf.Max(0.001f, settings.synthOnsetGlideSeconds);
            attackSeconds = Mathf.Max(0.001f, settings.synthAttackSeconds);
            releaseSeconds = Mathf.Max(0.001f, settings.synthReleaseSeconds);
            harmonic2 = Mathf.Clamp01(settings.synthHarmonic2);
            noiseLevel = Mathf.Clamp01(settings.synthNoise);
            volume = Mathf.Clamp01(settings.synthVolume);
            pitchSmoothSeconds = Mathf.Max(0.001f, settings.synthPitchSmoothSeconds);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels <= 0)
                return;

            var fs = (float)sampleRate;
            var dt = 1f / fs;
            var gateOpen = targetGate > 0.5f;
            var goalHz = targetHz;
            var level = targetLevel;
            var rate = trillRate;
            var trillDepth = trillDepthSemis;
            var glideSpan = glideSemis;
            var glideTime = glideSeconds;
            var h2 = harmonic2;
            var noiseAmp = noiseLevel;
            var gain = volume;

            var attackCoeff = 1f - Mathf.Exp(-dt / attackSeconds);
            var releaseCoeff = 1f - Mathf.Exp(-dt / releaseSeconds);
            var pitchCoeff = 1f - Mathf.Exp(-dt / pitchSmoothSeconds);

            if (gateOpen && !gateWasOpen)
            {
                glideRemaining = glideTime;
                // Start a fresh note at its target pitch when the previous one has faded.
                if (envelope < 0.05f)
                    currentHz = goalHz;
            }

            gateWasOpen = gateOpen;
            var envTarget = gateOpen ? level : 0f;

            var frames = data.Length / channels;
            for (var n = 0; n < frames; n++)
            {
                envelope += (envTarget - envelope) * (envTarget > envelope ? attackCoeff : releaseCoeff);

                if (envelope < 0.0001f && !gateOpen)
                {
                    for (var c = 0; c < channels; c++)
                        data[n * channels + c] = 0f;
                    continue;
                }

                currentHz += (goalHz - currentHz) * pitchCoeff;

                var glideOffset = 0f;
                if (glideRemaining > 0f)
                {
                    glideOffset = -glideSpan * (glideRemaining / glideTime);
                    glideRemaining -= dt;
                }

                trillPhase += rate * dt;
                if (trillPhase >= 1.0)
                    trillPhase -= 1.0;
                var trill = Mathf.Sin((float)(trillPhase * TwoPi));

                var semis = glideOffset + trill * trillDepth;
                var hz = currentHz * Mathf.Pow(2f, semis / 12f);

                phase += hz * dt;
                if (phase >= 1.0)
                    phase -= 1.0;
                var p = (float)(phase * TwoPi);
                var tone = Mathf.Sin(p) + h2 * Mathf.Sin(2f * p);

                // Band-pass the noise around the whistle so it reads as breath, not hiss.
                var white = NextNoise();
                var fc = Mathf.Min(hz, fs * 0.16f);
                var f = 2f * Mathf.Sin(Mathf.PI * fc / fs);
                var high = white - svfLow - 0.7f * svfBand;
                svfBand += f * high;
                svfLow += f * svfBand;
                var noise = svfBand * noiseAmp * 2f;

                // Slight amplitude flutter in time with the trill.
                var flutter = 0.85f + 0.15f * trill;
                var sample = (tone / (1f + h2) + noise) * envelope * flutter * gain;

                for (var c = 0; c < channels; c++)
                    data[n * channels + c] = sample;
            }
        }

        float NextNoise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}
