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
        public float voicedThreshold = 0.02f;
        public float loudnessGain = 8f;

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
        [Tooltip("After the first chirp, keep retriggering while voiced (cooldown only), not only on loudness spikes.")]
        public bool transformSustainedRetrigger = true;
        [Tooltip("Min loudness to allow sustained retrigger (0-1).")]
        public float transformSustainLoudnessMin = 0.08f;
        [Tooltip("Rise in loudness since last frame required for the first chirp after silence.")]
        public float onsetLoudnessDelta = 0.03f;
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
        [Tooltip("When true, Space breath drives wing spread (desktop + VR). Otherwise wings only follow hands / auto flap.")]
        public bool breathDrivesWings = true;
        [Tooltip("Max wing spread angle (degrees) at full inhale.")]
        public float wingBreathSpreadMax = 32f;
        [Tooltip("Subtle wing flutter amplitude on exhale (degrees).")]
        public float wingExhaleTremorMax = 6f;
        [Tooltip("Wing pitch offset on inhale (degrees, negative = wings lift slightly).")]
        public float wingBreathPitchOffset = -8f;
        [Tooltip("Optional second chest bone scale (e.g. rib cage). Leave unassigned in bootstrap.")]
        public float secondaryChestScaleMax = 0.08f;

        [Header("Breath")]
        public float inhaleSpeed = 0.85f;
        public float exhaleSpeed = 0.65f;

        [Header("Experiment")]
        public bool mirrorEnabled = true;
        public bool wingMappingEnabled = true;
        [Tooltip("When false, hides TaskFlowUI, TrainingHUD, and voice mode panels.")]
        public bool showHudPanels = false;
        public float recorderSampleHz = 30f;
    }
}
