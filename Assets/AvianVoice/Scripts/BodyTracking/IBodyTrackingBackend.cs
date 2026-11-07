using System;
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
        /// Drive a Unity Humanoid rig from the tracked skeleton. <paramref name="beforeRestPoseCapture"/> is
        /// invoked right before the backend records the rig's rest pose (it must put the rig into T-pose).
        /// Returns null when the backend cannot retarget.
        /// </summary>
        IHumanoidRetargetingHandle AttachHumanoidRetargeting(Animator humanoid, Action beforeRestPoseCapture);
    }

    public interface IHumanoidRetargetingHandle
    {
        /// <summary>True when tracked data is currently being written to the humanoid bones.</summary>
        bool IsDriving { get; }

        /// <summary>True while the tracker delivers body data, whether or not it is being applied.</summary>
        bool HasTrackingData { get; }

        /// <summary>Start / stop the tracker and the retargeting together.</summary>
        void SetEnabled(bool enabled);

        /// <summary>
        /// Pause / resume writing to the bones without stopping the tracker. While paused the owner poses the
        /// rig itself (fallback pose); the SDK would otherwise keep writing stale joints.
        /// </summary>
        void SetRetargetingActive(bool active);

        /// <summary>Hand / finger retargeting state, with the live tracked-vs-avatar hand error (diagnostics).</summary>
        string DescribeHands();

        void Dispose();
    }
}
