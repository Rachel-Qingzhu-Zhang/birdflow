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
        public static bool WasTPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                return Keyboard.current.tKey.wasPressedThisFrame;
#endif
            return Input.GetKeyDown(KeyCode.T);
        }

        public static bool WasDigitPressedThisFrame(int digit)
        {
            if (digit < 0 || digit > 9)
                return false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
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
            }
#endif
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
        }

        /// <summary>Open or close the wish / scene menu (M).</summary>
        public static bool WasWishMenuPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
                return true;
#endif
            return Input.GetKeyDown(KeyCode.M);
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

        /// <summary>Debug cheat (C). Checks every keyboard and the legacy manager so one backend cannot hide the press.</summary>
        public static bool WasTaskCheatPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var device in InputSystem.devices)
            {
                if (device is Keyboard keyboard && keyboard.cKey.wasPressedThisFrame)
                    return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.C))
                return true;
#endif
            return false;
        }

        /// <summary>
        /// Flight altitude: +1 up (E / UpArrow / R), -1 down (Q / DownArrow / F).
        /// </summary>
        public static float GetFlightAltitudeAxis()
        {
            var axis = 0f;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.eKey.isPressed || keyboard.upArrowKey.isPressed || keyboard.rKey.isPressed)
                    axis += 1f;
                if (keyboard.qKey.isPressed || keyboard.downArrowKey.isPressed || keyboard.fKey.isPressed)
                    axis -= 1f;
                return Mathf.Clamp(axis, -1f, 1f);
            }
#endif
            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.R))
                axis += 1f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.F))
                axis -= 1f;
            return Mathf.Clamp(axis, -1f, 1f);
        }
    }
}
