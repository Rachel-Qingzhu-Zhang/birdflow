using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// True when a task is active via TaskManager (in-scene switch) or a dedicated Task_* scene.
    /// </summary>
    public static class TaskActivity
    {
        static TaskManager cachedManager;
        static int cachedFrame = -1;

        /// <summary>The scene's TaskManager, looked up at most once per frame (FindAnyObjectByType is not free).</summary>
        public static TaskManager Manager
        {
            get
            {
                if (cachedManager == null && cachedFrame != Time.frameCount)
                {
                    cachedFrame = Time.frameCount;
                    cachedManager = Object.FindAnyObjectByType<TaskManager>();
                }

                return cachedManager;
            }
        }

        public static bool IsActive(string taskId)
        {
            var scene = SceneManager.GetActiveScene().name;
            if (taskId == "pitch" && scene.Contains("PitchMatching"))
                return true;
            if (taskId == "flight" && scene.Contains("FlightControl"))
                return true;
            if (taskId == "flying" && scene.Contains("FlightControl"))
                return true;
            if (taskId == "song" && scene.Contains("SongPerformance"))
                return true;

            var manager = Manager;
            return manager != null && manager.CurrentTaskId == taskId;
        }
    }
}
