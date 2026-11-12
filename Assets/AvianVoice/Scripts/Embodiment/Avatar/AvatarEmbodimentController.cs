using System;
using System.Collections.Generic;
using AvianVoice.BodyTracking;
using AvianVoice.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace AvianVoice.Embodiment.Avatars
{
    public enum AvatarKind
    {
        Bird,
        Human
    }

    /// <summary>
    /// Avian_Lab_Embodiment: the user IS the avatar. Body tracking (Meta Quest IOBT through the registered
    /// backend) drives the active avatar, the avatar eyes sit on the XR camera, and V / left X toggles
    /// between the parrot (PlayerParrot) and a human avatar loaded from Resources.
    /// </summary>
    [DefaultExecutionOrder(30990)]
    public class AvatarEmbodimentController : MonoBehaviour
    {
        [Header("Avatars")]
        [SerializeField]
        AvatarKind startAvatar = AvatarKind.Bird;

        [Tooltip("Human avatars (Resources paths of Humanoid-rigged models, no extension). The experimenter chooses among " +
                 "the selectable ones: gender-neutral (MakeHuman, gender 0.5, flat chest), male and female (Microsoft " +
                 "Rocketbox Male_Adult_01 / Female_Adult_01). The faceless robot mannequin (Unity Starter Assets Armature, " +
                 "Meta's stylized body-tracking sample body) is kept but not offered: tick 'selectable' to bring it back. " +
                 "Ids before 2026-11-12: 'neutral' was that mannequin (2026-11-05 to 11-12) and the MakeHuman body was " +
                 "'makehuman'. Missing models are reported and skipped.")]
        [SerializeField]
        HumanAvatarOption[] humanAvatars =
        {
            new()
            {
                id = "neutral", label = "neutral (MakeHuman)", labelJa = "中性",
                resourcePath = "Avatars/MakeHuman/Neutral_Adult_01/Neutral_Adult_01",
                // Measured in T-pose (menu: Debug / Measure Human Avatar Eye Point): head bone at 1.49 m, eye
                // bone / eyeball centres 0.038 m up and ~0.075 m forward (face front 0.124 m) -> camera at the eyeballs.
                eyeOffsetFromHeadBone = new Vector3(0f, 0.038f, 0.075f)
            },
            new()
            {
                id = "male", label = "male (Rocketbox)", labelJa = "男性",
                resourcePath = "Avatars/Rocketbox/Male_Adult_01/Male_Adult_01",
                // Measured in the rest pose (Debug / Measure Human Avatar Eye Point): head bone at 1.59 m, eye bone
                // (Bip01 LEye, eyeball centre) 0.099 m up and 0.086 m forward.
                eyeOffsetFromHeadBone = new Vector3(0f, 0.099f, 0.086f)
            },
            new()
            {
                id = "female", label = "female (Rocketbox)", labelJa = "女性",
                resourcePath = "Avatars/Rocketbox/Female_Adult_01/Female_Adult_01",
                // Measured in the rest pose: head bone at 1.52 m, eye bone (Bip01 LEye) 0.090 m up and 0.086 m forward.
                eyeOffsetFromHeadBone = new Vector3(0f, 0.090f, 0.086f)
            },
            new()
            {
                id = "mannequin", label = "mannequin (Armature)", labelJa = "マネキン",
                resourcePath = "Avatars/UnityArmature/Armature",
                // Measured on the Armature (menu: Debug / Measure Human Avatar Eye Point): head bone at 1.59 m, head
                // top 0.15 m above it, face front 0.11 m ahead -> eye line ~0.035 m up, ~0.09 m forward.
                eyeOffsetFromHeadBone = new Vector3(0f, 0.035f, 0.09f),
                selectable = false
            }
        };

        [Tooltip("Which human avatar is shown first when switching to Human.")]
        [SerializeField]
        string startHumanAvatarId = "neutral";

        [SerializeField]
        bool hideHumanHeadInFirstPerson = true;

        [Tooltip("Hide the bird's own head (head bone, beak, eyes) for the headset camera; it still shows in the mirror.")]
        [SerializeField]
        bool hideBirdHeadInFirstPerson = true;

        [Tooltip("Layer for the human avatar. HumanAvatar (12) is rendered by every camera, shows in the mirror and " +
                 "pond reflections, and is skipped by the bird-tuned reflection fill lights that otherwise wash it out.")]
        [SerializeField]
        int humanAvatarLayer = BirdSceneRoles.HumanAvatarLayer;

        [Header("Tracking space")]
        [Tooltip("Align the body tracking space to the world every frame from the headset's own pose in that space " +
                 "(backend) and the XR camera: exact, and it follows Quest recenters. The two checks below are only the " +
                 "fallback when the backend cannot report the headset pose.")]
        [SerializeField]
        bool alignFromHeadsetPose = true;

        [Tooltip("Smoothing of that alignment (s). The true transform only changes at recenters; this removes the small " +
                 "timing difference between the camera pose and the backend's headset pose during fast head motion.")]
        [SerializeField]
        float headsetAlignSmoothingSeconds = 0.25f;

        [Tooltip("A change of the alignment larger than this (m or degrees) is a recenter: applied at once, not smoothed.")]
        [SerializeField]
        float headsetAlignSnapMeters = 0.2f;

        [SerializeField]
        float headsetAlignSnapDegrees = 8f;

        [Tooltip("Fallback: assume body tracking and the XR Origin share the same floor-level tracking space. " +
                 "If the tracked head drifts away from the camera, the head+hands solver takes over automatically.")]
        [SerializeField]
        bool assumeSharedTrackingSpace = true;

        [SerializeField]
        float sharedSpaceMaxHeadErrorMeters = 0.25f;

        [Header("Participant input")]
        [Tooltip("Off (default): the participant's XR controllers are ignored; scenes and avatars are driven from the PC. " +
                 "The experimenter panel can change this at runtime.")]
        [SerializeField]
        bool participantControllersEnabled;

        [Tooltip("Off (default): everything the XR rig draws for the hands and controllers is hidden: hand meshes and " +
                 "joints, controller models, pinch / poke pointers and interactor rays (tracking stays on).")]
        [SerializeField]
        bool participantHandsVisible;

        [Header("UI")]
        [Tooltip("Head-locked status card below the participant's view (avatar, switch keys, body tracking state). " +
                 "Off for the study: the participant should not see the manipulation; the experimenter panel can turn it on.")]
        [SerializeField]
        bool showSwitchHint;

        [SerializeField]
        bool logStatus = true;

        /// <summary>One selectable human body.</summary>
        [Serializable]
        public class HumanAvatarOption
        {
            public string id = "";
            public string label = "";
            public string labelJa = "";
            public string resourcePath = "";

            [Tooltip("Eye point relative to the Humanoid head bone in the avatar root frame (meters, T-pose): where the headset camera sits.")]
            public Vector3 eyeOffsetFromHeadBone = new(0f, 0.07f, 0.09f);

            [Tooltip("Offered in the experimenter panel and the Next Human Avatar menu. Off: kept in the project and still " +
                     "usable as the start avatar, but not offered.")]
            public bool selectable = true;
        }

        sealed class HumanInstance
        {
            public HumanAvatarOption option;
            public GameObject root;
            public HumanFirstPersonDriver driver;
        }

        readonly TrackingSpaceSolver solver = new();
        readonly List<Renderer> birdRenderers = new();
        readonly List<XRHandSubsystem> handSubsystems = new();
        readonly Dictionary<string, HumanInstance> humanInstances = new();
        readonly Dictionary<string, bool> humanAvailability = new();

        Transform birdRoot;
        BirdFirstPersonDriver birdDriver;
        GameObject humanRoot;
        HumanFirstPersonDriver humanDriver;
        XRHandSubsystem handSubsystem;
        TextMeshProUGUI hintLabel;
        float nextHintRefresh;
        int sharedErrorFrames;
        bool sharedSpaceRejected;
        Pose headsetAlign = Pose.identity;
        bool headsetAlignValid;
        int headsetAlignSnaps;
        string lastTrackingSpaceMode = "";
        bool started;

        public AvatarKind Current { get; private set; }

        /// <summary>Id of the human body in use (one of <see cref="HumanAvatars"/>), whichever avatar is shown now.</summary>
        public string HumanAvatarId { get; private set; } = "";

        public HumanAvatarOption[] HumanAvatars => humanAvatars;

        public HumanAvatarOption CurrentHumanOption => FindHumanOption(HumanAvatarId);

        /// <summary>"Bird" or "Human:&lt;id&gt;", for logs and the study recorder.</summary>
        public string CurrentLabel => Current == AvatarKind.Bird ? "Bird" : "Human:" + HumanAvatarId;

        /// <summary>Model of the avatar shown now: the human body's Resources path, or the bird's object name.</summary>
        public string CurrentModel => Current == AvatarKind.Bird
            ? (BirdRoot != null ? BirdRoot.name : "bird")
            : CurrentHumanOption != null ? CurrentHumanOption.resourcePath : "";

        public event Action<AvatarKind> AvatarChanged;

        /// <summary>Raised when the human body changes (id), even while the bird is shown.</summary>
        public event Action<string> HumanAvatarChanged;

        public Camera XrCamera { get; private set; }

        /// <summary>Unity tracking space (Camera Offset, or the XR Origin root).</summary>
        public Transform TrackingSpace { get; private set; }

        public IBodyTrackingBackend Backend { get; private set; }

        public BodyPoseFrame Frame { get; } = new();

        public bool BodyTrackingValid { get; private set; }

        /// <summary>Body tracking space to world. Valid only while <see cref="TrackingToWorldValid"/>.</summary>
        public Pose TrackingToWorld { get; private set; } = Pose.identity;

        public bool TrackingToWorldValid { get; private set; }

        /// <summary>How <see cref="TrackingToWorld"/> is obtained: "headset" (exact), "shared" or "solver" (fallbacks), "none".</summary>
        public string TrackingSpaceMode { get; private set; } = "none";

        public Transform BirdRoot => birdRoot;

        public GameObject HumanRoot => humanRoot;

        /// <summary>One-line state dump for the diagnostics menu and bug reports.</summary>
        public string DescribeState()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("avatar=").Append(Current).Append(" human=").Append(HumanAvatarId);
            sb.Append(" backend=").Append(Backend != null ? Backend.DisplayName + (Backend.IsRunning ? " (running)" : " (stopped)") : "none");
            sb.Append(" bodyValid=").Append(BodyTrackingValid).Append(" conf=").Append(Frame.Confidence.ToString("0.00"));
            sb.Append(" fullBody=").Append(Frame.IsFullBody);
            sb.Append(" t2wValid=").Append(TrackingToWorldValid);
            sb.Append(" space=").Append(TrackingSpaceMode);
            if (TrackingSpaceMode == "headset" && TrackingSpace != null)
            {
                // How far the body tracking space is from the XR Origin's own space (non-zero after a recenter):
                // the error the old shared-space assumption would have had.
                sb.Append(" offsetFromXrOrigin=").Append(Vector3.Distance(TrackingSpace.position, TrackingToWorld.position).ToString("F2"))
                  .Append("m/").Append(Quaternion.Angle(TrackingSpace.rotation, TrackingToWorld.rotation).ToString("F0")).Append("deg");
                sb.Append(" recenterSnaps=").Append(headsetAlignSnaps);
            }
            else
            {
                sb.Append(" sharedSpace=").Append(assumeSharedTrackingSpace && !sharedSpaceRejected);
            }
            if (XrCamera != null)
            {
                sb.Append(" camPos=").Append(XrCamera.transform.position.ToString("F2"));
                sb.Append(" camYaw=").Append(TrackingSpaceSolver.YawOf(XrCamera.transform.forward).ToString("F0"));
            }

            if (TrackingToWorldValid && Frame.Has(BodyJoint.Head))
            {
                var head = TrackingSpaceSolver.TransformPoint(TrackingToWorld, Frame[BodyJoint.Head].position);
                sb.Append(" headJointWorld=").Append(head.ToString("F2"));
                if (XrCamera != null)
                    sb.Append(" headErr=").Append(Vector3.Distance(head, XrCamera.transform.position).ToString("F2"));
            }

            if (TryGetTorsoYawWorld(out var torsoYaw))
                sb.Append(" torsoYaw=").Append(torsoYaw.ToString("F0"));
            if (TryGetArmElevation(out var l, out var r))
                sb.Append(" armElev=").Append(l.ToString("F0")).Append('/').Append(r.ToString("F0"));
            if (birdRoot != null)
                sb.Append(" birdPos=").Append(birdRoot.position.ToString("F2")).Append(" birdYaw=").Append(birdRoot.eulerAngles.y.ToString("F0"))
                  .Append(" birdDriver=").Append(birdDriver != null && birdDriver.enabled);
            if (humanRoot != null)
                sb.Append(" humanActive=").Append(humanRoot.activeSelf).Append(" humanPos=").Append(humanRoot.transform.position.ToString("F2"))
                  .Append(" humanTracked=").Append(humanDriver != null && humanDriver.IsTracked)
                  .Append(" human[").Append(humanDriver != null ? humanDriver.Describe() : "").Append(']')
                  .Append(" humanLayer=").Append(humanRoot.layer);
            var handVisuals = GetComponent<ParticipantHandVisuals>();
            sb.Append(" controllers=").Append(Core.ParticipantInputPolicy.ControllersEnabled)
              .Append(" handsVisible=").Append(Core.ParticipantInputPolicy.HandsVisible)
              .Append(" handRenderersHidden=").Append(handVisuals != null ? handVisuals.HiddenRendererCount : 0);
            return sb.ToString();
        }

        /// <summary>Show or hide the head-locked status card (the experimenter panel toggles this).</summary>
        public bool HintVisible
        {
            get => showSwitchHint;
            set
            {
                showSwitchHint = value;
                if (hintLabel != null)
                    hintLabel.transform.parent.gameObject.SetActive(value);
                else if (value && started)
                    CreateHint();
            }
        }

        void Start()
        {
            Core.ParticipantInputPolicy.ControllersEnabled = participantControllersEnabled;
            Core.ParticipantInputPolicy.HandsVisible = participantHandsVisible;
            ResolveXrCamera();
            EnsureHandVisuals();
            Backend = BodyTrackingBackendRegistry.Create(gameObject);
            if (Backend != null)
            {
                Backend.RequestPermissions();
                if (logStatus)
                    Debug.Log("[AvianVoice] Embodiment body tracking backend: " + Backend.DisplayName);
            }
            else if (logStatus)
            {
                Debug.Log("[AvianVoice] No body tracking backend (Meta XR Core SDK missing?). " +
                          "Avatars follow the headset and hands only.");
            }

            ResolveBird();
            HumanAvatarId = FindHumanOption(startHumanAvatarId) != null ? startHumanAvatarId
                : humanAvatars.Length > 0 ? humanAvatars[0].id : "";
            ActivateHumanInstance(HumanAvatarId);
            CreateHint();
            started = true;
            SetAvatar(startAvatar);
        }

        void Update()
        {
            if (!started)
                return;

            if (AvatarSwitchInput.WasTogglePressedThisFrame())
                Toggle();

            if (hintLabel != null && Time.unscaledTime >= nextHintRefresh)
            {
                nextHintRefresh = Time.unscaledTime + 0.5f;
                RefreshHint();
            }
        }

        void LateUpdate()
        {
            if (!started)
                return;

            if (XrCamera == null)
                ResolveXrCamera();

            UpdateTracking(Time.deltaTime);

            if (Current == AvatarKind.Human)
                KeepBirdHidden();
        }

        public void Toggle()
        {
            SetAvatar(Current == AvatarKind.Bird ? AvatarKind.Human : AvatarKind.Bird);
        }

        public void SetAvatar(AvatarKind kind)
        {
            if (kind == AvatarKind.Human && humanRoot == null)
                ActivateHumanInstance(HumanAvatarId);

            if (kind == AvatarKind.Human && humanRoot == null)
            {
                Debug.LogWarning("[AvianVoice] Human avatar '" + HumanAvatarId + "' not available; staying on the bird.");
                kind = AvatarKind.Bird;
            }

            Current = kind;
            var bird = kind == AvatarKind.Bird;

            if (birdDriver != null)
                birdDriver.enabled = bird;
            SetBirdVisible(bird);

            if (humanRoot != null)
            {
                humanRoot.SetActive(!bird);
                if (humanDriver != null)
                    humanDriver.enabled = !bird;
            }

            RefreshHint();
            AvatarChanged?.Invoke(kind);
            if (logStatus)
                Debug.Log("[AvianVoice] Avatar: " + CurrentLabel);
        }

        /// <summary>
        /// Choose which human body is used (male / female / neutral ...). Switches the visible body at once
        /// when the human avatar is shown. Returns false when the id is unknown or its model is missing.
        /// </summary>
        public bool SetHumanAvatar(string id)
        {
            var option = FindHumanOption(id);
            if (option == null)
            {
                Debug.LogWarning("[AvianVoice] Unknown human avatar id: " + id);
                return false;
            }

            if (!HasHumanAvatar(id))
            {
                Debug.LogWarning("[AvianVoice] Human avatar '" + id + "' has no model at Resources/" + option.resourcePath);
                return false;
            }

            if (id == HumanAvatarId && humanRoot != null)
                return true;

            HumanAvatarId = id;
            ActivateHumanInstance(id);
            if (Current == AvatarKind.Human)
                SetAvatar(AvatarKind.Human);
            else
                RefreshHint();

            HumanAvatarChanged?.Invoke(id);
            if (logStatus)
                Debug.Log("[AvianVoice] Human avatar: " + id);
            return true;
        }

        /// <summary>True when the model for this human avatar id exists in Resources (checked once, cached).</summary>
        public bool HasHumanAvatar(string id)
        {
            var option = FindHumanOption(id);
            if (option == null || string.IsNullOrEmpty(option.resourcePath))
                return false;

            if (humanInstances.ContainsKey(id))
                return true;

            if (!humanAvailability.TryGetValue(id, out var available))
            {
                available = Resources.Load<GameObject>(option.resourcePath) != null;
                humanAvailability[id] = available;
            }

            return available;
        }

        public HumanAvatarOption FindHumanOption(string id)
        {
            if (string.IsNullOrEmpty(id) || humanAvatars == null)
                return null;

            foreach (var option in humanAvatars)
            {
                if (option != null && option.id == id)
                    return option;
            }

            return null;
        }

        // ---------------------------------------------------------------- tracking

        void UpdateTracking(float deltaTime)
        {
            BodyTrackingValid = Backend != null && Backend.TryGetFrame(Frame);
            if (!BodyTrackingValid || XrCamera == null)
            {
                TrackingToWorldValid = false;
                return;
            }

            var cam = XrCamera.transform;
            var cameraPose = new Pose(cam.position, cam.rotation);
            if (alignFromHeadsetPose && UpdateHeadsetAlignment(cameraPose, deltaTime))
            {
                TrackingToWorld = headsetAlign;
                TrackingToWorldValid = true;
                SetTrackingSpaceMode("headset");
                return;
            }

            var left = SampleHandWorld(XRNode.LeftHand, Handedness.Left);
            var right = SampleHandWorld(XRNode.RightHand, Handedness.Right);
            solver.Update(Frame, cameraPose, left, right, deltaTime);

            if (assumeSharedTrackingSpace && !sharedSpaceRejected && TrackingSpace != null)
            {
                var shared = new Pose(TrackingSpace.position, TrackingSpace.rotation);
                var predictedHead = TrackingSpaceSolver.TransformPoint(shared, Frame[BodyJoint.Head].position);
                var error = Vector3.Distance(predictedHead, cameraPose.position);
                if (error <= sharedSpaceMaxHeadErrorMeters)
                {
                    sharedErrorFrames = 0;
                    TrackingToWorld = shared;
                    TrackingToWorldValid = true;
                    SetTrackingSpaceMode("shared");
                    return;
                }

                if (++sharedErrorFrames > 90)
                {
                    sharedSpaceRejected = true;
                    Debug.Log("[AvianVoice] Body tracking space differs from the XR Origin (head error " +
                              error.ToString("0.00") + " m); switching to the head+hands alignment solver.");
                }
            }

            TrackingToWorld = solver.TrackingToWorld;
            TrackingToWorldValid = solver.HasSolution;
            SetTrackingSpaceMode(solver.HasSolution ? "solver" : "none");
        }

        /// <summary>
        /// Body tracking space to world from the headset: the backend reports the headset pose in the body tracking
        /// space, the camera gives it in world space, so world = R_yaw * tracking + t with R_yaw = yaw of
        /// (camera rotation * inverse headset rotation) and t = camera - R_yaw * headset. Both spaces are gravity
        /// aligned, so only yaw is kept. Smoothed (it is constant between recenters); a recenter-sized change snaps.
        /// </summary>
        bool UpdateHeadsetAlignment(Pose cameraPose, float deltaTime)
        {
            if (!HeadsetTrackingPose.TryGet(out var headset))
                return false;

            var delta = cameraPose.rotation * Quaternion.Inverse(headset.rotation);
            var yaw = Quaternion.Euler(0f, TrackingSpaceSolver.YawOf(delta * Vector3.forward), 0f);
            var target = new Pose(cameraPose.position - yaw * headset.position, yaw);

            if (!headsetAlignValid)
            {
                headsetAlign = target;
                headsetAlignValid = true;
                return true;
            }

            var jumpMeters = Vector3.Distance(target.position, headsetAlign.position);
            var jumpDegrees = Quaternion.Angle(target.rotation, headsetAlign.rotation);
            if (jumpMeters > headsetAlignSnapMeters || jumpDegrees > headsetAlignSnapDegrees)
            {
                headsetAlign = target;
                headsetAlignSnaps++;
                if (logStatus)
                    Debug.Log("[AvianVoice] Body tracking space re-aligned to the headset (recenter?): moved " + jumpMeters.ToString("0.00")
                              + " m / " + jumpDegrees.ToString("0") + " deg.");
                return true;
            }

            var k = headsetAlignSmoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / headsetAlignSmoothingSeconds);
            headsetAlign = new Pose(Vector3.Lerp(headsetAlign.position, target.position, k),
                Quaternion.Slerp(headsetAlign.rotation, target.rotation, k));
            return true;
        }

        void SetTrackingSpaceMode(string mode)
        {
            TrackingSpaceMode = mode;
            if (mode == lastTrackingSpaceMode)
                return;

            lastTrackingSpaceMode = mode;
            if (logStatus)
                Debug.Log("[AvianVoice] Body tracking space: " + mode + (mode == "headset" ? " (aligned from " + HeadsetTrackingPose.ProviderName + ")" : ""));
        }

        /// <summary>World position of a tracked joint, when both the joint and the space solution are valid.</summary>
        public bool TryGetJointWorld(BodyJoint joint, out Pose world)
        {
            world = Pose.identity;
            if (!TrackingToWorldValid || !Frame.Has(joint))
                return false;

            world = TrackingSpaceSolver.Compose(TrackingToWorld, Frame[joint]);
            return true;
        }

        /// <summary>Yaw (degrees, world) of the torso from the shoulder line, falling back to the upper arms.</summary>
        public bool TryGetTorsoYawWorld(out float yaw)
        {
            yaw = 0f;
            if (!TrackingToWorldValid)
                return false;

            Vector3 right;
            if (Frame.Has(BodyJoint.LeftShoulder) && Frame.Has(BodyJoint.RightShoulder))
                right = Frame[BodyJoint.RightShoulder].position - Frame[BodyJoint.LeftShoulder].position;
            else if (Frame.Has(BodyJoint.LeftUpperArm) && Frame.Has(BodyJoint.RightUpperArm))
                right = Frame[BodyJoint.RightUpperArm].position - Frame[BodyJoint.LeftUpperArm].position;
            else
                return false;

            right = TrackingToWorld.rotation * right;
            right.y = 0f;
            if (right.sqrMagnitude < 1e-4f)
                return false;

            var forward = Vector3.Cross(right.normalized, Vector3.up);
            yaw = TrackingSpaceSolver.YawOf(forward);
            return true;
        }

        /// <summary>Upper-arm elevation in degrees: 0 hanging down, 90 horizontal, 180 straight up.</summary>
        public bool TryGetArmElevation(out float leftDegrees, out float rightDegrees)
        {
            leftDegrees = rightDegrees = 0f;
            if (!BodyTrackingValid)
                return false;

            var okLeft = TryArmElevation(BodyJoint.LeftUpperArm, BodyJoint.LeftLowerArm, out leftDegrees);
            var okRight = TryArmElevation(BodyJoint.RightUpperArm, BodyJoint.RightLowerArm, out rightDegrees);
            return okLeft && okRight;
        }

        bool TryArmElevation(BodyJoint upper, BodyJoint lower, out float degrees)
        {
            degrees = 0f;
            if (!Frame.Has(upper) || !Frame.Has(lower))
                return false;

            var dir = Frame[lower].position - Frame[upper].position;
            if (dir.sqrMagnitude < 1e-5f)
                return false;

            degrees = Vector3.Angle(Vector3.down, dir);
            return true;
        }

        Vector3? SampleHandWorld(XRNode node, Handedness handedness)
        {
            var space = TrackingSpace;
            if (space == null)
                return null;

            var device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid
                && device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && tracked
                && device.TryGetFeatureValue(CommonUsages.devicePosition, out var devicePosition))
            {
                return space.TransformPoint(devicePosition);
            }

            if (handSubsystem == null || !handSubsystem.running)
                handSubsystem = FindHandSubsystem();
            if (handSubsystem == null)
                return null;

            var hand = handedness == Handedness.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!hand.isTracked)
                return null;

            return hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wrist)
                ? space.TransformPoint(wrist.position)
                : null;
        }

        XRHandSubsystem FindHandSubsystem()
        {
            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);
            foreach (var subsystem in handSubsystems)
            {
                if (subsystem != null && subsystem.running)
                    return subsystem;
            }

            return handSubsystems.Count > 0 ? handSubsystems[0] : null;
        }

        // ---------------------------------------------------------------- setup

        void ResolveXrCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (!t.name.Contains("XR Origin"))
                        continue;
                    cam = t.GetComponentInChildren<Camera>(true);
                    if (cam != null)
                        break;
                }
            }

            XrCamera = cam;
            TrackingSpace = cam != null ? (cam.transform.parent != null ? cam.transform.parent : cam.transform) : null;
        }

        void EnsureHandVisuals()
        {
            var rigRoot = TrackingSpace;
            while (rigRoot != null && rigRoot.parent != null && !rigRoot.name.Contains("XR Origin"))
                rigRoot = rigRoot.parent;
            if (rigRoot == null)
                return;

            var visuals = GetComponent<ParticipantHandVisuals>();
            if (visuals == null)
                visuals = gameObject.AddComponent<ParticipantHandVisuals>();
            visuals.Configure(rigRoot);
        }

        void ResolveBird()
        {
            birdRoot = BirdSceneRoles.Player;
            if (birdRoot == null)
            {
                foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.name == BirdSceneRoles.PlayerName)
                    {
                        birdRoot = t;
                        break;
                    }
                }
            }

            if (birdRoot == null)
            {
                Debug.LogWarning("[AvianVoice] Embodiment: no PlayerParrot in the scene; bird avatar unavailable.");
                return;
            }

            birdRoot.gameObject.SetActive(true);
            birdDriver = birdRoot.GetComponent<BirdFirstPersonDriver>();
            if (birdDriver == null)
                birdDriver = birdRoot.gameObject.AddComponent<BirdFirstPersonDriver>();
            birdDriver.Configure(this);
            birdDriver.enabled = false;

            if (hideBirdHeadInFirstPerson && birdRoot.GetComponent<FirstPersonHeadHider>() == null)
                birdRoot.gameObject.AddComponent<FirstPersonHeadHider>();
        }

        /// <summary>Make the instance for this id the active human body (instantiating it on first use).</summary>
        void ActivateHumanInstance(string id)
        {
            var next = GetOrCreateHumanInstance(id);
            if (humanRoot != null && (next == null || next.root != humanRoot))
            {
                humanRoot.SetActive(false);
                if (humanDriver != null)
                    humanDriver.enabled = false;
            }

            humanRoot = next?.root;
            humanDriver = next?.driver;
        }

        HumanInstance GetOrCreateHumanInstance(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            if (humanInstances.TryGetValue(id, out var existing))
                return existing;

            var option = FindHumanOption(id);
            if (option == null || string.IsNullOrEmpty(option.resourcePath))
                return null;

            var prefab = Resources.Load<GameObject>(option.resourcePath);
            humanAvailability[id] = prefab != null;
            if (prefab == null)
            {
                Debug.LogWarning("[AvianVoice] Human avatar '" + id + "' not found in Resources: " + option.resourcePath);
                return null;
            }

            var root = Instantiate(prefab);
            root.name = "HumanAvatar (" + id + ": " + prefab.name + ")";
            root.SetActive(false);

            var animator = root.GetComponent<Animator>();
            if (animator == null)
                animator = root.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.avatar == null || !animator.avatar.isHuman)
                Debug.LogWarning("[AvianVoice] Human avatar '" + id + "' is not imported as Humanoid; body tracking retargeting is disabled.");

            if (humanAvatarLayer >= 0 && humanAvatarLayer < 32)
                SetLayerRecursive(root, humanAvatarLayer);

            // Skin with 4 bones per vertex whatever the quality level says: the project runs at "Low" (2 bones), which
            // collapses blended joints such as MakeHuman's fingers into thin sticks as soon as they bend (the rigid-part
            // Armature robot never showed it).
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinned.quality = SkinQuality.Bone4;

            var driver = root.AddComponent<HumanFirstPersonDriver>();
            driver.Configure(this, Backend, hideHumanHeadInFirstPerson, option.eyeOffsetFromHeadBone);
            driver.enabled = false;

            var instance = new HumanInstance { option = option, root = root, driver = driver };
            humanInstances[id] = instance;
            return instance;
        }

        void SetBirdVisible(bool visible)
        {
            if (birdRoot == null)
                return;

            if (!visible)
            {
                birdRenderers.Clear();
                foreach (var renderer in birdRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || !renderer.enabled)
                        continue;
                    birdRenderers.Add(renderer);
                    renderer.enabled = false;
                }
                return;
            }

            foreach (var renderer in birdRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
            birdRenderers.Clear();
            BirdSceneRoles.EnsurePlayerBirdVisible();
        }

        void KeepBirdHidden()
        {
            // Task scripts re-enable the player bird; keep it hidden while the human avatar is active.
            foreach (var renderer in birdRenderers)
            {
                if (renderer != null && renderer.enabled)
                    renderer.enabled = false;
            }
        }

        void CreateHint()
        {
            if (!showSwitchHint || XrCamera == null)
                return;

            var go = new GameObject("AvatarSwitchHint");
            go.layer = BirdSceneRoles.UiLayer;
            go.transform.SetParent(XrCamera.transform, false);
            go.transform.localPosition = new Vector3(0f, -0.215f, 0.62f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.0012f;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 40;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(620f, 60f);
            go.AddComponent<AvianWorldCanvasCameraBinder>();

            var textGo = new GameObject("Label");
            textGo.layer = BirdSceneRoles.UiLayer;
            textGo.transform.SetParent(go.transform, false);
            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            hintLabel = textGo.AddComponent<TextMeshProUGUI>();
            hintLabel.fontSize = 20f;
            hintLabel.alignment = TextAlignmentOptions.Center;
            hintLabel.color = new Color(0.96f, 0.93f, 0.88f, 0.85f);
            hintLabel.raycastTarget = false;
            AvianUiFont.Apply(hintLabel);
            RefreshHint();
        }

        void RefreshHint()
        {
            if (hintLabel == null)
                return;

            string tracking;
            string trackingJa;
            if (BodyTrackingValid)
            {
                tracking = Frame.IsFullBody ? "full body" : "upper body";
                trackingJa = Frame.IsFullBody ? "全身" : "上半身";
            }
            else if (Backend == null)
            {
                tracking = "off (no backend)";
                trackingJa = "なし (バックエンド無し)";
            }
            else if (!Backend.IsRunning)
            {
                tracking = "off (tracker not running)";
                trackingJa = "なし (トラッカー未起動)";
            }
            else
            {
                tracking = "off (no data)";
                trackingJa = "なし (データ無し)";
            }

            var option = CurrentHumanOption;
            if (AvianUiLanguage.IsJapanese)
            {
                var avatar = Current == AvatarKind.Bird ? "鳥" : "人間 (" + (option != null ? option.labelJa : HumanAvatarId) + ")";
                hintLabel.text = "アバター: " + avatar + "　V キー / 左コントローラ X で切替　　体トラッキング: " + trackingJa;
            }
            else
            {
                var avatar = Current == AvatarKind.Bird ? "Bird" : "Human (" + (option != null ? option.label : HumanAvatarId) + ")";
                hintLabel.text = "Avatar: " + avatar + "   V key / left X button to switch   Body tracking: " + tracking;
            }
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
