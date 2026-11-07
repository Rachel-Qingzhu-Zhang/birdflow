#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Debug: record the Unity profiler for ~60 player frames and log the markers with the largest average
    /// self time and total time on the main thread, so the cost of each script / render stage is visible
    /// without opening the Profiler window.
    /// </summary>
    static class AvianProfileDumpMenu
    {
        const int FramesToCapture = 60;
        static int startFrame = -1;
        static int seenFrames;

        [MenuItem("Avian Voice/Embodiment/Debug/Profile 60 Frames And Log Top Markers (Play Mode)")]
        static void ProfileAndDump()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AvianVoice] Enter Play mode first.");
                return;
            }

            ProfilerDriver.ClearAllFrames();
            ProfilerDriver.profileEditor = true;
            ProfilerDriver.deepProfiling = false;
            ProfilerDriver.enabled = true;
            startFrame = Time.frameCount;
            seenFrames = 0;
            EditorApplication.update += WaitForFrames;
            Debug.Log("[AvianVoice] Profiling " + FramesToCapture + " frames…");
        }

        static void WaitForFrames()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= WaitForFrames;
                ProfilerDriver.enabled = false;
                return;
            }

            if (Time.frameCount - startFrame < FramesToCapture + 5)
                return;

            EditorApplication.update -= WaitForFrames;
            ProfilerDriver.enabled = false;
            Dump();
        }

        static void Dump()
        {
            var first = ProfilerDriver.firstFrameIndex;
            var last = ProfilerDriver.lastFrameIndex;
            if (first < 0 || last < first)
            {
                Debug.LogWarning("[AvianVoice] No profiler frames captured.");
                return;
            }

            var selfByName = new Dictionary<string, double>();
            var totalByName = new Dictionary<string, double>();
            var children = new List<int>();
            var frames = 0;
            double frameTimeSum = 0;
            for (var frame = Mathf.Max(first, last - FramesToCapture); frame <= last; frame++)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0, HierarchyFrameDataView.ViewModes.Default,
                    HierarchyFrameDataView.columnTotalTime, false);
                if (view == null || !view.valid)
                    continue;

                frames++;
                frameTimeSum += view.frameTimeMs;
                var stack = new Stack<int>();
                stack.Push(view.GetRootItemID());
                while (stack.Count > 0)
                {
                    var id = stack.Pop();
                    var name = view.GetItemName(id);
                    var self = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
                    var total = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
                    selfByName[name] = selfByName.TryGetValue(name, out var s) ? s + self : self;
                    totalByName[name] = totalByName.TryGetValue(name, out var t) ? t + total : total;
                    view.GetItemChildren(id, children);
                    foreach (var child in children)
                        stack.Push(child);
                }
            }

            if (frames == 0)
            {
                Debug.LogWarning("[AvianVoice] Profiler frames were not readable.");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("[AvianVoice] Profile of ").Append(frames).Append(" frames, main thread, avg frame ").Append((frameTimeSum / frames).ToString("0.0")).Append(" ms\n");
            sb.Append("  top self time (ms/frame):\n");
            foreach (var pair in selfByName.OrderByDescending(p => p.Value).Take(28))
                sb.Append("    ").Append((pair.Value / frames).ToString("0.00").PadLeft(7)).Append("  ").Append(pair.Key).Append('\n');
            sb.Append("  top total time (ms/frame):\n");
            foreach (var pair in totalByName.OrderByDescending(p => p.Value).Take(28))
                sb.Append("    ").Append((pair.Value / frames).ToString("0.00").PadLeft(7)).Append("  ").Append(pair.Key).Append('\n');
            Debug.Log(sb.ToString());
        }
    }
}
#endif
