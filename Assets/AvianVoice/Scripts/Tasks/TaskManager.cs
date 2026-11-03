using System;
using System.Collections;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.UI;
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

        const string MeetInstruction = "This is your bird self in the mirror. Look at it — no need to breathe yet.";
        const string BreathSyncInstruction = "Watch the bird's chest and breathe with it.";
        const string FlyingInstruction = "The bird glides on its own. Sing louder or higher to climb, stay quiet to glide down.";
        const string SingingInstruction = "The bird sings one note. When it stops, sing that same note. Then it sings the next one.";
        const string CallFriendsInstruction = "The bird sings one note. Sing it back and one friend flies in.";
        const string ReflectionInstruction = "Rest by the water and look over your scores. How did it feel to become a bird?";

        [SerializeField]
        TaskSwitchMode switchMode = TaskSwitchMode.InScene;

        [SerializeField]
        TaskEntry[] tasks =
        {
            new()
            {
                taskId = "meet",
                displayName = "Meet Your Bird",
                instruction = MeetInstruction,
                sceneName = ""
            },
            new()
            {
                taskId = "breath_sync",
                displayName = "Breath Sync",
                instruction = BreathSyncInstruction,
                sceneName = ""
            },
            new()
            {
                taskId = "singing",
                displayName = "Singing Branch",
                instruction = SingingInstruction,
                sceneName = ""
            },
            new()
            {
                taskId = "flying",
                displayName = "Flying",
                instruction = FlyingInstruction,
                sceneName = ""
            },
            new()
            {
                taskId = "call_friends",
                displayName = "Call Friends",
                instruction = CallFriendsInstruction,
                sceneName = ""
            },
            new()
            {
                taskId = "reflection",
                displayName = "Reflection",
                instruction = ReflectionInstruction,
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
        public string CurrentInstruction => tasks.Length > 0
            ? UI.AvianUiCopy.Instruction(tasks[currentIndex].taskId)
            : "";
        public string CurrentTaskId => tasks.Length > 0 ? tasks[currentIndex].taskId : "";
        public int CurrentIndex => currentIndex;
        public int TaskCount => tasks.Length;

        void Start()
        {
            EnsureCanonicalOrder();
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
            EnsureCanonicalOrder();
            BindBehaviour("meet", meetTask);
            BindBehaviour("breath_sync", breathSyncTask);
            BindBehaviour("flying", flyingTask);
            BindBehaviour("singing", singingTask);
            BindBehaviour("call_friends", callFriendsTask);
            BindBehaviour("reflection", reflectionTask);
            SetInstruction("meet", MeetInstruction);
            SetInstruction("breath_sync", BreathSyncInstruction);
            SetInstruction("flying", FlyingInstruction);
            SetInstruction("singing", SingingInstruction);
            SetInstruction("call_friends", CallFriendsInstruction);
            SetInstruction("reflection", ReflectionInstruction);
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
            StepTask(1, wrap: true);
        }

        public void StepTask(int delta, bool wrap = false)
        {
            if (tasks.Length == 0 || isLoading || delta == 0)
                return;

            var next = currentIndex + delta;
            if (wrap)
                next = (next % tasks.Length + tasks.Length) % tasks.Length;
            else if (next < 0 || next >= tasks.Length)
                return;

            currentIndex = next;
            ApplyCurrentTask();
        }

        static readonly string[] CanonicalOrder =
        {
            "meet",
            "breath_sync",
            "singing",
            "flying",
            "call_friends",
            "reflection"
        };

        void EnsureCanonicalOrder()
        {
            var next = new TaskEntry[CanonicalOrder.Length];
            for (var i = 0; i < CanonicalOrder.Length; i++)
            {
                TaskEntry found = null;
                if (tasks != null)
                {
                    for (var j = 0; j < tasks.Length; j++)
                    {
                        if (tasks[j] != null && tasks[j].taskId == CanonicalOrder[i])
                        {
                            found = tasks[j];
                            break;
                        }
                    }
                }

                next[i] = found ?? new TaskEntry { taskId = CanonicalOrder[i], unlocked = true };
            }

            tasks = next;
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
                DisableOtherTasks(entry);
                ApplyTaskStartPose(entry.taskId);
                if (entry.taskBehaviour != null)
                    entry.taskBehaviour.enabled = true;
                NotifyTaskChanged();
                return;
            }

            if (!string.IsNullOrEmpty(entry.sceneName))
                StartCoroutine(LoadTaskScene(entry.sceneName));
            else
                NotifyTaskChanged();
        }

        void DisableOtherTasks(TaskEntry active)
        {
            foreach (var entry in tasks)
            {
                if (entry.taskBehaviour == null || entry == active)
                    continue;
                entry.taskBehaviour.enabled = false;
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

            var showMirror = taskId is "meet" or "breath_sync" or "singing" or "reflection";
            mirror.SetMirrorActive(showMirror);
        }

        static void UpdatePondWaterForTask(string taskId)
        {
            var showPondReflection = taskId is "flying" or "call_friends";
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
            OnTaskChanged?.Invoke(tasks[currentIndex].displayName, CurrentInstruction);
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head != null)
                SceneWishMenu.Ensure(head);
        }
    }
}
