using AvianVoice.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace AvianVoice.Audio
{
    /// <summary>Runs after MicrophoneCapture / VoiceAnalyzer (order 0) so it reacts to this frame's analysis, not the previous one.</summary>
    [DefaultExecutionOrder(50)]
    public class BirdVoiceMapper : MonoBehaviour
    {
        AvianVoiceSettings settings;
        AvianSignals signals;
        VoiceAnalyzer analyzer;
        AudioMixer mixer;
        string pitchParam = "MicPitch";
        string reverbSendParam = "MicReverb";
        AudioSource transformSource;
        AudioSource assistiveMonitorSource;
        BirdCallLibrary library;
        BirdSongSynth synth;
        HumanVoiceSynth humanSynth;
        float lastSynthPitchHz;
        VoiceFeedback feedback = VoiceFeedback.Bird;

        VoiceMapMode mode = VoiceMapMode.Transform;
        float lastAssistiveUpdate;
        float lastFrameLoudness;
        float lastTriggerAttemptTime = -1f;
        bool requireOnsetAfterSilence = true;
        bool synthSawDip;
        bool loggedAssistiveMixerWarning;
        bool followVoice;

        public VoiceMapMode Mode => mode;

        /// <summary>What the participant's own voice is turned into: bird chirps, a human vowel, or nothing.</summary>
        public VoiceFeedback Feedback => feedback;

        /// <summary>"continuous" (bird voice open while voiced) or "chirps" (one chirp per onset, the previous behaviour).</summary>
        public string BirdFeedbackModeName => settings != null && !settings.synthContinuous ? "chirps" : "continuous";

        /// <summary>True while a guide note or the voice feedback is still sounding.</summary>
        public bool IsSounding => (synth != null && synth.IsSounding) || (humanSynth != null && humanSynth.IsSounding);

        /// <summary>
        /// Choose the feedback voice for the study condition. Takes effect at once (the current feedback
        /// sound stops); guide notes are not affected.
        /// </summary>
        public void SetFeedback(VoiceFeedback newFeedback)
        {
            if (feedback == newFeedback)
                return;

            feedback = newFeedback;
            synth?.Silence();
            humanSynth?.Silence();
            Debug.Log("[AvianVoice] Voice feedback: " + feedback);
        }

        /// <summary>
        /// When false, the microphone does not make bird sound. Guide notes still play.
        /// </summary>
        public void SetFollowVoice(bool enabled)
        {
            if (followVoice == enabled)
                return;

            followVoice = enabled;
            if (!enabled)
            {
                synth?.Silence();
                humanSynth?.Release();
            }
        }

        /// <summary>
        /// One guide note in the bird voice. Microphone playback stays off until SetFollowVoice(true).
        /// </summary>
        public void PlayGuideNote(float humanHz, float seconds)
        {
            PlayGuideNote(humanHz, seconds, VoiceFeedback.Bird);
        }

        /// <summary>
        /// One guide note in the bird voice (chirp, transposed into the bird register) or in the human voice
        /// (sung vowel at the given pitch). Microphone playback stays off until SetFollowVoice(true).
        /// </summary>
        public void PlayGuideNote(float humanHz, float seconds, VoiceFeedback voice)
        {
            followVoice = false;
            if (voice == VoiceFeedback.Human)
            {
                EnsureHumanSynth();
                synth?.Silence();
                humanSynth?.PlayNote(humanHz, seconds);
                return;
            }

            EnsureSynth();
            humanSynth?.Silence();
            synth?.PlayChirp(humanHz, 0.92f, seconds);
        }

        /// <summary>
        /// Debug: feed a synthetic voice (sawtooth at <paramref name="humanHz"/>) straight into the analyzer for
        /// <paramref name="seconds"/>, then silence, with the microphone paused, so the end-to-end response of the
        /// feedback voice (analysis -> gate -> synth) can be measured on output.wav. Events "debug_voice_inject"
        /// (phase start / end) carry the output frame of the first voiced and the first silent push.
        /// </summary>
        public void DebugInjectVoice(float humanHz, float seconds)
        {
            if (analyzer == null || settings == null)
                return;
            StartCoroutine(InjectVoiceRoutine(humanHz, seconds));
        }

        System.Collections.IEnumerator InjectVoiceRoutine(float humanHz, float seconds)
        {
            var mic = FindAnyObjectByType<MicrophoneCapture>();
            if (mic != null)
                mic.Paused = true;
            yield return null;

            var wasFollowing = followVoice;
            followVoice = true;
            var rate = settings.sampleRate > 0 ? settings.sampleRate : 48000;
            var frame = Mathf.Max(64, settings.frameSize);
            var buffer = new float[frame];
            var recorder = AvianVoice.Study.StudyRecorder.Instance;
            double phase = 0;
            var endVoice = Time.unscaledTime + seconds;
            var endSilence = endVoice + 1f;
            var started = false;
            var ended = false;
            while (Time.unscaledTime < endSilence)
            {
                var voiced = Time.unscaledTime < endVoice;
                for (var i = 0; i < frame; i++)
                {
                    if (voiced)
                    {
                        phase += humanHz / rate;
                        if (phase >= 1.0)
                            phase -= 1.0;
                        buffer[i] = (float)(phase * 2.0 - 1.0) * 0.08f;
                    }
                    else
                    {
                        buffer[i] = 0f;
                    }
                }

                if (voiced && !started)
                {
                    started = true;
                    recorder?.LogEvent("debug_voice_inject", j => j.Add("phase", "start").Add("hz", humanHz).Add("seconds", seconds));
                }
                else if (!voiced && !ended)
                {
                    ended = true;
                    recorder?.LogEvent("debug_voice_inject", j => j.Add("phase", "end").Add("hz", humanHz));
                }

                analyzer.PushSamples(buffer, frame);
                yield return null;
            }

            followVoice = wasFollowing;
            if (!followVoice)
            {
                synth?.Silence();
                humanSynth?.Silence();
            }

            if (mic != null)
                mic.Paused = false;
            Debug.Log("[AvianVoice] Voice injection test done (" + humanHz + " Hz for " + seconds + " s, then 1 s silence).");
        }

        /// <summary>Debug: hold the bird voice open at a human pitch for a while, as if the singer were voiced.</summary>
        public void DebugFollowBird(float humanHz, float seconds, float loudness = 0.85f)
        {
            EnsureSynth();
            if (synth == null)
                return;
            followVoice = false;
            StartCoroutine(FollowBirdRoutine(humanHz, seconds, loudness));
        }

        System.Collections.IEnumerator FollowBirdRoutine(float humanHz, float seconds, float loudness)
        {
            var end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end && !followVoice)
            {
                synth.Follow(humanHz, loudness);
                yield return null;
            }

            synth.Release();
        }

        public static BirdVoiceMapper Find()
        {
            return FindAnyObjectByType<BirdVoiceMapper>();
        }

        public void Initialize(
            AvianVoiceSettings voiceSettings,
            AvianSignals avianSignals,
            VoiceAnalyzer voiceAnalyzer,
            AudioMixerReference mixerRef,
            AudioSource transformBird,
            AudioSource assistiveMonitor,
            BirdCallLibrary callLibrary)
        {
            settings = voiceSettings;
            signals = avianSignals;
            analyzer = voiceAnalyzer;
            transformSource = transformBird;
            assistiveMonitorSource = assistiveMonitor;
            library = callLibrary;

            if (mixerRef != null)
            {
                mixer = mixerRef.mixer;
                pitchParam = mixerRef.pitchParam;
                reverbSendParam = mixerRef.reverbSendParam;
            }

            SetMode(VoiceMapMode.Transform);
        }

        public void SetMode(VoiceMapMode newMode)
        {
            mode = newMode;
            signals?.PublishVoiceMode(mode);

            if (assistiveMonitorSource != null)
                assistiveMonitorSource.mute = mode != VoiceMapMode.Assistive;

            if (mode == VoiceMapMode.Assistive && mixer == null && !loggedAssistiveMixerWarning)
            {
                loggedAssistiveMixerWarning = true;
                Debug.LogWarning(
                    "[AvianVoice] Assistive mode has no AudioMixer assigned. " +
                    "Only AssistiveMonitor pitch will change — wire mixerReference on AvianAppBootstrap for full Assistive audio.");
            }
        }

        void Update()
        {
            if (!followVoice || analyzer == null || settings == null)
                return;

            var v = analyzer.Current;

            if (feedback == VoiceFeedback.None)
            {
                // Own voice only: nothing is synthesized from the microphone.
                synth?.Silence();
                humanSynth?.Release();
                lastFrameLoudness = v.loudness;
                return;
            }

            if (feedback == VoiceFeedback.Human)
            {
                EnsureHumanSynth();
                synth?.Silence();
                UpdateHumanSynth(v);
                lastFrameLoudness = v.loudness;
                return;
            }

            humanSynth?.Release();
            if (mode == VoiceMapMode.Transform && settings.useSynthTransform)
                EnsureSynth();

            if (mode == VoiceMapMode.Transform && UseSynth)
            {
                UpdateSynth(v);
                lastFrameLoudness = v.loudness;
                return;
            }

            synth?.Silence();

            if (mode == VoiceMapMode.Assistive)
            {
                if (v.voiced < 0.5f)
                    return;
                ApplyAssistive(v);
                return;
            }

            if (v.voiced < 0.5f)
            {
                lastFrameLoudness = v.loudness;
                requireOnsetAfterSilence = true;
                return;
            }

            if (!ShouldTriggerTransformCall(v))
            {
                lastFrameLoudness = v.loudness;
                return;
            }

            requireOnsetAfterSilence = false;

            var pitchHz = v.pitchHz > 0f ? v.pitchHz : settings.referencePitchHz;
            library?.PlayNearest(transformSource, pitchHz, v.loudness, settings);
            lastTriggerAttemptTime = Time.time;
            lastFrameLoudness = v.loudness;
        }

        bool UseSynth => settings.useSynthTransform && synth != null;

        void EnsureSynth()
        {
            if (synth != null)
                return;

            var parent = transformSource != null ? transformSource.transform : transform;
            synth = BirdSongSynth.Ensure(parent, transformSource, settings);
        }

        void EnsureHumanSynth()
        {
            if (humanSynth != null)
                return;

            var parent = transformSource != null ? transformSource.transform : transform;
            humanSynth = HumanVoiceSynth.Ensure(parent, transformSource);
        }

        /// <summary>Continuous vowel at the singer's own pitch: open while voiced, released when the voice stops.</summary>
        void UpdateHumanSynth(VoiceFrame v)
        {
            if (humanSynth == null)
                return;

            var loud = v.loudness;
            // The gate is the analyzer's loudness envelope alone (fast attack / release); a held pitch must not keep it open.
            var voiced = v.voiced > 0.5f;
            if (v.pitchHz > 0f)
                lastSynthPitchHz = v.pitchHz;
            else if (lastSynthPitchHz <= 0f)
                lastSynthPitchHz = settings.referencePitchHz;

            if (!voiced)
            {
                humanSynth.Release();
                return;
            }

            humanSynth.Follow(lastSynthPitchHz, loud);
        }

        void UpdateSynth(VoiceFrame v)
        {
            var loud = v.loudness;
            // The gate is the analyzer's loudness envelope alone (fast attack / release); a held pitch must not keep it open.
            var voiced = v.voiced > 0.5f;
            if (v.pitchHz > 0f)
                lastSynthPitchHz = v.pitchHz;
            else if (lastSynthPitchHz <= 0f)
                lastSynthPitchHz = settings.referencePitchHz;

            if (settings.synthContinuous)
            {
                // Continuous bird voice: open while voiced, released when the voice stops (same gating as the
                // human voice, so the two feedback conditions differ only in timbre and register).
                if (voiced)
                    synth.Follow(lastSynthPitchHz, loud);
                else
                    synth.Release();
                requireOnsetAfterSilence = !voiced;
                synthSawDip = false;
                lastFrameLoudness = loud;
                return;
            }

            // Previous behaviour (AvianVoiceSettings.synthContinuous off): one chirp per voice onset / attack.
            if (!voiced)
            {
                requireOnsetAfterSilence = true;
                synthSawDip = false;
                lastFrameLoudness = loud;
                return;
            }

            if (loud + 0.02f < lastFrameLoudness)
                synthSawDip = true;

            // Fire on the singer's attacks, not on a metronome. Tiny debounce only.
            const float debounce = 0.045f;
            var ready = lastTriggerAttemptTime < 0f || Time.time - lastTriggerAttemptTime >= debounce;
            var attack = loud >= lastFrameLoudness + 0.03f;
            var onset = ready && (requireOnsetAfterSilence || (synthSawDip && attack));
            if (onset)
            {
                synth.PlayChirp(lastSynthPitchHz, loud);
                lastTriggerAttemptTime = Time.time;
                requireOnsetAfterSilence = false;
                synthSawDip = false;
            }

            lastFrameLoudness = loud;
        }

        bool ShouldTriggerTransformCall(VoiceFrame v)
        {
            var pitchHz = v.pitchHz > 0f ? v.pitchHz : settings.referencePitchHz;
            var cooldown = library != null
                ? library.GetTransformCooldown(pitchHz, settings)
                : Mathf.Clamp(
                    settings.transformCooldown,
                    settings.minTransformCooldown,
                    settings.maxTransformCooldown);
            var cooldownReady = lastTriggerAttemptTime < 0f ||
                                Time.time - lastTriggerAttemptTime >= cooldown;

            if (!cooldownReady)
                return false;

            var loudnessDelta = v.loudness - lastFrameLoudness;
            var onset = loudnessDelta >= settings.onsetLoudnessDelta;
            if (requireOnsetAfterSilence)
                return onset;

            var sustained = settings.transformSustainedRetrigger &&
                            v.loudness >= settings.transformSustainLoudnessMin;
            return onset || sustained;
        }

        void ApplyAssistive(VoiceFrame v)
        {
            var pitch = HzToMixerPitch(v.pitchHz, settings.referencePitchHz);
            if (assistiveMonitorSource != null)
                assistiveMonitorSource.pitch = pitch;

            if (mixer == null)
                return;

            if (Time.time - lastAssistiveUpdate < 0.03f)
                return;

            lastAssistiveUpdate = Time.time;
            var semitones = Mathf.Log(v.pitchHz / settings.referencePitchHz, 2f) * 12f;
            semitones = Mathf.Clamp(semitones, -settings.assistivePitchRangeSemitones, settings.assistivePitchRangeSemitones);
            var mixerPitch = Mathf.Pow(2f, semitones / 12f);
            mixer.SetFloat(pitchParam, mixerPitch);

            if (!string.IsNullOrEmpty(reverbSendParam))
                mixer.SetFloat(reverbSendParam, Mathf.Clamp01(v.loudness));
        }

        public static float HzToMixerPitch(float hz, float referenceHz)
        {
            if (hz <= 0f || referenceHz <= 0f)
                return 1f;
            var semitones = Mathf.Log(hz / referenceHz, 2f) * 12f;
            return Mathf.Pow(2f, Mathf.Clamp(semitones, -12f, 12f) / 12f);
        }
    }
}
