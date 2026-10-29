using System.Collections;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Flight FPV: player parrot follows the headset in world space (not parented to camera).
    /// Pack fly clip poses the wings; Main/COM pins stop fly-root drift.
    /// </summary>
    [DefaultExecutionOrder(10050)]
    public class ParrotFlightPresentation : MonoBehaviour
    {
        [SerializeField]
        bool worldSpaceFollow = true;

        [SerializeField]
        float followDistanceBehind = 1.15f;

        [SerializeField]
        float followDropBelowEye = 0.32f;

        [SerializeField]
        float followUniformScale = 0.95f;

        [Header("Beak anchor")]
        [SerializeField]
        bool useBeakAnchoredFollow = true;

        [SerializeField]
        float eyeAheadOfBeak = -0.95f;

        [SerializeField]
        float eyeBelowBeak = -0.12f;

        [SerializeField]
        float bodyObstructionPushBack = 0.28f;

        [Header("Self visibility")]
        [Tooltip("Hide the player's bird from the headset view during flight so looking down shows the pond. " +
                 "The pond reflection camera uses its own mask and still renders the bird.")]
        [SerializeField]
        bool hideSelfFromMainCamera = true;

        [Header("Legacy camera-parent mode (off by default)")]
        [SerializeField]
        Vector3 anchorLocalOffset = new(0f, -0.35f, -0.49f);

        [SerializeField]
        Vector3 anchorLocalEuler = Vector3.zero;

        [SerializeField]
        float takeoffDuration = 0.5f;

        [SerializeField]
        float landingDuration = 0.45f;

        Animator animator;
        WorldPlacedBird worldGuard;
        ParrotMainCtrlPin rigPin;
        Transform flightAnchor;
        Coroutine flightRoutine;
        bool flightActive;
        public bool IsFlying => flightActive;
        bool beakAnchorCaptured;
        float savedAnimatorSpeed = 1f;
        Vector3 savedWorldScale = Vector3.one;
        Vector3 rootToBeakLocal;
        Transform beakBone;
        SkinnedMeshRenderer bodyRenderer;
        readonly System.Collections.Generic.List<(Camera cam, int mask)> hiddenCameras = new();

        void Awake()
        {
            animator = GetComponent<Animator>();
            worldGuard = GetComponent<WorldPlacedBird>();
            rigPin = GetComponent<ParrotMainCtrlPin>() ?? gameObject.AddComponent<ParrotMainCtrlPin>();
        }

        public void BeginFlightPresentation()
        {
            if (flightActive)
                return;

            flightActive = true;
            beakAnchorCaptured = false;
            beakBone = null;
            savedWorldScale = transform.localScale;
            bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            rigPin.CaptureRestPose();
            rigPin.SetPinEnabled(true);
            // Role marker spheres sit on the head and fill the FPV FOV during flight.
            RoleMarker.SetMarkersVisibleOnBird(transform, false);
            RoleMarker.SetAllMarkersVisible(false);
            HideSelfFromMainCamera();

            worldGuard?.SaveWorldPose();
            if (!worldSpaceFollow)
                worldGuard?.SetFlightCameraParenting(true);

            TryResolveFlightAnchor();
            DetachFromCameraIfNeeded();

            if (animator != null)
            {
                ParrotEmbodimentWiring.RestorePackAnimatorForFlight(animator);
                animator.speed = 1f;
                savedAnimatorSpeed = 1f;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            if (flightRoutine != null)
                StopCoroutine(flightRoutine);
            flightRoutine = StartCoroutine(FlightEnterRoutine());
        }

        public void EndFlightPresentation()
        {
            if (!flightActive)
                return;

            flightActive = false;
            rigPin.SetPinEnabled(false);
            RoleMarker.SetAllMarkersVisible(true);
            RestoreMainCameraMask();

            if (animator != null)
                animator.speed = savedAnimatorSpeed;

            if (flightRoutine != null)
                StopCoroutine(flightRoutine);

            worldGuard?.SetFlightCameraParenting(false);
            if (transform.parent != null)
                transform.SetParent(null, true);

            transform.localScale = savedWorldScale;
            worldGuard?.RestoreWorldPose();
            flightRoutine = StartCoroutine(FlightExitRoutine());
        }

        void LateUpdate()
        {
            if (!flightActive)
                return;

            if (flightAnchor == null)
                TryResolveFlightAnchor();

            if (flightAnchor == null)
                return;

            if (worldSpaceFollow)
            {
                DetachFromCameraIfNeeded();
                ApplyWorldFollowPose();
                return;
            }

            if (transform.parent != flightAnchor)
            {
                worldGuard?.SetFlightCameraParenting(true);
                transform.SetParent(flightAnchor, false);
            }

            ApplyAnchorLocalPose();
        }

        void OnDisable()
        {
            RestoreMainCameraMask();
        }

        void HideSelfFromMainCamera()
        {
            if (!hideSelfFromMainCamera)
                return;

            RestoreMainCameraMask();
            var cameras = Camera.allCameras;
            for (var i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || cam.targetTexture != null)
                    continue;
                if (cam.name.IndexOf("Reflection", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                hiddenCameras.Add((cam, cam.cullingMask));
                cam.cullingMask &= ~(1 << BirdSceneRoles.MirrorLayer);
                cam.cullingMask |= 1 << BirdSceneRoles.UiLayer;
            }
        }

        void RestoreMainCameraMask()
        {
            for (var i = 0; i < hiddenCameras.Count; i++)
            {
                var snap = hiddenCameras[i];
                if (snap.cam != null)
                    snap.cam.cullingMask = snap.mask;
            }

            hiddenCameras.Clear();
        }

        void TryResolveFlightAnchor()
        {
            if (WorldPlacedBird.TryFindFlightAnchor(out flightAnchor, out _))
                return;

            flightAnchor = null;
        }

        void DetachFromCameraIfNeeded()
        {
            if (!worldSpaceFollow || transform.parent == null)
                return;

            if (transform.parent.name == "Camera Offset" || IsUnderXrRig(transform.parent))
                transform.SetParent(null, true);
        }

        void ApplyWorldFollowPose()
        {
            var view = flightAnchor;
            var forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = view.forward;
            forward.Normalize();

            var eye = view.position;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            if (useBeakAnchoredFollow && TryApplyBeakAnchoredPose(eye, forward, rotation))
            {
                ResolveCameraBodyObstruction(eye, forward);
            }
            else
            {
                var bodyPos = eye - forward * followDistanceBehind + Vector3.down * followDropBelowEye;
                transform.SetPositionAndRotation(bodyPos, rotation);
                ResolveCameraBodyObstruction(eye, forward);
            }

            if (followUniformScale > 0f)
                transform.localScale = savedWorldScale * followUniformScale;
        }

        bool TryApplyBeakAnchoredPose(Vector3 eye, Vector3 forward, Quaternion rotation)
        {
            if (beakBone == null)
                beakBone = FindFlightAnchorBone();

            if (beakBone == null)
                return false;

            if (!beakAnchorCaptured)
            {
                rootToBeakLocal = transform.InverseTransformPoint(beakBone.position);
                beakAnchorCaptured = true;
            }

            var targetBeak = eye - forward * eyeAheadOfBeak + Vector3.up * eyeBelowBeak;
            var rootPos = targetBeak - rotation * rootToBeakLocal;
            transform.SetPositionAndRotation(rootPos, rotation);
            return true;
        }

        static Transform FindFlightAnchorBone(Transform root)
        {
            Transform beak = null;
            Transform head = null;
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "parrot_Beak1_bone":
                        beak = bone;
                        break;
                    case "parrot_Head_bone":
                        head = bone;
                        break;
                }
            }

            return beak != null ? beak : head;
        }

        Transform FindFlightAnchorBone() => FindFlightAnchorBone(transform);

        void ResolveCameraBodyObstruction(Vector3 eye, Vector3 forward)
        {
            // The bird is already hidden from the headset; pushing it back would slide the
            // reflection so looking down only shows the head.
            if (hideSelfFromMainCamera || bodyRenderer == null)
                return;

            bodyRenderer.updateWhenOffscreen = true;
            if (!bodyRenderer.bounds.Contains(eye))
                return;

            transform.position -= forward * bodyObstructionPushBack;
        }

        void ApplyAnchorLocalPose()
        {
            transform.localPosition = anchorLocalOffset;
            transform.localRotation = Quaternion.Euler(anchorLocalEuler);
        }

        IEnumerator FlightEnterRoutine()
        {
            ResetFlightBools();
            if (animator != null)
                animator.speed = savedAnimatorSpeed;

            SetBool("idle", false);
            SetBool("takeoff", true);

            yield return new WaitForSeconds(takeoffDuration);

            SetBool("takeoff", false);
            SetBool("glide", false);
            SetBool("fly", true);

            if (animator != null)
            {
                animator.Play("fly", 0, 0f);
                yield return null;
                // Keep the clip running. Main/COM pins stop root drift; wings can still glide or flap.
                animator.speed = savedAnimatorSpeed > 0.05f ? savedAnimatorSpeed : 1f;
            }

            flightRoutine = null;
        }

        IEnumerator FlightExitRoutine()
        {
            if (animator != null)
                animator.speed = savedAnimatorSpeed;

            SetBool("fly", false);
            SetBool("glide", false);
            SetBool("takeoff", false);
            SetBool("landing", true);

            yield return new WaitForSeconds(landingDuration);

            SetBool("landing", false);
            ParrotEmbodimentWiring.DisablePackIdleAnimation(animator);
            worldGuard?.SaveWorldPose();
            flightRoutine = null;
        }

        public void SetGlide(bool glide)
        {
            if (!flightActive || animator == null)
                return;

            if (animator.speed < 0.05f)
                animator.speed = savedAnimatorSpeed > 0.05f ? savedAnimatorSpeed : 1f;

            SetBool("glide", glide);
            SetBool("fly", !glide);

            var state = glide ? "glide" : "fly";
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName(state))
                animator.Play(state, 0, 0f);
        }

        void ResetFlightBools()
        {
            SetBool("walk", false);
            SetBool("fly", false);
            SetBool("glide", false);
            SetBool("takeoff", false);
            SetBool("landing", false);
            SetBool("flyleft", false);
            SetBool("flyright", false);
            SetBool("turnleft", false);
            SetBool("turnright", false);
            SetBool("dance", false);
            SetBool("eat", false);
            SetBool("jump", false);
        }

        void SetBool(string param, bool value)
        {
            if (animator != null)
                animator.SetBool(param, value);
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
