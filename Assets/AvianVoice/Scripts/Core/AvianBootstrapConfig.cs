using AvianVoice.Audio;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Core
{
    public class AvianBootstrapConfig
    {
        public AvianVoiceSettings settings;
        public PlatformProfile platformProfile;
        public Transform headAnchor;
        public Transform leftWingBone;
        public Transform rightWingBone;
        public Animator birdAnimator;
        public SkinnedMeshRenderer beakMesh;
        public int beakBlendShapeIndex = -1;
        public Transform beakTransform;
        public Transform chestTransform;
        public Transform secondaryChestTransform;
        public ParticleSystem vocalParticles;
        public AudioMixerReference mixerReference;
        public AudioSource transformBirdSource;
        public AudioSource assistiveMonitorSource;
        public BirdCallLibrary callLibrary;
        public MirrorBodySetup mirrorSetup;
        public TrainingHUD trainingHud;
        public VoiceModeToggle voiceModeToggle;
        public Data.SessionRecorder sessionRecorder;
    }
}
