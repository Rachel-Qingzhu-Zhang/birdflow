using AvianVoice.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Core
{
    /// <summary>
    /// Ensures Avian lab setup runs in Avian scenes without manual component wiring.
    /// </summary>
    public static class AvianVoiceSceneAutoLoad
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            var scene = SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("Avian") && !scene.StartsWith("Task_"))
                return;

            AvianSceneBootstrapHelper.Apply();

            if (Object.FindAnyObjectByType<AvianLabRuntimeSetup>() == null)
            {
                var go = new GameObject("AvianLabRuntimeSetup");
                go.AddComponent<AvianLabRuntimeSetup>();
            }

            if (scene.StartsWith("Avian"))
                AvianInSceneTaskBootstrap.EnsureForLabScene();
            else
                EnsureTaskComponents(scene);
        }

        static void EnsureTaskComponents(string sceneName)
        {
            var taskManager = Object.FindAnyObjectByType<TaskManager>();
            if (taskManager == null)
            {
                var tasksGo = new GameObject("TaskManager");
                taskManager = tasksGo.AddComponent<TaskManager>();
                taskManager.SetSwitchMode(TaskManager.TaskSwitchMode.LoadScene);
            }

            var root = GameObject.Find("TaskRoot");
            if (root == null)
            {
                root = new GameObject("TaskRoot");
            }

            if (sceneName.Contains("PitchMatching") && root.GetComponent<PitchMatchingTask>() == null)
                root.AddComponent<PitchMatchingTask>();
            if (sceneName.Contains("FlightControl"))
            {
                if (root.GetComponent<FlightControlTask>() == null)
                    root.AddComponent<FlightControlTask>();
                if (root.GetComponent<CharacterController>() == null)
                {
                    var cc = root.AddComponent<CharacterController>();
                    cc.center = new Vector3(0f, 1f, 0f);
                    cc.height = 2f;
                    cc.radius = 0.3f;
                }
            }
            if (sceneName.Contains("SongPerformance") && root.GetComponent<SongPerformanceTask>() == null)
                root.AddComponent<SongPerformanceTask>();

            if (sceneName.Contains("PitchMatching"))
                taskManager.LoadTaskById("singing");
            else if (sceneName.Contains("FlightControl"))
                taskManager.LoadTaskById("flying");
            else if (sceneName.Contains("SongPerformance"))
                taskManager.LoadTaskById("call_friends");
        }
    }
}
