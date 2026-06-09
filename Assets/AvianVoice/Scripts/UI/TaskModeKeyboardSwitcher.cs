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
            "flying",
            "singing",
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

            if (AvianInput.WasNextTaskPressedThisFrame())
                taskManager.AdvanceTask();
        }
    }
}
