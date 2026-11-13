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
        public static readonly Vector3 WishPanelLocalPosition = new(0.38f, -0.04f, 0.78f);
        public static readonly Vector2 WishChipCanvasSize = new(168f, 48f);
        public static readonly float WishChipWorldScale = 0.0009f;
        public static readonly Vector3 WishChipLocalPosition = new(0.42f, 0.02f, 0.72f);

        // Room for three lines: the scene title, the instruction in Japanese and in English.
        public static readonly Vector2 GuideCanvasSize = new(460f, 96f);
        public static readonly float GuideWorldScale = 0.0009f;
        public static readonly Vector3 GuideLocalPosition = new(-0.34f, 0.19f, 0.78f);

        // The guide card fixed in the scene at the top of the mirror (SceneGuide, every scene that shows the mirror). Its
        // world scale is this value times the distance between the participant's standing place (the XR rig origin of
        // the scene) and the mirror, so the text has the same apparent size wherever the mirror stands: 17-unit text =
        // 22 mm per metre, about 1.3 degrees (it was 1.1 degrees on the head-locked card); the card is about 34 x 7 degrees.
        public static readonly float GuideMirrorScalePerMeter = 0.0013f;
        public static readonly float GuideMirrorMinDistance = 1f;
        public static readonly float GuideMirrorMaxDistance = 4f;
        // Free space between the top of the mirror frame and the bottom edge of the card (metres).
        public static readonly float GuideMirrorGap = 0.08f;
        // The card is never higher than this above eye level (its centre, seen from the standing place at
        // GuideNominalEyeHeight). The mirror of Avian_Lab_Embodiment is 4.1 m tall and 1.5 m away: above its frame the
        // card would be 60 degrees up. A card that would be higher sits on the upper part of the mirror instead, just
        // in front of the glass.
        public static readonly float GuideMirrorMaxElevationDegrees = 20f;
        // Eye height above the rig origin (the floor) used to place the card. The same for everyone, so the card is at
        // the same place in the scene for every participant and does not depend on where the headset is at that moment.
        public static readonly float GuideNominalEyeHeight = 1.55f;
        // The card leans toward the participant's eyes by at most this much.
        public static readonly float GuideMirrorMaxTiltDegrees = 30f;

        public static readonly Vector2 ReplayChipCanvasSize = new(168f, 48f);
        public static readonly float ReplayChipWorldScale = 0.0009f;
        public static readonly Vector3 ReplayChipLocalPosition = new(-0.34f, 0.08f, 0.74f);
    }
}
