#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;
using UnityEngine.XR;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Avatar switch input: keyboard V, or the primary face button of the left controller (X on Touch).
    /// </summary>
    public static class AvatarSwitchInput
    {
        static bool leftPrimaryHeld;

        public static bool WasTogglePressedThisFrame()
        {
            if (WasKeyboardTogglePressed())
                return true;

            var device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var pressed = false;
            if (device.isValid)
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out pressed);

            var edge = pressed && !leftPrimaryHeld;
            leftPrimaryHeld = pressed;
            return edge;
        }

        static bool WasKeyboardTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.vKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(KeyCode.V);
        }
    }
}
