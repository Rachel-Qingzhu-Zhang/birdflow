using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// The headset's (center eye) pose expressed in the body tracking space, supplied by the backend that also
    /// supplies the body joints (Meta: OVRPlugin node pose in the same tracking origin as OVRBody).
    /// Comparing it with the XR camera's world pose gives the exact body-tracking-space to world transform every
    /// frame, whatever recentering or reference-space differences there are between the body tracker and the XR
    /// Origin (the shared-space assumption broke at Quest recenters, and the head+hands solver that replaced it
    /// rotated the whole avatar with the noise of the hand positions).
    /// </summary>
    public static class HeadsetTrackingPose
    {
        public delegate bool Getter(out Pose headsetInTrackingSpace);

        /// <summary>Set by the backend assembly at startup; null when no backend can supply the pose.</summary>
        public static Getter Provider;

        /// <summary>Human-readable name of the provider (diagnostics).</summary>
        public static string ProviderName = "";

        /// <summary>Debug: ask the runtime to recenter the tracking origin (what the Quest does on a long press of the menu button).</summary>
        public static System.Func<bool> Recenter;

        /// <summary>
        /// Debug: switch the body tracker's reference origin (floor / stage / eye level) while running. The body joints and
        /// the headset pose move to another space while the XR camera does not: the same mismatch a Quest recenter makes.
        /// Returns the new origin name.
        /// </summary>
        public static System.Func<string> CycleTrackingOrigin;

        public static bool TryGet(out Pose headsetInTrackingSpace)
        {
            headsetInTrackingSpace = Pose.identity;
            return Provider != null && Provider(out headsetInTrackingSpace);
        }
    }
}
