using AvianVoice.Audio;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Wires Avian Voice subsystems on the XR rig root.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AvianAppBootstrap : MonoBehaviour
    {
        [SerializeField]
        AvianVoiceSettings settings;

        [SerializeField]
        PlatformProfile platformProfile;

        [SerializeField]
        Transform headAnchor;

        [SerializeField]
        Transform leftWingBone;

        [SerializeField]
        Transform rightWingBone;

        [SerializeField]
        Animator birdAnimator;

        [SerializeField]
        SkinnedMeshRenderer beakMesh;

        [SerializeField]
        int beakBlendShapeIndex = -1;

        [SerializeField]
        Transform beakTransform;

        [SerializeField]
        Transform chestTransform;

        [SerializeField]
        Transform secondaryChestTransform;

        [SerializeField]
        ParticleSystem vocalParticles;

        [SerializeField]
        AudioMixerReference mixerReference;

        [SerializeField]
        AudioSource transformBirdSource;

        [SerializeField]
        AudioSource assistiveMonitorSource;

        [SerializeField]
        BirdCallLibrary callLibrary;

        [SerializeField]
        MirrorBodySetup mirrorSetup;

        [SerializeField]
        TrainingHUD trainingHud;

        [SerializeField]
        VoiceModeToggle voiceModeToggle;

        [SerializeField]
        SessionRecorder sessionRecorder;

        [SerializeField]
        bool autoWireParrotEmbodiment = true;

        bool initialized;

        void Awake()
        {
            if (!initialized)
                InitializeFromSerializedFields();
        }

        public void Configure(AvianBootstrapConfig config)
        {
            if (config == null)
                return;

            settings = config.settings;
            platformProfile = config.platformProfile;
            headAnchor = config.headAnchor;
            leftWingBone = config.leftWingBone;
            rightWingBone = config.rightWingBone;
            birdAnimator = config.birdAnimator;
            beakMesh = config.beakMesh;
            beakBlendShapeIndex = config.beakBlendShapeIndex;
            beakTransform = config.beakTransform;
            chestTransform = config.chestTransform;
            secondaryChestTransform = config.secondaryChestTransform;
            vocalParticles = config.vocalParticles;
            mixerReference = config.mixerReference;
            transformBirdSource = config.transformBirdSource;
            assistiveMonitorSource = config.assistiveMonitorSource;
            callLibrary = config.callLibrary;
            mirrorSetup = config.mirrorSetup;
            trainingHud = config.trainingHud;
            voiceModeToggle = config.voiceModeToggle;
            sessionRecorder = config.sessionRecorder;

            InitializeFromSerializedFields();
        }

        void InitializeFromSerializedFields()
        {
            if (initialized)
                return;

            if (headAnchor == null)
                headAnchor = FindCameraOffsetInHierarchy(transform);
            if (autoWireParrotEmbodiment)
                TryAutoWireParrotEmbodiment();

            ApplyPlatformAudioSettings();

            var signalsGo = new GameObject("AvianSignals");
            var signals = signalsGo.AddComponent<AvianSignals>();

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<AvianVoiceSettings>();
                Debug.LogWarning(
                    "[AvianVoice] AvianAppBootstrap.settings is empty — using temporary defaults. " +
                    "Assign Assets/AvianVoice/Settings/AvianVoiceSettings.asset for saved tuning.");
            }

            var mic = gameObject.AddComponent<MicrophoneCapture>();
            var analyzer = gameObject.AddComponent<VoiceAnalyzer>();
            var voiceMapper = gameObject.AddComponent<BirdVoiceMapper>();
            var breath = gameObject.AddComponent<BreathSimulator>();
            var body = gameObject.AddComponent<BirdBodyController>();
            var wings = gameObject.AddComponent<WingHandMapper>();
            var beakFx = gameObject.AddComponent<BeakVocalFX>();
            var bodySync = gameObject.AddComponent<BreathBodySync>();

            ResolveCallLibrary();
            EnsureAudioSources();

            mic.Initialize(settings);
            analyzer.Initialize(settings, signals);
            voiceMapper.Initialize(settings, signals, analyzer, mixerReference, transformBirdSource, assistiveMonitorSource, callLibrary);
            breath.Initialize(settings, signals);
            body.Initialize(settings, birdAnimator, beakMesh, beakBlendShapeIndex, chestTransform, secondaryChestTransform);
            wings.Initialize(settings, ResolvePlatformProfile(), leftWingBone, rightWingBone, headAnchor);
            beakFx.Initialize(beakTransform, vocalParticles);
            bodySync.Initialize(signals, analyzer, breath, body, beakFx, wings);

            if (mirrorSetup != null)
                mirrorSetup.Initialize(settings);

            TryResolveUiFromHeadAnchor();
            InitializeUi(signals, analyzer, breath, voiceMapper);

            if (sessionRecorder == null)
                sessionRecorder = gameObject.AddComponent<SessionRecorder>();
            sessionRecorder.Initialize(settings, signals, analyzer, breath, voiceMapper);

            initialized = true;
        }

        void Start()
        {
            if (trainingHud != null)
                return;

            TryResolveUiFromHeadAnchor();
            if (trainingHud == null || !initialized)
                return;

            var signals = FindAnyObjectByType<AvianSignals>();
            var analyzer = GetComponent<VoiceAnalyzer>();
            var breath = GetComponent<BreathSimulator>();
            var voiceMapper = GetComponent<BirdVoiceMapper>();
            InitializeUi(signals, analyzer, breath, voiceMapper);
        }

        void TryResolveUiFromHeadAnchor()
        {
            if (headAnchor == null)
                return;

            if (trainingHud == null)
                trainingHud = headAnchor.GetComponentInChildren<TrainingHUD>(true);
            if (voiceModeToggle == null)
                voiceModeToggle = headAnchor.GetComponentInChildren<VoiceModeToggle>(true);
        }

        /// <summary>
        /// Attach or refresh audio output after bootstrap already ran (e.g. scene had Mic but empty AudioSource refs).
        /// </summary>
        public void AttachAudio(AudioSource transformSourceRef, AudioSource assistiveSourceRef, BirdCallLibrary library)
        {
            if (transformSourceRef != null)
                transformBirdSource = transformSourceRef;
            if (assistiveSourceRef != null)
                assistiveMonitorSource = assistiveSourceRef;
            if (library != null)
                callLibrary = library;

            if (!initialized)
                return;

            var signals = FindAnyObjectByType<AvianSignals>();
            var analyzer = GetComponent<VoiceAnalyzer>();
            var voiceMapper = GetComponent<BirdVoiceMapper>();
            if (voiceMapper != null && analyzer != null && signals != null)
                voiceMapper.Initialize(settings, signals, analyzer, mixerReference, transformBirdSource, assistiveMonitorSource, callLibrary);
        }

        /// <summary>
        /// Attach HUD / mode UI created after bootstrap already ran (common when AvianAppBootstrap is saved on the XR rig).
        /// </summary>
        public void AttachUi(TrainingHUD hud, VoiceModeToggle toggle)
        {
            trainingHud = hud;
            voiceModeToggle = toggle;
            if (!initialized)
                return;

            var signals = FindAnyObjectByType<AvianSignals>();
            var analyzer = GetComponent<VoiceAnalyzer>();
            var breath = GetComponent<BreathSimulator>();
            var voiceMapper = GetComponent<BirdVoiceMapper>();
            InitializeUi(signals, analyzer, breath, voiceMapper);
        }

        void InitializeUi(AvianSignals signals, VoiceAnalyzer analyzer, BreathSimulator breath, BirdVoiceMapper voiceMapper)
        {
            if (trainingHud != null && signals != null && analyzer != null && breath != null)
                trainingHud.Initialize(signals, analyzer, breath);

            if (voiceModeToggle != null && signals != null && voiceMapper != null)
                voiceModeToggle.Initialize(signals, voiceMapper);
        }

        void ResolveCallLibrary()
        {
            if (callLibrary != null)
                return;

            callLibrary = GetComponent<BirdCallLibrary>();
            if (callLibrary == null)
                callLibrary = gameObject.AddComponent<BirdCallLibrary>();
        }

        void EnsureAudioSources()
        {
            if (transformBirdSource == null)
                transformBirdSource = AvianAudioSourceUtility.FindOrCreate(transform, AvianAudioSourceUtility.TransformSourceName);
            if (assistiveMonitorSource == null)
                assistiveMonitorSource = AvianAudioSourceUtility.FindOrCreate(transform, AvianAudioSourceUtility.AssistiveSourceName);
        }

        PlatformProfile ResolvePlatformProfile()
        {
            if (platformProfile != null)
                return platformProfile;

            var fallback = ScriptableObject.CreateInstance<PlatformProfile>();
            fallback.useSimulatedHands = true;
            return fallback;
        }

        public void ApplyEmbodimentBones(
            Transform leftWing,
            Transform rightWing,
            Transform chest,
            Transform secondaryChest,
            Transform beak,
            SkinnedMeshRenderer beakRenderer,
            Animator animator,
            int beakBlendIndex = -1)
        {
            if (initialized)
                return;

            leftWingBone = leftWing;
            rightWingBone = rightWing;
            chestTransform = chest;
            secondaryChestTransform = secondaryChest;
            beakTransform = beak;
            beakMesh = beakRenderer;
            beakBlendShapeIndex = beakBlendIndex;
            if (animator != null)
                birdAnimator = animator;
        }

        public void SetAutoWireParrotEmbodiment(bool enabled)
        {
            autoWireParrotEmbodiment = enabled;
        }

        public void RegisterMirrorBird(Transform birdRoot)
        {
            if (birdRoot == null)
                return;

            if (mirrorSetup == null)
                mirrorSetup = GetComponent<MirrorBodySetup>() ?? gameObject.AddComponent<MirrorBodySetup>();
            mirrorSetup.SetBirdBodyRoot(birdRoot);
        }

        void TryAutoWireParrotEmbodiment()
        {
            if (PigeonFpvSetup.FindPigeonInScene() != null)
                return;

            var parrotRoot = FindActiveParrotInScene();
            if (parrotRoot == null)
                return;

            if (NeedsParrotEmbodimentRewire() &&
                ParrotEmbodimentWiring.TryResolve(parrotRoot, out var refs))
            {
                leftWingBone = refs.leftWing;
                rightWingBone = refs.rightWing;
                chestTransform = refs.chest;
                secondaryChestTransform = refs.secondaryChest;
                if (refs.neck != null && (beakTransform == null || IsParrotControlBone(beakTransform)))
                {
                    var headBone = refs.neck.Find("parrot_Head_bone");
                    if (headBone != null)
                        beakTransform = headBone;
                }
                if (birdAnimator == null)
                    birdAnimator = refs.animator;
            }
            else if (NeedsParrotEmbodimentRewire())
            {
                Debug.LogWarning("[AvianVoice] Parrot found but breath bones could not be resolved on '" + parrotRoot.name + "'.");
            }

            ParrotEmbodimentWiring.ApplyAnimatorMeditationDefaults(birdAnimator);
            ParrotEmbodimentWiring.DisableConflictingParrotDemoScript(parrotRoot);
            EnsureParrotRuntimeComponents(parrotRoot);

            if (beakTransform == null || IsParrotControlBone(beakTransform))
            {
                var headBone = parrotRoot.Find("parrot_Head_bone");
                if (headBone == null && parrotRoot.GetComponentInChildren<Transform>(true) != null)
                {
                    foreach (var bone in parrotRoot.GetComponentsInChildren<Transform>(true))
                    {
                        if (bone.name == "parrot_Head_bone")
                        {
                            headBone = bone;
                            break;
                        }
                    }
                }

                if (headBone != null)
                    beakTransform = headBone;
            }
        }

        static void EnsureParrotRuntimeComponents(Transform parrotRoot)
        {
            if (parrotRoot == null)
                return;

            var guard = parrotRoot.GetComponent<WorldPlacedBird>() ?? parrotRoot.gameObject.AddComponent<WorldPlacedBird>();
            var avianLabScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("Avian");

            if (avianLabScene || parrotRoot.name is BirdSceneRoles.FriendName or BirdSceneRoles.PlayerName)
            {
                guard.ConfigureForFixedWorldPlacement();
                WireSceneRoleParrotComponents(parrotRoot);
                return;
            }

            if (parrotRoot.GetComponent<ParrotBreathEmbodiment>() == null)
                parrotRoot.gameObject.AddComponent<ParrotBreathEmbodiment>();
            if (parrotRoot.GetComponent<ParrotMainCtrlPin>() == null)
                parrotRoot.gameObject.AddComponent<ParrotMainCtrlPin>();
            guard.EnsureVisibleAndGrounded();
        }

        static void WireSceneRoleParrotComponents(Transform parrotRoot)
        {
            if (parrotRoot.name == BirdSceneRoles.FriendName || parrotRoot.name == "Parrot")
            {
                var playerBreath = parrotRoot.GetComponent<ParrotBreathEmbodiment>();
                if (playerBreath != null)
                    UnityEngine.Object.Destroy(playerBreath);
                if (parrotRoot.GetComponent<FriendBreathVisual>() == null)
                    parrotRoot.gameObject.AddComponent<FriendBreathVisual>();
            }
            else if (parrotRoot.GetComponent<ParrotBreathEmbodiment>() == null)
                parrotRoot.gameObject.AddComponent<ParrotBreathEmbodiment>();

            var pin = parrotRoot.GetComponent<ParrotMainCtrlPin>() ?? parrotRoot.gameObject.AddComponent<ParrotMainCtrlPin>();
            pin.SetPinEnabled(true);
        }

        bool NeedsParrotEmbodimentRewire()
        {
            if (leftWingBone == null || rightWingBone == null || chestTransform == null)
                return true;

            if (IsParrotControlBone(chestTransform) || IsParrotControlBone(beakTransform))
                return true;

            if (leftWingBone.name.Contains("_Ulna_") || rightWingBone.name.Contains("_Ulna_"))
                return true;

            return secondaryChestTransform == null;
        }

        static bool IsParrotControlBone(Transform bone)
        {
            return bone != null && bone.name.EndsWith("_ctrl");
        }

        static Transform FindActiveParrotInScene()
        {
            Transform friend = null;
            Transform legacy = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!t.gameObject.activeInHierarchy)
                    continue;
                if (t.name == BirdSceneRoles.FriendName)
                    friend = t;
                else if (t.name == "Parrot")
                    legacy = t;
            }

            return friend != null ? friend : legacy;
        }

        static Transform FindCameraOffsetInHierarchy(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Camera Offset")
                    return t;
            }
            return null;
        }

        void ApplyPlatformAudioSettings()
        {
            var profile = ResolvePlatformProfile();
            var dspSize = profile != null ? profile.DspBufferSize : 256;
            if (settings != null && settings.dspBufferSizeOverride > 0)
                dspSize = settings.dspBufferSizeOverride;

            var config = AudioSettings.GetConfiguration();
            config.dspBufferSize = Mathf.Clamp(dspSize, 64, 1024);
            AudioSettings.Reset(config);
        }
    }

    [System.Serializable]
    public class AudioMixerReference
    {
        public UnityEngine.Audio.AudioMixer mixer;
        public string pitchParam = "MicPitch";
        public string reverbSendParam = "MicReverb";
    }
}
