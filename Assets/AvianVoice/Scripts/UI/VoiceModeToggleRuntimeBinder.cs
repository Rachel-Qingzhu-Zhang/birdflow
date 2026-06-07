using AvianVoice.Audio;
using AvianVoice.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    public class VoiceModeToggleRuntimeBinder : MonoBehaviour
    {
        VoiceModeToggle voiceToggle;
        Toggle toggle;
        Text label;

        public void Bind(VoiceModeToggle vt, Toggle t, Text l)
        {
            voiceToggle = vt;
            toggle = t;
            label = l;
        }

        void Start()
        {
            var mapper = FindAnyObjectByType<BirdVoiceMapper>();
            var signals = FindAnyObjectByType<AvianSignals>();
            if (voiceToggle != null && mapper != null && signals != null)
            {
                voiceToggle.Initialize(signals, mapper);
                if (toggle != null)
                    toggle.onValueChanged.AddListener(a => mapper.SetMode(a ? VoiceMapMode.Assistive : VoiceMapMode.Transform));
            }
        }
    }
}
