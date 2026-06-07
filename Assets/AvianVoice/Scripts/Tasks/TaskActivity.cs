using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// True when a task is active via TaskManager (in-scene switch) or a dedicated Task_* scene.
    /// </summary>
    public static class TaskActivity
    {
        public static bool IsActive(string taskId)
        {
            var scene = SceneManager.GetActiveScene().name;
            if (taskId == "pitch" && scene.Contains("PitchMatching"))
                return true;
            if (taskId == "flight" && scene.Contains("FlightControl"))
                return true;
            if (taskId == "song" && scene.Contains("SongPerformance"))
                return true;

            var manager = Object.FindAnyObjectByType<TaskManager>();
            return manager != null && manager.CurrentTaskId == taskId;
        }
    }
}
