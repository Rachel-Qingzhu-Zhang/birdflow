using AvianVoice.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace AvianVoice.Audio
{
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

        VoiceMapMode mode = VoiceMapMode.Transform;
        float lastAssistiveUpdate;
        float lastFrameLoudness;
        float lastTriggerAttemptTime = -1f;
        bool requireOnsetAfterSilence = true;
        bool loggedAssistiveMixerWarning;

        public VoiceMapMode Mode => mode;

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
            if (analyzer == null || settings == null)
                return;

            var v = analyzer.Current;

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
            var loudness = v.loudness;
            if (AvianVoice.Tasks.TaskActivity.IsActive("singing"))
                loudness = ApplySingingRichness(loudness);
            library?.PlayNearest(transformSource, pitchHz, loudness, settings);
            lastTriggerAttemptTime = Time.time;
            lastFrameLoudness = v.loudness;
        }

        float ApplySingingRichness(float loudness)
        {
            var breathSim = FindAnyObjectByType<AvianVoice.Physiology.BreathSimulator>();
            if (breathSim != null && breathSim.Current.depth >= 0.7f)
                return Mathf.Clamp01(loudness * 1.35f + 0.06f);
            return loudness;
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
