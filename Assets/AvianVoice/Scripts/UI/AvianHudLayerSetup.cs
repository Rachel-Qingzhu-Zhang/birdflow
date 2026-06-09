using AvianVoice.Embodiment;
using UnityEngine;

namespace AvianVoice.UI
{
    /// <summary>
    /// Head-locked HUD canvases must not appear in the mirror reflection camera.
    /// </summary>
    public static class AvianHudLayerSetup
    {
        public static void ApplyToXrRig()
        {
            var xr = GameObject.Find("XR Origin Hands (XR Rig)") ?? GameObject.Find("XR Origin");
            if (xr == null)
                return;

            foreach (var canvas in xr.GetComponentsInChildren<Canvas>(true))
                SetLayerRecursive(canvas.gameObject, BirdSceneRoles.UiLayer);
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
