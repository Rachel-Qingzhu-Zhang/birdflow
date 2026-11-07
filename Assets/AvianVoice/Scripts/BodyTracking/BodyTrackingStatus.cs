namespace AvianVoice.BodyTracking
{
    /// <summary>Low-level body tracker state for diagnostics (filled by the backend assembly).</summary>
    public struct BodyTrackingStatus
    {
        public bool HasData;
        public double Time;
        public float Confidence;
        public long SkeletonChangedCount;
        public string Calibration;
        public string Fidelity;
        public string JointSet;
        public int JointCount;
        public int ValidJoints;
        /// <summary>Joints whose position is actively tracked (the rest are inferred by the body model).</summary>
        public int TrackedJoints;
        /// <summary>The wrists' positions are actively tracked (hand tracking feeds the body tracker), not inferred.</summary>
        public bool LeftWristTracked;
        public bool RightWristTracked;

        /// <summary>The human avatar's own retargeter (null fields when there is none).</summary>
        public bool RetargeterPresent;
        public bool RetargeterEnabled;
        public bool RetargeterInitialized;
        public bool RetargeterDataValid;
        public long RetargeterSkeletonChangedCount;
    }

    /// <summary>Set by the backend assembly; lets Assembly-CSharp diagnostics read SDK-level state without an SDK reference.</summary>
    public static class BodyTrackingStatusSource
    {
        public delegate bool Getter(out BodyTrackingStatus status);

        public static Getter Provider;

        /// <summary>Debug: make the body tracker recalibrate to this height (m); 0 = reset to automatic. Returns whether the runtime accepted.</summary>
        public static System.Func<float, bool> SuggestCalibrationHeight;

        public static bool TryGet(out BodyTrackingStatus status)
        {
            status = default;
            return Provider != null && Provider(out status);
        }
    }
}
