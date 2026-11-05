namespace AvianVoice.Core
{
    public enum VoiceMapMode
    {
        Assistive,
        Transform
    }

    /// <summary>
    /// What the participant hears of their own voice: bird chirps (bird avatar), a sung human vowel
    /// (human avatar), or nothing (only their natural voice).
    /// </summary>
    public enum VoiceFeedback
    {
        Bird,
        Human,
        None
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
