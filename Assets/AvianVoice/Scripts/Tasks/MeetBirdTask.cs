using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// Meet Your Bird: look at your bird self in the mirror. No breath exercise.
    /// Completes after a short look, then the player can move on.
    /// </summary>
    public class MeetBirdTask : MonoBehaviour
    {
        const float LookSeconds = 6f;

        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        BirdFriendDemonstrator demonstrator;

        [SerializeField]
        bool enableDebugCheat = true;

        TaskFlowUI taskUi;
        float elapsed;
        bool completed;

        void Start()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("meet"))
                return;

            completed = false;
            elapsed = 0f;
            ResolveReferences();
            BirdVoiceMapper.Find()?.SetFollowVoice(false);
            demonstrator?.SetTeachingActive(false);
            Debug.Log("[AvianVoice] Meet Your Bird: look at your bird self. Breathing stays in the next scene.");
        }

        void OnDisable()
        {
            demonstrator?.SetTeachingActive(true);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("meet"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                if (!completed)
                    Complete();
                Debug.Log("[AvianVoice] Meet cheat.");
                return;
            }

            if (completed)
                return;

            elapsed += Time.deltaTime;
            if (elapsed >= LookSeconds)
                Complete();
        }

        void Complete()
        {
            completed = true;
            taskManager?.ReportScore(1f);
            SetStatus("You met your bird self.");
        }

        void ResolveReferences()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
        }

        void SetStatus(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
