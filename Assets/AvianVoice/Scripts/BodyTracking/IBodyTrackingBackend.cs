using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// A source of tracked body joints (for example Meta Quest IOBT through the Meta XR Core SDK).
    /// Implementations live in their own assembly so the rest of the project compiles without the SDK.
    /// </summary>
    public interface IBodyTrackingBackend
    {
        string DisplayName { get; }

        /// <summary>True while the native tracker is running (permission granted, device supports it).</summary>
        bool IsRunning { get; }

        /// <summary>Fills <paramref name="frame"/> with the latest joints. Returns <c>frame.IsValid</c>.</summary>
        bool TryGetFrame(BodyPoseFrame frame);

        /// <summary>Ask the OS for the body tracking permission if it is not granted yet (Android).</summary>
        void RequestPermissions();

        /// <summary>
        /// Drive a Unity Humanoid rig from the tracked skeleton. The animator must be in T-pose when
        /// this is called. Returns null when the backend cannot retarget.
        /// </summary>
        IHumanoidRetargetingHandle AttachHumanoidRetargeting(Animator humanoid);
    }

    public interface IHumanoidRetargetingHandle
    {
        /// <summary>True when tracked data is currently being written to the humanoid bones.</summary>
        bool IsDriving { get; }

        void SetEnabled(bool enabled);

        void Dispose();
    }
}
