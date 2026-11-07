using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AvianVoice.Audio;
using AvianVoice.Tasks;
using UnityEngine;

namespace AvianVoice.Study
{
    /// <summary>
    /// One timestamp in every time base the study uses, so audio and events can be aligned offline:
    /// wall clock (UTC and local), Unity real time, the audio DSP clock, and the absolute microphone
    /// sample index in the session microphone WAV.
    /// </summary>
    [Serializable]
    public struct StudyTime
    {
        public string utc;
        public string local;
        public double unity;
        public double dsp;
        public long micSample;
        public long outFrame;

        public bool IsSet => !string.IsNullOrEmpty(utc);

        public static StudyTime Now(StudyRecorder recorder)
        {
            var now = DateTime.Now;
            return new StudyTime
            {
                utc = now.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                local = now.ToString("o", CultureInfo.InvariantCulture),
                unity = Time.realtimeSinceStartupAsDouble,
                dsp = AudioSettings.dspTime,
                micSample = recorder != null ? recorder.EstimateMicSampleNow() : -1,
                outFrame = recorder != null ? recorder.OutputFramesWritten : -1
            };
        }

        /// <summary>Same instant moved back by <paramref name="seconds"/> (for onsets detected after a hold time).</summary>
        public StudyTime Shift(double seconds, int micRate, int outRate)
        {
            var t = this;
            t.unity -= seconds;
            t.dsp -= seconds;
            if (micSample >= 0 && micRate > 0)
                t.micSample = Math.Max(0, micSample - (long)Math.Round(seconds * micRate));
            if (outFrame >= 0 && outRate > 0)
                t.outFrame = Math.Max(0, outFrame - (long)Math.Round(seconds * outRate));
            if (DateTime.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                var shifted = parsed.AddSeconds(-seconds);
                t.utc = shifted.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
                t.local = shifted.ToLocalTime().ToString("o", CultureInfo.InvariantCulture);
            }

            return t;
        }
    }

    /// <summary>Minimal JSON object builder for one-line log records (no reflection, invariant culture).</summary>
    public sealed class JsonLine
    {
        readonly StringBuilder sb = new("{");
        bool first = true;

        public JsonLine Add(string key, string value)
        {
            Key(key);
            if (value == null)
                sb.Append("null");
            else
                sb.Append('"').Append(Escape(value)).Append('"');
            return this;
        }

        public JsonLine Add(string key, double value)
        {
            Key(key);
            if (double.IsNaN(value) || double.IsInfinity(value))
                sb.Append("null");
            else
                sb.Append(value.ToString("R", CultureInfo.InvariantCulture));
            return this;
        }

        public JsonLine Add(string key, long value)
        {
            Key(key);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
            return this;
        }

        public JsonLine Add(string key, bool value)
        {
            Key(key);
            sb.Append(value ? "true" : "false");
            return this;
        }

        public JsonLine Add(string key, StudyTime t)
        {
            Key(key);
            if (!t.IsSet)
            {
                sb.Append("null");
                return this;
            }

            sb.Append(new JsonLine()
                .Add("utc", t.utc).Add("local", t.local).Add("unity", t.unity).Add("dsp", t.dsp)
                .Add("mic_sample", t.micSample).Add("out_frame", t.outFrame));
            return this;
        }

        public JsonLine AddArray(string key, float[] values)
        {
            Key(key);
            sb.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(values[i].ToString("R", CultureInfo.InvariantCulture));
                }
            }

