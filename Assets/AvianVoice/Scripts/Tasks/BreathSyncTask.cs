using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// Breath Sync: follow the bird through Pattern A, then the slower Pattern B.
    /// Each pattern advances after its configured cycle count (AvianVoiceSettings).
    /// </summary>
    public class BreathSyncTask : MonoBehaviour
    {
        enum Phase
        {
            PatternA,
            PatternB,
            Complete
        }

        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        BirdFriendDemonstrator demonstrator;

        [SerializeField]
        SuccessChirpBurst successChirp;

        [SerializeField]
        bool enableDebugCheat = true;

        AvianVoiceSettings settings;
        ParrotMatchVisuals birdVisuals;
        TaskFlowUI taskUi;
        SessionRecorder recorder;
        Phase phase = Phase.PatternA;

        void Start()
        {
            ResolveReferences();
            EnsureBirdVisuals();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("breath_sync"))
                return;

            phase = Phase.PatternA;
            ResolveReferences();
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternA4x4);
            demonstrator?.RestartPattern();
            SetHud("Pattern A");
        }

        void OnDisable()
        {
            birdVisuals?.ApplyMatch(0f);
            BirdExperienceAudio.Ensure()?.StopAll();
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("breath_sync"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                if (phase != Phase.Complete)
                    AdvancePhase();
                else
                    BirdExperienceAudio.Ensure()?.PlayForestReward();
                Debug.Log("[AvianVoice] Breath sync cheat. Phase=" + phase);
                return;
            }

            if (phase == Phase.Complete || demonstrator == null)
                return;

            var required = RequiredCycles();
            var progress = Mathf.Clamp01((demonstrator.CompletedCycles + demonstrator.CycleProgress) / required);
            var overall = phase == Phase.PatternA ? progress * 0.5f : 0.5f + progress * 0.5f;
            taskManager?.ReportScore(overall);
            recorder?.SetBreathSyncContext(progress, phase == Phase.PatternA ? "A" : "B");
            EnsureBirdVisuals();
            birdVisuals?.ApplyMatch(demonstrator.CurrentDepth * 0.6f);

            if (demonstrator.CompletedCycles >= required)
            {
                AdvancePhase();
                return;
            }

            var label = phase == Phase.PatternA ? "Pattern A" : "Pattern B";
            var breath = demonstrator.IsInhaling ? "inhale" : "exhale";
            SetHud($"{label}: {breath} {demonstrator.SecondsLeftInPhase:0}s · cycle {demonstrator.CompletedCycles + 1}/{required}");
        }

        void AdvancePhase()
        {
            if (BirdExperienceAudio.Ensure()?.PlayForestReward() != true)
                successChirp?.PlaySuccessBurst();
            birdVisuals?.PulsePerfect();

            if (phase == Phase.PatternA)
            {
                phase = Phase.PatternB;
                demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternB6x6);
                demonstrator?.RestartPattern();
                SetHud("Pattern B: slower breaths. Keep following your bird.");
                return;
            }

            phase = Phase.Complete;
            taskManager?.ReportScore(1f);
            SessionScoreCache.RecordSynchronization(1f);
            SessionScoreCache.RecordBreathing(1f);
            SetHud("Breath sync complete!");
        }

        int RequiredCycles()
        {
            var cycles = phase == Phase.PatternA
                ? settings != null ? settings.patternACycles : 3
                : settings != null ? settings.patternBCycles : 2;
            return Mathf.Max(1, cycles);
        }

        void ResolveReferences()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            if (successChirp == null)
                successChirp = FindAnyObjectByType<SuccessChirpBurst>();
            if (recorder == null)
                recorder = FindAnyObjectByType<SessionRecorder>();
            if (settings == null)
                settings = AvianVoiceSettings.FindLoaded();
        }

        void EnsureBirdVisuals()
        {
            if (birdVisuals != null)
                return;
            var bird = BirdSceneRoles.Player;
            if (bird == null)
                return;
            birdVisuals = bird.GetComponent<ParrotMatchVisuals>() ?? bird.gameObject.AddComponent<ParrotMatchVisuals>();
            birdVisuals.BindFromScene();
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
