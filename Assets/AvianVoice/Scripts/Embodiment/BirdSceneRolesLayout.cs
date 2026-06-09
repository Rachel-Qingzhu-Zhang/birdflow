using System.Collections;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Re-wires scene parrot components after the first frame (no repositioning).
    /// </summary>
    public class BirdSceneRolesLayout : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;
            BirdSceneRoles.EnsureSetup();
        }
    }
}
