using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Wires pitch/song tasks on TaskRoot and flight on XR Origin for in-scene task switching in Avian_Lab.
    /// </summary>
    public static class AvianInSceneTaskBootstrap
    {
        public static void EnsureForLabScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (!scene.StartsWith("Avian"))
                return;

            var xrOrigin = FindXrOrigin();
            if (xrOrigin == null)
            {
                Debug.LogWarning("[AvianVoice] In-scene tasks need XR Origin Hands in the scene.");
                return;
            }

            var taskRoot = GameObject.Find("TaskRoot") ?? new GameObject("TaskRoot");
            var parrot = WorldPlacedBird.FindParrotInScene();
            if (parrot != null)
            {
                var guard = parrot.GetComponent<WorldPlacedBird>() ?? parrot.gameObject.AddComponent<WorldPlacedBird>();
                if (parrot.GetComponent<ParrotFlightPresentation>() == null)
                    parrot.gameObject.AddComponent<ParrotFlightPresentation>();
                if (parrot.GetComponent<ParrotBreathEmbodiment>() == null)
                    parrot.gameObject.AddComponent<ParrotBreathEmbodiment>();
                if (parrot.GetComponent<ParrotMainCtrlPin>() == null)
                    parrot.gameObject.AddComponent<ParrotMainCtrlPin>();
                guard.EnsureVisibleAndGrounded();
                taskRoot.transform.position = parrot.position;
                taskRoot.transform.rotation = parrot.rotation;
            }
            else
            {
                Debug.LogWarning("[AvianVoice] No 'Parrot' object in scene — add Parrot prefab to Avian_Lab.");
            }

            var pitch = taskRoot.GetComponent<PitchMatchingTask>() ?? taskRoot.AddComponent<PitchMatchingTask>();
            var song = taskRoot.GetComponent<SongPerformanceTask>() ?? taskRoot.AddComponent<SongPerformanceTask>();
            pitch.enabled = false;
            song.enabled = false;

            var flight = xrOrigin.GetComponent<FlightControlTask>() ?? xrOrigin.AddComponent<FlightControlTask>();
            EnsureCharacterController(xrOrigin);
            flight.enabled = false;

            var manager = Object.FindAnyObjectByType<TaskManager>();
            if (manager == null)
            {
                var go = new GameObject("TaskManager");
                manager = go.AddComponent<TaskManager>();
            }

            manager.ConfigureInSceneTasks(
                TaskManager.TaskSwitchMode.InScene,
                pitch,
                flight,
                song);
        }

        static GameObject FindXrOrigin()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.Contains("XR Origin") && root.name.Contains("Hands"))
                    return root;
            }

            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.Contains("XR Origin"))
                    return root;
            }

            return null;
        }

        static void EnsureCharacterController(GameObject xrOrigin)
        {
            if (xrOrigin.GetComponent<CharacterController>() != null)
                return;

            var cc = xrOrigin.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 1f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.3f;
        }
    }
}
