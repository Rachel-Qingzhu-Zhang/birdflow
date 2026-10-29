using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Inverts face culling while the mirror render camera draws so reflected meshes appear correctly.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MirrorReflectionCamera : MonoBehaviour
    {
        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void OnPreRender()
        {
            GL.invertCulling = true;
        }

        void OnPostRender()
        {
            GL.invertCulling = false;
            if (cam != null)
            {
                cam.ResetProjectionMatrix();
                cam.ResetWorldToCameraMatrix();
            }
        }
    }
}
