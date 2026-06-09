using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class MeetBirdTask : MonoBehaviour
    {
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
        float holdSeconds = 10f;

        [SerializeField]
        bool enableDebugCheat = true;

        ParrotMatchVisuals friendVisuals;
        TaskFlowUI taskUi;
        SessionRecorder recorder;
        float syncHold;
        float score;
        bool completed;

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
            if (!TaskActivity.IsActive("meet"))
                return;

            completed = false;
            syncHold = 0f;
            score = 0f;
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.Intro4x4);
            demonstrator?.RestartPattern();
            SetHud("Orange bird teaches: watch chest + listen. Match 4s in / 4s out with Space.");
            Debug.Log("[AvianVoice] Meet Your Bird: sync breath with friend for 10s above 80%.");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("meet") || completed)
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                CompleteSuccess(1f);
                return;
            }

            if (breath == null || demonstrator == null)
                return;

            var sync = BreathSyncEvaluator.Evaluate(breath.Current, demonstrator);
            score = Mathf.Lerp(score, sync, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            recorder?.SetBreathSyncContext(sync, "intro_4x4");
            SessionScoreCache.RecordSynchronization(sync);
            SessionScoreCache.RecordBreathing(sync);
            friendVisuals?.ApplyMatch(sync);

            if (sync >= syncThreshold)
                syncHold += Time.deltaTime;
            else
                syncHold = 0f;

            if (syncHold >= holdSeconds)
            {
                CompleteSuccess(score);
                return;
            }

            var teaching = demonstrator.FormatTeachingLine();
            SetHud($"{teaching}  |  sync {sync * 100f:0}% — hold {Mathf.Max(0f, holdSeconds - syncHold):0.0}s");
        }

        void CompleteSuccess(float finalScore)
        {
            completed = true;
            score = finalScore;
            taskManager?.ReportScore(score);
            friendVisuals?.PulsePerfect();
            successChirp?.PlaySuccessBurst();
            SetHud("Success! You met your bird self.");
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
