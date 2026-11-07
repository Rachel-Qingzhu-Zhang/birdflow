using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace AvianVoice.Study
{
    /// <summary>
    /// Debug: samples the frame time for a few seconds and logs mean / 95th percentile / worst frame, the
    /// number of hitches, and the average cost of the main player-loop stages (profiler markers), to tell a
    /// smooth session from a stuttering one and to see where the time goes (menu: Debug / Log Frame Times).
    /// </summary>
    public sealed class FrameTimeProbe : MonoBehaviour
    {
        static readonly (string name, ProfilerCategory category)[] Markers =
        {
            ("EditorLoop", ProfilerCategory.Internal),
            ("PlayerLoop", ProfilerCategory.Internal),
            ("BehaviourUpdate", ProfilerCategory.Scripts),
            ("LateBehaviourUpdate", ProfilerCategory.Scripts),
            ("FixedBehaviourUpdate", ProfilerCategory.Scripts),
            ("Animators.Update", ProfilerCategory.Animation),
            ("Physics.Simulate", ProfilerCategory.Physics),
            ("Camera.Render", ProfilerCategory.Render),
            ("Gfx.WaitForPresentOnGfxThread", ProfilerCategory.Render),
            ("Gfx.WaitForRenderThread", ProfilerCategory.Render),
            ("WaitForTargetFPS", ProfilerCategory.Internal),
            ("XR.WaitFrame", ProfilerCategory.Vr),
            ("XR.WaitForGPU", ProfilerCategory.Vr),
            ("GUI.Repaint", ProfilerCategory.Gui),
            ("AudioManager.Update", ProfilerCategory.Audio),
        };

        public float seconds = 3f;
        public float hitchMs = 25f;
        readonly List<float> samples = new();
        readonly List<ProfilerRecorder> recorders = new();
        readonly List<double> markerSums = new();
        float elapsed;
        int presentStart = -1;
        int droppedStart = -1;

        public static void Run(float seconds = 3f)
        {
            var probe = new GameObject("FrameTimeProbe").AddComponent<FrameTimeProbe>();
            probe.seconds = seconds;
        }

        void Awake()
        {
            foreach (var (name, category) in Markers)
            {
                recorders.Add(ProfilerRecorder.StartNew(category, name));
                markerSums.Add(0);
            }
        }

        void OnDestroy()
        {
            foreach (var r in recorders)
                r.Dispose();
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (presentStart < 0 && UnityEngine.XR.XRStats.TryGetFramePresentCount(out var presents0))
            {
                presentStart = presents0;
                UnityEngine.XR.XRStats.TryGetDroppedFrameCount(out droppedStart);
            }

            if (elapsed > 0f)
            {
                samples.Add(dt * 1000f);
                for (var i = 0; i < recorders.Count; i++)
                {
                    if (recorders[i].Valid)
                        markerSums[i] += recorders[i].LastValue;
                }
            }

            elapsed += dt;
            if (elapsed < seconds)
                return;

            samples.Sort();
            var count = samples.Count;
            var sum = 0f;
            var hitches = 0;
            foreach (var s in samples)
            {
                sum += s;
                if (s > hitchMs)
                    hitches++;
            }

            var mean = count > 0 ? sum / count : 0f;
            var p95 = count > 0 ? samples[Mathf.Clamp(Mathf.RoundToInt(count * 0.95f), 0, count - 1)] : 0f;
            var max = count > 0 ? samples[count - 1] : 0f;
            var sb = new StringBuilder();
            sb.Append("[AvianVoice] Frame times over ").Append(seconds).Append(" s: ").Append(count).Append(" frames, mean ")
              .Append(mean.ToString("0.0")).Append(" ms (").Append(mean > 0f ? (1000f / mean).ToString("0") : "-").Append(" fps), p95 ")
              .Append(p95.ToString("0.0")).Append(" ms, worst ").Append(max.ToString("0.0")).Append(" ms, hitches > ").Append(hitchMs)
              .Append(" ms: ").Append(hitches).Append(" | markers (avg ms/frame):");
            for (var i = 0; i < recorders.Count; i++)
            {
                if (!recorders[i].Valid || count == 0)
                    continue;
                sb.Append(' ').Append(Markers[i].name).Append('=').Append((markerSums[i] / count / 1e6).ToString("0.0"));
            }

            if (presentStart >= 0 && UnityEngine.XR.XRStats.TryGetFramePresentCount(out var presents1))
            {
                UnityEngine.XR.XRStats.TryGetDroppedFrameCount(out var dropped1);
                sb.Append(" | xr presents=").Append(presents1 - presentStart).Append(" (").Append(((presents1 - presentStart) / seconds).ToString("0")).Append("/s) dropped=").Append(dropped1 - droppedStart);
                if (UnityEngine.XR.XRStats.TryGetGPUTimeLastFrame(out var gpuMs))
                    sb.Append(" gpuLast=").Append(gpuMs.ToString("0.0")).Append(" ms");
            }
            sb.Append(" | xr: device=").Append(UnityEngine.XR.XRSettings.loadedDeviceName)
              .Append(" refresh=").Append(UnityEngine.XR.XRDevice.refreshRate.ToString("0"))
              .Append(" eyeTex=").Append(UnityEngine.XR.XRSettings.eyeTextureWidth).Append('x').Append(UnityEngine.XR.XRSettings.eyeTextureHeight)
              .Append(" resScale=").Append(UnityEngine.XR.XRSettings.eyeTextureResolutionScale.ToString("0.00"))
              .Append(" viewportScale=").Append(UnityEngine.XR.XRSettings.renderViewportScale.ToString("0.00"))
              .Append(" gameViewMirror=").Append(UnityEngine.XR.XRSettings.gameViewRenderMode)
              .Append(" vSync=").Append(QualitySettings.vSyncCount).Append(" targetFps=").Append(Application.targetFrameRate)
              .Append(" quality=").Append(QualitySettings.names[QualitySettings.GetQualityLevel()]);
            Debug.Log(sb.ToString());
            Destroy(gameObject);
        }
    }
}
