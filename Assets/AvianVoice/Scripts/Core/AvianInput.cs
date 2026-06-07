#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Safe keyboard access when Project Settings use Input System Package only.
    /// </summary>
    public static class AvianInput
    {
        public static bool IsSpaceHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.isPressed)
                return true;

            // Desktop fallback when Keyboard device is not created (common in XR focus).
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;
#endif
            return Input.GetKey(KeyCode.Space);
        }

        public static bool WasTPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.T);
#endif
        }

        public static bool WasDigitPressedThisFrame(int digit)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current == null || digit < 0 || digit > 9)
                return false;
            return digit switch
            {
                0 => Keyboard.current.digit0Key.wasPressedThisFrame,
                1 => Keyboard.current.digit1Key.wasPressedThisFrame,
                2 => Keyboard.current.digit2Key.wasPressedThisFrame,
                3 => Keyboard.current.digit3Key.wasPressedThisFrame,
                4 => Keyboard.current.digit4Key.wasPressedThisFrame,
                5 => Keyboard.current.digit5Key.wasPressedThisFrame,
                6 => Keyboard.current.digit6Key.wasPressedThisFrame,
                7 => Keyboard.current.digit7Key.wasPressedThisFrame,
                8 => Keyboard.current.digit8Key.wasPressedThisFrame,
                9 => Keyboard.current.digit9Key.wasPressedThisFrame,
                _ => false
            };
#else
            return digit switch
            {
                0 => Input.GetKeyDown(KeyCode.Alpha0),
                1 => Input.GetKeyDown(KeyCode.Alpha1),
                2 => Input.GetKeyDown(KeyCode.Alpha2),
                3 => Input.GetKeyDown(KeyCode.Alpha3),
                4 => Input.GetKeyDown(KeyCode.Alpha4),
                5 => Input.GetKeyDown(KeyCode.Alpha5),
                6 => Input.GetKeyDown(KeyCode.Alpha6),
                7 => Input.GetKeyDown(KeyCode.Alpha7),
                8 => Input.GetKeyDown(KeyCode.Alpha8),
                9 => Input.GetKeyDown(KeyCode.Alpha9),
                _ => false
            };
#endif
        }

        public static bool WasNextTaskPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.nKey.wasPressedThisFrame || Keyboard.current.rightBracketKey.wasPressedThisFrame)
                    return true;
            }
#endif
            return Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.RightBracket);
        }

        /// <summary>Debug cheat for pitch/song tasks (simulate a perfect vocal match).</summary>
        public static bool WasTaskCheatPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.C);
#endif
        }
    }
}
