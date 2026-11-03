using AvianVoice.Core;
using AvianVoice.Tasks;
using UnityEngine;

namespace AvianVoice.UI
{
    public class TaskModeKeyboardSwitcher : MonoBehaviour
    {
        static readonly string[] TaskIds =
        {
            "meet",
            "breath_sync",
            "singing",
            "flying",
            "call_friends",
            "reflection"
        };

        TaskManager taskManager;

        void Start()
        {
            taskManager = FindAnyObjectByType<TaskManager>();
        }

        void Update()
        {
            if (taskManager == null)
                return;

            for (var i = 0; i < TaskIds.Length; i++)
            {
                if (AvianInput.WasDigitPressedThisFrame(i + 1))
                {
                    taskManager.LoadTaskById(TaskIds[i]);
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
