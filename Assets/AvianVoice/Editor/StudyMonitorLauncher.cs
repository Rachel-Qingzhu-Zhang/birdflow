#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using AvianVoice.Study;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Opens the study data monitor (Tools/StudyMonitor/study_monitor.py: a separate process with its own window
    /// that draws mic.wav / output.wav / events / tracking while they are written) when Play starts in the
    /// embodiment scene, or as soon as a recording session opens in any other scene. Unity only starts it; the
    /// monitor reads the StudyData files by itself and keeps running when Play stops. Starting it twice is
    /// harmless: a running monitor is reused and its window is only reopened if it was closed.
    /// Menu: Avian Voice / Study Monitor.
    /// </summary>
    [InitializeOnLoad]
    public static class StudyMonitorLauncher
    {
        const string AutoOpenKey = "AvianVoice.StudyMonitor.AutoOpen";
        const string OpenMenu = "Avian Voice/Study Monitor/Open Now";
        const string AutoMenu = "Avian Voice/Study Monitor/Open Automatically With Play";

        /// <summary>The monitor stops by itself this long after its window was closed.</summary>
        const int ExitWhenIdleSeconds = 600;

        static bool waitingForSession;

        public static bool AutoOpen
        {
            get => EditorPrefs.GetBool(AutoOpenKey, true);
            set => EditorPrefs.SetBool(AutoOpenKey, value);
        }

        public static string ScriptPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "StudyMonitor", "study_monitor.py"));

        static StudyMonitorLauncher()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && AutoOpen)
            {
                if (SceneManager.GetActiveScene().path == AvianEmbodimentSceneSetup.ScenePath)
                {
                    Launch();
                    return;
                }

                // Other scenes: only when something is actually recorded.
                waitingForSession = true;
                EditorApplication.update -= WaitForSession;
                EditorApplication.update += WaitForSession;
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopWaiting();
            }
        }

        static void WaitForSession()
        {
            if (!waitingForSession || !EditorApplication.isPlaying)
            {
                StopWaiting();
                return;
            }

            var recorder = StudyRecorder.Instance;
            if (recorder == null || !recorder.SessionOpen)
                return;

            StopWaiting();
            Launch();
        }

        static void StopWaiting()
        {
            waitingForSession = false;
            EditorApplication.update -= WaitForSession;
        }

        [MenuItem(OpenMenu)]
        static void OpenNow() => Launch();

        [MenuItem(AutoMenu)]
        static void ToggleAuto() => AutoOpen = !AutoOpen;

        [MenuItem(AutoMenu, true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked(AutoMenu, AutoOpen);
            return true;
        }

        /// <summary>Start the monitor process (or bring its window back if it is already running).</summary>
        public static void Launch()
        {
            var script = ScriptPath;
            if (!File.Exists(script))
            {
                Debug.LogWarning("[AvianVoice] Study monitor not found: " + script);
                return;
            }

            var arguments = "\"" + script + "\" --root \"" + StudyRecorder.RootDirectory + "\" --exit-when-idle " + ExitWhenIdleSeconds;
            Exception last = null;
            foreach (var python in new[] { "python", "py" })
            {
                try
                {
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = python,
                        Arguments = (python == "py" ? "-3 " : "") + arguments,
                        WorkingDirectory = Path.GetDirectoryName(script),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    Debug.Log("[AvianVoice] Study monitor started (" + python + "): it shows the recording in its own window.");
                    return;
                }
                catch (Exception e)
                {
                    last = e;
                }
            }

            Debug.LogWarning("[AvianVoice] Study monitor not started: Python 3 was not found on PATH (" + (last != null ? last.Message : "") +
                             "). Install Python 3 or run Tools/StudyMonitor/run_monitor.bat by hand.");
        }
    }
}
#endif
