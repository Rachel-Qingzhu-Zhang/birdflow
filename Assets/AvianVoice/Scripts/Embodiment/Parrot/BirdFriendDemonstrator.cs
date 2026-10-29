using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Teacher breathing timeline. It drives the bird's breath (via BreathSimulator Guided source).
    /// Durations and depth range come from the AvianVoiceSettings asset, Teacher Breath section.
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

        [SerializeField]
        AvianVoiceSettings settings;

        float phaseTime;
        float inhaleDuration = 4f;
        float exhaleDuration = 4f;
        bool inhaling = true;
        bool lastInhaling = true;

        public float CurrentDepth { get; private set; }

        bool idleMotion;
        float idleTime;
        public bool IsInhaling => inhaling;
        public BreathPattern ActivePattern => pattern;
        public float InhaleDuration => inhaleDuration;
        public float ExhaleDuration => exhaleDuration;

        /// <summary>Full inhale + exhale cycles finished since the last SetPattern / RestartPattern.</summary>
        public int CompletedCycles { get; private set; }

        /// <summary>0-1 progress through the current cycle.</summary>
        public float CycleProgress
        {
            get
            {
                var total = Mathf.Max(0.01f, inhaleDuration + exhaleDuration);
                var elapsed = inhaling ? phaseTime : inhaleDuration + phaseTime;
                return Mathf.Clamp01(elapsed / total);
            }
        }

        public float SecondsLeftInPhase
        {
            get
            {
                var duration = inhaling ? inhaleDuration : exhaleDuration;
                return Mathf.Max(0f, duration - phaseTime);
            }
        }

        public string FormatTeachingLine()
        {
            var phase = inhaling ? "INHALE" : "EXHALE";
            var arrow = inhaling ? "↑" : "↓";
            return $"Bird: {phase} {arrow} ({SecondsLeftInPhase:0.0}s) — breathe along";
        }

        public void SetTeachingActive(bool active)
        {
            autoRun = active;
            idleMotion = !active;
        }

        public void SetPattern(BreathPattern newPattern)
        {
            autoRun = true;
            idleMotion = false;
            pattern = newPattern;
            ApplyPatternDurations();
            phaseTime = 0f;
            inhaling = true;
            lastInhaling = inhaling;
            CompletedCycles = 0;
        }

        public void RestartPattern()
        {
            autoRun = true;
            idleMotion = false;
            ApplyPatternDurations();
            phaseTime = 0f;
            inhaling = true;
            lastInhaling = true;
            CompletedCycles = 0;
            CurrentDepth = DepthAt(0f);
            OnPhaseChanged?.Invoke(inhaling);
        }

        void Awake()
        {
            ApplyPatternDurations();
        }

        void Update()
        {
            if (autoRun)
            {
                AdvanceBreath(Time.deltaTime);
                return;
            }

            if (idleMotion)
                AdvanceIdle(Time.deltaTime);
        }

        void AdvanceIdle(float deltaTime)
        {
            idleTime += deltaTime;
            var wave = 0.5f + 0.5f * Mathf.Sin(idleTime * 0.85f);
            CurrentDepth = Mathf.Lerp(DepthAt(0.05f), DepthAt(0.2f), wave);
            inhaling = wave > 0.55f;
        }

        public void AdvanceBreath(float deltaTime)
        {
            var duration = inhaling ? inhaleDuration : exhaleDuration;
            phaseTime += deltaTime;
            var t = Mathf.Clamp01(phaseTime / Mathf.Max(duration, 0.01f));
            CurrentDepth = DepthAt(inhaling ? t : 1f - t);

            if (phaseTime < duration)
                return;

            phaseTime = 0f;
            inhaling = !inhaling;
            if (inhaling)
                CompletedCycles++;
            // Pick up Inspector edits at each phase boundary.
            ApplyPatternDurations();
            if (inhaling != lastInhaling)
            {
                lastInhaling = inhaling;
                OnPhaseChanged?.Invoke(inhaling);
            }
        }

        public event System.Action<bool> OnPhaseChanged;

        public float GetTargetDepth() => CurrentDepth;

        float DepthAt(float normalized)
        {
            ResolveSettings();
            var min = settings != null ? settings.breathDepthMin : 0f;
            var max = settings != null ? settings.breathDepthMax : 1f;
            if (max < min)
                max = min;
            return Mathf.Lerp(min, max, Mathf.Clamp01(normalized));
        }

        void ResolveSettings()
        {
            if (settings == null)
                settings = AvianVoiceSettings.FindLoaded();
        }

        void ApplyPatternDurations()
        {
            ResolveSettings();
            switch (pattern)
            {
                case BreathPattern.PatternB6x6:
                    inhaleDuration = settings != null ? settings.patternBInhaleSeconds : 6f;
                    exhaleDuration = settings != null ? settings.patternBExhaleSeconds : 6f;
                    break;
                case BreathPattern.PatternA4x4:
                    inhaleDuration = settings != null ? settings.patternAInhaleSeconds : 4f;
                    exhaleDuration = settings != null ? settings.patternAExhaleSeconds : 4f;
                    break;
                default:
                    inhaleDuration = settings != null ? settings.introInhaleSeconds : 4f;
                    exhaleDuration = settings != null ? settings.introExhaleSeconds : 4f;
                    break;
            }

            inhaleDuration = Mathf.Max(0.3f, inhaleDuration);
            exhaleDuration = Mathf.Max(0.3f, exhaleDuration);
        }
    }
}
