using System;
using System.Globalization;
using System.IO;
using System.Text;
using AvianVoice.BodyTracking;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Tracking health log for Avian_Lab_Embodiment: one row per second (and an extra row whenever something changes)
    /// written to StudyData/diagnostics/tracking_&lt;time&gt;.csv and mirrored as a short Console line on changes.
    /// Columns: time since Play, frame times, avatar, tracking-space mode and errors, body tracker state (data, how long
    /// the joints have not changed, confidence, calibration, fidelity, skeleton updates), the human retargeter, and
    /// how far the avatar's arms are from the tracked arms (degrees). Made to find out what changes when tracking
    /// "suddenly stops working" during a session.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class TrackingHealthMonitor : MonoBehaviour
    {
        const string Header =
            "t_s,frames,dt_mean_ms,dt_max_ms,avatar,space,headset_err_m,headjoint_err_m,"
            + "body_data,body_stale_ms,body_stale_max_ms,conf,calib,fidelity,jointset,joints_valid,skel_changes,"
            + "retarget_enabled,retarget_init,retarget_valid,retarget_skel_changes,human_tracked,eye_err_m,"
            + "arm_err_L_deg,arm_err_R_deg,forearm_err_L_deg,forearm_err_R_deg,torso_vs_avatar_yaw_deg,"
            + "headset_stale_ms,headjoint_err_max_m,headjoint_vs_camera_yaw_deg,skel_changes_this_row,"
            + "joints_tracked,wrist_tracked_L,wrist_tracked_R,xr_hand_L,xr_hand_R,controller_L,controller_R,"
            + "head_to_eye_trk_m,eye_trk_x,eye_trk_y,eye_trk_z,headjoint_trk_x,headjoint_trk_y,headjoint_trk_z,"
            + "root_trk_x,root_trk_y,root_trk_z,sample_age_ms,origin,event";

        AvatarEmbodimentController controller;
        StreamWriter writer;
        float startTime;
        float nextRow;
        int frames;
        float dtSum;
        float dtMax;
        double lastBodyTime = double.NaN;
        float lastBodyChangeAt;
        float staleMax;
        long skelChanges;
        long lastSkel = -1;
        long retargetSkelChanges;
        long lastRetargetSkel = -1;
        string lastKey = "";
        Animator humanAnimator;
        GameObject humanAnimatorRoot;
        Pose lastHeadset;
        bool hasLastHeadset;
        float lastHeadsetChangeAt;
        float headJointErrMax;
        long skelChangesAtLastRow;

        public static string FilePath { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            var controller = FindAnyObjectByType<AvatarEmbodimentController>();
            if (controller != null && controller.GetComponent<TrackingHealthMonitor>() == null)
                controller.gameObject.AddComponent<TrackingHealthMonitor>();
        }

        void Start()
        {
            controller = GetComponent<AvatarEmbodimentController>();
            startTime = Time.unscaledTime;
            try
            {
                var dir = Path.Combine(Study.StudyRecorder.RootDirectory, "diagnostics");
                Directory.CreateDirectory(dir);
                FilePath = Path.Combine(dir, "tracking_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
                writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };
                writer.WriteLine(Header);
                Debug.Log("[AvianVoice] Tracking health log: " + FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AvianVoice] Tracking health log not written: " + e.Message);
            }
        }

        void OnDestroy()
        {
            writer?.Dispose();
            writer = null;
        }

        void LateUpdate()
        {
            if (controller == null)
                return;

            var dt = Time.unscaledDeltaTime;
            frames++;
            dtSum += dt;
            dtMax = Mathf.Max(dtMax, dt);

            BodyTrackingStatusSource.TryGet(out var status);
            if (status.HasData)
            {
                if (status.Time != lastBodyTime)
                {
                    lastBodyTime = status.Time;
                    lastBodyChangeAt = Time.unscaledTime;
                }

                if (lastSkel >= 0 && status.SkeletonChangedCount != lastSkel)
                    skelChanges++;
                lastSkel = status.SkeletonChangedCount;
            }

            if (status.RetargeterPresent)
            {
                if (lastRetargetSkel >= 0 && status.RetargeterSkeletonChangedCount != lastRetargetSkel)
                    retargetSkelChanges++;
                lastRetargetSkel = status.RetargeterSkeletonChangedCount;
            }

            var stale = status.HasData ? (Time.unscaledTime - lastBodyChangeAt) * 1000f : 0f;
            staleMax = Mathf.Max(staleMax, stale);

            // Is the backend's headset pose (which the tracking-space alignment is built from) still updating?
            if (HeadsetTrackingPose.TryGet(out var headsetNow))
            {
                if (!hasLastHeadset || headsetNow.position != lastHeadset.position || headsetNow.rotation != lastHeadset.rotation)
                    lastHeadsetChangeAt = Time.unscaledTime;
                lastHeadset = headsetNow;
                hasLastHeadset = true;
            }

            var cameraNow = controller.XrCamera != null ? controller.XrCamera.transform : null;
            if (cameraNow != null && controller.TrackingToWorldValid && controller.Frame.Has(BodyJoint.Head))
                headJointErrMax = Mathf.Max(headJointErrMax, Vector3.Distance(
                    TrackingSpaceSolver.TransformPoint(controller.TrackingToWorld, controller.Frame[BodyJoint.Head].position), cameraNow.position));

            // A row when something discrete changes, and once per second.
            var key = controller.Current + "|" + controller.TrackingSpaceMode + "|" + status.HasData + "|" + status.Calibration + "|"
                      + status.LeftWristTracked + "|" + status.RightWristTracked + "|" + XrHandTracked(true) + "|" + XrHandTracked(false) + "|"
                      + ControllerTracked(UnityEngine.XR.XRNode.LeftHand) + "|" + ControllerTracked(UnityEngine.XR.XRNode.RightHand) + "|"
                      + status.Fidelity + "|" + status.JointSet + "|" + status.RetargeterEnabled + "|" + status.RetargeterDataValid
                      + "|" + (stale > 250f);
            var changed = key != lastKey;
            if (!changed && Time.unscaledTime < nextRow)
                return;

            var evt = changed && lastKey.Length > 0 ? "change" : "";
            lastKey = key;
            nextRow = Time.unscaledTime + 1f;
            WriteRow(status, stale, evt);
            if (evt.Length > 0)
                Debug.Log("[AvianVoice] Tracking health change at " + (Time.unscaledTime - startTime).ToString("0.0", CultureInfo.InvariantCulture)
                          + " s: avatar=" + controller.Current + " space=" + controller.TrackingSpaceMode + " data=" + status.HasData
                          + " stale=" + stale.ToString("0") + "ms calib=" + status.Calibration + " fidelity=" + status.Fidelity
                          + " set=" + status.JointSet + " retarget=" + (status.RetargeterPresent ? status.RetargeterEnabled + "/" + status.RetargeterDataValid : "-")
                          + " wristsTracked=" + status.LeftWristTracked + "/" + status.RightWristTracked
                          + " xrHands=" + XrHandTracked(true) + "/" + XrHandTracked(false)
                          + " controllers=" + ControllerTracked(UnityEngine.XR.XRNode.LeftHand) + "/" + ControllerTracked(UnityEngine.XR.XRNode.RightHand));

            frames = 0;
            dtSum = 0f;
            dtMax = 0f;
            staleMax = 0f;
            headJointErrMax = 0f;
            skelChangesAtLastRow = skelChanges;
        }

        void WriteRow(BodyTrackingStatus status, float stale, string evt)
        {
            if (writer == null)
                return;

            var c = CultureInfo.InvariantCulture;
            var cam = controller.XrCamera != null ? controller.XrCamera.transform : null;
            var headsetErr = float.NaN;
            var headJointErr = float.NaN;
            if (cam != null && controller.TrackingToWorldValid)
            {
                if (HeadsetTrackingPose.TryGet(out var headset))
                    headsetErr = Vector3.Distance(TrackingSpaceSolver.TransformPoint(controller.TrackingToWorld, headset.position), cam.position);
                if (controller.Frame.Has(BodyJoint.Head))
                    headJointErr = Vector3.Distance(TrackingSpaceSolver.TransformPoint(controller.TrackingToWorld, controller.Frame[BodyJoint.Head].position), cam.position);
            }

            var humanTracked = false;
            var eyeErr = float.NaN;
            float armL = float.NaN, armR = float.NaN, foreL = float.NaN, foreR = float.NaN;
            var torsoVsAvatar = float.NaN;
            if (controller.Current == AvatarKind.Human && controller.HumanRoot != null)
            {
                var driver = controller.HumanRoot.GetComponent<HumanFirstPersonDriver>();
                if (driver != null)
                {
                    humanTracked = driver.IsTracked;
                    eyeErr = driver.EyeError;
                }

                if (humanAnimatorRoot != controller.HumanRoot)
                {
                    humanAnimatorRoot = controller.HumanRoot;
                    humanAnimator = controller.HumanRoot.GetComponent<Animator>();
                }

                if (humanAnimator != null && humanAnimator.isHuman && controller.TrackingToWorldValid)
                {
                    armL = SegmentError(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, BodyJoint.LeftUpperArm, BodyJoint.LeftLowerArm);
                    armR = SegmentError(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, BodyJoint.RightUpperArm, BodyJoint.RightLowerArm);
                    foreL = SegmentError(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, BodyJoint.LeftLowerArm, BodyJoint.LeftWrist);
                    foreR = SegmentError(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, BodyJoint.RightLowerArm, BodyJoint.RightWrist);
                }
            }

            if (controller.TryGetTorsoYawWorld(out var torsoYaw))
            {
                Transform avatarRoot = controller.Current == AvatarKind.Bird ? controller.BirdRoot
                    : controller.HumanRoot != null ? controller.HumanRoot.transform : null;
                Transform chest = null;
                if (controller.Current == AvatarKind.Human && humanAnimator != null)
                    chest = humanAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? humanAnimator.GetBoneTransform(HumanBodyBones.Spine);
                var reference = chest != null ? chest : avatarRoot;
                if (reference != null)
                    torsoVsAvatar = Mathf.DeltaAngle(torsoYaw, TrackingSpaceSolver.YawOf(reference.forward));
            }

            var sb = new StringBuilder();
            sb.Append((Time.unscaledTime - startTime).ToString("0.00", c)).Append(',')
              .Append(frames).Append(',')
              .Append(frames > 0 ? (dtSum / frames * 1000f).ToString("0.0", c) : "").Append(',')
              .Append((dtMax * 1000f).ToString("0.0", c)).Append(',')
              .Append(controller.Current == AvatarKind.Human ? "Human:" + controller.HumanAvatarId : "Bird").Append(',')
              .Append(controller.TrackingSpaceMode).Append(',')
              .Append(F(headsetErr, "0.000")).Append(',').Append(F(headJointErr, "0.000")).Append(',')
              .Append(status.HasData).Append(',').Append(stale.ToString("0", c)).Append(',').Append(staleMax.ToString("0", c)).Append(',')
              .Append(status.Confidence.ToString("0.00", c)).Append(',').Append(status.Calibration).Append(',').Append(status.Fidelity).Append(',')
              .Append(status.JointSet).Append(',').Append(status.ValidJoints).Append('/').Append(status.JointCount).Append(',')
              .Append(skelChanges).Append(',')
              .Append(status.RetargeterPresent ? status.RetargeterEnabled.ToString() : "").Append(',')
              .Append(status.RetargeterPresent ? status.RetargeterInitialized.ToString() : "").Append(',')
              .Append(status.RetargeterPresent ? status.RetargeterDataValid.ToString() : "").Append(',')
              .Append(status.RetargeterPresent ? retargetSkelChanges.ToString(c) : "").Append(',')
              .Append(humanTracked).Append(',').Append(F(eyeErr, "0.000")).Append(',')
              .Append(F(armL, "0.0")).Append(',').Append(F(armR, "0.0")).Append(',')
              .Append(F(foreL, "0.0")).Append(',').Append(F(foreR, "0.0")).Append(',')
              .Append(F(torsoVsAvatar, "0.0")).Append(',')
              .Append(hasLastHeadset ? ((Time.unscaledTime - lastHeadsetChangeAt) * 1000f).ToString("0", c) : "").Append(',')
              .Append(headJointErrMax.ToString("0.000", c)).Append(',')
              .Append(F(HeadJointVsCameraYaw(), "0.0")).Append(',')
              .Append(skelChanges - skelChangesAtLastRow).Append(',')
              .Append(status.TrackedJoints).Append(',')
              .Append(status.LeftWristTracked).Append(',').Append(status.RightWristTracked).Append(',')
              .Append(XrHandTracked(true)).Append(',').Append(XrHandTracked(false)).Append(',')
              .Append(ControllerTracked(UnityEngine.XR.XRNode.LeftHand)).Append(',').Append(ControllerTracked(UnityEngine.XR.XRNode.RightHand)).Append(',')
              .Append(F(status.HeadToEye, "0.000")).Append(',')
              .Append(V(status.EyeTracking, status.HasEye)).Append(',')
              .Append(V(status.HeadJointTracking, status.HasData)).Append(',')
              .Append(V(status.RootTracking, status.HasData)).Append(',')
              .Append(status.HasData ? status.SampleAgeMs.ToString("0", c) : "").Append(',')
              .Append(status.TrackingOrigin).Append(',')
              .Append(evt);
            writer.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Yaw between the tracked head joint (through the tracking-space alignment) and the camera. Constant while the
        /// alignment is right; if it swings when the participant turns, the alignment is wrong.
        /// </summary>
        static string V(Vector3 v, bool valid)
        {
            if (!valid)
                return ",,";
            var c = CultureInfo.InvariantCulture;
            return v.x.ToString("0.000", c) + "," + v.y.ToString("0.000", c) + "," + v.z.ToString("0.000", c);
        }

        float HeadJointVsCameraYaw()
        {
            var cam = controller.XrCamera != null ? controller.XrCamera.transform : null;
            if (cam == null || !controller.TrackingToWorldValid || !controller.Frame.Has(BodyJoint.Head))
                return float.NaN;
            var head = controller.TrackingToWorld.rotation * controller.Frame[BodyJoint.Head].rotation;
            return Mathf.DeltaAngle(TrackingSpaceSolver.YawOf(cam.forward), TrackingSpaceSolver.YawOf(head * Vector3.forward));
        }

        readonly System.Collections.Generic.List<UnityEngine.XR.Hands.XRHandSubsystem> handSubsystems = new();

        /// <summary>Whether Unity's XR Hands reports this hand as tracked (the participant's hands seen by the headset).</summary>
        bool XrHandTracked(bool left)
        {
            handSubsystems.Clear();
            SubsystemManager.GetSubsystems(handSubsystems);
            foreach (var subsystem in handSubsystems)
            {
                if (subsystem == null || !subsystem.running)
                    continue;
                return left ? subsystem.leftHand.isTracked : subsystem.rightHand.isTracked;
            }

            return false;
        }

        /// <summary>
        /// Whether a Quest controller is tracked on this side. When the runtime switches from hands to controllers (a
        /// controller on the desk gets bumped), the body tracker takes the wrists from the controllers.
        /// </summary>
        static bool ControllerTracked(UnityEngine.XR.XRNode node)
        {
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid || (device.characteristics & UnityEngine.XR.InputDeviceCharacteristics.Controller) == 0)
                return false;
            return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
        }

        /// <summary>Angle between the avatar's bone segment and the tracked one, both in world space.</summary>
        float SegmentError(HumanBodyBones avatarFrom, HumanBodyBones avatarTo, BodyJoint trackedFrom, BodyJoint trackedTo)
        {
            var a = humanAnimator.GetBoneTransform(avatarFrom);
            var b = humanAnimator.GetBoneTransform(avatarTo);
            var frame = controller.Frame;
            if (a == null || b == null || !frame.Has(trackedFrom) || !frame.Has(trackedTo))
                return float.NaN;

            var t2w = controller.TrackingToWorld;
            var tracked = TrackingSpaceSolver.TransformPoint(t2w, frame[trackedTo].position) - TrackingSpaceSolver.TransformPoint(t2w, frame[trackedFrom].position);
            var avatar = b.position - a.position;
            if (tracked.sqrMagnitude < 1e-6f || avatar.sqrMagnitude < 1e-6f)
                return float.NaN;
            return Vector3.Angle(tracked, avatar);
        }

        static string F(float v, string format) => float.IsNaN(v) ? "" : v.ToString(format, CultureInfo.InvariantCulture);
    }
}
