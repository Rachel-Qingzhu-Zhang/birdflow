using AvianVoice.Core;
using AvianVoice.Embodiment;
using UnityEngine;

namespace AvianVoice.Physiology
{
    /// <summary>
    /// Breath signal for the bird body. There is no breath input: Guided copies the teacher pattern
    /// from BirdFriendDemonstrator (tunable in AvianVoiceSettings, Teacher Breath section).
    /// </summary>
    public class BreathSimulator : MonoBehaviour
    {
        public enum Source
        {
            Guided,
            Random
        }

        AvianVoiceSettings settings;
        AvianSignals signals;
        BirdFriendDemonstrator demonstrator;

        [SerializeField]
        Source source = Source.Guided;

        public BreathFrame Current { get; private set; }

        public void Initialize(AvianVoiceSettings voiceSettings, AvianSignals avianSignals)
        {
            settings = voiceSettings;
            signals = avianSignals;
        }

        public void SetSource(Source newSource) => source = newSource;

        void Update()
        {
            float depth;
            bool inhaling;

            if (source == Source.Guided)
            {
                if (demonstrator == null)
                    demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
                if (demonstrator == null)
                    return;

                depth = demonstrator.CurrentDepth;
                inhaling = demonstrator.IsInhaling;
            }
            else
            {
                var noise = Mathf.PerlinNoise(Time.time * 0.2f, 0f);
                depth = noise;
                inhaling = noise > 0.55f;
            }

            Current = new BreathFrame
            {
                depth = depth,
                phase = depth,
                isInhaling = inhaling
            };

            signals?.PublishBreath(Current);
        }
    }
}
