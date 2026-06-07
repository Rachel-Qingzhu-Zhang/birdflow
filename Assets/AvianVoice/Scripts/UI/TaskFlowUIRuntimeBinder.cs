using AvianVoice.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    public class TaskFlowUIRuntimeBinder : MonoBehaviour
    {
        TaskFlowUI flowUi;
        Text titleText;
        Text instructionText;
        Text scoreText;

        public void Bind(TaskFlowUI ui, Text title, Text instruction, Text score)
        {
            flowUi = ui;
            titleText = title;
            instructionText = instruction;
            scoreText = score;
        }

        void Start()
        {
            var manager = FindAnyObjectByType<TaskManager>();
            if (flowUi == null || manager == null)
                return;

            flowUi.Bind(manager, titleText, instructionText, scoreText);
        }
    }
}
