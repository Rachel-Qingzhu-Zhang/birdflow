using UnityEngine;

namespace AvianVoice.Core
{
    [CreateAssetMenu(fileName = "AvianVoiceSettings", menuName = "Avian Voice/Settings")]
    public class AvianVoiceSettings : ScriptableObject
    {
        [Header("Audio")]
        public int sampleRate = 48000;
        [Tooltip("Samples per pitch/loudness analysis window. 1024 @ 48kHz ≈ 21ms. Keep ≥ 512 for low voices.")]
        public int frameSize = 1024;
        [Tooltip("Process a new analysis every N new mic samples (lower = less input lag, more CPU).")]
        public int analysisHopSize = 256;
        public int ringBufferSeconds = 1;
        public float minPitchHz = 80f;
        public float maxPitchHz = 800f;
        [Tooltip("Higher = smoother HUD/trigger but slower response to your voice.")]
        public float loudnessSmooth = 8f;
        [Tooltip("Override Unity DSP buffer size at startup (0 = use Platform Profile only). 128–256 = lower latency.")]
        public int dspBufferSizeOverride = 256;
        [Tooltip("Raw mic RMS required before a frame counts as voiced. Above room noise and fan hum.")]
        public float voicedThreshold = 0.012f;
        public float loudnessGain = 8f;

        [Header("Pitch Tracking (YIN)")]
        [Tooltip("YIN absolute threshold. Lower = stricter periodicity, fewer octave errors.")]
        [Range(0.05f, 0.5f)]
        public float yinThreshold = 0.15f;
        [Tooltip("Below this confidence the last good pitch is held instead of updated.")]
        [Range(0f, 1f)]
        public float pitchMinConfidence = 0.55f;
        [Tooltip("How long a held pitch survives low-confidence frames before dropping to 0.")]
        public float pitchHoldSeconds = 0.2f;
        [Tooltip("Pitch smoothing time constant in seconds (applied in semitones).")]
        public float pitchSmoothingSeconds = 0.04f;
        [Tooltip("Jumps larger than this (semitones) snap immediately as a new note.")]
        public float pitchSnapSemitones = 3f;

        [Header("Voice Mapping")]
        public float referencePitchHz = 200f;
        public float transformCooldown = 0.14f;
        public float minTransformCooldown = 0.05f;
        public float maxTransformCooldown = 10f;
        public float assistivePitchRangeSemitones = 12f;

        [Header("Transform Bird Call")]
        [Tooltip("Retrigger interval follows the selected clip length (1s audio ≈ one chirp per second).")]
        public bool transformCooldownUsesClipLength = true;
        [Tooltip("Extra seconds after each clip before the next trigger (0 = strictly clip length).")]
        public float transformClipGapSeconds = 0f;
        [Tooltip("When enabled, keep retriggering on cooldown while loudness stays high. Leave off so room noise cannot chain chirps.")]
        public bool transformSustainedRetrigger = false;
        [Tooltip("Min loudness (0-1, after gain) required before a sustained retrigger is allowed.")]
        public float transformSustainLoudnessMin = 0.45f;
        [Tooltip("Rise in gained loudness since last frame required to start a chirp.")]
        public float onsetLoudnessDelta = 0.15f;
        [Tooltip("Map playback pitch from voice Hz / referencePitchHz (easier to hear than clip basePitchHz alone).")]
        public bool transformPitchFromReference = true;
        [Tooltip("Smoothing for mapped playback pitch (higher = faster tracking).")]
        public float pitchSmooth = 20f;
        [Tooltip("Max Hz jump from last triggered pitch before waiting for cooldown.")]
        public float transformPitchStabilityHz = 40f;
        [Tooltip("Playback pitch ratio clamp (AudioSource.pitch).")]
        public float transformMinPitch = 0.4f;
        public float transformMaxPitch = 2.5f;
        [Tooltip("Output volume range after loudness curve.")]
        public float transformVolumeMin = 0.15f;
        public float transformVolumeMax = 1f;
        [Tooltip("Skip new PlayOneShot while source is already playing (prevents stacking).")]
        public bool transformBlockOverlap = true;

