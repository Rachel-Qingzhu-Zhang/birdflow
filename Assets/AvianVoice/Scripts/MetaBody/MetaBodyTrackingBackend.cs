#if AVIAN_META_XR_CORE
using System;
using System.Reflection;
using AvianVoice.BodyTracking;
using UnityEngine;

namespace AvianVoice.MetaBody
{
    /// <summary>
    /// Meta Quest body tracking (IOBT / full body when the headset supports it) through the Meta XR Core SDK
    /// (<see cref="OVRBody"/>). Runs on the Unity OpenXR plugin backend: the "Meta XR Feature" OpenXR feature
    /// must be enabled (menu: Avian Voice / Embodiment / Configure Meta Body Tracking).
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class MetaBodyTrackingBackend : MonoBehaviour, IBodyTrackingBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterSelf()
        {
            BodyTrackingBackendRegistry.Register(
                "Meta XR Core SDK (OVRBody / IOBT)",
                host => host.GetComponent<MetaBodyTrackingBackend>() ?? host.AddComponent<MetaBodyTrackingBackend>());
        }

        // BodyJoint -> OVRPlugin.BoneId (FullBody ids share the upper-body values below 70).
        static readonly OVRPlugin.BoneId[] JointIds =
        {
            OVRPlugin.BoneId.FullBody_Root,
            OVRPlugin.BoneId.FullBody_Hips,
            OVRPlugin.BoneId.FullBody_SpineLower,
            OVRPlugin.BoneId.FullBody_SpineMiddle,
            OVRPlugin.BoneId.FullBody_SpineUpper,
            OVRPlugin.BoneId.FullBody_Chest,
            OVRPlugin.BoneId.FullBody_Neck,
            OVRPlugin.BoneId.FullBody_Head,
            OVRPlugin.BoneId.FullBody_LeftShoulder,
            OVRPlugin.BoneId.FullBody_LeftArmUpper,
            OVRPlugin.BoneId.FullBody_LeftArmLower,
            OVRPlugin.BoneId.FullBody_LeftHandWrist,
            OVRPlugin.BoneId.FullBody_RightShoulder,
            OVRPlugin.BoneId.FullBody_RightArmUpper,
            OVRPlugin.BoneId.FullBody_RightArmLower,
            OVRPlugin.BoneId.FullBody_RightHandWrist,
            OVRPlugin.BoneId.FullBody_LeftUpperLeg,
            OVRPlugin.BoneId.FullBody_LeftLowerLeg,
            OVRPlugin.BoneId.FullBody_LeftFootAnkle,
            OVRPlugin.BoneId.FullBody_RightUpperLeg,
            OVRPlugin.BoneId.FullBody_RightLowerLeg,
            OVRPlugin.BoneId.FullBody_RightFootAnkle
        };

        OVRBody body;
        bool permissionHooked;
        bool loggedNoData;
        float nextStatusLog;
        bool lastHadData;

