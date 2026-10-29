using UnityEngine;

namespace AvianVoice.UI
{
    /// <summary>
    /// World-space canvases need an event camera in some Unity/XR setups or they do not render.
    /// </summary>
    public class AvianWorldCanvasCameraBinder : MonoBehaviour
    {
        void LateUpdate()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;
            if (canvas.worldCamera != cam)
                canvas.worldCamera = cam;
        }
    }
}
