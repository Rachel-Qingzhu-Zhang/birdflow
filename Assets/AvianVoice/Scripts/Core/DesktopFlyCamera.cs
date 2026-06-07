#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif
using UnityEngine;
using UnityEngine.XR;

namespace AvianVoice.Core
{
    /// <summary>
    /// Desktop/editor fly camera: hold right mouse to look, WASD to move, Q/E for up/down.
    /// Disabled on Quest and when an XR headset is actively driving the HMD.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class DesktopFlyCamera : MonoBehaviour
    {
        [SerializeField]
        Transform xrOriginRoot;

        [SerializeField]
        Transform cameraOffset;

        [SerializeField]
        Transform viewCamera;

        [SerializeField]
        float moveSpeed = 4f;

        [SerializeField]
        float sprintMultiplier = 2.5f;

        [SerializeField]
        float lookSensitivity = 0.15f;

        [SerializeField]
        float pitchMin = -85f;

        [SerializeField]
        float pitchMax = 85f;

        [SerializeField]
        bool requireRightMouseToLook = true;

        [SerializeField]
        bool lockCursorWhileLooking = true;

        float yaw;
        float pitch;
        bool active;
#if ENABLE_INPUT_SYSTEM
        TrackedPoseDriver[] trackedPoseDrivers;
#endif

        public static DesktopFlyCamera Ensure(
            GameObject xrOrigin,
            Transform headAnchor,
            PlatformProfile platformProfile)
        {
            if (xrOrigin == null || headAnchor == null)
                return null;

            if (platformProfile != null && platformProfile.IsQuest)
                return null;

            if (!Application.isEditor && Application.platform == RuntimePlatform.Android)
                return null;

            if (platformProfile != null && !platformProfile.enableDesktopFlyCamera)
                return null;

            var fly = xrOrigin.GetComponent<DesktopFlyCamera>();
            if (fly == null)
                fly = xrOrigin.AddComponent<DesktopFlyCamera>();

            var mainCam = headAnchor.GetComponentInChildren<Camera>(true);
            fly.Configure(xrOrigin.transform, headAnchor, mainCam != null ? mainCam.transform : headAnchor);
            return fly;
        }

        public void Configure(Transform originRoot, Transform head, Transform camera)
        {
            xrOriginRoot = originRoot;
            cameraOffset = head;
            viewCamera = camera != null ? camera : head;
        }

        void OnEnable()
        {
            active = ShouldRun();
            if (!active)
                return;

            CacheAngles();
            DisableHeadTracking();
        }

        void OnDisable()
        {
            RestoreCursor();
            EnableHeadTracking();
        }

        void Update()
        {
            if (!active)
                return;

            // In Editor, keep fly camera for desktop preview even if XR Simulation is enabled.
            if (!Application.isEditor && XRSettings.enabled && XRSettings.isDeviceActive)
            {
                active = false;
                EnableHeadTracking();
                RestoreCursor();
                return;
            }

            HandleLook();
            HandleMove();
        }

        bool ShouldRun()
        {
#if UNITY_ANDROID
            return false;
#else
            return Application.isEditor || Application.isPlaying;
#endif
        }

        void CacheAngles()
        {
            if (cameraOffset == null)
                return;

            yaw = cameraOffset.eulerAngles.y;
            pitch = viewCamera != null ? viewCamera.localEulerAngles.x : 0f;
            if (pitch > 180f)
                pitch -= 360f;
        }

        void HandleLook()
        {
#if !ENABLE_INPUT_SYSTEM
            return;
#else
            var mouse = Mouse.current;
            if (mouse == null || cameraOffset == null)
                return;

            var lookActive = !requireRightMouseToLook || mouse.rightButton.isPressed;
            if (!lookActive)
            {
                if (lockCursorWhileLooking)
                    RestoreCursor();
                return;
            }

            if (lockCursorWhileLooking)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            var delta = mouse.delta.ReadValue() * lookSensitivity;
            yaw += delta.x;
            pitch -= delta.y;
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            cameraOffset.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (viewCamera != null)
                viewCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
#endif
        }

        void HandleMove()
        {
#if !ENABLE_INPUT_SYSTEM
            return;
#else
            var keyboard = Keyboard.current;
            if (keyboard == null || xrOriginRoot == null || cameraOffset == null)
                return;

            var forward = cameraOffset.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = xrOriginRoot.forward;
            forward.Normalize();

            var right = cameraOffset.right;
            right.y = 0f;
            right.Normalize();

            var move = Vector3.zero;
            if (keyboard.wKey.isPressed)
                move += forward;
            if (keyboard.sKey.isPressed)
                move -= forward;
            if (keyboard.dKey.isPressed)
                move += right;
            if (keyboard.aKey.isPressed)
                move -= right;
            if (keyboard.eKey.isPressed)
                move += Vector3.up;
            if (keyboard.qKey.isPressed)
                move -= Vector3.up;

            if (move.sqrMagnitude < 0.0001f)
                return;

            var speed = moveSpeed;
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                speed *= sprintMultiplier;

            xrOriginRoot.position += move.normalized * (speed * Time.deltaTime);
#endif
        }

        void DisableHeadTracking()
        {
#if ENABLE_INPUT_SYSTEM
            if (viewCamera == null)
                return;

            trackedPoseDrivers = viewCamera.GetComponents<TrackedPoseDriver>();
            foreach (var driver in trackedPoseDrivers)
                driver.enabled = false;
#endif
        }

        void EnableHeadTracking()
        {
#if ENABLE_INPUT_SYSTEM
            if (trackedPoseDrivers == null)
                return;

            foreach (var driver in trackedPoseDrivers)
            {
                if (driver != null)
                    driver.enabled = true;
            }
#endif
        }

        static void RestoreCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
