using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Horizontal pond surface with a planar reflection camera feeding the water material.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public class PondWaterReflection : MonoBehaviour
    {
        public const string WaterObjectName = "PondWater";

        [SerializeField]
        int textureSize = 1024;

        [SerializeField]
        LayerMask reflectionMask;

        [SerializeField]
        float clipDistance = 80f;

        [SerializeField]
        bool enableReflectionFillLight = true;

        [SerializeField]
        [Range(0.5f, 2f)]
        float reflectionBrightness = 1.38f;

        [SerializeField]
        Material waterMaterial;

        Camera reflectionCamera;
        RenderTexture renderTexture;
        MeshRenderer meshRenderer;
        ReflectionFillLighting reflectionFillLighting;
        bool built;
        bool reflectionActive;
        bool birdsPrepared;
        float savedReflectionScale = -1f;

        static readonly int ReflectionScaleId = Shader.PropertyToID("_ReflectionScale");

        public static void SetAllReflectionActive(bool active)
        {
            foreach (var pond in Object.FindObjectsByType<PondWaterReflection>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pond != null)
                    pond.SetReflectionActive(active);
            }
        }

        public static void SetAllReflectionScaleMultiplier(float multiplier)
        {
            foreach (var pond in Object.FindObjectsByType<PondWaterReflection>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pond != null)
                    pond.ApplyReflectionScaleMultiplier(multiplier);
            }
        }

        public static void RestoreAllReflectionScales()
        {
            foreach (var pond in Object.FindObjectsByType<PondWaterReflection>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pond != null)
                    pond.RestoreReflectionScale();
            }
        }

        public void SetReflectionActive(bool active)
        {
            reflectionActive = active;
            if (!built && active)
                Build();

            if (reflectionCamera != null)
                reflectionCamera.enabled = active;

            if (reflectionFillLighting != null)
                reflectionFillLighting.SetFillActive(active && enableReflectionFillLight);

            enabled = active;
        }

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (reflectionMask.value == 0)
                reflectionMask = PlanarReflectionUtility.BuildDefaultReflectionMask();

            Build();
            SetReflectionActive(false);
        }

        void Build()
        {
            if (built)
                return;

            meshRenderer ??= GetComponent<MeshRenderer>();
            SetLayerRecursive(gameObject, BirdSceneRoles.MirrorDisplayLayer);

            renderTexture = new RenderTexture(textureSize, textureSize, 16);
            renderTexture.Create();

            EnsureReflectionCamera();
            EnsureReflectionFillLight();
            ApplyWaterMaterial();
            built = true;
        }

        void EnsureReflectionCamera()
        {
            var camTransform = transform.Find("PondReflectionCamera");
            if (camTransform != null)
                reflectionCamera = camTransform.GetComponent<Camera>();

            if (reflectionCamera == null)
            {
                var camGo = new GameObject("PondReflectionCamera");
                camGo.transform.SetParent(transform, false);
                reflectionCamera = camGo.AddComponent<Camera>();
            }

            if (reflectionCamera.GetComponent<MirrorReflectionCamera>() == null)
                reflectionCamera.gameObject.AddComponent<MirrorReflectionCamera>();

            PlanarReflectionUtility.ConfigureReflectionCamera(
                reflectionCamera,
                renderTexture,
                reflectionMask,
                clipDistance);
            MirrorUrpCameraBootstrap.Configure(reflectionCamera);
            reflectionCamera.gameObject.layer = BirdSceneRoles.MirrorDisplayLayer;
        }

        void EnsureReflectionFillLight()
        {
            if (!enableReflectionFillLight)
                return;

            reflectionFillLighting = ReflectionFillLighting.Ensure(transform);
            reflectionFillLighting.SetFillActive(reflectionActive);
        }

        static readonly int ReflectionBrightnessId = Shader.PropertyToID("_ReflectionBrightness");

        void ApplyWaterMaterial()
        {
            if (meshRenderer == null)
                return;

            Material mat;
            if (waterMaterial != null)
                mat = new Material(waterMaterial);
            else
            {
                var shader = Shader.Find("AvianVoice/PondWater")
                             ?? Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
            }

            if (mat.HasProperty("_ReflectionTex"))
                mat.SetTexture("_ReflectionTex", renderTexture);
            if (mat.HasProperty(ReflectionBrightnessId))
                mat.SetFloat(ReflectionBrightnessId, reflectionBrightness);

            meshRenderer.sharedMaterial = mat;
            CacheReflectionScale();
        }

        void CacheReflectionScale()
        {
            if (meshRenderer?.sharedMaterial == null || !meshRenderer.sharedMaterial.HasProperty(ReflectionScaleId))
                return;

            savedReflectionScale = meshRenderer.sharedMaterial.GetFloat(ReflectionScaleId);
        }

        public void ApplyReflectionScaleMultiplier(float multiplier)
        {
            if (meshRenderer?.sharedMaterial == null || !meshRenderer.sharedMaterial.HasProperty(ReflectionScaleId))
                return;

            if (savedReflectionScale <= 0f)
                CacheReflectionScale();

            if (savedReflectionScale <= 0f)
                return;

            meshRenderer.sharedMaterial.SetFloat(ReflectionScaleId, savedReflectionScale * multiplier);
        }

        public void RestoreReflectionScale()
        {
            if (meshRenderer?.sharedMaterial == null || savedReflectionScale <= 0f)
                return;

            if (!meshRenderer.sharedMaterial.HasProperty(ReflectionScaleId))
                return;

            meshRenderer.sharedMaterial.SetFloat(ReflectionScaleId, savedReflectionScale);
        }

        void LateUpdate()
        {
            if (!reflectionActive || reflectionCamera == null)
                return;

            if (!birdsPrepared)
            {
                PlanarReflectionUtility.EnsureBirdRenderersVisible();
                birdsPrepared = true;
            }

            var viewer = WorldPlacedBird.FindMainCameraTransform();
            if (viewer == null)
                return;

            var planeNormal = PlanarReflectionUtility.ResolvePlaneNormalTowardViewer(transform, viewer);
            PlanarReflectionUtility.UpdateReflectionCameraPose(
                reflectionCamera,
                viewer,
                transform.position,
                planeNormal);
            PlanarReflectionUtility.PrepareReflectionCameraMatrices(
                reflectionCamera,
                transform.position,
                planeNormal,
                mirrorSurface: null,
                useObliqueClip: true,
                obliqueSideSign: -1f);

            if (reflectionFillLighting != null)
            {
                reflectionFillLighting.SetFillActive(enableReflectionFillLight);
                reflectionFillLighting.UpdateForPond(
                    transform,
                    ReflectionFillLighting.ResolveDefaultFocusPoint());
            }
        }

        void OnDestroy()
        {
            if (renderTexture != null)
                renderTexture.Release();
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            if (layer < 0)
                return;

            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
