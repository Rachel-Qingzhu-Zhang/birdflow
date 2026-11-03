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

        /// <summary>Scene start resets to the default (controllers allowed) unless a scene component sets it.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            controllersEnabled = true;
            Changed = null;
        }
    }
}
