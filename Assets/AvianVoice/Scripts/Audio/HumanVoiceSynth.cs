using System.Threading;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// A sung human vowel ("ah") that follows the singer's pitch and loudness without transposition:
    /// a band-limited glottal-like sawtooth with spectral tilt, through four formant resonators.
    /// The human-avatar counterpart of <see cref="BirdSongSynth"/> (the participant hears their own
    /// voice as a human voice instead of as bird chirps). Also sings the teacher's guide note when the
    /// teacher voice follows the avatar. Parameters are written on the main thread and read on the
    /// audio thread.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class HumanVoiceSynth : MonoBehaviour
    {
        const float TwoPi = Mathf.PI * 2f;
        const int FormantCount = 4;

        [Header("Vowel (neutral 'ah', between male and female formants)")]
        [SerializeField]
        float[] formantHz = { 750f, 1200f, 2600f, 3300f };

        [SerializeField]
        float[] formantBandwidthHz = { 90f, 110f, 160f, 220f };

        [SerializeField]
        float[] formantGain = { 1f, 0.55f, 0.3f, 0.2f };

        [Tooltip("One-pole low-pass on the sawtooth source (Hz): lower = darker, more glottal-like.")]
        [SerializeField]
        float sourceTiltHz = 1100f;

        [Header("Expression")]
        [SerializeField]
        float vibratoRateHz = 5.2f;

        [SerializeField]
        float vibratoDepthSemitones = 0.25f;

        [Tooltip("Vibrato fades in over this long after the voice starts.")]
        [SerializeField]
        float vibratoOnsetSeconds = 0.35f;

        [Tooltip("Envelope attack (s). Kept short so the vowel starts with the singer's onset.")]
        [SerializeField]
        float attackSeconds = 0.006f;

        [Tooltip("Envelope release time constant (s) after the voice gate closes: 0.015 reaches -30 dB in about 50 ms. " +
                 "The analyzer's loudness release (~0.012 s) comes before it.")]
        [SerializeField]
        float releaseSeconds = 0.015f;

        [SerializeField]
        float pitchSmoothSeconds = 0.02f;

        [SerializeField]
        float levelSmoothSeconds = 0.015f;

        [SerializeField, Range(0f, 0.3f)]
        float breathNoise = 0.03f;

        [Tooltip("Output level. 1 = a held note lands at about -11 dBFS, the level of the bird chirp guide note (BirdSongSynth), " +
                 "so the human-voice and bird-voice conditions are equally loud.")]
        [SerializeField, Range(0f, 1f)]
        float volume = 1f;

        [SerializeField]
        float minHz = 70f;

        [SerializeField]
        float maxHz = 1100f;

        AudioSource source;

        // Main thread -> audio thread.
        volatile float targetHz = 220f;
        volatile float targetLevel = 0.8f;
        volatile bool followGate;
        // Main thread writes with Interlocked; the audio thread reads a snapshot at the start of a callback
        // and writes the decremented value back only if no new note was requested in the meantime.
        int holdFramesLeft;

        // Audio thread state.
        int sampleRate = 48000;
        double phase;
        double vibratoPhase;
        float currentHz = 220f;
        float envelope;
        float level;
        float tilt;
        float gateSeconds;
        bool gateWasOpen;
        readonly float[] x1 = new float[FormantCount];
        readonly float[] x2 = new float[FormantCount];
        readonly float[] y1 = new float[FormantCount];
        readonly float[] y2 = new float[FormantCount];
        readonly float[] b0 = new float[FormantCount];
        readonly float[] a1 = new float[FormantCount];
        readonly float[] a2 = new float[FormantCount];
        uint noiseState = 0x2545F491u;

        public bool IsSounding => Volatile.Read(ref holdFramesLeft) > 0 || followGate || envelope > 0.02f;

        public static HumanVoiceSynth Ensure(Transform parent, AudioSource template)
        {
            if (parent == null)
                return null;

            var existing = parent.GetComponentInChildren<HumanVoiceSynth>(true);
            if (existing != null)
                return existing;

            var go = new GameObject("HumanVoiceSynth");
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

            return go.AddComponent<HumanVoiceSynth>();
        }

        /// <summary>Call every frame while the singer is voiced: the vowel follows this pitch and loudness.</summary>
        public void Follow(float hz, float loudness)
        {
            if (hz > 0f)
                targetHz = Mathf.Clamp(hz, minHz, maxHz);
            targetLevel = Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(Mathf.Clamp01(loudness)));
            followGate = true;
            EnsurePlaying();
        }

        /// <summary>The singer went silent: close the gate (the release tail still plays).</summary>
        public void Release()
        {
            followGate = false;
        }

        /// <summary>One held note (the teacher's guide note in the human voice).</summary>
        public void PlayNote(float hz, float seconds, float loudness = 0.85f)
        {
            if (hz <= 0f)
                return;

            targetHz = Mathf.Clamp(hz, minHz, maxHz);
            targetLevel = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(loudness));
            Interlocked.Exchange(ref holdFramesLeft, Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.05f, seconds) * sampleRate)));
            EnsurePlaying();
        }

        public void Silence()
        {
            followGate = false;
            Interlocked.Exchange(ref holdFramesLeft, 0);
        }

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
            Silence();
        }

        void EnsurePlaying()
        {
            if (source == null)
                source = GetComponent<AudioSource>();
            if (source != null && !source.isPlaying && isActiveAndEnabled)
                source.Play();
        }

        void UpdateFormantCoefficients(float fs)
        {
            for (var i = 0; i < FormantCount; i++)
            {
                var f = formantHz != null && i < formantHz.Length ? formantHz[i] : 0f;
                var bw = formantBandwidthHz != null && i < formantBandwidthHz.Length ? formantBandwidthHz[i] : 100f;
                if (f <= 0f || f >= fs * 0.45f)
                {
                    b0[i] = 0f;
                    a1[i] = 0f;
                    a2[i] = 0f;
                    continue;
                }

                // RBJ band-pass (constant 0 dB peak gain), Q = f / bandwidth.
                var w0 = TwoPi * f / fs;
                var q = Mathf.Max(0.5f, f / Mathf.Max(10f, bw));
                var alpha = Mathf.Sin(w0) / (2f * q);
                var a0 = 1f + alpha;
                b0[i] = alpha / a0;
                a1[i] = -2f * Mathf.Cos(w0) / a0;
                a2[i] = (1f - alpha) / a0;
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels <= 0)
                return;

            var fs = (float)sampleRate;
            var dt = 1f / fs;
            UpdateFormantCoefficients(fs);

            var framesSnapshot = Volatile.Read(ref holdFramesLeft);
            var framesLeft = framesSnapshot;
            var goalHz = targetHz;
            var goalLevel = targetLevel;
            var follow = followGate;
            var gain = volume;
            var noiseAmp = breathNoise;
            var vibRate = vibratoRateHz;
            var vibDepth = vibratoDepthSemitones;
            var vibOnset = Mathf.Max(0.01f, vibratoOnsetSeconds);
            var tiltCoeff = 1f - Mathf.Exp(-TwoPi * Mathf.Clamp(sourceTiltHz, 100f, fs * 0.45f) / fs);
            var attackCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, attackSeconds));
            var releaseCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, releaseSeconds));
            var pitchCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, pitchSmoothSeconds));
            var levelCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, levelSmoothSeconds));
            var g0 = formantGain != null && formantGain.Length > 0 ? formantGain[0] : 1f;
            var g1 = formantGain != null && formantGain.Length > 1 ? formantGain[1] : 0.5f;
            var g2 = formantGain != null && formantGain.Length > 2 ? formantGain[2] : 0.3f;
            var g3 = formantGain != null && formantGain.Length > 3 ? formantGain[3] : 0.2f;

            var frames = data.Length / channels;
            for (var n = 0; n < frames; n++)
            {
                var open = follow || framesLeft > 0;
                if (framesLeft > 0)
                    framesLeft--;

                if (open && !gateWasOpen)
                {
                    gateSeconds = 0f;
                    if (envelope < 0.05f)
                    {
                        currentHz = goalHz;
                        level = goalLevel;
                    }
                }
                gateWasOpen = open;

                var envTarget = open ? 1f : 0f;
                envelope += (envTarget - envelope) * (envTarget > envelope ? attackCoeff : releaseCoeff);
                if (envelope < 0.0001f && !open)
                {
                    for (var c = 0; c < channels; c++)
                        data[n * channels + c] = 0f;
                    continue;
                }

                if (open)
                    gateSeconds += dt;
                currentHz += (goalHz - currentHz) * pitchCoeff;
                level += (goalLevel - level) * levelCoeff;

                vibratoPhase += vibRate * dt;
                if (vibratoPhase >= 1.0)
                    vibratoPhase -= 1.0;
                var vibAmount = Mathf.Clamp01(gateSeconds / vibOnset) * vibDepth;
                var hz = currentHz * Mathf.Pow(2f, vibAmount * Mathf.Sin((float)(vibratoPhase * TwoPi)) / 12f);

                // Band-limited sawtooth (PolyBLEP) as the glottal source, then a low-pass for spectral tilt.
                var inc = hz * dt;
                phase += inc;
                if (phase >= 1.0)
                    phase -= 1.0;
                var t = (float)phase;
                var saw = 2f * t - 1f - PolyBlep(t, inc);
                tilt += (saw - tilt) * tiltCoeff;
                var excitation = tilt + NextNoise() * noiseAmp;

                var voice = Resonator(0, excitation) * g0
                            + Resonator(1, excitation) * g1
                            + Resonator(2, excitation) * g2
                            + Resonator(3, excitation) * g3;

                // Soft limit so loud passages stay round instead of clipping. The 4.0 pre-gain puts a full-level note
                // at about -11 dBFS after the limiter (measured 2026-11-05), matching the bird chirp guide note.
                var sample = voice * 4.0f * envelope * level * gain;
                sample = sample / (1f + Mathf.Abs(sample) * 0.6f);

                for (var c = 0; c < channels; c++)
                    data[n * channels + c] = sample;
            }

            Interlocked.CompareExchange(ref holdFramesLeft, framesLeft, framesSnapshot);
        }

        float Resonator(int i, float x)
        {
            // b1 = 0 and b2 = -b0 for the RBJ band-pass.
            var y = b0[i] * (x - x2[i]) - a1[i] * y1[i] - a2[i] * y2[i];
            x2[i] = x1[i];
            x1[i] = x;
            y2[i] = y1[i];
            y1[i] = y;
            return y;
        }

        static float PolyBlep(float t, float dt)
        {
            if (dt <= 0f)
                return 0f;
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1f;
            }

            if (t > 1f - dt)
            {
                t = (t - 1f) / dt;
                return t * t + t + t + 1f;
            }

            return 0f;
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
