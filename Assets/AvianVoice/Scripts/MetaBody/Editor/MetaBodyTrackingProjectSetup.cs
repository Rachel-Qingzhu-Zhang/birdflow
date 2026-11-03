#if AVIAN_META_XR_CORE
using System.Reflection;
using Meta.XR;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace AvianVoice.MetaBody.Editor
{
    /// <summary>
    /// One-click project configuration for Meta Quest body tracking on the Unity OpenXR plugin:
    /// enables the "Meta XR Feature" OpenXR feature, marks body tracking as supported in the Oculus project
    /// config (adds the Android permission + feature tags at build time) and selects the full-body joint set.
    /// </summary>
    public static class MetaBodyTrackingProjectSetup
    {
        const string MenuPath = "Avian Voice/Embodiment/Configure Meta Body Tracking (IOBT)";
        const string AutoConfigureSessionKey = "AvianVoice.MetaBodyTracking.AutoConfigured";

        /// <summary>
        /// Applies the configuration once per editor session when it is still missing, so a fresh clone
        /// is ready for body tracking without a manual menu step.
        /// </summary>
        [InitializeOnLoadMethod]
        static void AutoConfigureOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(AutoConfigureSessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                SessionState.SetBool(AutoConfigureSessionKey, true);
                try
                {
                    if (!NeedsConfiguration())
                        return;

                    Configure();
                    AssetDatabase.SaveAssets();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[AvianVoice] Meta body tracking auto-configuration skipped: " + e.Message);
                }
            };
        }

        static bool NeedsConfiguration()
        {
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var android = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var feature = android != null ? android.GetFeature<MetaXRFeature>() : null;
            if (feature == null || !feature.enabled)
                return true;

            var runtime = OVRRuntimeSettings.GetRuntimeSettings();
            if (runtime != null && runtime.BodyTrackingJointSet != OVRPlugin.BodyJointSet.FullBody)
                return true;

            var config = OVRProjectConfig.CachedProjectConfig;
            return config != null && config.bodyTrackingSupport == OVRProjectConfig.FeatureSupport.None;
        }

        [MenuItem(MenuPath)]
        public static void ConfigureFromMenu()
        {
            Configure();
            AssetDatabase.SaveAssets();
            Debug.Log("[AvianVoice] Meta body tracking configured. Build for Android (Quest) to use IOBT.");
        }

        /// <summary>Called from the batch verification as well. Returns a short status string.</summary>
        public static string Configure()
        {
            var status = "";
            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                FeatureHelpers.RefreshFeatures(group);
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (settings == null)
                {
                    status += group + ": no OpenXR settings; ";
                    continue;
                }

                var feature = settings.GetFeature<MetaXRFeature>();
                if (feature == null)
                {
                    status += group + ": MetaXRFeature missing; ";
                    continue;
                }

                if (!feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    EditorUtility.SetDirty(settings);
                }

                status += group + ": MetaXRFeature on; ";
            }

            var config = OVRProjectConfig.CachedProjectConfig ?? CreateProjectConfig();
            if (config != null)
            {
                if (config.bodyTrackingSupport == OVRProjectConfig.FeatureSupport.None)
                    config.bodyTrackingSupport = OVRProjectConfig.FeatureSupport.Supported;
                OVRProjectConfig.CommitProjectConfig(config);
                status += "bodyTrackingSupport=" + config.bodyTrackingSupport + "; ";
            }
            else
            {
                status += "OVRProjectConfig unavailable; ";
            }

            var runtime = OVRRuntimeSettings.GetRuntimeSettings();
            if (runtime != null)
            {
                runtime.BodyTrackingJointSet = OVRPlugin.BodyJointSet.FullBody;
                runtime.BodyTrackingFidelity = OVRPlugin.BodyTrackingFidelity2.High;
                OVRRuntimeSettings.CommitRuntimeSettings(runtime);
                status += "jointSet=FullBody fidelity=High";
            }

            Debug.Log("[AvianVoice] Meta body tracking setup: " + status);
            return status;
        }

        static OVRProjectConfig CreateProjectConfig()
        {
            var method = typeof(OVRProjectConfig).GetMethod(
                "GetOrCreateProjectConfig",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            return method?.Invoke(null, null) as OVRProjectConfig;
        }
    }
}
#endif
