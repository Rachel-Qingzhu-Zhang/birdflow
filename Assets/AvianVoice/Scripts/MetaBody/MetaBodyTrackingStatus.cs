#if AVIAN_META_XR_CORE
using AvianVoice.BodyTracking;
using UnityEngine;

namespace AvianVoice.MetaBody
{
    /// <summary>Supplies <see cref="BodyTrackingStatusSource"/> from the backend's OVRBody and the human avatar's retargeter.</summary>
    static class MetaBodyTrackingStatus
    {
        static MetaBodyTrackingBackend backend;
        static AvianHumanoidRetargeter retargeter;
        static float nextLookup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            BodyTrackingStatusSource.Provider = TryGet;
            BodyTrackingStatusSource.SuggestCalibrationHeight = height =>
                height > 0f ? OVRBody.SuggestBodyTrackingCalibrationOverride(height) : OVRBody.ResetBodyTrackingCalibration();
            backend = null;
            retargeter = null;
            nextLookup = 0f;
        }

        static bool TryGet(out BodyTrackingStatus status)
        {
            status = default;
            if ((backend == null || retargeter == null) && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 1f;
                if (backend == null)
                    backend = Object.FindAnyObjectByType<MetaBodyTrackingBackend>();
                if (retargeter == null)
                    retargeter = Object.FindAnyObjectByType<AvianHumanoidRetargeter>(FindObjectsInactive.Include);
            }

            var body = backend != null ? backend.GetComponent<OVRBody>() : null;
            if (body == null)
                return false;

            var state = body.isActiveAndEnabled ? body.BodyState : null;
            status.HasData = state.HasValue;
            if (state.HasValue)
            {
                var s = state.Value;
                status.Time = s.Time;
                status.Confidence = s.Confidence;
                status.SkeletonChangedCount = s.SkeletonChangedCount;
                status.Calibration = s.CalibrationStatus.ToString();
                status.Fidelity = s.Fidelity.ToString();
                status.JointSet = s.JointSet.ToString();
                status.JointCount = s.JointLocations?.Length ?? 0;
                if (s.JointLocations != null)
                {
                    foreach (var j in s.JointLocations)
                    {
                        if (j.PositionValid && j.OrientationValid)
                            status.ValidJoints++;
                        if (j.PositionTracked)
                            status.TrackedJoints++;
                    }

                    var left = (int)OVRPlugin.BoneId.FullBody_LeftHandWrist;
                    var right = (int)OVRPlugin.BoneId.FullBody_RightHandWrist;
                    status.LeftWristTracked = left < s.JointLocations.Length && s.JointLocations[left].PositionTracked;
                    status.RightWristTracked = right < s.JointLocations.Length && s.JointLocations[right].PositionTracked;
                }
            }

            if (retargeter != null)
            {
                status.RetargeterPresent = true;
                status.RetargeterEnabled = retargeter.isActiveAndEnabled;
                status.RetargeterInitialized = retargeter.IsInitialized;
                status.RetargeterDataValid = retargeter.IsDataValid;
                status.RetargeterSkeletonChangedCount = retargeter.SkeletonChangedCount;
            }

            return true;
        }
    }
}
#endif
