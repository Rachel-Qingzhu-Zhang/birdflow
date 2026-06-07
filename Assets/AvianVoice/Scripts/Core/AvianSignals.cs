using System;
using UnityEngine;

namespace AvianVoice.Core
{
    /// <summary>
    /// Central event hub for voice, breath, and task signals.
    /// </summary>
    public class AvianSignals : MonoBehaviour
    {
        public static AvianSignals Instance { get; private set; }

        public event Action<VoiceFrame> OnVoiceFrame;
        public event Action<BreathFrame> OnBreathFrame;
        public event Action<VoiceMapMode> OnVoiceModeChanged;
        public event Action<string, float> OnTaskScore;

        [SerializeField]
        AvianVoiceSettings settings;

        public AvianVoiceSettings Settings => settings;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PublishVoice(VoiceFrame frame) => OnVoiceFrame?.Invoke(frame);

        public void PublishBreath(BreathFrame frame) => OnBreathFrame?.Invoke(frame);

        public void PublishVoiceMode(VoiceMapMode mode) => OnVoiceModeChanged?.Invoke(mode);

        public void PublishTaskScore(string taskId, float score) => OnTaskScore?.Invoke(taskId, score);
    }
}
