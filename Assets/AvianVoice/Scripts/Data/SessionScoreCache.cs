namespace AvianVoice.Data
{
    /// <summary>
    /// Running session scores for Reflection placeholder UI.
    /// </summary>
    public static class SessionScoreCache
    {
        public static float BreathingScore { get; private set; }
        public static float VoiceScore { get; private set; }
        public static float SynchronizationScore { get; private set; }

        public static void RecordBreathing(float score) =>
            BreathingScore = UnityEngine.Mathf.Max(BreathingScore, UnityEngine.Mathf.Clamp01(score));

        public static void RecordVoice(float score) =>
            VoiceScore = UnityEngine.Mathf.Max(VoiceScore, UnityEngine.Mathf.Clamp01(score));

        public static void RecordSynchronization(float score) =>
            SynchronizationScore = UnityEngine.Mathf.Max(SynchronizationScore, UnityEngine.Mathf.Clamp01(score));

        public static void Reset()
        {
            BreathingScore = 0f;
            VoiceScore = 0f;
            SynchronizationScore = 0f;
        }
    }
}
