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
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
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
