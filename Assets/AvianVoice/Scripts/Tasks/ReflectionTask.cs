using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class ReflectionTask : MonoBehaviour
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
        float fadeDuration = 2f;

        TaskFlowUI taskUi;
        SessionRecorder recorder;
        ParrotMatchVisuals friendVisuals;
        float timer;
        bool farewellPlayed;
        float liveSync;

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
            if (!TaskActivity.IsActive("reflection"))
                return;

            timer = 0f;
            farewellPlayed = false;
            liveSync = 0f;
            BirdSceneRoles.EnsurePlayerBirdVisible();
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.Intro4x4);
            demonstrator?.RestartPattern();
            ShowScores();
            Debug.Log("[AvianVoice] Reflection: session scores + Task 1 breath/chirp sync.");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            BirdSceneRoles.EnsurePlayerBirdVisible();
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("reflection"))
                return;

            if (AvianInput.WasTaskCheatPressedThisFrame())
            {
                timer = fadeDuration;
                if (!farewellPlayed)
                {
                    farewellPlayed = true;
                    successChirp?.PlaySuccessBurst();
                }

                ShowScores();
                SetHud(BuildScoreText());
                Debug.Log("[AvianVoice] Reflection cheat: scores and question shown.");
                return;
            }

            timer += Time.deltaTime;
            UpdateBreathSync();

            if (!farewellPlayed && timer > 0.5f)
            {
                farewellPlayed = true;
                successChirp?.PlaySuccessBurst();
            }

            SetHud(BuildScoreText());
        }

        void UpdateBreathSync()
        {
            if (breath == null || demonstrator == null)
                return;

            var sync = BreathSyncEvaluator.Evaluate(breath.Current, demonstrator);
            liveSync = Mathf.Lerp(liveSync, sync, Time.deltaTime * 2f);
            recorder?.SetBreathSyncContext(sync, "intro_4x4");
            friendVisuals?.ApplyMatch(sync);
        }

        void ShowScores()
        {
            taskManager?.ReportScore(
                (SessionScoreCache.BreathingScore +
                 SessionScoreCache.VoiceScore +
                 SessionScoreCache.SynchronizationScore) / 3f);
        }

        static string BuildScoreText()
        {
            return
                $"Breathing Control: {SessionScoreCache.BreathingScore * 100f:0}%\n" +
                $"Voice Control: {SessionScoreCache.VoiceScore * 100f:0}%\n" +
                $"Synchronization: {SessionScoreCache.SynchronizationScore * 100f:0}%";
        }

        void EnsureFriendVisuals()
        {
            if (friendVisuals != null)
                return;
            var bird = BirdSceneRoles.Friend != null ? BirdSceneRoles.Friend : BirdSceneRoles.Player;
            var host = bird != null ? bird.gameObject : gameObject;
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
