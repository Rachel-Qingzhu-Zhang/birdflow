using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    public enum PigeonPlacementMode
    {
        WorldFixed,
        FollowCamera
    }

    /// <summary>
    /// Wires Pigeon_PBR to AvianAppBootstrap and configures placement / breath-friendly pose.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class PigeonFpvSetup : MonoBehaviour
    {
        [SerializeField]
        PigeonPlacementMode placement = PigeonPlacementMode.WorldFixed;

        [SerializeField]
        bool reparentUnderCameraOffset = false;

        [SerializeField]
        Vector3 fpvLocalPosition = new(0f, -0.2f, 0.25f);

        [SerializeField]
        Vector3 fpvLocalEuler = new(0f, 180f, 0f);

        [SerializeField]
        float fpvUniformScale = 1f;

        [Header("Pack animator")]
        [SerializeField]
        bool disablePackAnimator = true;

        [Header("Breath wing tuning (WingHandMapper)")]
        [SerializeField]
        float wingBreathSpreadScale = 0.28f;

        [SerializeField]
        float wingBreathPitchScale = 0.35f;

        [SerializeField]
        bool setBirdMirrorLayer = true;

        [SerializeField]
        int birdMirrorLayer = 9;

        [SerializeField]
        bool disableWorldParrotOnAwake = true;

        [SerializeField]
        bool disableCrowFpvOnAwake = true;

        bool wired;

        public PigeonPlacementMode PlacementMode => placement;

        void Awake()
        {
            if (!PigeonEmbodimentWiring.TryResolve(transform, out var refs))
            {
                Debug.LogWarning("[AvianVoice] PigeonFpvSetup could not resolve bones on '" + name + "'.");
                return;
            }

            PigeonEmbodimentWiring.ApplyAnimatorDefaults(refs.animator, disablePackAnimator);
            EnsureCompanionComponents();
            ApplyPlacementMode();

            if (setBirdMirrorLayer)
                SetLayerRecursive(gameObject, birdMirrorLayer);

            if (disableWorldParrotOnAwake)
                DisableSceneBirdNamed("Parrot");
            if (disableCrowFpvOnAwake)
                DisableCrowUnderCameraOffset();

            WireBootstrap(refs);
        }

        void Start()
        {
            RefreshBreathDrivers();
        }

        void EnsureCompanionComponents()
        {
            if (GetComponent<PigeonBreathEmbodiment>() == null)
                gameObject.AddComponent<PigeonBreathEmbodiment>();

            var visuals = GetComponent<ParrotMatchVisuals>();
            if (visuals == null)
                visuals = gameObject.AddComponent<ParrotMatchVisuals>();
            visuals.BindFromScene();
        }

        void ApplyPlacementMode()
        {
            if (placement == PigeonPlacementMode.FollowCamera && reparentUnderCameraOffset)
            {
                ReparentUnderCameraOffset();
                return;
            }

            DetachFromXrRigIfNeeded();
            var worldBird = GetComponent<WorldPlacedBird>() ?? gameObject.AddComponent<WorldPlacedBird>();
            worldBird.ConfigureForFixedWorldPlacement();
            worldBird.SaveWorldPose();
        }

        void DetachFromXrRigIfNeeded()
        {
            if (transform.parent == null || !IsUnderXrRig(transform.parent))
                return;

            transform.SetParent(null, true);
        }

        void RefreshBreathDrivers()
        {
            if (disablePackAnimator)
                PigeonEmbodimentWiring.DisablePackAnimations(GetComponent<Animator>());

            var breath = GetComponent<PigeonBreathEmbodiment>();
            breath?.CacheRestPose();

            var wings = FindAnyObjectByType<WingHandMapper>();
            if (wings != null)
            {
                wings.SetBreathWingScales(wingBreathSpreadScale, wingBreathPitchScale);
                wings.RefreshRestPose();
            }
        }

        void LateUpdate()
        {
            if (!disablePackAnimator)
                return;

            var animator = GetComponent<Animator>();
            if (animator == null)
                return;

            if (animator.enabled || animator.runtimeAnimatorController != null)
                PigeonEmbodimentWiring.DisablePackAnimations(animator);
        }

        void WireBootstrap(PigeonEmbodimentWiring.RigRefs refs)
        {
            if (wired)
                return;

            var bootstrap = FindAnyObjectByType<AvianAppBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[AvianVoice] PigeonFpvSetup: AvianAppBootstrap not found in scene.");
                return;
            }

            bootstrap.ApplyEmbodimentBones(
                refs.leftWing,
                refs.rightWing,
                refs.chest,
                refs.secondaryChest,
                refs.head,
                refs.skinMesh,
                refs.animator,
                -1);
            bootstrap.SetAutoWireParrotEmbodiment(false);
            bootstrap.RegisterMirrorBird(transform);

            wired = true;
            Debug.Log("[AvianVoice] Pigeon wired to AvianAppBootstrap on '" + bootstrap.name + "'.");
        }

        void ReparentUnderCameraOffset()
        {
            if (!WorldPlacedBird.TryFindFlightAnchor(out var anchor, out _) || anchor == null)
                return;

            if (transform.parent != anchor)
                transform.SetParent(anchor, false);

            transform.localPosition = fpvLocalPosition;
            transform.localRotation = Quaternion.Euler(fpvLocalEuler);
            if (fpvUniformScale > 0f)
                transform.localScale = Vector3.one * fpvUniformScale;
        }

        static void DisableSceneBirdNamed(string birdName)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != birdName)
                    continue;
                if (t.GetComponentInParent<PigeonFpvSetup>() != null)
                    continue;
                t.gameObject.SetActive(false);
            }
        }

        static void DisableCrowUnderCameraOffset()
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "Crow")
                    continue;
                if (t.parent == null || t.parent.name != "Camera Offset")
                    continue;
                t.gameObject.SetActive(false);
            }
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            if (layer < 0 || layer > 31)
                return;
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        static bool IsUnderXrRig(Transform t)
        {
            while (t != null)
            {
                if (t.name.Contains("XR Origin"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        public static Transform FindPigeonInScene()
        {
            foreach (var setup in FindObjectsByType<PigeonFpvSetup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (setup != null && setup.gameObject.activeInHierarchy)
                    return setup.transform;
            }

            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PigeonEmbodimentWiring.IsPigeonRoot(t))
                    return t;
            }

            return null;
        }
    }
}
