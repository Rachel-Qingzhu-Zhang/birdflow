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

        [Tooltip("Resources path of a Humanoid-rigged model (no extension).")]
        [SerializeField]
        string humanAvatarResourcePath = "Avatars/Rocketbox/Male_Adult_01/Male_Adult_01";

        [SerializeField]
        bool hideHumanHeadInFirstPerson = true;

        [Tooltip("Put the human avatar on the BirdMirror layer so the mirror and pond reflections show it.")]
        [SerializeField]
        bool humanAvatarOnMirrorLayer = true;

        [Header("Tracking space")]
        [Tooltip("Assume body tracking and the XR Origin share the same floor-level tracking space. " +
                 "If the tracked head drifts away from the camera, the head+hands solver takes over automatically.")]
        [SerializeField]
        bool assumeSharedTrackingSpace = true;

        [SerializeField]
        float sharedSpaceMaxHeadErrorMeters = 0.25f;

        [Header("UI")]
        [SerializeField]
        bool showSwitchHint = true;

        [SerializeField]
        bool logStatus = true;

        readonly TrackingSpaceSolver solver = new();
        readonly List<Renderer> birdRenderers = new();
        readonly List<XRHandSubsystem> handSubsystems = new();

        Transform birdRoot;
        BirdFirstPersonDriver birdDriver;
        GameObject humanRoot;
        HumanFirstPersonDriver humanDriver;
        XRHandSubsystem handSubsystem;
        TextMeshProUGUI hintLabel;
        float nextHintRefresh;
        int sharedErrorFrames;
        bool sharedSpaceRejected;
        bool started;

        public AvatarKind Current { get; private set; }

        public event Action<AvatarKind> AvatarChanged;

        public Camera XrCamera { get; private set; }

        /// <summary>Unity tracking space (Camera Offset, or the XR Origin root).</summary>
        public Transform TrackingSpace { get; private set; }

        public IBodyTrackingBackend Backend { get; private set; }

        public BodyPoseFrame Frame { get; } = new();

        public bool BodyTrackingValid { get; private set; }

        /// <summary>Body tracking space to world. Valid only while <see cref="TrackingToWorldValid"/>.</summary>
        public Pose TrackingToWorld { get; private set; } = Pose.identity;

        public bool TrackingToWorldValid { get; private set; }

        public Transform BirdRoot => birdRoot;

        public GameObject HumanRoot => humanRoot;

        void Start()
        {
            ResolveXrCamera();
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
            CreateHumanAvatar();
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
            {
                Debug.LogWarning("[AvianVoice] Human avatar not available; staying on the bird.");
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
                Debug.Log("[AvianVoice] Avatar: " + kind);
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
        }

        void CreateHumanAvatar()
        {
            if (string.IsNullOrEmpty(humanAvatarResourcePath))
                return;

            var prefab = Resources.Load<GameObject>(humanAvatarResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[AvianVoice] Human avatar not found in Resources: " + humanAvatarResourcePath);
                return;
            }

            humanRoot = Instantiate(prefab);
            humanRoot.name = "HumanAvatar (" + prefab.name + ")";
            humanRoot.SetActive(false);

            var animator = humanRoot.GetComponent<Animator>();
            if (animator == null)
                animator = humanRoot.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.avatar == null || !animator.avatar.isHuman)
                Debug.LogWarning("[AvianVoice] Human avatar is not imported as Humanoid; body tracking retargeting is disabled.");

            if (humanAvatarOnMirrorLayer)
                SetLayerRecursive(humanRoot, BirdSceneRoles.MirrorLayer);

            humanDriver = humanRoot.AddComponent<HumanFirstPersonDriver>();
            humanDriver.Configure(this, Backend, hideHumanHeadInFirstPerson);
            humanDriver.enabled = false;
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

            var tracking = BodyTrackingValid ? (Frame.IsFullBody ? "full body" : "upper body") : "off";
            if (AvianUiLanguage.IsJapanese)
            {
                var avatar = Current == AvatarKind.Bird ? "鳥" : "人間";
                var trackingJa = BodyTrackingValid ? (Frame.IsFullBody ? "全身" : "上半身") : "なし";
                hintLabel.text = "アバター: " + avatar + "　V キー / 左コントローラ X で切替　　体トラッキング: " + trackingJa;
            }
            else
            {
                hintLabel.text = "Avatar: " + Current + "   V key / left X button to switch   Body tracking: " + tracking;
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
