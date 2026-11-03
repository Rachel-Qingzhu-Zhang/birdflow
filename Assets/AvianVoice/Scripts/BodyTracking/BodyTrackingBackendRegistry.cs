using System;
using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// Backends register a factory at startup (RuntimeInitializeOnLoad). The embodiment controller asks
    /// here for a backend, so Assembly-CSharp never references the Meta SDK directly.
    /// </summary>
    public static class BodyTrackingBackendRegistry
    {
        static Func<GameObject, IBodyTrackingBackend> factory;
        static string factoryName;

        public static bool HasBackend => factory != null;

        public static string BackendName => factoryName ?? "none";

        public static void Register(string name, Func<GameObject, IBodyTrackingBackend> create)
        {
            if (create == null)
                return;

            factory = create;
            factoryName = name;
            Debug.Log("[AvianVoice] Body tracking backend registered: " + name);
        }

        public static IBodyTrackingBackend Create(GameObject host)
        {
            if (factory == null || host == null)
                return null;

            try
            {
                return factory(host);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AvianVoice] Body tracking backend failed to start: " + e.Message);
                return null;
            }
        }
    }
}
