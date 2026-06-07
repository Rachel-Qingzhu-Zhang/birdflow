using System;
using System.Collections;
using AvianVoice.Core;
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
                taskId = "lab",
                displayName = "Avian Lab",
                instruction = "Space=breath, T=voice. Parrot in world: wings/chest follow breath.",
                sceneName = ""
            },
            new()
            {
                taskId = "pitch",
                displayName = "Pitch Matching",
                instruction = "Reference chirp every 3s — hum to match; parrot glows cyan.",
                sceneName = "Task_PitchMatching"
            },
            new()
            {
                taskId = "flight",
                displayName = "Flight Control",
                instruction = "You fly: pitch=height, steady breath=glide. Parrot flies beside you (built-in anim).",
                sceneName = "Task_FlightControl"
            },
            new()
            {
                taskId = "song",
                displayName = "Song Performance",
                instruction = "Listen → hold pitch → flock joins & echoes. Finale chorus at end.",
                sceneName = "Task_SongPerformance"
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

        void Start()
        {
            ApplyCurrentTask();
        }

        public void SetSwitchMode(TaskSwitchMode mode) => switchMode = mode;

        public void ConfigureInSceneTasks(
            TaskSwitchMode mode,
            MonoBehaviour pitchTask,
            MonoBehaviour flightTask,
            MonoBehaviour songTask)
        {
            switchMode = mode;
            EnsureLabEntry();
            currentIndex = 0;
            BindBehaviour("pitch", pitchTask);
            BindBehaviour("flight", flightTask);
            BindBehaviour("song", songTask);
            ApplyCurrentTask();
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
            if (switchMode == TaskSwitchMode.InScene)
            {
                ApplyInSceneTask(entry);
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
                entry.taskBehaviour.enabled = entry == active && entry.taskId != "lab";
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

        void EnsureLabEntry()
        {
            if (tasks.Length > 0 && tasks[0].taskId == "lab")
                return;

            var list = new TaskEntry[tasks.Length + 1];
            list[0] = new TaskEntry
            {
                taskId = "lab",
                displayName = "Avian Lab",
                instruction = "Free exploration: breath, voice, bird body.",
                sceneName = ""
            };
            Array.Copy(tasks, 0, list, 1, tasks.Length);
            tasks = list;
            currentIndex = 0;
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
