using UnityEngine;

namespace AvianVoice.UI
{
    /// <summary>
    /// Shared world-space HUD sizing for runtime-created canvases.
    /// </summary>
    public static class AvianHudLayout
    {
        public static readonly Vector2 ModePanelCanvasSize = new(200f, 88f);
        public static readonly Vector2 MetricsPanelCanvasSize = new(260f, 180f);
        public static readonly float ModePanelWorldScale = 0.0009f;
        public static readonly float MetricsPanelWorldScale = 0.001f;
        public static readonly Vector3 ModePanelLocalPosition = new(-0.32f, 0.02f, 0.55f);
        public static readonly Vector3 MetricsPanelLocalPosition = new(0.32f, -0.08f, 0.55f);

        public static readonly Vector2 ModeLabelSize = new(170f, 28f);
        public static readonly int ModeLabelFontSize = 15;

        public static readonly Vector2 MetricsBarSize = new(200f, 14f);
        public static readonly Vector2 MetricsLabelSize = new(210f, 22f);
        public static readonly int MetricsLabelFontSize = 14;
        public static readonly float MetricsBarSpacing = 52f;

        public static readonly Vector2 TaskPanelCanvasSize = new(240f, 120f);
        public static readonly float TaskPanelWorldScale = 0.0009f;
        public static readonly Vector3 TaskPanelLocalPosition = new(0f, 0.18f, 0.55f);
        public static readonly Vector2 TaskTitleSize = new(220f, 26f);
        public static readonly Vector2 TaskInstructionSize = new(220f, 40f);
        public static readonly Vector2 TaskScoreSize = new(220f, 22f);
        public static readonly int TaskTitleFontSize = 14;
        public static readonly int TaskBodyFontSize = 12;
    }
}
