using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
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
        BreathSimulator breath;

        [SerializeField]
        BirdFriendDemonstrator demonstrator;

        [SerializeField]
        SuccessChirpBurst successChirp;

        [SerializeField]
        float syncThreshold = 0.8f;

        [SerializeField]
        float patternHoldSeconds = 8f;

        [SerializeField]
        bool enableDebugCheat = true;

        ParrotMatchVisuals friendVisuals;
        TaskFlowUI taskUi;
        SessionRecorder recorder;
        Phase phase = Phase.PatternA;
        float syncHold;
        float score;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (breath == null)
                breath = FindAnyObjectByType<BreathSimulator>();
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            if (successChirp == null)
                successChirp = FindAnyObjectByType<SuccessChirpBurst>();
            recorder = FindAnyObjectByType<SessionRecorder>();
            EnsureFriendVisuals();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("breath_sync"))
                return;

            phase = Phase.PatternA;
            syncHold = 0f;
            score = 0f;
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternA4x4);
            demonstrator?.RestartPattern();
            SetHud("Orange bird leads Pattern A (4s/4s). Copy its rhythm with Space.");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("breath_sync") || phase == Phase.Complete)
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                AdvancePhase(1f);
                return;
            }

            if (breath == null || demonstrator == null)
                return;

            var sync = BreathSyncEvaluator.Evaluate(breath.Current, demonstrator);
            score = Mathf.Lerp(score, sync, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            recorder?.SetBreathSyncContext(sync, phase == Phase.PatternA ? "A_4x4" : "B_6x6");
            SessionScoreCache.RecordSynchronization(sync);
            SessionScoreCache.RecordBreathing(sync);
            friendVisuals?.ApplyMatch(sync);

            if (sync >= syncThreshold)
                syncHold += Time.deltaTime;
            else
                syncHold = 0f;

            if (syncHold >= patternHoldSeconds)
                AdvancePhase(score);
            else
            {
                var teaching = demonstrator.FormatTeachingLine();
                SetHud($"{phase} — {teaching}  |  sync {sync * 100f:0}% — hold {Mathf.Max(0f, patternHoldSeconds - syncHold):0.0}s");
            }
        }

        void AdvancePhase(float finalScore)
        {
            syncHold = 0f;
            successChirp?.PlaySuccessBurst();
            friendVisuals?.PulsePerfect();

            if (phase == Phase.PatternA)
            {
                phase = Phase.PatternB;
                demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternB6x6);
                demonstrator?.RestartPattern();
                SetHud("Orange bird leads Pattern B (6s/6s). Watch + listen, then match.");
                return;
            }

            phase = Phase.Complete;
            taskManager?.ReportScore(finalScore);
            SetHud("Breath sync complete!");
        }

        void EnsureFriendVisuals()
        {
            if (friendVisuals != null)
                return;
            var friend = BirdSceneRoles.Friend;
            var host = friend != null ? friend.gameObject : gameObject;
            friendVisuals = host.GetComponent<ParrotMatchVisuals>() ?? host.AddComponent<ParrotMatchVisuals>();
            friendVisuals.BindFromScene();
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
