using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Removes the old chest-sphere cue. Breath is shown on the body only,
    /// so a chirp is not mistaken for a note to sing.
    /// </summary>
    [DefaultExecutionOrder(31999)]
    public class FriendTeachingCue : MonoBehaviour
    {
        void Awake()
        {
            StripLegacyChestIndicator(transform);
        }

        static void StripLegacyChestIndicator(Transform birdRoot)
        {
            if (birdRoot == null)
                return;

            foreach (var child in birdRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "FriendBreathCue")
                    Destroy(child.gameObject);
            }
        }
    }
}
