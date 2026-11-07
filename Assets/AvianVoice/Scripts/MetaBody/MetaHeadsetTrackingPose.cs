#if AVIAN_META_XR_CORE
using AvianVoice.BodyTracking;
using UnityEngine;

namespace AvianVoice.MetaBody
{
    /// <summary>
    /// Supplies <see cref="HeadsetTrackingPose"/> from OVRPlugin: the center-eye pose in OVRPlugin's tracking
    /// space, which is the space OVRBody reports the body joints in (same flipped-Z conversion as the joints).
    /// </summary>
    static class MetaHeadsetTrackingPose
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            HeadsetTrackingPose.Provider = TryGet;
            HeadsetTrackingPose.ProviderName = "OVRPlugin center eye";
            HeadsetTrackingPose.Recenter = () => OVRPlugin.initialized && OVRPlugin.RecenterTrackingOrigin(OVRPlugin.RecenterFlags.Default);
            HeadsetTrackingPose.CycleTrackingOrigin = CycleTrackingOrigin;
        }

        /// <summary>
        /// Test only: switch OVRManager between FloorLevel and EyeLevel. Under Unity OpenXR, OVRManager applies this to
        /// Unity's XR input origin (the camera's space) while the body joints stay in OVRPlugin's space: the same kind of
        /// mismatch a Quest recenter creates between the two.
        /// </summary>
        static string CycleTrackingOrigin()
        {
            var manager = OVRManager.instance;
            if (manager == null)
                return "no OVRManager";

            var current = manager.trackingOriginType;
            var next = current == OVRManager.TrackingOrigin.EyeLevel ? OVRManager.TrackingOrigin.FloorLevel : OVRManager.TrackingOrigin.EyeLevel;
            manager.trackingOriginType = next;
            return current + " -> " + next + ", now " + manager.trackingOriginType + " (OVRPlugin " + OVRPlugin.GetTrackingOriginType() + ")";
        }

        static bool TryGet(out Pose pose)
        {
            pose = Pose.identity;
            if (!OVRPlugin.initialized)
                return false;
            if (!OVRPlugin.GetNodeOrientationValid(OVRPlugin.Node.EyeCenter) || !OVRPlugin.GetNodePositionValid(OVRPlugin.Node.EyeCenter))
                return false;

            var p = OVRPlugin.GetNodePose(OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render);
            var rotation = p.Orientation.FromFlippedZQuatf();
            if (Mathf.Abs(rotation.x) + Mathf.Abs(rotation.y) + Mathf.Abs(rotation.z) + Mathf.Abs(rotation.w) < 0.5f)
                return false;

            pose = new Pose(p.Position.FromFlippedZVector3f(), rotation);
            return true;
        }
    }
}
#endif
