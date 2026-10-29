namespace AvianVoice.Core
{
    public enum VoiceMapMode
    {
        Assistive,
        Transform
    }

    public struct VoiceFrame
    {
        public float pitchHz;
        public float loudness;
        public float voiced;
        public float latencyMs;
        /// <summary>YIN periodicity confidence of the raw estimate (0-1).</summary>
        public float pitchConfidence;
    }

    public struct BreathFrame
    {
        public float depth;
        public float phase;
        public bool isInhaling;
    }
}
