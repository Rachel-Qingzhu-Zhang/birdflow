using AvianVoice.Embodiment;
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

            EnsureXrThumbstickLocomotion();
        }

        static void EnsureXrThumbstickLocomotion()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!root.name.Contains("XR Origin"))
                    continue;

                if (root.GetComponent<AvianXrThumbstickLocomotion>() == null)
                    root.AddComponent<AvianXrThumbstickLocomotion>();
                return;
            }
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
                var xrOrigin = FindXrOriginForTaskScene();
                var flightHost = xrOrigin != null ? xrOrigin : root;
                if (flightHost.GetComponent<FlightControlTask>() == null)
                    flightHost.AddComponent<FlightControlTask>();
                if (flightHost.GetComponent<CharacterController>() == null)
                {
                    var cc = flightHost.AddComponent<CharacterController>();
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

        static GameObject FindXrOriginForTaskScene()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.Contains("XR Origin"))
                    return root;
            }

            return null;
        }
    }
}
