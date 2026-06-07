using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Audio
{
    /// <summary>
    /// Captures microphone input into a ring buffer for low-latency analysis.
    /// </summary>
    public class MicrophoneCapture : MonoBehaviour
    {
        AvianVoiceSettings settings;
        VoiceAnalyzer analyzer;

        AudioClip clip;
        string deviceName;
        float[] readBuffer;
        int lastMicPosition;

        public bool IsCapturing { get; private set; }

        public void Initialize(AvianVoiceSettings voiceSettings)
        {
            settings = voiceSettings;
            analyzer = GetComponent<VoiceAnalyzer>();
            readBuffer = new float[settings.frameSize];
        }

        void Start()
        {
            StartCapture();
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

            deviceName = Microphone.devices[0];
            clip = Microphone.Start(deviceName, true, settings.ringBufferSeconds, settings.sampleRate);
            lastMicPosition = 0;
            IsCapturing = true;
        }

        public void StopCapture()
        {
            if (!IsCapturing)
                return;

            if (!string.IsNullOrEmpty(deviceName))
                Microphone.End(deviceName);

            IsCapturing = false;
            clip = null;
        }

        void Update()
        {
            if (!IsCapturing || clip == null || analyzer == null)
                return;

            var micPos = Microphone.GetPosition(deviceName);
            if (micPos < 0)
                return;

            var clipSamples = clip.samples;
            var available = micPos - lastMicPosition;
            if (available < 0)
                available += clipSamples;

            var frameSize = settings.frameSize;
            var hop = Mathf.Clamp(settings.analysisHopSize, 64, frameSize);
            if (available < hop)
                return;

            var start = micPos - frameSize;
            if (start < 0)
            {
                lastMicPosition = micPos;
                return;
            }

            if (readBuffer == null || readBuffer.Length < frameSize)
                readBuffer = new float[frameSize];

            clip.GetData(readBuffer, start);
            analyzer.PushSamples(readBuffer, frameSize);
            lastMicPosition = micPos - frameSize + hop;
            if (lastMicPosition < 0)
                lastMicPosition += clipSamples;
        }
    }
}
