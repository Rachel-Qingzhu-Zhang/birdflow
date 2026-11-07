#if UNITY_EDITOR
using AvianVoice.BodyTracking;
using AvianVoice.Embodiment.Avatars;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>Debug menus for the body tracking space alignment.</summary>
    static class AvianTrackingDebugMenu
    {
        /// <summary>Recenter like the Quest's long press: the avatar must stay on the participant (space=headset re-aligns).</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Recenter Headset (Play Mode)")]
        static void RecenterHeadset()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AvianVoice] Enter Play mode first.");
                return;
            }

            var ok = HeadsetTrackingPose.Recenter != null && HeadsetTrackingPose.Recenter();
            Debug.Log("[AvianVoice] Recenter requested: " + (ok ? "accepted" : "not available"));
        }

        /// <summary>Test: move the body tracker to another reference origin, as a recenter would (Floor <-> Eye level via OVRManager).</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Switch Body Tracking Origin (test, Play Mode)")]
        static void CycleOrigin()
        {
            if (!Application.isPlaying || HeadsetTrackingPose.CycleTrackingOrigin == null)
            {
                Debug.LogWarning("[AvianVoice] Enter Play mode first (Meta backend required).");
                return;
            }

            Debug.Log("[AvianVoice] Body tracking origin: " + HeadsetTrackingPose.CycleTrackingOrigin());
        }

        /// <summary>Test: make the body tracker recalibrate (the skeleton changes, as when a participant's height is learned).</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Force Body Calibration 1.60 m (test, Play Mode)")]
        static void Calibrate160() => SuggestHeight(1.60f);

        [MenuItem("Avian Voice/Embodiment/Debug/Force Body Calibration 1.85 m (test, Play Mode)")]
        static void Calibrate185() => SuggestHeight(1.85f);

        [MenuItem("Avian Voice/Embodiment/Debug/Reset Body Calibration (Play Mode)")]
        static void CalibrateReset() => SuggestHeight(0f);

        static void SuggestHeight(float height)
        {
            if (!Application.isPlaying || BodyTrackingStatusSource.SuggestCalibrationHeight == null)
            {
                Debug.LogWarning("[AvianVoice] Enter Play mode first (Meta backend required).");
                return;
            }

            var ok = BodyTrackingStatusSource.SuggestCalibrationHeight(height);
            Debug.Log("[AvianVoice] Body calibration " + (height > 0f ? "override " + height.ToString("0.00") + " m" : "reset") + ": " + (ok ? "accepted" : "refused"));
        }

        /// <summary>Tracking-space alignment and the tracked head versus the camera, now (the head error should stay ~0.1 m).</summary>
        [MenuItem("Avian Voice/Embodiment/Debug/Log Tracking Space (Play Mode)")]
        static void LogTrackingSpace()
        {
            var controller = Object.FindAnyObjectByType<AvatarEmbodimentController>();
            if (controller == null)
            {
                Debug.LogWarning("[AvianVoice] No AvatarEmbodimentController (Play Avian_Lab_Embodiment).");
                return;
            }

            var hasHeadset = HeadsetTrackingPose.TryGet(out var headset);
            var line = "[AvianVoice] Tracking space: mode=" + controller.TrackingSpaceMode + " t2w=" + controller.TrackingToWorld.position.ToString("F2")
                       + " yaw=" + controller.TrackingToWorld.rotation.eulerAngles.y.ToString("F1")
                       + " headsetInTracking=" + (hasHeadset ? headset.position.ToString("F2") : "n/a");
            if (controller.TrackingSpace != null)
                line += " xrOrigin(CameraOffset)=" + controller.TrackingSpace.position.ToString("F2") + " yaw=" + controller.TrackingSpace.eulerAngles.y.ToString("F1");
            if (controller.XrCamera != null && controller.TrackingToWorldValid && controller.Frame.Has(BodyJoint.Head))
            {
                var head = TrackingSpaceSolver.TransformPoint(controller.TrackingToWorld, controller.Frame[BodyJoint.Head].position);
                line += " headJointErr=" + Vector3.Distance(head, controller.XrCamera.transform.position).ToString("F3") + "m";
            }

            if (controller.XrCamera != null && hasHeadset && controller.TrackingToWorldValid)
            {
                var predicted = TrackingSpaceSolver.TransformPoint(controller.TrackingToWorld, headset.position);
                line += " headsetErr=" + Vector3.Distance(predicted, controller.XrCamera.transform.position).ToString("F3") + "m";
            }

            Debug.Log(line);
        }
    }
}
#endif
