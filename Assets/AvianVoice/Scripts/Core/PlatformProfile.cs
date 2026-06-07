using UnityEngine;

namespace AvianVoice.Core
{
    [CreateAssetMenu(fileName = "PlatformProfile", menuName = "Avian Voice/Platform Profile")]
    public class PlatformProfile : ScriptableObject
    {
        public enum RuntimePlatform
        {
            Auto,
            Desktop,
            Quest
        }

        [SerializeField]
        RuntimePlatform target = RuntimePlatform.Auto;

        [Header("Desktop")]
        public bool useSimulatedHands = true;
        [Tooltip("WASD + right-mouse look on the XR rig when not wearing a headset.")]
        public bool enableDesktopFlyCamera = true;
        public int desktopDspBufferSize = 256;

        [Header("Quest")]
        public int questDspBufferSize = 512;
        public bool requestMicrophonePermission = true;

        public bool IsQuest
        {
            get
            {
                if (target == RuntimePlatform.Quest)
                    return true;
                if (target == RuntimePlatform.Desktop)
                    return false;
#if UNITY_ANDROID
                return true;
#else
                return false;
#endif
            }
        }

        public int DspBufferSize => IsQuest ? questDspBufferSize : desktopDspBufferSize;

        public bool UseSimulatedHands => !IsQuest && useSimulatedHands;
    }
}
