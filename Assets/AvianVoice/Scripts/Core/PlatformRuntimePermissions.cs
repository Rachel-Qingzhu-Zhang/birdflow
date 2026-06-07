using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Requests microphone permission on Android/Quest builds.
    /// </summary>
    public class PlatformRuntimePermissions : MonoBehaviour
    {
        [SerializeField]
        PlatformProfile profile;

        void Start()
        {
            if (profile == null || !profile.IsQuest || !profile.requestMicrophonePermission)
                return;

#if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            }
#endif
        }
    }
}
