using UnityEngine;

namespace AvianVoice.UI
{
    /// <summary>
    /// Shows or hides runtime HUD canvases on the XR rig (metrics, task flow, voice mode).
    /// </summary>
    public static class AvianHudVisibility
    {
        public static readonly string[] HudCanvasNames =
        {
            "AvianHUD",
            "TaskModeHUD",
            "ModeToggle"
        };

        public static void SetAllVisible(bool visible)
        {
            var xr = GameObject.Find("XR Origin Hands (XR Rig)") ?? GameObject.Find("XR Origin");
            if (xr == null)
                return;

            foreach (var canvas in xr.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas == null || !IsHudCanvas(canvas.gameObject))
                    continue;
                canvas.gameObject.SetActive(visible);
            }
        }

        public static bool IsHudCanvas(GameObject root)
        {
            foreach (var name in HudCanvasNames)
            {
                if (root.name == name)
                    return true;
            }

            return false;
        }
    }
}