        [Header("Bird Song Synth (Transform mode)")]
        [Tooltip("Synthesized bird chirps that follow your pitch. Off = play sample chirps instead.")]
        public bool useSynthTransform = true;
        [Tooltip("Bird Hz = human Hz * this ratio, then clamped to the bird range. 8 = three octaves up.")]
        public float synthPitchRatio = 8f;
        [Tooltip("Length of one chirp in seconds.")]
        public float synthChirpSeconds = 0.08f;
        [Tooltip("Unused for timing. Chirps follow voice onsets; this is only a fallback.")]
        public float synthChirpGapSeconds = 0.05f;
        public float synthHumanMinHz = 90f;
        public float synthHumanMaxHz = 900f;
        public float synthBirdMinHz = 1600f;
        public float synthBirdMaxHz = 5500f;
        [Tooltip("Trill (frequency wobble) rate range in Hz. Louder voice = faster trill.")]
        public float synthTrillRateMin = 18f;
        public float synthTrillRateMax = 28f;
        [Tooltip("Trill depth in semitones.")]
        public float synthTrillDepthSemitones = 0.6f;
        [Tooltip("Upward glide at note onset, in semitones below the target.")]
        public float synthOnsetGlideSemitones = 2.5f;
        public float synthOnsetGlideSeconds = 0.04f;
        public float synthAttackSeconds = 0.008f;
        public float synthReleaseSeconds = 0.09f;
        [Tooltip("2nd harmonic level relative to the fundamental.")]
        [Range(0f, 1f)]
        public float synthHarmonic2 = 0.18f;
        [Tooltip("Breathy band-limited noise level.")]
        [Range(0f, 1f)]
        public float synthNoise = 0.06f;
        [Range(0f, 1f)]
        public float synthVolume = 0.65f;
        [Tooltip("Pitch glide time constant between voice frames (seconds).")]
        public float synthPitchSmoothSeconds = 0.04f;

        [Header("Body")]
        public float paramSmooth = 10f;
        public float wingRotationScale = 1f;
        public float chestScaleMax = 0.15f;
        [Tooltip("Uniform scale on parrot root at full breath (0 = off; use chest/wings instead).")]
        public float bodyRootBreathScale = 0f;
        [Tooltip("Extra scale on Spine1_ctrl at full breath.")]
        public float spineLowerChestScaleMax = 0.22f;
        [Tooltip("Neck pitch (degrees) on inhale — makes chest rise more visible.")]
        public float breathNeckPitchMax = 14f;

        [Header("Meditation Breath")]
        [Tooltip("When true, the teacher breath drives wing spread. Otherwise wings only follow hands / auto flap.")]
        public bool breathDrivesWings = true;
        [Tooltip("Max wing spread angle (degrees) at full inhale.")]
        public float wingBreathSpreadMax = 32f;
        [Tooltip("Subtle wing flutter amplitude on exhale (degrees).")]
        public float wingExhaleTremorMax = 6f;
        [Tooltip("Wing pitch offset on inhale (degrees, negative = wings lift slightly).")]
        public float wingBreathPitchOffset = -8f;
        [Tooltip("Optional second chest bone scale (e.g. rib cage). Leave unassigned in bootstrap.")]
        public float secondaryChestScaleMax = 0.08f;

        [Header("Teacher Breath (drives the bird; no breath input)")]
        [Tooltip("Meet task: seconds for a full inhale (0 to max depth).")]
        public float introInhaleSeconds = 4f;
        public float introExhaleSeconds = 4f;
        [Tooltip("Breath Sync first pattern and the default pattern for later tasks.")]
        public float patternAInhaleSeconds = 4f;
        public float patternAExhaleSeconds = 4f;
        [Tooltip("Breath Sync second, slower pattern.")]
        public float patternBInhaleSeconds = 6f;
        public float patternBExhaleSeconds = 6f;
        [Tooltip("Breath depth at the end of an exhale (0-1).")]
        [Range(0f, 1f)]
        public float breathDepthMin = 0f;
        [Tooltip("Breath depth at the top of an inhale (0-1). Lower = smaller breathing amplitude.")]
        [Range(0f, 1f)]
        public float breathDepthMax = 1f;
        [Tooltip("Upper chest bone growth at full depth (0.4 = +40%).")]
        public float chestInhaleScale = 0.40f;
        [Tooltip("Belly / mid chest bone growth at full depth.")]
        public float bellyInhaleScale = 0.28f;
        [Tooltip("1 = linear. >1 keeps the chest calm until late in the inhale.")]
        public float breathCurvePower = 1f;
        [Tooltip("Breath cycles to watch in Meet before it completes.")]
        public int meetCycles = 3;
        [Tooltip("Breath cycles of Pattern A in Breath Sync before switching to Pattern B.")]
        public int patternACycles = 3;
        [Tooltip("Breath cycles of Pattern B before Breath Sync completes.")]
        public int patternBCycles = 2;

        [Header("Experiment")]
        public bool mirrorEnabled = true;
        public bool wingMappingEnabled = true;
        [Tooltip("When false, hides TaskFlowUI, TrainingHUD, and voice mode panels.")]
        public bool showHudPanels = false;
        public float recorderSampleHz = 30f;

        static AvianVoiceSettings cached;

        /// <summary>The loaded AvianVoiceSettings asset, if any object in the scene references it.</summary>
        public static AvianVoiceSettings FindLoaded()
        {
            if (cached != null)
                return cached;

            foreach (var asset in Resources.FindObjectsOfTypeAll<AvianVoiceSettings>())
            {
                if (asset != null && asset.name.Contains("AvianVoiceSettings"))
                {
                    cached = asset;
                    break;
                }
            }

            return cached;
        }
    }
}
