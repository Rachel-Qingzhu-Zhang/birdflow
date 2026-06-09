using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Place on a scene parrot to mark it as the friend or player bird. Drag both parrots in the scene yourself.
    /// </summary>
    [DisallowMultipleComponent]
    public class BirdSceneRole : MonoBehaviour
    {
        public enum RoleKind
        {
            Friend,
            Player
        }

        [SerializeField]
        RoleKind role = RoleKind.Friend;

        public RoleKind Role => role;

        public void SetRole(RoleKind value)
        {
            role = value;
        }
    }
}
