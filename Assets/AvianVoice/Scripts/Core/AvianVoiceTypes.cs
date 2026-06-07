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
    }

    public struct BreathFrame
    {
        public float depth;
        public float phase;
        public bool isInhaling;
    }
}
