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
    /// <item>aspiration noise gated by the open phase of the glottis, loud enough to be heard as breath
    /// (harmonics-to-noise ratio about 28 dB overall and 10 dB in the 2-5 kHz band at neutral effort);</item>
    /// <item>vocal tract: F1 and F2 as a cascade of two-pole resonators with unity gain at DC, so the fundamental
    /// and the low harmonics keep their natural strength and the relative level of F1 and F2 follows from their
    /// frequencies; F3-F5 as parallel band-pass branches with fixed levels (the upper spectrum). Formant
    /// frequencies halfway between typical adult male and female values, and F1 that rises with high pitches
    /// as singers do;</item>
    /// <item>vocal effort: the singer's loudness changes the voice quality, not only the level. Soft: longer
    /// open phase, slower closure, darker source, more breath, slightly lower F1. Loud: the opposite. The
    /// level change that the quality itself would add is compensated, so the output level follows the
    /// loudness exactly as before.</item>
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

        [Tooltip("Formant bandwidths. F1 at 130 Hz includes the damping of the open glottis; narrower makes one harmonic ring out.")]
        [SerializeField]
        float[] formantBandwidthHz = { 130f, 100f, 130f, 160f, 220f };

        [Tooltip("Levels of F3, F4 and F5 (parallel branches) relative to the F1-F2 cascade. The defaults put the 1.5-3 kHz band " +
                 "about 22 dB and the 3-5 kHz band about 28 dB below the total at neutral effort.")]
        [SerializeField]
        float[] upperFormantGain = { 5.3f, 3.3f, 1.8f };

        [Tooltip("Raise F1 to at least this multiple of the pitch (singers open the vowel on high notes; keeps high notes from sounding hollow).")]
        [SerializeField]
        float f1MinPitchRatio = 1.15f;

        [Header("Glottal source (values at neutral vocal effort)")]
        [Tooltip("Open quotient: fraction of the cycle the glottis is open (0.5 pressed, 0.7 breathy).")]
        [SerializeField, Range(0.4f, 0.8f)]
        float openQuotient = 0.57f;

        [Tooltip("Share of the open phase spent closing (0.25 abrupt closure, bright; 0.4 slow closure, soft).")]
        [SerializeField, Range(0.2f, 0.45f)]
        float closingShare = 0.31f;

        [Tooltip("Pitch jitter: random cycle-to-cycle pitch deviation (% of the pitch). Natural voices: 0.2-0.6.")]
        [SerializeField, Range(0f, 2f)]
        float jitterPercent = 0.4f;

        [Tooltip("Shimmer: random cycle-to-cycle amplitude deviation in dB. Natural voices: 0.3-0.8.")]
        [SerializeField, Range(0f, 3f)]
        float shimmerDb = 0.5f;

        [Tooltip("Slow random pitch drift (semitones), the natural wobble of a held note.")]
        [SerializeField, Range(0f, 0.5f)]
        float driftSemitones = 0.08f;

        [Tooltip("Aspiration noise through the vocal tract, gated by the open phase (breathiness). 0.45 = harmonics-to-noise " +
                 "ratio of about 28 dB overall and 10 dB in the 2-5 kHz band; halving it adds 6 dB. Scaled with the square root " +
                 "of the pitch so that high notes are not cleaner than low ones.")]
        [SerializeField, Range(0f, 1.5f)]
        float breathNoise = 0.45f;

        [Tooltip("One-pole low-pass on the source (Hz): radiation / lip rounding, tames aliasing of the closure.")]
        [SerializeField]
        float sourceTiltHz = 3500f;

        [Header("Vocal effort (loudness changes the voice quality)")]
        [Tooltip("0 = the voice quality ignores loudness (neutral values throughout), 1 = full effect.")]
        [SerializeField, Range(0f, 1f)]
        float effortAmount = 1f;

        [Tooltip("Analyzer loudness (0-1) of the softest voice (about the voiced threshold): effort -1.")]
        [SerializeField, Range(0f, 1f)]
        float softLoudness = 0.1f;

        [Tooltip("Analyzer loudness (0-1) at which the voice has its neutral quality: effort 0. Loudness 1 is effort +1.")]
        [SerializeField, Range(0f, 1f)]
        float neutralLoudness = 0.36f;

        [Tooltip("Open quotient change: soft adds this much, loud subtracts it.")]
        [SerializeField, Range(0f, 0.2f)]
        float effortOpenQuotient = 0.12f;

        [Tooltip("Closing share change: soft adds this much (slower closure), loud subtracts it (more abrupt closure).")]
        [SerializeField, Range(0f, 0.1f)]
        float effortClosingShare = 0.06f;

        [Tooltip("Source low-pass shift in octaves: soft lowers Source Tilt Hz by this much (darker), loud raises it (brighter).")]
        [SerializeField, Range(0f, 2f)]
        float effortTiltOctaves = 1.2f;

        [Tooltip("Breath noise change in dB: soft adds this much, loud subtracts it.")]
        [SerializeField, Range(0f, 12f)]
        float effortBreathDb = 6f;

        [Tooltip("F1 change in percent: loud raises F1 by this much (the jaw opens), soft lowers it.")]
        [SerializeField, Range(0f, 15f)]
        float effortF1Percent = 6f;

        [Tooltip("Level the voice quality alone would add per unit of effort (dB), taken out again so that the output level follows " +
                 "the loudness as before. 2.8 is the measured value for the defaults above (mean over 110-660 Hz).")]
        [SerializeField, Range(0f, 6f)]
        float effortLevelCompensationDb = 2.8f;

        [Tooltip("How fast the voice quality follows a loudness change (s); slower than the level, as in a real voice.")]
        [SerializeField]
        float effortSmoothSeconds = 0.04f;

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
        // Vocal effort: -1 soft, 0 neutral, +1 loud (already scaled by effortAmount).
        volatile float targetEffort;
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
        float effort;
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
        // This glottal cycle's shape, picked at the closure before it (see BeginCycle).
        float cycleOq = 0.57f;
        float cycleTp = 0.57f * 0.69f;
        float cycleTn = 0.57f * 0.31f;
        float cycleTiltCoeff = 0.37f;
        float cycleBreath;
        float cycleGain = 1f;
        // F1 -> F2 cascade: y = a x + b y1 + c y2 (two-pole resonator, gain 1 at DC).
        float f1A, f1B, f1C, f1Y1, f1Y2;
        float f2A, f2B, f2C, f2Y1, f2Y2;
        // F3-F5 parallel band-pass branches (RBJ, 0 dB peak): shared input history, own output history.
        const int UpperCount = FormantCount - 2;
        float exc1, exc2;
        readonly float[] y1 = new float[UpperCount];
        readonly float[] y2 = new float[UpperCount];
        readonly float[] b0 = new float[UpperCount];
        readonly float[] a1 = new float[UpperCount];
        readonly float[] a2 = new float[UpperCount];
        readonly float[] upperPolarity = { 1f, -1f, 1f };
        readonly float[] g = new float[UpperCount];
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
            SetFollowTargets(hz, loudness);
            followGate = true;
            EnsurePlaying();
        }

        void SetFollowTargets(float hz, float loudness)
        {
            if (hz > 0f)
                targetHz = Mathf.Clamp(hz, minHz, maxHz);
            targetLevel = Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(Mathf.Clamp01(loudness)));
            targetEffort = EffortFromLoudness(loudness);
        }

        void SetNoteTargets(float hz, float loudness)
        {
            targetHz = Mathf.Clamp(hz, minHz, maxHz);
            targetLevel = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(loudness));
            targetEffort = 0f;
        }

        /// <summary>The singer went silent: close the gate (the release tail still plays).</summary>
        public void Release()
        {
            followGate = false;
        }

        /// <summary>
        /// Vocal effort for an analyzer loudness: -1 at <see cref="softLoudness"/>, 0 at <see cref="neutralLoudness"/>,
        /// +1 at loudness 1, linear in the square root of the loudness (as the level is), scaled by effortAmount.
        /// </summary>
        float EffortFromLoudness(float loudness)
        {
            var x = Mathf.Sqrt(Mathf.Clamp01(loudness));
            var neutral = Mathf.Sqrt(Mathf.Clamp01(neutralLoudness));
            var soft = Mathf.Sqrt(Mathf.Clamp01(softLoudness));
            var e = x >= neutral
                ? (x - neutral) / Mathf.Max(0.01f, 1f - neutral)
                : (x - neutral) / Mathf.Max(0.01f, neutral - soft);
            return Mathf.Clamp(e, -1f, 1f) * Mathf.Clamp01(effortAmount);
        }

        /// <summary>One held note (the teacher's guide note in the human voice), sung at neutral vocal effort.</summary>
        public void PlayNote(float hz, float seconds, float loudness = 0.85f)
        {
            if (hz <= 0f)
                return;

            SetNoteTargets(hz, loudness);
            Interlocked.Exchange(ref holdFramesLeft, Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.05f, seconds) * sampleRate)));
            EnsurePlaying();
        }

        /// <summary>
        /// Offline rendering for the editor test files (no AudioSource, no audio thread): the next mono block at
        /// <paramref name="rate"/>, computed by the same code as the live voice. While <paramref name="gateOpen"/>
        /// the voice follows the pitch and analyzer loudness as in <see cref="Follow"/>, or holds them as the
        /// guide note of <see cref="PlayNote"/> when <paramref name="asGuideNote"/>; otherwise it is released.
        /// </summary>
        public void RenderOffline(float[] mono, int rate, float hz, float loudness, bool gateOpen, bool asGuideNote = false)
        {
            sampleRate = rate;
            if (gateOpen && hz > 0f)
            {
                if (asGuideNote)
                    SetNoteTargets(hz, loudness);
                else
                    SetFollowTargets(hz, loudness);
            }

            followGate = gateOpen;
            OnAudioFilterRead(mono, 1);
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

        float FormantHz(int i) => formantHz != null && i < formantHz.Length ? formantHz[i] : 0f;

        float FormantBandwidthHz(int i) =>
            formantBandwidthHz != null && i < formantBandwidthHz.Length ? Mathf.Max(10f, formantBandwidthHz[i]) : 100f;

        void UpdateFormantCoefficients(float fs, float pitchHz, float effortNow)
        {
            // F1 follows the vocal effort (the jaw opens for loud notes) and stays above the pitch; F2 stays above F1.
            var f1 = FormantHz(0) * (1f + effortF1Percent * 0.01f * effortNow);
            if (f1MinPitchRatio > 0f)
                f1 = Mathf.Max(f1, pitchHz * f1MinPitchRatio);
            f1 = Mathf.Clamp(f1, 100f, fs * 0.4f);
            var f2 = Mathf.Clamp(Mathf.Max(FormantHz(1), f1 * 1.2f), 100f, fs * 0.45f);
            SetResonator(f1, FormantBandwidthHz(0), fs, out f1A, out f1B, out f1C);
            SetResonator(f2, FormantBandwidthHz(1), fs, out f2A, out f2B, out f2C);

            for (var i = 0; i < UpperCount; i++)
            {
                var f = FormantHz(i + 2);
                if (f <= 0f || f >= fs * 0.45f)
                {
                    b0[i] = 0f;
                    a1[i] = 0f;
                    a2[i] = 0f;
                    continue;
                }

                // RBJ band-pass (constant 0 dB peak gain), Q = f / bandwidth.
                var w0 = TwoPi * f / fs;
                var q = Mathf.Max(0.5f, f / FormantBandwidthHz(i + 2));
                var alpha = Mathf.Sin(w0) / (2f * q);
                var a0 = 1f + alpha;
                b0[i] = alpha / a0;
                a1[i] = -2f * Mathf.Cos(w0) / a0;
                a2[i] = (1f - alpha) / a0;
            }
        }

        /// <summary>Klatt two-pole resonator y = a x + b y1 + c y2 with unity gain at DC.</summary>
        static void SetResonator(float f, float bandwidth, float fs, out float a, out float b, out float c)
        {
            c = -Mathf.Exp(-TwoPi * bandwidth / fs);
            b = 2f * Mathf.Exp(-Mathf.PI * bandwidth / fs) * Mathf.Cos(TwoPi * f / fs);
            a = 1f - b - c;
        }

        /// <summary>
        /// Audio thread, at every glottal closure (and at a note onset): the shape of the coming cycle from the
        /// vocal effort. Soft: long open phase, slow closure, dark source, more breath. Loud: the opposite.
        /// </summary>
        void BeginCycle(float hz, float fs)
        {
            var e = effort;
            cycleOq = Mathf.Clamp(openQuotient - effortOpenQuotient * e, 0.4f, 0.8f);
            var share = Mathf.Clamp(closingShare - effortClosingShare * e, 0.15f, 0.5f);
            cycleTn = cycleOq * share;
            cycleTp = cycleOq - cycleTn;
            var tiltHz = Mathf.Clamp(sourceTiltHz * Mathf.Pow(2f, effortTiltOctaves * e), 300f, fs * 0.45f);
            cycleTiltCoeff = 1f - Mathf.Exp(-TwoPi * tiltHz / fs);
            cycleBreath = breathNoise * Mathf.Pow(10f, -effortBreathDb * e / 20f) * Mathf.Sqrt(Mathf.Max(20f, hz) / 220f);
            cycleGain = Mathf.Pow(10f, -effortLevelCompensationDb * e / 20f);
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
            var goalEffort = targetEffort;
            var follow = followGate;
            var gain = volume;
            var vibRate = vibratoRateHz;
            var vibDepth = vibratoDepthSemitones;
            var vibOnset = Mathf.Max(0.01f, vibratoOnsetSeconds);
            var jitter = jitterPercent * 0.01f;
            var shimmer = shimmerDb;
            var driftDepth = driftSemitones;
            var attackCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, attackSeconds));
            var releaseCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, releaseSeconds));
            var pitchCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, pitchSmoothSeconds));
            var levelCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, levelSmoothSeconds));
            var effortCoeff = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, effortSmoothSeconds));
            var driftCoeff = 1f - Mathf.Exp(-dt / 0.25f);
            var noiseHpCoeff = 1f - Mathf.Exp(-TwoPi * 1200f / fs);

            UpdateFormantCoefficients(fs, currentHz, effort);
            for (var i = 0; i < UpperCount; i++)
                g[i] = (upperFormantGain != null && i < upperFormantGain.Length ? upperFormantGain[i] : 0f) * upperPolarity[i];

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
                        effort = goalEffort;
                        phase = 0.0;
                        BeginCycle(currentHz, fs);
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
                effort += (goalEffort - effort) * effortCoeff;

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

                // Glottal cycle: at every closure pick this cycle's jitter, shimmer and shape (vocal effort).
                var inc = hz * dt;
                phase += inc;
                if (phase >= 1.0)
                {
                    phase -= 1.0;
                    cycleJitter = NextNoise() * jitter;
                    cycleShimmer = Mathf.Pow(10f, NextNoise() * shimmer / 20f);
                    BeginCycle(hz, fs);
                }

                var oq = cycleOq;
                var tp = cycleTp;   // opening phase (fraction of the cycle)
                var tn = cycleTn;   // closing phase

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
                tilt += (pulse - tilt) * cycleTiltCoeff;

                // Aspiration: high-passed noise, strongest while the glottis is open.
                var white = NextNoise();
                noiseHp += (white - noiseHp) * noiseHpCoeff;
                var aspiration = (white - noiseHp) * cycleBreath * (0.25f + 0.75f * openness);

                var excitation = tilt + aspiration;

                // F1 -> F2 in cascade: the fundamental passes at unity gain, the F1 / F2 peaks rise above it.
                var r1 = f1A * excitation + f1B * f1Y1 + f1C * f1Y2;
                f1Y2 = f1Y1;
                f1Y1 = r1;
                var voice = f2A * r1 + f2B * f2Y1 + f2C * f2Y2;
                f2Y2 = f2Y1;
                f2Y1 = voice;

                // F3-F5 in parallel (b1 = 0 and b2 = -b0 for the RBJ band-pass).
                var difference = excitation - exc2;
                for (var i = 0; i < UpperCount; i++)
                {
                    var y = b0[i] * difference - a1[i] * y1[i] - a2[i] * y2[i];
                    y2[i] = y1[i];
                    y1[i] = y;
                    voice += y * g[i];
                }
                exc2 = exc1;
                exc1 = excitation;

                // Soft limit so loud passages stay round instead of clipping. The pre-gain puts a full-level note
                // at about -11 dBFS after the limiter, matching the bird chirp guide note (calibrated against the
                // previous parallel-bank version: within 1 dB at every pitch from 110 to 660 Hz).
                var sample = voice * 0.127f * cycleGain * envelope * level * gain;
                sample = sample / (1f + Mathf.Abs(sample) * 0.6f);

                for (var c = 0; c < channels; c++)
                    data[n * channels + c] = sample;
            }

            Interlocked.CompareExchange(ref holdFramesLeft, framesLeft, framesSnapshot);
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
