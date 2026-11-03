using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Guarantees the embodiment controller exists in the Avian_Lab_Embodiment scene even if the
    /// scene object was removed.
    /// </summary>
    public static class AvatarEmbodimentBootstrap
    {
        public const string SceneName = "Avian_Lab_Embodiment";
        public const string ControllerObjectName = "Avian Embodiment";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            if (SceneManager.GetActiveScene().name != SceneName)
                return;

            if (Object.FindAnyObjectByType<AvatarEmbodimentController>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject(ControllerObjectName);
            go.AddComponent<AvatarEmbodimentController>();
            Debug.Log("[AvianVoice] Added AvatarEmbodimentController at runtime.");
        }
    }
}
