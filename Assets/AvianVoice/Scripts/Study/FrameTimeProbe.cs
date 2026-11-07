using System.Collections.Generic;
using UnityEngine;

namespace AvianVoice.Study
{
    /// <summary>
    /// Debug: samples the frame time for a few seconds and logs mean / 95th percentile / worst frame and the
    /// number of hitches, to tell a smooth session from a stuttering one (menu: Debug / Log Frame Times).
    /// </summary>
    public sealed class FrameTimeProbe : MonoBehaviour
    {
        public float seconds = 3f;
        public float hitchMs = 25f;
        readonly List<float> samples = new();
        float elapsed;

        public static void Run(float seconds = 3f)
        {
            var probe = new GameObject("FrameTimeProbe").AddComponent<FrameTimeProbe>();
            probe.seconds = seconds;
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            if (elapsed > 0f)
                samples.Add(dt * 1000f);
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
            Debug.Log("[AvianVoice] Frame times over " + seconds + " s: " + count + " frames, mean " + mean.ToString("0.0") + " ms ("
                      + (mean > 0f ? (1000f / mean).ToString("0") : "-") + " fps), p95 " + p95.ToString("0.0") + " ms, worst " + max.ToString("0.0")
                      + " ms, hitches > " + hitchMs + " ms: " + hitches);
            Destroy(gameObject);
        }
    }
}
