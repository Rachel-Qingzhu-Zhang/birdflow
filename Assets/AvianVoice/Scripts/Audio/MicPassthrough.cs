using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// The participant's own microphone, heard as it is: no analysis, no synthesis. The feedback voice of the
    /// human-avatar condition since 2026-11-12 (before: <see cref="HumanVoiceSynth"/>).
    /// An AudioSource plays the live microphone ring clip (<see cref="MicrophoneCapture.Clip"/>) a fixed distance
    /// behind the microphone's write position (AvianVoiceSettings.micPassthroughLatencyMs). The microphone and
    /// the output run on different clocks, so the distance is measured every frame and held by trimming the
    /// playback rate by at most 0.3 % (5 cents); only when the play head is far off (or has passed the write
    /// head) it jumps back, which is counted in <see cref="Resyncs"/> and logged.
    /// The source keeps playing while the condition is active and only the gain opens and closes
    /// (<see cref="Run"/>), so the voice is there at once when a response window opens.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MicPassthrough : MonoBehaviour
    {
        const float HardResyncExtraMs = 120f;
        const float ResyncCooldownSeconds = 0.5f;
        const float MaxRateTrim = 0.003f;
        const float RateTrimPerMs = 0.0002f;
        const float LagSmoothingSeconds = 1f;
        const float GainRampSeconds = 0.005f;

        AvianVoiceSettings settings;
        MicrophoneCapture mic;
        AudioSource source;
        AudioClip boundClip;
        int boundGeneration = -1;
        bool running;
        bool open;
        float smoothedLagMs;
        float nextResyncAllowedAt;
        float traceMin = float.MaxValue;
        float traceMax = float.MinValue;
        float traceSum;
        int traceCount;
        float nextTraceAt;

        // Main thread -> audio thread.
        volatile float targetGain;
        volatile int outputRate = 48000;

        // Audio thread state.
        float gain;

        /// <summary>True while the source plays the microphone (audible or not).</summary>
        public bool IsRunning => running && source != null && source.isPlaying;

        /// <summary>True while the microphone is audible.</summary>
        public bool IsOpen => IsRunning && open;

        /// <summary>Distance between the microphone's write position and the play head, last frame (ms).</summary>
        public float LagMs { get; private set; }

        /// <summary>The same, smoothed over about a second (what the rate trim steers to the target).</summary>
        public float SmoothedLagMs => smoothedLagMs;

        public float TargetLagMs => settings != null ? Mathf.Clamp(settings.micPassthroughLatencyMs, 10f, 200f) : 30f;

        /// <summary>Jumps of the play head since the start (each one is an audible discontinuity when open).</summary>
        public int Resyncs { get; private set; }

        public static MicPassthrough Ensure(Transform parent, AudioSource template, AvianVoiceSettings voiceSettings, MicrophoneCapture capture)
        {
            if (parent == null)
                return null;

            var existing = parent.GetComponentInChildren<MicPassthrough>(true);
            if (existing != null)
            {
                existing.settings = voiceSettings;
                if (capture != null)
                    existing.mic = capture;
                return existing;
            }

            var go = new GameObject("MicPassthrough");
            go.transform.SetParent(parent, false);
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.clip = null;
            audio.spatialBlend = 0f;
            audio.spatialize = false;
            audio.dopplerLevel = 0f;
            audio.volume = 1f;
            if (template != null)
                audio.outputAudioMixerGroup = template.outputAudioMixerGroup;

            var passthrough = go.AddComponent<MicPassthrough>();
            passthrough.settings = voiceSettings;
            passthrough.mic = capture;
            return passthrough;
        }

        /// <summary>
        /// Call every frame while the condition is active: keeps the microphone playing and makes it audible
        /// (<paramref name="audible"/>) or silent.
        /// </summary>
        public void Run(bool audible)
        {
            running = true;
            var level = audible && settings != null ? Mathf.Clamp(settings.micPassthroughGain, 0f, 4f) : audible ? 1f : 0f;
            targetGain = level;
            if (audible == open)
                return;

            open = audible;
            LogState(audible ? "open" : "close");
        }

        /// <summary>The condition ended: stop playing the microphone.</summary>
        public void Stop()
        {
            if (!running && boundClip == null)
                return;

            if (open)
                LogState("close");
            running = false;
            open = false;
            targetGain = 0f;
            boundClip = null;
            boundGeneration = -1;
            if (source != null)
            {
                source.Stop();
                source.clip = null;
            }
        }

        void Awake()
        {
            source = GetComponent<AudioSource>();
            ReadOutputRate();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        }

        void OnDisable()
        {
            Stop();
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            // The reset stops every source and restarts the microphone; Update binds to the new clip.
            ReadOutputRate();
        }

        void ReadOutputRate()
        {
            outputRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
        }

        void Update()
        {
            if (!running)
                return;

            if (mic == null)
            {
                mic = FindAnyObjectByType<MicrophoneCapture>();
                if (mic == null)
                    return;
            }

            var clip = mic.Clip;
            var device = mic.DeviceName;
            if (clip == null || !mic.IsCapturing || string.IsNullOrEmpty(device))
            {
                // Capture is restarting (device change, Windows audio reset): silent until the new clip is there.
                if (boundClip != null)
                {
                    source.Stop();
                    source.clip = null;
                    boundClip = null;
                }

                return;
            }

            var pos = Microphone.GetPosition(device);
            var samples = clip.samples;
            var rate = clip.frequency;
            if (pos < 0 || samples <= 0 || rate <= 0)
                return;

            var targetMs = TargetLagMs;
            var targetSamples = Mathf.Clamp(Mathf.RoundToInt(targetMs * rate / 1000f), 1, samples - 1);

            if (clip != boundClip || mic.ClipGeneration != boundGeneration || !source.isPlaying)
            {
                // A new clip starts as silence, so playing "behind" position 0 is harmless; wait only until
                // the device delivers samples at all.
                if (pos <= 0)
                    return;

                // Same clip but the source is not playing (audio reset in progress, audio disabled): retry at
                // most twice a second instead of every frame.
                var sameClip = clip == boundClip && mic.ClipGeneration == boundGeneration;
                if (sameClip && Time.unscaledTime < nextResyncAllowedAt)
                    return;

                boundClip = clip;
                boundGeneration = mic.ClipGeneration;
                source.clip = clip;
                source.loop = true;
                source.pitch = 1f;
                source.timeSamples = Wrap(pos - targetSamples, samples);
                source.Play();
                smoothedLagMs = targetMs;
                LagMs = targetMs;
                nextResyncAllowedAt = Time.unscaledTime + ResyncCooldownSeconds;
                LogState("start");
                return;
            }

            var lag = Wrap(pos - source.timeSamples, samples);
            var lagMs = lag * 1000f / rate;
            LagMs = lagMs;

            // Far behind, or the play head passed the write head (then the distance reads as almost one ring
            // and the source is playing audio that is one ring old): jump back to the target.
            if (lagMs > targetMs + HardResyncExtraMs)
            {
                if (Time.unscaledTime < nextResyncAllowedAt)
                    return;

                source.pitch = 1f;
                source.timeSamples = Wrap(pos - targetSamples, samples);
                smoothedLagMs = targetMs;
                nextResyncAllowedAt = Time.unscaledTime + ResyncCooldownSeconds;
                Resyncs++;
                var passed = lag > samples / 2;
                Debug.LogWarning("[AvianVoice] Mic passthrough resync #" + Resyncs + ": " + (passed ? "the play head passed the microphone" : "the play head fell behind")
                                 + " (distance " + lagMs.ToString("0") + " ms, target " + targetMs.ToString("0") + " ms)."
                                 + (passed ? " If this repeats, raise AvianVoiceSettings.micPassthroughLatencyMs." : ""));
                LogState(passed ? "resync_passed" : "resync_behind", lagMs);
                return;
            }

            var dt = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f;
            smoothedLagMs = Mathf.Lerp(smoothedLagMs, lagMs, 1f - Mathf.Exp(-dt / LagSmoothingSeconds));
            source.pitch = 1f + Mathf.Clamp((smoothedLagMs - targetMs) * RateTrimPerMs, -MaxRateTrim, MaxRateTrim);

            Trace(lagMs);
        }

        /// <summary>
        /// While the microphone is audible, one "mic_passthrough_trace" event per second: the distance the
        /// participant's voice was played behind the microphone (min / max / mean over the frames of that second).
        /// The distance steps up when the output device stalls for a moment (seen as about +16 ms every ~13 s in
        /// the editor on 2026-11-12) and is trimmed back to the target over the next seconds.
        /// </summary>
        void Trace(float lagMs)
        {
            traceMin = Mathf.Min(traceMin, lagMs);
            traceMax = Mathf.Max(traceMax, lagMs);
            traceSum += lagMs;
            traceCount++;
            if (Time.unscaledTime < nextTraceAt)
                return;

            nextTraceAt = Time.unscaledTime + 1f;
            var lo = traceMin;
            var hi = traceMax;
            var mean = traceSum / Mathf.Max(1, traceCount);
            var frames = traceCount;
            traceMin = float.MaxValue;
            traceMax = float.MinValue;
            traceSum = 0f;
            traceCount = 0;
            if (!open)
                return;

            var rateTrim = source.pitch - 1f;
            AvianVoice.Study.StudyRecorder.Instance?.LogEvent("mic_passthrough_trace", j => j
                .Add("lag_min_ms", lo).Add("lag_max_ms", hi).Add("lag_mean_ms", mean).Add("lag_smoothed_ms", smoothedLagMs)
                .Add("target_ms", TargetLagMs).Add("rate_trim", rateTrim).Add("frames", frames));
        }

        static int Wrap(int value, int length)
        {
            value %= length;
            return value < 0 ? value + length : value;
        }

        void LogState(string phase, float lagAtEventMs = -1f)
        {
            var recorder = AvianVoice.Study.StudyRecorder.Instance;
            if (recorder == null)
                return;

            var device = mic != null ? mic.DeviceName : "";
            var clipRate = boundClip != null ? boundClip.frequency : 0;
            var lagMs = lagAtEventMs >= 0f ? lagAtEventMs : LagMs;
            recorder.LogEvent("mic_passthrough", j => j
                .Add("phase", phase)
                .Add("target_ms", TargetLagMs)
                .Add("lag_ms", lagMs)
                .Add("lag_smoothed_ms", smoothedLagMs)
                .Add("gain", settings != null ? settings.micPassthroughGain : 1f)
                .Add("resyncs", Resyncs)
                .Add("device", device ?? "")
                .Add("clip_rate", clipRate));
        }

        /// <summary>Gate and gain on the source's own output, ramped per sample so opening and closing do not click.</summary>
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels <= 0)
                return;

            var goal = targetGain;
            var g = gain;
            if (goal <= 0f && g < 0.00001f)
            {
                System.Array.Clear(data, 0, data.Length);
                gain = 0f;
                return;
            }

            var k = 1f - Mathf.Exp(-1f / (GainRampSeconds * outputRate));
            var frames = data.Length / channels;
            for (var n = 0; n < frames; n++)
            {
                g += (goal - g) * k;
                var baseIndex = n * channels;
                for (var c = 0; c < channels; c++)
                    data[baseIndex + c] *= g;
            }

            gain = g;
        }
    }
}
