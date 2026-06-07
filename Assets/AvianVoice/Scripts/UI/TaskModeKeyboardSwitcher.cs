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

            if (AvianInput.WasDigitPressedThisFrame(1))
                taskManager.LoadTaskById("lab");
            else if (AvianInput.WasDigitPressedThisFrame(2))
                taskManager.LoadTaskById("pitch");
            else if (AvianInput.WasDigitPressedThisFrame(3))
                taskManager.LoadTaskById("flight");
            else if (AvianInput.WasDigitPressedThisFrame(4))
                taskManager.LoadTaskById("song");
            else if (AvianInput.WasNextTaskPressedThisFrame())
                taskManager.AdvanceTask();
        }
    }
}
