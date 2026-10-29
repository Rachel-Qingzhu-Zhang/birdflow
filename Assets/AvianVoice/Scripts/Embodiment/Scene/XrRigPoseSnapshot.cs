using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Saves XR Origin world pose when entering flight and restores it when leaving.
    /// </summary>
    public class XrRigPoseSnapshot : MonoBehaviour
    {
        Vector3 savedPosition;
        Quaternion savedRotation;
        bool hasSavedPose;

        public void SavePose()
        {
            savedPosition = transform.position;
            savedRotation = transform.rotation;
            hasSavedPose = true;
        }

        public void RestorePose()
        {
            if (!hasSavedPose)
                return;

            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            transform.SetPositionAndRotation(savedPosition, savedRotation);

            if (cc != null)
                cc.enabled = true;
        }
    }
}
