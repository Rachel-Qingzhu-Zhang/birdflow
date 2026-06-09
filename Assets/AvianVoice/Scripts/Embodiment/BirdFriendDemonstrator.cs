using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Independent breathing timeline for the bird friend (not tied to player BreathSimulator).
    /// </summary>
    public class BirdFriendDemonstrator : MonoBehaviour
    {
        public enum BreathPattern
        {
            Intro4x4,
            PatternA4x4,
            PatternB6x6
        }

        [SerializeField]
        BreathPattern pattern = BreathPattern.Intro4x4;

        [SerializeField]
        bool autoRun = true;

        float phaseTime;
        float inhaleDuration = 4f;
        float exhaleDuration = 4f;
        bool inhaling = true;
        bool lastInhaling = true;

        public float CurrentDepth { get; private set; }
        public bool IsInhaling => inhaling;
        public BreathPattern ActivePattern => pattern;
        public float InhaleDuration => inhaleDuration;
        public float ExhaleDuration => exhaleDuration;

        public float SecondsLeftInPhase
        {
            get
            {
                var duration = inhaling ? inhaleDuration : exhaleDuration;
                return Mathf.Max(0f, duration - phaseTime);
            }
        }

        public string FormatTeachingLine(string youAction = null)
        {
            var phase = inhaling ? "INHALE" : "EXHALE";
            var arrow = inhaling ? "↑" : "↓";
            var you = youAction ?? (inhaling ? "hold Space" : "release Space");
            return $"Friend: {phase} {arrow} ({SecondsLeftInPhase:0.0}s)  |  You: {you}";
        }

        public void SetPattern(BreathPattern newPattern)
        {
            pattern = newPattern;
            ApplyPatternDurations();
            phaseTime = 0f;
            inhaling = true;
            lastInhaling = inhaling;
        }

        public void RestartPattern()
        {
            phaseTime = 0f;
            inhaling = true;
            lastInhaling = true;
            CurrentDepth = 0f;
            OnPhaseChanged?.Invoke(inhaling);
        }

        void Awake()
        {
            ApplyPatternDurations();
        }

        void Update()
        {
            if (!autoRun)
                return;

            AdvanceBreath(Time.deltaTime);
        }

        public void AdvanceBreath(float deltaTime)
        {
            var duration = inhaling ? inhaleDuration : exhaleDuration;
            phaseTime += deltaTime;
            var t = Mathf.Clamp01(phaseTime / Mathf.Max(duration, 0.01f));
            CurrentDepth = inhaling ? t : 1f - t;

            if (phaseTime < duration)
                return;

            phaseTime = 0f;
            inhaling = !inhaling;
            if (inhaling != lastInhaling)
            {
                lastInhaling = inhaling;
                OnPhaseChanged?.Invoke(inhaling);
            }
        }

        public event System.Action<bool> OnPhaseChanged;

        public float GetTargetDepth()
        {
            var duration = inhaling ? inhaleDuration : exhaleDuration;
            var t = Mathf.Clamp01(phaseTime / Mathf.Max(duration, 0.01f));
            return inhaling ? t : 1f - t;
        }

        void ApplyPatternDurations()
        {
            switch (pattern)
            {
                case BreathPattern.PatternB6x6:
                    inhaleDuration = 6f;
                    exhaleDuration = 6f;
                    break;
                default:
                    inhaleDuration = 4f;
                    exhaleDuration = 4f;
                    break;
            }
        }
    }
}
