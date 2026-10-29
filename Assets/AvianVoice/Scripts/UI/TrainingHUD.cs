using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Physiology;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    public class TrainingHUD : MonoBehaviour
    {
        [SerializeField]
        Slider breathBar;

        [SerializeField]
        Slider loudnessBar;

        [SerializeField]
        Slider pitchBar;

        [SerializeField]
        Text pitchText;

        [SerializeField]
        Text latencyText;

        [SerializeField]
        Text breathHintText;

        AvianSignals signals;
        VoiceAnalyzer analyzer;
        BreathSimulator breath;

        public void Initialize(AvianSignals avianSignals, VoiceAnalyzer voiceAnalyzer, BreathSimulator breathSimulator)
        {
            signals = avianSignals;
            analyzer = voiceAnalyzer;
            breath = breathSimulator;

            if (breathHintText != null)
                breathHintText.text = "Breathe along with your bird";
        }

        void Update()
        {
            if (analyzer == null || breath == null)
                return;

            var v = analyzer.Current;
            var b = breath.Current;

            if (breathBar != null)
                breathBar.value = b.depth;
            if (loudnessBar != null)
                loudnessBar.value = v.loudness;
            if (pitchBar != null && v.pitchHz > 0f)
                pitchBar.value = Mathf.InverseLerp(80f, 800f, v.pitchHz);

            if (pitchText != null)
                pitchText.text = v.pitchHz > 0f ? $"{v.pitchHz:0} Hz" : "-- Hz";
            if (latencyText != null)
                latencyText.text = $"~{v.latencyMs:0} ms";
        }
    }
}
