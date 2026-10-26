using System;
using System.Collections;
using AvianVoice.Core;
using AvianVoice.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Tasks
{
    public class TaskManager : MonoBehaviour
    {
        public enum TaskSwitchMode
        {
            InScene,
            LoadScene
        }

        [Serializable]
        public class TaskEntry
        {
            public string taskId;
            public string displayName;
            public string instruction;
            public string sceneName;
            public MonoBehaviour taskBehaviour;
            public bool unlocked = true;
        }

        [SerializeField]
        TaskSwitchMode switchMode = TaskSwitchMode.InScene;

        [SerializeField]
        TaskEntry[] tasks =
        {
            new()
            {
                taskId = "meet",
                displayName = "Meet Your Bird",
                instruction = "Space=inhale, release=exhale. Follow the bird friend (4s in / 4s out).",
                sceneName = ""
            },
            new()
            {
                taskId = "breath_sync",
                displayName = "Breath Sync",
                instruction = "Match Pattern A (4/4) then Pattern B (6/6) with the bird friend.",
                sceneName = ""
            },
            new()
            {
                taskId = "flying",
                displayName = "Flying",
                instruction = "Hold Space to climb, release to glide down. E/Q still nudge height.",
                sceneName = ""
            },
            new()
            {
                taskId = "singing",
                displayName = "Singing Branch",
                instruction = "Hold pitch while breathing. Deep inhale = richer bird song.",
                sceneName = ""
            },
            new()
            {
                taskId = "call_friends",
                displayName = "Call Friends",
                instruction = "Sing the melody with steady breath to summon nearby birds.",
                sceneName = ""
            },
            new()
            {
                taskId = "reflection",
                displayName = "Reflection",
                instruction = "Review scores. How did it feel to become a bird?",
                sceneName = ""
            }
        };

        [SerializeField]
        float fadeDuration = 0.5f;

        int currentIndex;
        bool isLoading;

        public event Action<string, string> OnTaskChanged;
        public event Action<float> OnScoreUpdated;

        public TaskSwitchMode SwitchMode => switchMode;
        public string CurrentTaskName => tasks.Length > 0 ? tasks[currentIndex].displayName : "None";
        public string CurrentInstruction => tasks.Length > 0 ? tasks[currentIndex].instruction : "";
        public string CurrentTaskId => tasks.Length > 0 ? tasks[currentIndex].taskId : "";
        public int TaskCount => tasks.Length;

        void Start()
        {
            ApplyCurrentTask();
        }

        public void SetSwitchMode(TaskSwitchMode mode) => switchMode = mode;

        public void ConfigureBirdyTasks(
            TaskSwitchMode mode,
            MonoBehaviour meetTask,
            MonoBehaviour breathSyncTask,
            MonoBehaviour flyingTask,
            MonoBehaviour singingTask,
            MonoBehaviour callFriendsTask,
            MonoBehaviour reflectionTask)
        {
            switchMode = mode;
            currentIndex = 0;
            BindBehaviour("meet", meetTask);
            BindBehaviour("breath_sync", breathSyncTask);
            BindBehaviour("flying", flyingTask);
            BindBehaviour("singing", singingTask);
            BindBehaviour("call_friends", callFriendsTask);
            BindBehaviour("reflection", reflectionTask);
            SetInstruction("flying", "Hold Space to climb, release to glide down. E/Q still nudge height.");
            ApplyCurrentTask();
        }

        void SetInstruction(string taskId, string instruction)
        {
            for (var i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].taskId != taskId)
                    continue;
                tasks[i].instruction = instruction;
                return;
            }
        }

        public void AdvanceTask()
        {
            if (tasks.Length == 0 || isLoading)
                return;

            currentIndex = (currentIndex + 1) % tasks.Length;
            ApplyCurrentTask();
        }

        public void LoadTaskById(string taskId)
        {
            for (var i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].taskId != taskId)
                    continue;
                currentIndex = i;
                ApplyCurrentTask();
                return;
            }
        }

        public void SwitchToTaskIndex(int index)
        {
            if (tasks.Length == 0 || index < 0 || index >= tasks.Length || isLoading)
                return;

            currentIndex = index;
            ApplyCurrentTask();
        }

        public void ReportScore(float score)
        {
            OnScoreUpdated?.Invoke(score);
            AvianSignals.Instance?.PublishTaskScore(CurrentTaskId, score);
        }

        void ApplyCurrentTask()
        {
            if (tasks.Length == 0)
                return;

            var entry = tasks[currentIndex];
            UpdateMirrorForTask(entry.taskId);
            UpdatePondWaterForTask(entry.taskId);
            UpdateRoleMarkersForTask(entry.taskId);
            // Perch fill lights stay off while restoring Fragilem17 mirror imaging.
            UpdateRecorderScene(entry.taskId);

            if (switchMode == TaskSwitchMode.InScene)
            {
                ApplyInSceneTask(entry);
                ApplyTaskStartPose(entry.taskId);
                NotifyTaskChanged();
                return;
            }

            if (!string.IsNullOrEmpty(entry.sceneName))
                StartCoroutine(LoadTaskScene(entry.sceneName));
            else
                NotifyTaskChanged();
        }

        void ApplyInSceneTask(TaskEntry active)
        {
            foreach (var entry in tasks)
            {
                if (entry.taskBehaviour == null)
                    continue;
                entry.taskBehaviour.enabled = entry == active;
            }
        }

        void BindBehaviour(string taskId, MonoBehaviour behaviour)
        {
            for (var i = 0; i < tasks.Length; i++)
            {
                if (tasks[i].taskId != taskId)
                    continue;
                tasks[i].taskBehaviour = behaviour;
                if (behaviour != null)
                    behaviour.enabled = false;
                return;
            }
        }

        static void UpdateMirrorForTask(string taskId)
        {
            var mirror = UnityEngine.Object.FindAnyObjectByType<Embodiment.MirrorBodySetup>();
            if (mirror == null)
                return;

            var showMirror = taskId is "meet" or "breath_sync" or "reflection";
            mirror.SetMirrorActive(showMirror);
        }

        static void UpdatePondWaterForTask(string taskId)
        {
            var showPondReflection = taskId is "flying" or "singing" or "call_friends";
            Embodiment.PondWaterReflection.SetAllReflectionActive(showPondReflection);
        }

        static void UpdateRoleMarkersForTask(string taskId)
        {
            Embodiment.RoleMarker.SetMarkersForTask(taskId);
        }

        static void UpdateRecorderScene(string taskId)
        {
            var recorder = UnityEngine.Object.FindAnyObjectByType<SessionRecorder>();
            recorder?.SetActiveSceneId(taskId);
        }

        static void ApplyTaskStartPose(string taskId)
        {
            foreach (var poses in UnityEngine.Object.FindObjectsByType<Embodiment.XrRigTaskStartPoses>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (poses == null)
                    continue;
                poses.ApplyForTask(taskId);
                return;
            }
        }

        IEnumerator LoadTaskScene(string sceneName)
        {
            isLoading = true;
            if (fadeDuration > 0f)
                yield return new WaitForSeconds(fadeDuration);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            while (op != null && !op.isDone)
                yield return null;

            isLoading = false;
            NotifyTaskChanged();
        }

        void NotifyTaskChanged()
        {
            if (tasks.Length == 0)
                return;
            OnTaskChanged?.Invoke(tasks[currentIndex].displayName, tasks[currentIndex].instruction);
        }
    }
}
