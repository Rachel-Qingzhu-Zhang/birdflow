using AvianVoice.Audio;
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
            FadePlayerBird(1f);
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.Intro4x4);
            demonstrator?.RestartPattern();
            ShowScores();
            Debug.Log("[AvianVoice] Reflection: session scores + Task 1 breath/chirp sync.");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            FadePlayerBird(1f);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("reflection"))
                return;

            timer += Time.deltaTime;
            UpdateBreathSync();

            var t = Mathf.Clamp01(timer / fadeDuration);
            FadePlayerBird(1f - t * 0.85f);

            if (!farewellPlayed && timer > 0.5f)
            {
                farewellPlayed = true;
                successChirp?.PlaySuccessBurst();
            }

            SetHud(BuildHudText(timer > fadeDuration));
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

        string BuildHudText(bool showQuestionnaire)
        {
            var scores = BuildScoreText();
            var teaching = demonstrator != null
                ? demonstrator.FormatTeachingLine()
                : string.Empty;
            var syncLine = $"sync {liveSync * 100f:0}%";
            var body = string.IsNullOrEmpty(teaching)
                ? $"{scores}\n{syncLine}"
                : $"{scores}\n{teaching}  |  {syncLine}";

            if (!showQuestionnaire)
                return body;

            return body + "\nHow did it feel to become a bird? (placeholder — press N for next task)";
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
            var friend = BirdSceneRoles.Friend;
            var host = friend != null ? friend.gameObject : gameObject;
            friendVisuals = host.GetComponent<ParrotMatchVisuals>() ?? host.AddComponent<ParrotMatchVisuals>();
            friendVisuals.BindFromScene();
        }

        void FadePlayerBird(float alpha)
        {
            var player = BirdSceneRoles.Player;
            if (player == null)
                return;

            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                var c = r.material.color;
                c.a = alpha;
                r.material.color = c;
            }
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
