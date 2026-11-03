using AvianVoice.Core;
using AvianVoice.Tasks;
using UnityEngine;

namespace AvianVoice.UI
{
    public class TaskModeKeyboardSwitcher : MonoBehaviour
    {
        TaskManager taskManager;

        void Start()
        {
            taskManager = FindAnyObjectByType<TaskManager>();
        }

        void Update()
        {
            if (taskManager == null)
                return;

            // Digits follow the session flow (1 = first task), so a four-scene study uses 1-4.
            for (var i = 0; i < taskManager.TaskCount && i < 9; i++)
            {
                if (AvianInput.WasDigitPressedThisFrame(i + 1))
                {
                    taskManager.SwitchToTaskIndex(i);
                    return;
                }
            }

            if (AvianInput.WasPreviousTaskPressedThisFrame())
                taskManager.StepTask(-1);
            else if (AvianInput.WasNextTaskPressedThisFrame())
                taskManager.StepTask(1);
        }
    }
}
