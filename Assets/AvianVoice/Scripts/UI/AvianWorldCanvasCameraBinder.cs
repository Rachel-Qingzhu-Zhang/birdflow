using UnityEngine;

namespace AvianVoice.UI
{
    /// <summary>
    /// World-space canvases need an event camera in some Unity/XR setups or they do not render.
    /// </summary>
    public class AvianWorldCanvasCameraBinder : MonoBehaviour
    {
        void Start()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                return;

            if (canvas.worldCamera != null)
                return;

            var cam = Camera.main;
            if (cam == null)
                cam = FindAnyObjectByType<Camera>();
            if (cam != null)
                canvas.worldCamera = cam;
        }
    }
}
