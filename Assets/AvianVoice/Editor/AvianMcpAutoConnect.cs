#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Starts the Coplay MCP HTTP bridge if it is idle, so Cursor can talk to this Unity instance
    /// without opening Window > MCP For Unity first.
    /// </summary>
    [InitializeOnLoad]
    static class AvianMcpAutoConnect
    {
        static AvianMcpAutoConnect()
        {
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorApplication.delayCall += () => { _ = TryStart(); };
        }

        [MenuItem("Avian Voice/Start MCP Session")]
        static void StartFromMenu()
        {
            _ = TryStart();
        }

        static async Task TryStart()
        {
            try
            {
                var locator = Type.GetType("MCPForUnity.Editor.Services.MCPServiceLocator, MCPForUnity.Editor");
                var bridgeProp = locator?.GetProperty("Bridge", BindingFlags.Public | BindingFlags.Static);
                var bridge = bridgeProp?.GetValue(null);
                if (bridge == null)
                {
                    Debug.LogWarning("[AvianVoice] MCP For Unity is not loaded. Open Window > MCP For Unity.");
                    return;
                }

                var runningProp = bridge.GetType().GetProperty("IsRunning");
                var running = runningProp != null && (bool)runningProp.GetValue(bridge);
                if (running)
                    return;

                var start = bridge.GetType().GetMethod("StartAsync", Type.EmptyTypes);
                if (start == null)
                    return;

                var task = start.Invoke(bridge, null) as Task;
                if (task != null)
                    await task;
                Debug.Log("[AvianVoice] MCP session start requested.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AvianVoice] Could not auto-start MCP: " + ex.Message);
            }
        }
    }
}
#endif
