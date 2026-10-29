using System.Collections;
using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// Captures microphone input into a ring buffer for low-latency analysis.
    /// AudioSettings.Reset is required on Windows to restore speakers after Microphone.Start,
    /// but it also kills the clip — capture is restarted once after that reset.
    /// </summary>
    public class MicrophoneCapture : MonoBehaviour
    {
        const float DeadMicRestartSeconds = 1.25f;

        AvianVoiceSettings settings;
        VoiceAnalyzer analyzer;

        AudioClip clip;
        string deviceName;
        float[] readBuffer;
        int lastMicPosition;
        bool speakersRestoreDone;
        float lastPushedAt;
        float nextRestartAllowedAt;

        public bool IsCapturing { get; private set; }

        public void Initialize(AvianVoiceSettings voiceSettings)
        {
            settings = voiceSettings;
            analyzer = GetComponent<VoiceAnalyzer>();
            if (settings != null)
                readBuffer = new float[Mathf.Max(64, settings.frameSize)];
        }

        void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        }

        void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            StopCapture();
        }

        void Start()
        {
            StartCapture();
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            if (!isActiveAndEnabled || settings == null || !speakersRestoreDone)
                return;
            ScheduleRestart();
        }

        public void StartCapture()
        {
            if (IsCapturing || settings == null)
                return;

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[AvianVoice] No microphone devices found.");
                return;
            }

            deviceName = PickDevice();
            Microphone.GetDeviceCaps(deviceName, out var minFreq, out var maxFreq);
            var rate = settings.sampleRate > 0 ? settings.sampleRate : 48000;
            if (maxFreq > 0)
                rate = Mathf.Clamp(rate, Mathf.Max(11025, minFreq), maxFreq);

            clip = Microphone.Start(deviceName, true, Mathf.Max(1, settings.ringBufferSeconds), rate);
            lastMicPosition = 0;
            lastPushedAt = Time.unscaledTime;
            IsCapturing = clip != null;
            if (IsCapturing && !speakersRestoreDone)
                StartCoroutine(RestoreSpeakersAfterMicrophone());
        }

        static string PickDevice()
        {
            var devices = Microphone.devices;
            for (var i = 0; i < devices.Length; i++)
            {
                var n = devices[i];
                if (string.IsNullOrEmpty(n))
                    continue;
                var lower = n.ToLowerInvariant();
                if (lower.Contains("stereo mix") || lower.Contains("what u hear") || lower.Contains("loopback"))
                    continue;
                if (lower.Contains("headset") || lower.Contains("headphone") || lower.Contains("mic"))
                    return n;
            }

            return devices[0];
        }

        IEnumerator RestoreSpeakersAfterMicrophone()
        {
            var waited = 0f;
            while (waited < 1.5f && !string.IsNullOrEmpty(deviceName) && Microphone.GetPosition(deviceName) <= 0)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return null;
            var config = AudioSettings.GetConfiguration();
            AudioSettings.Reset(config);
            AvianSceneBootstrapHelper.EnsureHearing();
            speakersRestoreDone = true;
            StopCapture();
            StartCapture();
        }

        void ScheduleRestart()
        {
            if (Time.unscaledTime < nextRestartAllowedAt)
                return;
            nextRestartAllowedAt = Time.unscaledTime + 1.5f;
            StopCapture();
            StartCapture();
        }

        public void StopCapture()
        {
            if (!string.IsNullOrEmpty(deviceName) && Microphone.IsRecording(deviceName))
                Microphone.End(deviceName);

            IsCapturing = false;
            clip = null;
        }

        void Update()
        {
            if (settings == null)
                return;

            if (!IsCapturing || clip == null)
            {
                if (Time.unscaledTime >= nextRestartAllowedAt)
                    StartCapture();
                return;
            }

            var micPos = Microphone.GetPosition(deviceName);
            if (micPos < 0 || !Microphone.IsRecording(deviceName))
            {
                ScheduleRestart();
                return;
            }

            var clipSamples = clip.samples;
            if (clipSamples <= 0)
            {
                ScheduleRestart();
                return;
            }

            var available = micPos - lastMicPosition;
            if (available < 0)
                available += clipSamples;

            var frameSize = settings.frameSize;
            var hop = Mathf.Clamp(settings.analysisHopSize, 64, frameSize);
            if (available < hop)
            {
                if (Time.unscaledTime - lastPushedAt > DeadMicRestartSeconds)
                    ScheduleRestart();
                return;
            }

            var start = micPos - frameSize;
            if (start < 0)
                start += clipSamples;

            if (readBuffer == null || readBuffer.Length < frameSize)
                readBuffer = new float[frameSize];

            clip.GetData(readBuffer, start);
            if (analyzer != null)
                analyzer.PushSamples(readBuffer, frameSize);
            lastPushedAt = Time.unscaledTime;
            lastMicPosition = micPos - frameSize + hop;
            if (lastMicPosition < 0)
                lastMicPosition += clipSamples;
            else if (lastMicPosition >= clipSamples)
                lastMicPosition -= clipSamples;
        }
    }
}
