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
        float guidedInhaleSeconds = -1f;
        float guidedExhaleSeconds = -1f;

        public BreathFrame Current { get; private set; }

        public void Initialize(AvianVoiceSettings voiceSettings, AvianSignals avianSignals)
        {
            settings = voiceSettings;
            signals = avianSignals;
        }

        public void SetSource(Source newSource) => source = newSource;

        /// <summary>
        /// Match a demonstrated inhale/exhale length so holding Space tracks the friend bird.
        /// Pass seconds for a full 0→1 or 1→0 travel. Clear to restore settings speeds.
        /// </summary>
        public void SetGuidedCycle(float inhaleSeconds, float exhaleSeconds)
        {
            guidedInhaleSeconds = Mathf.Max(0.35f, inhaleSeconds);
            guidedExhaleSeconds = Mathf.Max(0.35f, exhaleSeconds);
        }

        public void ClearGuidedCycle()
        {
            guidedInhaleSeconds = -1f;
            guidedExhaleSeconds = -1f;
        }

        public void ResetDepth()
        {
            depth = 0f;
            inhaling = false;
            Current = new BreathFrame
            {
                depth = 0f,
                phase = 0f,
                isInhaling = false
            };
            signals?.PublishBreath(Current);
        }

        void Update()
        {
            if (settings == null)
                return;

            if (source == Source.Keyboard)
                inhaling = IsInhalePressed();
            else
                inhaling = Mathf.PerlinNoise(Time.time * 0.2f, 0f) > 0.55f;

            var speed = ResolveBreathSpeed(inhaling);
            depth = Mathf.MoveTowards(depth, inhaling ? 1f : 0f, speed * Time.deltaTime);

            Current = new BreathFrame
            {
                depth = depth,
                phase = depth,
                isInhaling = inhaling
            };

            signals?.PublishBreath(Current);
        }

        float ResolveBreathSpeed(bool inhale)
        {
            if (inhale && guidedInhaleSeconds > 0f)
                return 1f / guidedInhaleSeconds;
            if (!inhale && guidedExhaleSeconds > 0f)
                return 1f / guidedExhaleSeconds;
            return inhale ? settings.inhaleSpeed : settings.exhaleSpeed;
        }

        static bool IsInhalePressed() => AvianInput.IsSpaceHeld();
    }
}
