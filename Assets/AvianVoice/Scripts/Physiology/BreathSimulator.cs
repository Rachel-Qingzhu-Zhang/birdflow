using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Physiology
{
    public class BreathSimulator : MonoBehaviour
    {
        public enum Source
        {
            Keyboard,
            Random
        }

        AvianVoiceSettings settings;
        AvianSignals signals;

        [SerializeField]
        Source source = Source.Keyboard;

        float depth;
        bool inhaling;

        public BreathFrame Current { get; private set; }

        public void Initialize(AvianVoiceSettings voiceSettings, AvianSignals avianSignals)
        {
            settings = voiceSettings;
            signals = avianSignals;
        }

        public void SetSource(Source newSource) => source = newSource;

        void Update()
        {
            if (settings == null)
                return;

            if (source == Source.Keyboard)
                inhaling = IsInhalePressed();
            else
                inhaling = Mathf.PerlinNoise(Time.time * 0.2f, 0f) > 0.55f;

            var speed = inhaling ? settings.inhaleSpeed : settings.exhaleSpeed;
            depth = Mathf.MoveTowards(depth, inhaling ? 1f : 0f, speed * Time.deltaTime);

            Current = new BreathFrame
            {
                depth = depth,
                phase = depth,
                isInhaling = inhaling
            };

            signals?.PublishBreath(Current);
        }

        static bool IsInhalePressed() => AvianInput.IsSpaceHeld();
    }
}