        /// <summary>Human-readable state for logs and on-screen hints.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("OVRBody=").Append(body == null ? "none" : body.isActiveAndEnabled ? "enabled" : "disabled");
            sb.Append(" xrApi=").Append(OVRPlugin.nativeXrApi);
            sb.Append(" supported=").Append(OVRPlugin.bodyTrackingSupported);
            sb.Append(" trackerEnabled=").Append(OVRPlugin.bodyTrackingEnabled);
            sb.Append(" inputFocus=").Append(OVRManager.hasInputFocus);
            sb.Append(" jointSet=").Append(OVRRuntimeSettings.GetRuntimeSettings()?.BodyTrackingJointSet);
            var state = body != null ? body.BodyState : null;
            if (state.HasValue)
            {
                var s = state.Value;
                var valid = 0;
                if (s.JointLocations != null)
                {
                    foreach (var j in s.JointLocations)
                    {
                        if (j.PositionValid && j.OrientationValid)
                            valid++;
                    }
                }

                sb.Append(" state: joints=").Append(s.JointLocations?.Length ?? 0)
                  .Append(" valid=").Append(valid)
                  .Append(" conf=").Append(s.Confidence.ToString("0.00"))
                  .Append(" set=").Append(s.JointSet)
                  .Append(" calib=").Append(s.CalibrationStatus)
                  .Append(" fidelity=").Append(s.Fidelity);
            }
            else
            {
                sb.Append(" state: none");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            sb.Append(" permission=").Append(OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.BodyTracking));
#endif
            return sb.ToString();
        }

        void Update()
        {
            // Periodic status while nothing arrives, and one line when data starts/stops.
            var hasData = body != null && body.isActiveAndEnabled && body.BodyState.HasValue;
            if (hasData != lastHadData)
            {
                lastHadData = hasData;
                Debug.Log("[AvianVoice] Body tracking " + (hasData ? "data started: " : "data stopped: ") + Describe());
                return;
            }

            if (hasData || Time.unscaledTime < nextStatusLog)
                return;

            nextStatusLog = Time.unscaledTime + 5f;
            Debug.Log("[AvianVoice] Body tracking waiting: " + Describe());
        }

        public string DisplayName => "Meta XR Core SDK (OVRBody / IOBT)";

        public bool IsRunning => body != null && body.isActiveAndEnabled;

        void Awake()
        {
            EnsureOvrManager();
            PreferFullBodyJointSet();

            body = GetComponent<OVRBody>();
            if (body == null)
                body = gameObject.AddComponent<OVRBody>();
            body.ProvidedSkeletonType = OVRPlugin.BodyJointSet.FullBody;
        }

        void OnEnable()
        {
            if (permissionHooked)
                return;

            OVRPermissionsRequester.PermissionGranted += OnPermissionGranted;
            permissionHooked = true;
        }

        void OnDestroy()
        {
            if (!permissionHooked)
                return;

            OVRPermissionsRequester.PermissionGranted -= OnPermissionGranted;
            permissionHooked = false;
        }

        void OnPermissionGranted(string permissionId)
        {
            if (permissionId != OVRPermissionsRequester.GetPermissionId(OVRPermissionsRequester.Permission.BodyTracking))
                return;

            Debug.Log("[AvianVoice] Body tracking permission granted; restarting OVRBody.");
            if (body == null)
                return;

            body.enabled = false;
            body.enabled = true;
        }

        public void RequestPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.BodyTracking))
                OVRPermissionsRequester.Request(new[] { OVRPermissionsRequester.Permission.BodyTracking });