            sb.Append(']');
            return this;
        }

        public JsonLine AddArray(string key, int[] values)
        {
            Key(key);
            sb.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(values[i].ToString(CultureInfo.InvariantCulture));
                }
            }

            sb.Append(']');
            return this;
        }

        public JsonLine AddArray(string key, string[] values)
        {
            Key(key);
            sb.Append('[');
            if (values != null)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('"').Append(Escape(values[i] ?? "")).Append('"');
                }
            }

            sb.Append(']');
            return this;
        }

        void Key(string key)
        {
            if (!first)
                sb.Append(',');
            first = false;
            sb.Append('"').Append(Escape(key)).Append("\":");
        }

        public override string ToString() => sb + "}";

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            var b = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            b.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            b.Append(c);
                        break;
                }
            }

            return b.ToString();
        }
    }

    /// <summary>
    /// Session data for analysis. One folder per Play session under <see cref="RootDirectory"/>
    /// (<c>&lt;project&gt;/StudyData/&lt;date&gt;_&lt;participant&gt;/</c> in the Editor and on PC, ignored by git;
    /// <c>persistentDataPath/StudyData/...</c> on a headset build):
    /// <list type="bullet">
    /// <item><c>session.json</c>: who / when / devices / sample rates.</item>
    /// <item><c>events.jsonl</c>: every timestamped event (scene changes, avatar changes, trial phases, voice onsets).</item>
    /// <item><c>trials.csv</c>: one row per singing trial with all timestamps and summary measures.</item>
    /// <item><c>mic.wav</c>: the participant microphone for the whole session (mono, 16-bit).</item>
    /// <item><c>output.wav</c>: what the participant heard (mono downmix of the listener), same session span.</item>
    /// <item><c>segments/trial_NNN_*.wav</c>: the microphone cut per trial, from trial start to trial end.</item>
    /// </list>
    /// Every event carries the microphone sample index, so <c>mic.wav</c> can be sliced exactly offline.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class StudyRecorder : MonoBehaviour
    {
        public const string RootFolderName = "StudyData";
        const float RingSeconds = 120f;

        /// <summary>
        /// Where sessions are written: the StudyData folder next to Assets in the Editor / on PC (kept out of the
        /// repository by .gitignore and outside Assets so Unity never imports it), persistentDataPath on a device.
        /// </summary>
        public static string RootDirectory
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", RootFolderName));
#else
                return Path.Combine(Application.persistentDataPath, RootFolderName);
#endif
            }
        }

        [SerializeField]
        bool recordMicrophone = true;

        [SerializeField]
        bool recordOutput = true;

        [SerializeField]
        bool writeTrialSegments = true;

        public static StudyRecorder Instance { get; private set; }

        public bool SessionOpen { get; private set; }
        public string SessionDirectory { get; private set; } = "";
        public string SessionId { get; private set; } = "";
        public string ParticipantId { get; private set; } = "";
        public string CurrentTaskId { get; private set; } = "";
        public string CurrentAvatar { get; set; } = "";

        /// <summary>"bird" / "human" / "none": what the participant hears of their own voice (set by StudyFlowController).</summary>
        public string CurrentVoiceFeedback { get; set; } = "";
        public bool WriteTrialSegments => writeTrialSegments;
        public bool MicrophoneRecording => micWav != null && micWav.IsOpen;
        public bool OutputRecording => outWav != null && outWav.IsOpen;
        public int MicSampleRate => micRate;
        public int OutputSampleRate => outRate;
        public long MicSamplesWritten => micSamplesWritten;
        public long OutputFramesWritten => outFrames;
        public double MicSecondsWritten => micRate > 0 ? micSamplesWritten / (double)micRate : 0.0;
        public double OutputSecondsWritten => outRate > 0 ? outFrames / (double)outRate : 0.0;
        public int TrialRowsWritten { get; private set; }
        public string MicDeviceName => mic != null ? mic.DeviceName : "";

        MicrophoneCapture mic;
        AudioClip micClip;
        int micGeneration = -1;
        int micReadPos;
        int micRate;
        WavWriter micWav;
        long micSamplesWritten;
        float[] ring;

        ListenerOutputTap tap;
        WavWriter outWav;
        int outRate;
        long outFrames;
        float nextTapCheck;

        StreamWriter events;
        StreamWriter trials;
        TaskManager taskManager;
        float nextFlush;

        public static StudyRecorder Ensure()
        {
            if (Instance != null)
                return Instance;

            var existing = FindAnyObjectByType<StudyRecorder>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("StudyRecorder");
            return go.AddComponent<StudyRecorder>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void BeginSession(string participantId, string sceneName)
        {
            if (SessionOpen)
                return;

            ParticipantId = SanitizeId(participantId);
            SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + ParticipantId;
            SessionDirectory = Path.Combine(RootDirectory, SessionId);
            Directory.CreateDirectory(SessionDirectory);
            if (writeTrialSegments)
                Directory.CreateDirectory(Path.Combine(SessionDirectory, "segments"));

            var utf8 = new UTF8Encoding(false);
            events = new StreamWriter(Path.Combine(SessionDirectory, "events.jsonl"), false, utf8) { AutoFlush = true };
            trials = new StreamWriter(Path.Combine(SessionDirectory, "trials.csv"), false, utf8) { AutoFlush = true };
            trials.WriteLine(TrialRecord.CsvHeader);
            SessionOpen = true;

            mic = FindAnyObjectByType<MicrophoneCapture>();
            outRate = AudioSettings.outputSampleRate;
            if (recordOutput)
                EnsureOutputTap();

            taskManager = FindAnyObjectByType<TaskManager>();
            if (taskManager != null)
            {
                CurrentTaskId = taskManager.CurrentTaskId;
                taskManager.OnTaskChanged += OnTaskChanged;
            }

            WriteSessionInfo(sceneName);
            LogEvent("session_start", j => j.Add("scene", sceneName).Add("participant", ParticipantId));
            Debug.Log("[AvianVoice] Study session recording to " + SessionDirectory);
        }

        public void EndSession()
        {
            if (!SessionOpen)
                return;

            // A trial still running when the session ends (Play stopped, application quit) is closed first so its
            // trial_end event and trials.csv row are not lost: the recorder is destroyed before the task scripts.
            foreach (var task in FindObjectsByType<SingingTrialTask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                try
                {
                    task?.EndTrialNow("session_end");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[AvianVoice] Could not close the running trial at session end: " + e.Message);
                }
            }

            LogEvent("session_end", j => j
                .Add("mic_samples", micSamplesWritten)
                .Add("output_frames", outFrames)
                .Add("trial_rows", TrialRowsWritten));

            if (taskManager != null)
                taskManager.OnTaskChanged -= OnTaskChanged;
            if (tap != null)
                tap.Capturing = false;

            DrainOutput();
            micWav?.Close();
            outWav?.Close();
            micWav = null;
            outWav = null;
            events?.Dispose();
            trials?.Dispose();
            events = null;
            trials = null;
            SessionOpen = false;
            Debug.Log("[AvianVoice] Study session closed: " + SessionDirectory);
        }

        void OnDestroy()
        {
            EndSession();
            if (Instance == this)
                Instance = null;
        }

        void OnApplicationQuit() => EndSession();

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                micWav?.Flush();
                outWav?.Flush();
            }
        }

        void OnTaskChanged(string taskName, string instruction)
        {
            var id = taskManager != null ? taskManager.CurrentTaskId : "";
            CurrentTaskId = id;
            LogEvent("task_changed", j => j.Add("task_id", id).Add("task_name", taskName ?? ""));
        }

        void Update()
        {
            if (!SessionOpen)
                return;

            PullMicrophone();
            DrainOutput();

            if (recordOutput && Time.unscaledTime >= nextTapCheck)
            {
                nextTapCheck = Time.unscaledTime + 2f;
                EnsureOutputTap();
            }

            if (Time.unscaledTime >= nextFlush)
            {
                nextFlush = Time.unscaledTime + 5f;
                micWav?.Flush();
                outWav?.Flush();
            }
        }

        // ---- events / trials ----------------------------------------------------------------------

        public StudyTime Now() => StudyTime.Now(this);

        public void LogEvent(string type, Action<JsonLine> fill = null)
        {
            if (!SessionOpen || events == null)
                return;

            var t = Now();
            var j = new JsonLine()
                .Add("type", type)
                .Add("t_utc", t.utc)
                .Add("t_local", t.local)
                .Add("t_unity", t.unity)
                .Add("t_dsp", t.dsp)
                .Add("mic_sample", t.micSample)
                .Add("out_frame", t.outFrame)
                .Add("task", CurrentTaskId)
                .Add("avatar", CurrentAvatar)
                .Add("voice_feedback", CurrentVoiceFeedback);
            fill?.Invoke(j);
            events.WriteLine(j.ToString());
        }

        public void WriteTrial(TrialRecord record)
        {
            if (!SessionOpen || trials == null || record == null)
                return;

            trials.WriteLine(record.ToCsvRow());
            TrialRowsWritten++;
        }

        // ---- microphone ---------------------------------------------------------------------------

        /// <summary>Absolute microphone sample index of "now" (samples already on disk plus those still in the ring clip).</summary>
        public long EstimateMicSampleNow()
        {
            if (micWav == null)
                return -1;

            var pending = 0;
            if (micClip != null && mic != null && mic.IsCapturing && !string.IsNullOrEmpty(mic.DeviceName))
            {
                var pos = Microphone.GetPosition(mic.DeviceName);
                if (pos >= 0)
                {
                    pending = pos - micReadPos;
                    if (pending < 0)
                        pending += micClip.samples;
                }
            }

            return micSamplesWritten + pending;
        }

        void PullMicrophone()
        {
            if (!recordMicrophone)
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
                return;

            if (mic.ClipGeneration != micGeneration || clip != micClip)
            {
                micGeneration = mic.ClipGeneration;
                micClip = clip;
                micReadPos = 0;
                var rate = clip.frequency;
                if (micWav == null)
                {
                    micRate = rate;
                    micWav = new WavWriter(Path.Combine(SessionDirectory, "mic.wav"), rate, 1);
                    ring = new float[Mathf.Max(rate, Mathf.RoundToInt(rate * RingSeconds))];
                    LogEvent("mic_start", j => j.Add("device", device).Add("sample_rate", rate).Add("channels", clip.channels));
                }
                else
                {
                    // Capture restarted (device change / Windows audio reset). The WAV keeps its original rate;
                    // the event marks where continuity breaks so analysis can account for the gap.
                    LogEvent("mic_restart", j => j.Add("device", device).Add("sample_rate", rate).Add("file_rate", micRate));
                }
            }

            var pos = Microphone.GetPosition(device);
            if (pos < 0)
                return;

            var available = pos - micReadPos;
            if (available < 0)
                available += clip.samples;
            if (available <= 0)
                return;

            var channels = Mathf.Max(1, clip.channels);
            var firstCount = Mathf.Min(available, clip.samples - micReadPos);
            AppendMic(clip, micReadPos, firstCount, channels);
            if (available > firstCount)
                AppendMic(clip, 0, available - firstCount, channels);

            micReadPos = pos;
        }

        void AppendMic(AudioClip clip, int offsetSamples, int count, int channels)
        {
            if (count <= 0)
                return;

            var buffer = new float[count * channels];
            clip.GetData(buffer, offsetSamples);
            if (channels > 1)
            {
                var scale = 1f / channels;
                for (var n = 0; n < count; n++)
                {
                    var sum = 0f;
                    for (var c = 0; c < channels; c++)
                        sum += buffer[n * channels + c];
                    buffer[n] = sum * scale;
                }
            }

            micWav.Write(buffer, 0, count);
            if (ring != null)
            {
                var write = (int)(micSamplesWritten % ring.Length);
                for (var i = 0; i < count; i++)
                {
                    ring[write] = buffer[i];
                    write++;
                    if (write >= ring.Length)
                        write = 0;
                }
            }

            micSamplesWritten += count;
        }

        /// <summary>
        /// Cut [startSample, endSample) of the session microphone stream into its own WAV (from the in-memory
        /// ring, so only the last <see cref="RingSeconds"/> are available). Returns the path relative to the
        /// session folder, or null.
        /// </summary>
        public string WriteSegment(long startSample, long endSample, string fileName)
        {
            if (!SessionOpen || !writeTrialSegments || ring == null || micRate <= 0)
                return null;

            var oldest = Math.Max(0, micSamplesWritten - ring.Length);
            startSample = Math.Max(startSample, oldest);
            endSample = Math.Min(endSample, micSamplesWritten);
            if (endSample <= startSample)
                return null;

            var count = (int)(endSample - startSample);
            var buffer = new float[count];
            var read = (int)(startSample % ring.Length);
            for (var i = 0; i < count; i++)
            {
                buffer[i] = ring[read];
                read++;
                if (read >= ring.Length)
                    read = 0;
            }

            var relative = Path.Combine("segments", fileName);
            WavWriter.WriteFile(Path.Combine(SessionDirectory, relative), buffer, count, micRate, 1);
            return relative.Replace('\\', '/');
        }

        // ---- listener output ----------------------------------------------------------------------

        void EnsureOutputTap()
        {
            AudioListener listener = null;
            foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    listener = candidate;
                    break;
                }
            }

            if (listener == null)
                return;

            if (tap != null && tap.gameObject == listener.gameObject && tap.isActiveAndEnabled)
            {
                tap.Capturing = true;
                return;
            }

            if (tap != null)
            {
                tap.Capturing = false;
                DrainOutput();
            }

            tap = listener.GetComponent<ListenerOutputTap>() ?? listener.gameObject.AddComponent<ListenerOutputTap>();
            tap.Capturing = true;
            LogEvent("output_tap", j => j.Add("listener", listener.gameObject.name));
        }

        void DrainOutput()
        {
            if (tap == null)
                return;

            while (tap.TryDequeue(out var mono))
            {
                if (outWav == null)
                {
                    outRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
                    outWav = new WavWriter(Path.Combine(SessionDirectory, "output.wav"), outRate, 1);
                    LogEvent("output_start", j => j.Add("sample_rate", outRate));
                }

                outWav.Write(mono, 0, mono.Length);
                outFrames += mono.Length;
            }
        }

        // ---- session info -------------------------------------------------------------------------

        void WriteSessionInfo(string sceneName)
        {
            var config = AudioSettings.GetConfiguration();
            var j = new JsonLine()
                .Add("session_id", SessionId)
                .Add("participant", ParticipantId)
                .Add("started_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
                .Add("started_local", DateTime.Now.ToString("o", CultureInfo.InvariantCulture))
                .Add("scene", sceneName)
                .Add("unity_version", Application.unityVersion)
                .Add("platform", Application.platform.ToString())
                .Add("device", SystemInfo.deviceModel)
                .Add("xr_device", UnityEngine.XR.XRSettings.loadedDeviceName)
                .AddArray("microphone_devices", Microphone.devices)
                .Add("output_sample_rate", config.sampleRate)
                .Add("dsp_buffer_size", config.dspBufferSize)
                .Add("files", "events.jsonl, trials.csv, mic.wav (participant microphone, mono 16-bit), output.wav (listener mix, mono 16-bit), segments/trial_*.wav")
                .Add("time_bases", "t_utc/t_local: wall clock ISO-8601; t_unity: Time.realtimeSinceStartup (s); t_dsp: AudioSettings.dspTime (s); mic_sample: sample index into mic.wav; out_frame: frame index into output.wav");
            File.WriteAllText(Path.Combine(SessionDirectory, "session.json"), j.ToString(), new UTF8Encoding(false));
        }

        static string SanitizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return "P000";

            var b = new StringBuilder();
            foreach (var c in id.Trim())
                b.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return b.Length > 0 ? b.ToString() : "P000";
        }

        public string Describe()
        {
            if (!SessionOpen)
                return "not recording";
            return "dir=" + SessionDirectory
                   + " mic=" + (MicrophoneRecording ? MicSecondsWritten.ToString("0.0") + "s@" + micRate + "Hz (" + MicDeviceName + ")" : "none")
                   + " output=" + (OutputRecording ? OutputSecondsWritten.ToString("0.0") + "s@" + outRate + "Hz" : "none")
                   + " trials=" + TrialRowsWritten;
        }
    }
}
