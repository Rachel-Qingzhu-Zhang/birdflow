using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Shared runtime AudioSource creation for bootstrap and lab setup.
    /// </summary>
    public static class AvianAudioSourceUtility
    {
        public const string TransformSourceName = "BirdTransformAudio";
        public const string AssistiveSourceName = "AssistiveMonitor";

        public static AudioSource FindOrCreate(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null && existing.TryGetComponent<AudioSource>(out var source))
                return source;

            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            var created = go.AddComponent<AudioSource>();
            created.playOnAwake = false;
            created.spatialBlend = 1f;
            created.volume = 0.8f;
            return created;
        }
    }
}
