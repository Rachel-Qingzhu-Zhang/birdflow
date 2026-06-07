using AvianVoice.Audio;
using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.UI
{
    public class KeyboardModeSwitcher : MonoBehaviour
    {
        void Update()
        {
            if (!AvianInput.WasTPressedThisFrame())
                return;

            var mapper = FindAnyObjectByType<BirdVoiceMapper>();
            if (mapper == null)
                return;

            var next = mapper.Mode == VoiceMapMode.Assistive ? VoiceMapMode.Transform : VoiceMapMode.Assistive;
            mapper.SetMode(next);
        }
    }
}
