using System;
using System.Reflection;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Ensures mirror reflection cameras participate in URP lighting and shadows when URP is present.
    /// </summary>
    static class MirrorUrpCameraBootstrap
    {
        static Type urpCameraDataType;
        static PropertyInfo renderShadowsProperty;
        static PropertyInfo requiresDepthOptionProperty;
        static object depthOffValue;

        public static void Configure(Camera camera)
        {
            if (camera == null || !TryResolveUrpTypes())
                return;

            var data = camera.GetComponent(urpCameraDataType);
            if (data == null)
                data = camera.gameObject.AddComponent(urpCameraDataType);

            renderShadowsProperty?.SetValue(data, true);

            if (requiresDepthOptionProperty != null && depthOffValue != null)
                requiresDepthOptionProperty.SetValue(data, depthOffValue);
        }

        static bool TryResolveUrpTypes()
        {
            if (urpCameraDataType != null)
                return true;

            urpCameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (urpCameraDataType == null)
                return false;

            renderShadowsProperty = urpCameraDataType.GetProperty("renderShadows");
            requiresDepthOptionProperty = urpCameraDataType.GetProperty("requiresDepthOption");

            var depthOptionType = Type.GetType(
                "UnityEngine.Rendering.Universal.CameraOverrideOption, Unity.RenderPipelines.Universal.Runtime");
            if (depthOptionType != null)
                depthOffValue = Enum.Parse(depthOptionType, "Off");

            return true;
        }
    }
}
