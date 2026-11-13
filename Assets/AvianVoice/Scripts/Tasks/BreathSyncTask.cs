using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// Breath Sync: follow the slower breath only. After patternBCycles the next scene starts
    /// (5 cycles of 6 s in + 6 s out = 60 s).
    /// </summary>
    public class BreathSyncTask : MonoBehaviour
    {
        enum Phase
        {
            Breathing,
            Complete
        }

        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        BirdFriendDemonstrator demonstrator;

        [SerializeField]
        bool enableDebugCheat = true;

        AvianVoiceSettings settings;
        ParrotMatchVisuals birdVisuals;
        TaskFlowUI taskUi;
        SessionRecorder recorder;
        Phase phase = Phase.Breathing;
        bool advancing;

        void Start()
        {
            ResolveReferences();
            EnsureBirdVisuals();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("breath_sync"))
                return;

            phase = Phase.Breathing;
            advancing = false;
            ResolveReferences();
            BirdVoiceMapper.Find()?.SetFollowVoice(false);
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternB6x6);
            demonstrator?.RestartPattern();
            SetHud("Breathe with the mirror");
        }

        void OnDisable()
        {
            birdVisuals?.ApplyMatch(0f);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("breath_sync"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                if (phase != Phase.Complete)
                    AdvancePhase();
                Debug.Log("[AvianVoice] Breath sync cheat. Phase=" + phase);
                return;
            }

            if (phase == Phase.Complete || demonstrator == null)
                return;

            var required = RequiredCycles();
            var progress = Mathf.Clamp01((demonstrator.CompletedCycles + demonstrator.CycleProgress) / required);
            taskManager?.ReportScore(progress);
            recorder?.SetBreathSyncContext(progress, "B");
            EnsureBirdVisuals();
            birdVisuals?.ApplyMatch(demonstrator.CurrentDepth * 0.6f);

            if (demonstrator.CompletedCycles >= required)
            {
                AdvancePhase();
                return;
            }

            var breath = demonstrator.IsInhaling ? "inhale" : "exhale";
            SetHud($"{breath} {demonstrator.SecondsLeftInPhase:0}s · cycle {demonstrator.CompletedCycles + 1}/{required}");
        }

        void AdvancePhase()
        {
            if (phase == Phase.Complete || advancing)
                return;

            birdVisuals?.PulsePerfect();

            phase = Phase.Complete;
            taskManager?.ReportScore(1f);
            SessionScoreCache.RecordSynchronization(1f);
            SessionScoreCache.RecordBreathing(1f);
            SetHud("Breath sync complete!");
            advancing = true;
            StartCoroutine(AdvanceAfterCue());
        }

        System.Collections.IEnumerator AdvanceAfterCue()
        {
            yield return new WaitForSeconds(0.6f);
            taskManager?.AdvanceTask();
        }

        int RequiredCycles()
        {
            var cycles = settings != null ? settings.patternBCycles : 5;
            return Mathf.Max(1, cycles);
        }

        void ResolveReferences()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
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
