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

        void OnScore(float score) => SetScore(score);

        void OnDestroy()
        {
            if (taskManager == null)
                return;
            taskManager.OnTaskChanged -= Refresh;
            taskManager.OnScoreUpdated -= OnScore;
        }

        public void SetScore(float score)
        {
            if (scoreText != null)
                scoreText.text = $"Score: {score:0}";
        }

        void Refresh(string taskName, string instruction)
        {
            if (titleText != null)
                titleText.text = taskName;
            if (instructionText != null)
                instructionText.text = instruction;
        }

        /// <summary>Live task progress (pitch/song phases) without changing the task title.</summary>
        public void SetProgressInstruction(string message)
        {
            if (!string.IsNullOrEmpty(message) && instructionText != null)
                instructionText.text = message;
        }
    }
}
