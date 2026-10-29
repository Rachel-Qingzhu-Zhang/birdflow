using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Keeps a world-space parrot visible: ground align, default layer, no CharacterController sink.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class WorldPlacedBird : MonoBehaviour
    {
        [SerializeField]
        bool logIfReparentBlocked = true;

        [SerializeField]
        bool blockReparentUnderXrRig = true;

        [SerializeField]
        bool allowFlightCameraParenting;

        [SerializeField]
        bool alignToGroundOnPlay = true;

        [SerializeField]
        float groundOffset = 0.08f;

        [SerializeField]
        float minVisibleExtentMeters = 0.2f;

        [SerializeField]
        bool repositionInFrontOfXrOnPlay = true;

        [SerializeField]
        float viewDistanceMeters = 2.2f;

        [SerializeField]
        float viewHeightOffsetMeters = -0.15f;

        bool keepAnimatorDisabled;
        bool fixedWorldPlacement;

        Vector3 savedWorldPosition;
        Quaternion savedWorldRotation;
        Vector3 savedWorldScale;
        bool poseSaved;

        void Awake()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("Avian") && !scene.StartsWith("Task_"))
                return;

            if (name is "Parrot" or BirdSceneRoles.FriendName or BirdSceneRoles.PlayerName)
                ConfigureForFixedWorldPlacement();
        }

        void Start()
        {
            if (fixedWorldPlacement)
                return;
            EnsureVisibleAndGrounded();
        }

        public void SetFlightCameraParenting(bool enabled)
        {
            allowFlightCameraParenting = enabled;
        }

        void OnTransformParentChanged()
        {
            if (!blockReparentUnderXrRig || !poseSaved)
                return;

            if (allowFlightCameraParenting)
                return;

            if (transform.parent == null)
                return;

            if (!IsUnderXrRig(transform.parent))
                return;

            if (logIfReparentBlocked)
                Debug.LogWarning("[AvianVoice] Blocked reparenting '" + name + "' under XR rig; restoring world placement.");

            transform.SetParent(null, true);
            RestoreWorldPose();
        }

        public void SaveWorldPose()
        {
            savedWorldPosition = transform.position;
            savedWorldRotation = transform.rotation;
            savedWorldScale = transform.lossyScale;
            poseSaved = true;
        }

        public void RestoreWorldPose()
        {
            if (!poseSaved)
                return;
            transform.SetPositionAndRotation(savedWorldPosition, savedWorldRotation);
            if (transform.parent == null)
                transform.localScale = savedWorldScale;
        }

        public void ConfigureForFixedWorldPlacement()
        {
            fixedWorldPlacement = true;
            repositionInFrontOfXrOnPlay = false;
            alignToGroundOnPlay = false;
            blockReparentUnderXrRig = true;
            allowFlightCameraParenting = false;
            keepAnimatorDisabled = true;
        }

        public void CommitWorldPose()
        {
            SaveWorldPose();
        }

        public void EnsureVisibleAndGrounded()
        {
            if (fixedWorldPlacement || IsLabSceneParrot())
                return;

            gameObject.SetActive(true);
            if (repositionInFrontOfXrOnPlay || alignToGroundOnPlay)
                SetLayerRecursive(gameObject, 0);
            DisableCharacterController();
            EnableAllRenderers();
            FixAnimatorOnRoot();
            AutoScaleIfMeshIsTiny();
            if (repositionInFrontOfXrOnPlay)
                PlaceInFrontOfXrView();
            else if (alignToGroundOnPlay)
                AlignToGround();
            SaveWorldPose();
            LogVisibilityDiagnostic();
        }

        bool IsLabSceneParrot()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("Avian") && !scene.StartsWith("Task_"))
                return false;

            return name is BirdSceneRoles.FriendName or BirdSceneRoles.PlayerName or "Parrot";
        }

        void PlaceInFrontOfXrView()
        {
            if (IsLabSceneParrot())
                return;

            var view = FindMainCameraTransform();
            if (view == null)
                return;

            var forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = view.forward;
            forward.Normalize();

            var pos = view.position + forward * viewDistanceMeters;
            pos.y = view.position.y + viewHeightOffsetMeters;

            if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out var hit, 12f))
                pos.y = hit.point.y + groundOffset;

            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-forward, Vector3.up));
        }

        public static Transform FindMainCameraTransform()
        {
            Camera taggedMain = null;
            Camera namedMain = null;
            Camera bestDepth = null;
            var bestDepthValue = float.NegativeInfinity;

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam == null || !cam.enabled || cam.targetTexture != null)
                    continue;

                if (cam.CompareTag("MainCamera"))
                    taggedMain = cam;
                else if (cam.name.Contains("Main Camera"))
                    namedMain = cam;

                if (cam.depth > bestDepthValue)
                {
                    bestDepthValue = cam.depth;
                    bestDepth = cam;
                }
            }

            if (taggedMain != null)
                return taggedMain.transform;
            if (namedMain != null)
                return namedMain.transform;
            if (bestDepth != null)
                return bestDepth.transform;
            return Camera.main != null ? Camera.main.transform : null;
        }

        public static bool TryFindFlightAnchor(out Transform anchor, out bool isCameraOffset)
        {
            isCameraOffset = false;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root.name.Contains("XR Origin"))
                    continue;

                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Camera Offset")
                        continue;
                    anchor = t;
                    isCameraOffset = true;
                    return true;
                }
            }

            anchor = FindMainCameraTransform();
            if (anchor != null)
                return true;

            anchor = Camera.main != null ? Camera.main.transform : null;
            return anchor != null;
        }

        public static Transform FindXrViewTransform() => FindMainCameraTransform();

        void AlignToGround()
        {
            var origin = transform.position + Vector3.up * 3f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 30f))
                return;

            var p = transform.position;
            p.y = hit.point.y + groundOffset;
            transform.position = p;
        }

        void AutoScaleIfMeshIsTiny()
        {
            if (fixedWorldPlacement)
                return;

            var extent = GetMaxWorldExtent();
            if (extent >= minVisibleExtentMeters)
                return;

            var target = 0.8f;
            var mult = target / Mathf.Max(extent, 0.001f);
            transform.localScale *= mult;
            Debug.LogWarning(
                "[AvianVoice] Parrot mesh bounds were very small (" + extent.ToString("F3") +
                " m). Applied scale multiplier " + mult.ToString("F1") + " on '" + name + "'.");
        }

        float GetMaxWorldExtent()
        {
            var max = 0f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled)
                    continue;
                max = Mathf.Max(max, r.bounds.extents.magnitude);
            }
            return max;
        }

        void EnableAllRenderers()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || ParrotEmbodimentWiring.IsRigControlObject(r.gameObject))
                    continue;
                r.enabled = true;
            }

            ParrotEmbodimentWiring.HideRigControlMeshes(transform);
        }

        void DisableCharacterController()
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
        }

        void FixAnimatorOnRoot()
        {
            var animator = GetComponent<Animator>();
            if (animator == null)
                return;

            animator.applyRootMotion = false;

            if (GetComponent<PigeonFpvSetup>() != null)
            {
                PigeonEmbodimentWiring.DisablePackAnimations(animator);
                return;
            }

            if (keepAnimatorDisabled)
            {
                ParrotEmbodimentWiring.DisablePackIdleAnimation(animator);
                return;
            }

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
        }

        void LogVisibilityDiagnostic()
        {
            var cam = Camera.main;
            var dist = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : -1f;
            var ext = GetMaxWorldExtent();
            Debug.Log(
                "[AvianVoice] Parrot ready at " + transform.position +
                ", size~ " + (ext * 2f).ToString("F2") + " m, distance to main camera " +
                dist.ToString("F1") + " m. Layer=" + gameObject.layer);
        }

        public static Transform FindParrotInScene()
        {
            if (BirdSceneRoles.Player != null)
                return BirdSceneRoles.Player;

            Transform legacy = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == BirdSceneRoles.PlayerName)
                    return t;
                if (t.name == "Parrot")
                    legacy = t;
            }
            return legacy;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
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
    }
}
