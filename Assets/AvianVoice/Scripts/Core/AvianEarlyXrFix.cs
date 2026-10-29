using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Disables duplicate XR rigs before their interactors OnEnable (runs very early in Awake).
    /// Add to a root GameObject in Avian/Task scenes, or rely on scene prefab m_IsActive overrides.
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    public class AvianEarlyXrFix : MonoBehaviour
    {
        float nextHearingSweep;

        void Awake()
        {
            AvianSceneBootstrapHelper.ApplyEarly();
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextHearingSweep)
                return;
            nextHearingSweep = Time.unscaledTime + 0.5f;
            AvianSceneBootstrapHelper.EnsureHearing();
        }
    }
}
