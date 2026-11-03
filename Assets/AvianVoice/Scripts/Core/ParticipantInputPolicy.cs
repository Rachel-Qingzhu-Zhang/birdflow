using System;
using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// What the participant (person in the headset) may do with controllers. In experiment scenes the
    /// controllers are ignored: scene changes and avatar switches are driven from the PC (keyboard or the
    /// Avian Voice / Experimenter Panel window), and the participant only uses head, hands, body and voice.
    /// </summary>
    public static class ParticipantInputPolicy
    {
        static bool controllersEnabled = true;

        /// <summary>True when XR controller buttons and thumbsticks are honoured.</summary>
        public static bool ControllersEnabled
        {
            get => controllersEnabled;
            set
            {
                if (controllersEnabled == value)
                    return;

                controllersEnabled = value;
                Debug.Log("[AvianVoice] Participant controllers " + (value ? "enabled" : "disabled") + ".");
                Changed?.Invoke(value);
            }
        }

        public static event Action<bool> Changed;

        static bool handsVisible = true;

        /// <summary>
        /// True when the XR rig draws the tracked hands (hand mesh, joints, interactor rays). Tracking itself
        /// stays on either way; the embodiment scene hides them because the avatar body shows the hands.
        /// </summary>
        public static bool HandsVisible
        {
            get => handsVisible;
            set
            {
                if (handsVisible == value)
                    return;

                handsVisible = value;
                Debug.Log("[AvianVoice] Participant hand visuals " + (value ? "shown" : "hidden") + ".");
                HandsVisibleChanged?.Invoke(value);
            }
        }

        public static event Action<bool> HandsVisibleChanged;

        /// <summary>Scene start resets to the defaults unless a scene component sets them.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            controllersEnabled = true;
            handsVisible = true;
            Changed = null;
            HandsVisibleChanged = null;
        }
    }
}
