using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Physiology;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    /// <summary>
    /// Binds runtime-created sliders to voice/breath data when TrainingHUD fields are not assigned.
    /// </summary>
    public class TrainingHUDRuntimeBars : MonoBehaviour
    {
        Slider breathBar;
        Slider loudnessBar;
        Slider pitchBar;
        Text pitchText;
        Text latencyText;

        void Start()
        {
            var spacing = AvianHudLayout.MetricsBarSpacing;
            breathBar = CreateBar("Breath", spacing * 1.4f, Color.green);
            loudnessBar = CreateBar("Loudness", spacing * 0.7f, Color.yellow);
            pitchBar = CreateBar("Pitch", 0f, Color.cyan);
            pitchText = CreateLabel("PitchText", -spacing * 0.55f, "-- Hz");
            latencyText = CreateLabel("LatencyText", -spacing * 1.05f, "-- ms");
            CreateLabel("Hint", spacing * 2f, "1-6=Task | T=Voice | C=Cheat");
        }

        void Update()
        {
            var analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            var breath = FindAnyObjectByType<BreathSimulator>();
            if (analyzer == null || breath == null)
                return;

            var v = analyzer.Current;
            var b = breath.Current;
            if (breathBar != null) breathBar.value = b.depth;
            if (loudnessBar != null) loudnessBar.value = v.loudness;
            if (pitchBar != null && v.pitchHz > 0f)
                pitchBar.value = Mathf.InverseLerp(80f, 800f, v.pitchHz);
            if (pitchText != null)
                pitchText.text = v.pitchHz > 0f ? $"{v.pitchHz:0} Hz" : "-- Hz";
            if (latencyText != null)
                latencyText.text = $"~{v.latencyMs:0} ms";
        }

        Slider CreateBar(string name, float y, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = AvianHudLayout.MetricsBarSize;
            var slider = go.AddComponent<Slider>();
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = color;
            slider.fillRect = fillRect;
            slider.targetGraphic = bg;
            return slider;
        }

        Text CreateLabel(string name, float y, string text)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = AvianHudLayout.MetricsLabelSize;
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = AvianHudLayout.MetricsLabelFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = text;
            return label;
        }
    }
}