#endif
        }

        public bool TryGetFrame(BodyPoseFrame frame)
        {
            frame.Clear();
            if (body == null || !body.isActiveAndEnabled)
                return false;

            var state = body.BodyState;
            if (!state.HasValue)
            {
                if (!loggedNoData)
                {
                    loggedNoData = true;
                    Debug.Log("[AvianVoice] OVRBody has no body state yet (no headset, no permission, or tracker still starting).");
                }
                return false;
            }

            var s = state.Value;
            var joints = s.JointLocations;
            if (joints == null || joints.Length == 0)
                return false;

            frame.Confidence = s.Confidence;
            frame.Time = s.Time;
            frame.IsFullBody = s.JointSet == OVRPlugin.BodyJointSet.FullBody
                               && joints.Length > (int)OVRPlugin.BoneId.FullBody_LeftUpperLeg;

            for (var i = 0; i < JointIds.Length; i++)
            {
                var id = (int)JointIds[i];
                if (id < 0 || id >= joints.Length)
                    continue;

                var location = joints[id];
                if (!location.PositionValid || !location.OrientationValid)
                    continue;

                frame.Set(
                    (BodyJoint)i,
                    location.Pose.Position.FromFlippedZVector3f(),
                    location.Pose.Orientation.FromFlippedZQuatf());
            }

            frame.IsValid = frame.JointValid[(int)BodyJoint.Head] && frame.JointValid[(int)BodyJoint.Hips];
            return frame.IsValid;
        }

        public IHumanoidRetargetingHandle AttachHumanoidRetargeting(Animator humanoid)
        {
            if (humanoid == null || humanoid.avatar == null || !humanoid.avatar.isHuman)
            {
                Debug.LogWarning("[AvianVoice] Humanoid retargeting needs an Animator with a Humanoid avatar.");
                return null;
            }

            var go = humanoid.gameObject;
            var wasActive = go.activeSelf;
            // Keep the object inactive while adding components so OVRSkeleton.Awake sees the final skeleton type.
            if (wasActive)
                go.SetActive(false);

            var provider = go.GetComponent<OVRBody>();
            if (provider == null)
                provider = go.AddComponent<OVRBody>();
            provider.ProvidedSkeletonType = OVRPlugin.BodyJointSet.FullBody;

            var retargeter = go.GetComponent<OVRUnityHumanoidSkeletonRetargeter>();
            if (retargeter == null)
                retargeter = go.AddComponent<OVRUnityHumanoidSkeletonRetargeter>();
            SetSkeletonType(retargeter, OVRSkeleton.SkeletonType.FullBody);

            if (wasActive)
                go.SetActive(true);

            return new Handle(provider, retargeter);
        }

        static void SetSkeletonType(OVRSkeleton skeleton, OVRSkeleton.SkeletonType type)
        {
            var field = typeof(OVRSkeleton).GetField("_skeletonType", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(skeleton, type);
            else
                Debug.LogWarning("[AvianVoice] Could not set OVRSkeleton._skeletonType (SDK layout changed?).");
        }

        static void EnsureOvrManager()
        {
            if (OVRManager.instance != null || FindAnyObjectByType<OVRManager>() != null)
                return;

            var go = new GameObject("OVRManager (Avian body tracking)");
            go.AddComponent<OVRManager>();
            Debug.Log("[AvianVoice] Added OVRManager for Meta body tracking.");
        }

        static void PreferFullBodyJointSet()
        {
            try
            {
                var settings = OVRRuntimeSettings.GetRuntimeSettings();
                if (settings == null)
                    return;

                if (settings.BodyTrackingJointSet != OVRPlugin.BodyJointSet.FullBody)
                {
                    settings.BodyTrackingJointSet = OVRPlugin.BodyJointSet.FullBody;
                    Debug.Log("[AvianVoice] OVRRuntimeSettings.BodyTrackingJointSet forced to FullBody for this session. " +
                              "Run Avian Voice / Embodiment / Configure Meta Body Tracking to persist it.");
                }

                if (settings.BodyTrackingFidelity != OVRPlugin.BodyTrackingFidelity2.High)
                    settings.BodyTrackingFidelity = OVRPlugin.BodyTrackingFidelity2.High;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AvianVoice] Could not read OVRRuntimeSettings: " + e.Message);
            }
        }

        sealed class Handle : IHumanoidRetargetingHandle
        {
            readonly OVRBody provider;
            readonly OVRUnityHumanoidSkeletonRetargeter retargeter;

            public Handle(OVRBody provider, OVRUnityHumanoidSkeletonRetargeter retargeter)
            {
                this.provider = provider;
                this.retargeter = retargeter;
            }

            public bool IsDriving =>
                retargeter != null && retargeter.isActiveAndEnabled && retargeter.IsInitialized && retargeter.IsDataValid;

            public void SetEnabled(bool enabled)
            {
                if (provider != null)
                    provider.enabled = enabled;
                if (retargeter != null)
                    retargeter.enabled = enabled;
            }

            public void Dispose()
            {
                if (retargeter != null)
                    Destroy(retargeter);
                if (provider != null)
                    Destroy(provider);
            }
        }
    }
}
#endif
