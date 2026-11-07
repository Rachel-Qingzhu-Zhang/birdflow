using System.Threading;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// A sung, gender-neutral human vowel ("ah") that follows the singer's pitch and loudness without
    /// transposition. Source-filter synthesis in the Klatt tradition, tuned to sound like a voice rather
    /// than a buzzer:
    /// <list type="bullet">
    /// <item>glottal source: Rosenberg glottal-flow pulses (open / closing / closed phases), radiated as the flow
    /// derivative, so the spectrum has a voice-like tilt without a sawtooth buzz;</item>
    /// <item>micro-variation: per-cycle jitter (pitch) and shimmer (amplitude), a slow pitch drift and vibrato that
    /// fades in, so no two cycles are identical;</item>
    /// <item>aspiration noise gated by the open phase of the glottis (breathiness);</item>
    /// <item>vocal tract: five formant resonators with alternating polarity (parallel Klatt bank) at formant
    /// frequencies halfway between typical adult male and female values, and F1 that rises with high pitches
    /// as singers do.</item>
    /// </list>
    /// The human-avatar counterpart of <see cref="BirdSongSynth"/>: the participant hears their own voice as a
    /// human voice instead of as bird chirps. Also sings the teacher's guide note when the teacher voice follows
    /// the avatar. Parameters are written on the main thread and read on the audio thread.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class HumanVoiceSynth : MonoBehaviour
    {
        const float TwoPi = Mathf.PI * 2f;
        const int FormantCount = 5;

        [Header("Vowel 'ah' (neutral: between adult male and female formants)")]
        [SerializeField]
        float[] formantHz = { 780f, 1150f, 2550f, 3400f, 4450f };

        [SerializeField]
        float[] formantBandwidthHz = { 80f, 90f, 130f, 160f, 220f };

        [SerializeField]
        float[] formantGain = { 1f, 0.6f, 0.35f, 0.22f, 0.12f };

        [Tooltip("Raise F1 to at least this multiple of the pitch (singers open the vowel on high notes; keeps high notes from sounding hollow).")]
        [SerializeField]
        float f1MinPitchRatio = 1.15f;

        [Header("Glottal source")]
        [Tooltip("Open quotient: fraction of the cycle the glottis is open (0.5 pressed, 0.7 breathy).")]
        [SerializeField, Range(0.4f, 0.8f)]
        float openQuotient = 0.6f;

        [Tooltip("Pitch jitter: random cycle-to-cycle pitch deviation (% of the pitch). Natural voices: 0.2-0.6.")]
        [SerializeField, Range(0f, 2f)]
        float jitterPercent = 0.4f;

        [Tooltip("Shimmer: random cycle-to-cycle amplitude deviation in dB. Natural voices: 0.3-0.8.")]
        [SerializeField, Range(0f, 3f)]
        float shimmerDb = 0.5f;

        [Tooltip("Slow random pitch drift (semitones), the natural wobble of a held note.")]
        [SerializeField, Range(0f, 0.5f)]
        float driftSemitones = 0.08f;

        [Tooltip("Aspiration noise through the vocal tract, gated by the open phase (breathiness).")]
        [SerializeField, Range(0f, 0.3f)]
        float breathNoise = 0.07f;

        [Tooltip("One-pole low-pass on the source (Hz): radiation / lip rounding, tames aliasing of the closure.")]
        [SerializeField]
        float sourceTiltHz = 3500f;

        [Header("Expression")]
        [SerializeField]
        float vibratoRateHz = 5.5f;

        [SerializeField]
        float vibratoDepthSemitones = 0.35f;

        [Tooltip("Vibrato fades in over this long after the voice starts.")]
        [SerializeField]
        float vibratoOnsetSeconds = 0.45f;

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
        float cycleJitter;
        float cycleShimmer = 1f;
        float drift;
        float driftTarget;
        float driftTimer;
        float vibratoWobble;
        float noiseHp;
        readonly float[] x1 = new float[FormantCount];
        readonly float[] x2 = new float[FormantCount];
        readonly float[] y1 = new float[FormantCount];
        readonly float[] y2 = new float[FormantCount];
        readonly float[] b0 = new float[FormantCount];
        readonly float[] a1 = new float[FormantCount];
        readonly float[] a2 = new float[FormantCount];
        readonly float[] polarity = { 1f, -1f, 1f, -1f, 1f };
        readonly float[] g = new float[FormantCount];
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

        void UpdateFormantCoefficients(float fs, float pitchHz)
        {
            for (var i = 0; i < FormantCount; i++)
            {
                var f = formantHz != null && i < formantHz.Length ? formantHz[i] : 0f;
                if (i == 0 && f1MinPitchRatio > 0f)
                    f = Mathf.Max(f, pitchHz * f1MinPitchRatio);
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
            var oq = Mathf.Clamp(openQuotient, 0.4f, 0.8f);
            var tp = oq * 0.66f;               // opening phase (fraction of the cycle)
            var tn = oq - tp;                  // closing phase
            var jitter = jitterPercent * 0.01f;
            var shimmer = shimmerDb;
            var driftDepth = driftSemitones;
            var tiltCoeff = 1f - Mathf.Exp(-TwoPi * Mathf.Clamp(sourceTiltHz, 300f, fs * 0.45f) / fs);
            var attackCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, attackSeconds));
            var releaseCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, releaseSeconds));
            var pitchCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, pitchSmoothSeconds));
            var levelCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, levelSmoothSeconds));
            var driftCoeff = 1f - Mathf.Exp(-dt / 0.25f);
            var noiseHpCoeff = 1f - Mathf.Exp(-TwoPi * 1200f / fs);

            UpdateFormantCoefficients(fs, currentHz);
            for (var i = 0; i < FormantCount; i++)
                g[i] = (formantGain != null && i < formantGain.Length ? formantGain[i] : 0f) * polarity[i];

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
                        phase = 0.0;
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

                // Slow drift: a new random target every ~0.3 s, approached smoothly.
                driftTimer -= dt;
                if (driftTimer <= 0f)
                {
                    driftTimer = 0.2f + 0.2f * (NextNoise() * 0.5f + 0.5f);
                    driftTarget = NextNoise() * driftDepth;
                    vibratoWobble = NextNoise() * 0.08f;
                }
                drift += (driftTarget - drift) * driftCoeff;

                vibratoPhase += vibRate * (1f + vibratoWobble) * dt;
                if (vibratoPhase >= 1.0)
                    vibratoPhase -= 1.0;
                var vibAmount = Mathf.Clamp01(gateSeconds / vibOnset) * vibDepth;
                var semis = vibAmount * Mathf.Sin((float)(vibratoPhase * TwoPi)) + drift;
                var hz = currentHz * Mathf.Pow(2f, semis / 12f) * (1f + cycleJitter);

                // Glottal cycle: at every closure pick this cycle's jitter and shimmer.
                var inc = hz * dt;
                phase += inc;
                if (phase >= 1.0)
                {
                    phase -= 1.0;
                    cycleJitter = NextNoise() * jitter;
                    cycleShimmer = Mathf.Pow(10f, NextNoise() * shimmer / 20f);
                }

                // Rosenberg glottal flow derivative (what the lips radiate): positive hump while opening,
                // sharp negative pulse at closure, zero while closed.
                var t = (float)phase;
                float flowDerivative;
                float openness;
                if (t < tp)
                {
                    flowDerivative = 0.5f * Mathf.PI / tp * Mathf.Sin(Mathf.PI * t / tp);
                    openness = 0.5f * (1f - Mathf.Cos(Mathf.PI * t / tp));
                }
                else if (t < oq)
                {
                    var u = (t - tp) / tn;
                    flowDerivative = -0.5f * Mathf.PI / tn * Mathf.Sin(0.5f * Mathf.PI * u);
                    openness = Mathf.Cos(0.5f * Mathf.PI * u);
                }
                else
                {
                    flowDerivative = 0f;
                    openness = 0f;
                }

                // Normalise the pulse amplitude (the derivative scales with 1 / phase length) and smooth the closure.
                var pulse = flowDerivative * tp * 0.9f * cycleShimmer;
                tilt += (pulse - tilt) * tiltCoeff;

                // Aspiration: high-passed noise, strongest while the glottis is open.
                var white = NextNoise();
                noiseHp += (white - noiseHp) * noiseHpCoeff;
                var aspiration = (white - noiseHp) * noiseAmp * (0.25f + 0.75f * openness);

                var excitation = tilt + aspiration;
                var voice = 0f;
                for (var i = 0; i < FormantCount; i++)
                    voice += Resonator(i, excitation) * g[i];

                // Soft limit so loud passages stay round instead of clipping. The pre-gain puts a full-level note
                // at about -11 dBFS after the limiter, matching the bird chirp guide note.
                var sample = voice * 2.5f * envelope * level * gain;
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

        float NextNoise()
        {
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            return (noiseState / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}
