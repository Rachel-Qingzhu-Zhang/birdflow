using AvianVoice.Audio;
using AvianVoice.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    public class VoiceModeToggle : MonoBehaviour
    {
        [SerializeField]
        Toggle assistiveToggle;

        [SerializeField]
        Text label;

        AvianSignals signals;
        BirdVoiceMapper mapper;

        public void Initialize(AvianSignals avianSignals, BirdVoiceMapper voiceMapper)
        {
            signals = avianSignals;
            mapper = voiceMapper;

            if (assistiveToggle != null)
            {
                assistiveToggle.onValueChanged.AddListener(OnToggle);
                assistiveToggle.isOn = false;
            }

            if (signals != null)
                signals.OnVoiceModeChanged += OnModeChanged;
            OnModeChanged(mapper != null ? mapper.Mode : VoiceMapMode.Transform);
        }

        void OnDestroy()
        {
            if (signals != null)
                signals.OnVoiceModeChanged -= OnModeChanged;
        }

        void OnToggle(bool assistive)
        {
            mapper?.SetMode(assistive ? VoiceMapMode.Assistive : VoiceMapMode.Transform);
        }

        void OnModeChanged(VoiceMapMode mode)
        {
            if (label != null)
                label.text = mode == VoiceMapMode.Assistive ? "Assistive" : "Transform";
        }
    }
}
