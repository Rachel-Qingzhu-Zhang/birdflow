using System;

namespace AvianVoice.Data
{
    [Serializable]
    public class TrialLogEntry
    {
        public string timestamp;
        public string taskId;
        public string voiceMode;
        public float pitchHz;
        public float loudness;
        public float breathDepth;
        public float score;
        public float latencyMs;
    }
}
