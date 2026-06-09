using AvianVoice.Audio;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class ReflectionTask : MonoBehaviour
    {
        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        SuccessChirpBurst successChirp;

        [SerializeField]
        float fadeDuration = 2f;

        TaskFlowUI taskUi;
        float timer;
        bool farewellPlayed;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (successChirp == null)
                successChirp = FindAnyObjectByType<SuccessChirpBurst>();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("reflection"))
                return;

            timer = 0f;
            farewellPlayed = false;
            FadePlayerBird(1f);
            ShowScores();
            Debug.Log("[AvianVoice] Reflection: placeholder scores and questionnaire.");
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("reflection"))
                return;

            timer += Time.deltaTime;
            var t = Mathf.Clamp01(timer / fadeDuration);
            FadePlayerBird(1f - t * 0.85f);

            if (!farewellPlayed && timer > 0.5f)
            {
                farewellPlayed = true;
                successChirp?.PlaySuccessBurst();
            }

            if (timer > fadeDuration)
                SetHud(BuildScoreText() + "\nHow did it feel to become a bird? (placeholder — press N for next task)");
        }

        void ShowScores()
        {
            taskManager?.ReportScore(
                (SessionScoreCache.BreathingScore +
                 SessionScoreCache.VoiceScore +
                 SessionScoreCache.SynchronizationScore) / 3f);
            SetHud(BuildScoreText());
        }

        static string BuildScoreText()
        {
            return
                $"Breathing Control: {SessionScoreCache.BreathingScore * 100f:0}%\n" +
                $"Voice Control: {SessionScoreCache.VoiceScore * 100f:0}%\n" +
                $"Synchronization: {SessionScoreCache.SynchronizationScore * 100f:0}%";
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
