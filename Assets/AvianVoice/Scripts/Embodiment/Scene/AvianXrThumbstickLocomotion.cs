using UnityEngine;
using UnityEngine.XR;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// VR thumbstick yaw on the XR Origin (snap or continuous). Flight and walk tasks use Camera Offset forward.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class AvianXrThumbstickLocomotion : MonoBehaviour
    {
        [SerializeField]
        float continuousTurnSpeed = 90f;

        [SerializeField]
        float snapTurnDegrees = 45f;

        [SerializeField]
        float snapInputThreshold = 0.65f;

        [SerializeField]
        float deadzone = 0.18f;

        [SerializeField]
        bool useSnapTurn = true;

        [SerializeField]
        float flightTurnMultiplier = 1.35f;

        Transform cameraOffset;
        bool snapReady = true;

        void Awake()
        {
            cameraOffset = FindCameraOffset(transform);
        }

        void Update()
        {
            if (!IsXrActive())
                return;

            if (!TryReadThumbstickX(out var stickX))
            {
                snapReady = true;
                return;
            }

            if (Mathf.Abs(stickX) < deadzone)
            {
                snapReady = true;
                return;
            }

            var turnScale = IsFlightTaskActive() ? flightTurnMultiplier : 1f;
            if (useSnapTurn)
            {
                if (!snapReady)
                    return;

                RotateRig(Mathf.Sign(stickX) * snapTurnDegrees * turnScale);
                snapReady = false;
                return;
            }

            RotateRig(stickX * continuousTurnSpeed * turnScale * Time.deltaTime);
        }

        void RotateRig(float yawDegrees)
        {
            if (Mathf.Abs(yawDegrees) < 0.001f)
                return;

            var pivot = cameraOffset != null
                ? cameraOffset.position
                : transform.position + Vector3.up * 1.6f;
            transform.RotateAround(pivot, Vector3.up, yawDegrees);
        }

        static bool IsFlightTaskActive()
        {
            return Tasks.TaskActivity.IsActive("flying") || Tasks.TaskActivity.IsActive("flight");
        }

        static bool IsXrActive()
        {
            return XRSettings.enabled && XRSettings.isDeviceActive;
        }

        static bool TryReadThumbstickX(out float x)
        {
            x = 0f;
            if (TryReadNode(XRNode.RightHand, out x))
                return true;
            return TryReadNode(XRNode.LeftHand, out x);
        }

        static bool TryReadNode(XRNode node, out float x)
        {
            x = 0f;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return false;

            if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var stick))
                return false;

            x = stick.x;
            return Mathf.Abs(x) > 0.0001f;
        }

        static Transform FindCameraOffset(Transform root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Camera Offset")
                    return child;
            }

            return null;
        }
    }
}
