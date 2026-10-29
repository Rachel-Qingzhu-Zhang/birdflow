using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Marks a manually placed AvianWorldMirror. While this component is present,
    /// runtime code will not move the mirror, birds, or perch — the saved scene layout is used as-is.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldMirrorScenePlacement : MonoBehaviour
    {
        public static bool HasManualLayoutInScene()
        {
            foreach (var placement in Object.FindObjectsByType<WorldMirrorScenePlacement>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (placement != null)
                    return true;
            }

            return false;
        }
    }
}
