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

        public static readonly Vector2 TaskPanelCanvasSize = new(320f, 176f);
        public static readonly float TaskPanelWorldScale = 0.0009f;
        public static readonly Vector3 TaskPanelLocalPosition = new(0f, 0.24f, 0.55f);
        public static readonly Vector2 TaskTitleSize = new(300f, 24f);
        public static readonly Vector2 TaskInstructionSize = new(300f, 56f);
        public static readonly Vector2 TaskScoreSize = new(300f, 64f);
        public static readonly int TaskTitleFontSize = 14;
        public static readonly int TaskBodyFontSize = 12;

        public static readonly Vector2 WishPanelCanvasSize = new(380f, 390f);
        public static readonly float WishPanelWorldScale = 0.0009f;
        public static readonly Vector3 WishPanelLocalPosition = new(-0.22f, -0.02f, 0.72f);
        public static readonly Vector2 WishChipCanvasSize = new(168f, 48f);
        public static readonly float WishChipWorldScale = 0.0009f;
        public static readonly Vector3 WishChipLocalPosition = new(-0.30f, -0.18f, 0.58f);

        public static readonly Vector2 GuideCanvasSize = new(460f, 96f);
        public static readonly float GuideWorldScale = 0.0009f;
        public static readonly Vector3 GuideLocalPosition = new(0f, 0.16f, 0.70f);
    }
}
