#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Logs every open editor window (type, title, screen rect, docked, focus) plus the main window rect,
    /// so "the panel does not show" can be told apart from "the panel is open but hidden / off-screen".
    /// </summary>
    static class AvianEditorWindowDiagnostics
    {
        [MenuItem("Avian Voice/Debug/Log Editor Windows")]
        static void LogEditorWindows()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AvianVoice] Editor windows: main=" + EditorGUIUtility.GetMainWindowPosition()
                + " playing=" + EditorApplication.isPlaying
                + " focused=" + (EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetType().Name : "none")
                + " screen=" + Screen.currentResolution.width + "x" + Screen.currentResolution.height);
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                sb.AppendLine("[AvianVoice]   " + window.GetType().FullName + " '" + window.titleContent.text + "' pos=" + window.position
                    + " docked=" + window.docked + " hasFocus=" + window.hasFocus);
            }

            Debug.Log(sb.ToString());
        }
    }
}
#endif
