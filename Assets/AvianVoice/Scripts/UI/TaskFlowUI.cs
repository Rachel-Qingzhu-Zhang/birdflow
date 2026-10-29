using AvianVoice.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    public class TaskFlowUI : MonoBehaviour
    {
        [SerializeField]
        Text titleText;

        [SerializeField]
        Text scoreText;

        [SerializeField]
        Text instructionText;

        [SerializeField]
        Button nextTaskButton;

        TaskManager taskManager;
        string liveStatus = "";
        float lastScore;

        public void Bind(TaskManager manager)
        {
            Bind(manager, titleText, instructionText, scoreText);
        }

        public void Bind(TaskManager manager, Text title, Text instruction, Text score)
        {
            if (manager == null)
                return;

            taskManager = manager;
            titleText = title;
            instructionText = instruction;
            scoreText = score;

            if (nextTaskButton != null)
                nextTaskButton.onClick.AddListener(() => taskManager?.AdvanceTask());

            taskManager.OnTaskChanged += Refresh;
            taskManager.OnScoreUpdated += OnScore;
            Refresh(taskManager.CurrentTaskName, taskManager.CurrentInstruction);
            OnScore(0f);
        }

        void OnScore(float score)
        {
            lastScore = score;
            PaintStatus();
        }

        void OnDestroy()
        {
            if (taskManager == null)
                return;
            taskManager.OnTaskChanged -= Refresh;
            taskManager.OnScoreUpdated -= OnScore;
        }

        public void SetScore(float score)
        {
            lastScore = score;
            PaintStatus();
        }

        void Refresh(string taskName, string instruction)
        {
            liveStatus = "";
            if (titleText != null)
                titleText.text = taskName;
            if (instructionText != null)
                instructionText.text = instruction;
            PaintStatus();
        }

        /// <summary>Live progress under the guide. Does not replace the two-sentence guide.</summary>
        public void SetProgressInstruction(string message)
        {
            liveStatus = message ?? "";
            PaintStatus();
        }

        void PaintStatus()
        {
            if (scoreText == null)
                return;

            scoreText.text = string.IsNullOrEmpty(liveStatus)
                ? $"Score: {lastScore:0}"
                : liveStatus;
        }
    }
}
