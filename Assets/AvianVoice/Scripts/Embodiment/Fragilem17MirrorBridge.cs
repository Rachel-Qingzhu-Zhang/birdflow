using Fragilem17.MirrorsAndPortals;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Wires AvianVoice mirror quads to Fragilem17 Mirrors and reflections for VR.
    /// </summary>
    public static class Fragilem17MirrorBridge
    {
        const string UnlitMaterialAssetPath =
            "Assets/Fragilem17/Mirrors and reflections for VR/Demo/Materials/Mirror Unlit.mat";

        const string DefaultMaterialAssetPath =
            "Assets/Fragilem17/Mirrors and reflections for VR/Materials/MirrorSurfaceDefault.mat";

        public struct Settings
        {
            public int TextureSize;
            public Vector2 MirrorPanelSize;
            public MirrorRenderer.AA AntiAliasing;
            public float MaxRenderingDistance;
            public LayerMask ReflectionLayers;
            public Material MirrorMaterial;
            public float ReflectionBrightness;
            public float ExtendFrustumForCulling;
            public bool DisablePixelLightsInReflection;
        }

        public static Settings CreateDefaultSettings()
        {
            return new Settings
            {
                TextureSize = 1280,
                MirrorPanelSize = new Vector2(1.5f, 2.2f),
                AntiAliasing = MirrorRenderer.AA.Low,
                MaxRenderingDistance = 80f,
                ReflectionLayers = PlanarReflectionUtility.BuildDefaultReflectionMask(),
                MirrorMaterial = null,
                ReflectionBrightness = 1.62f,
                ExtendFrustumForCulling = 0.25f,
                DisablePixelLightsInReflection = false
            };
        }

        public static Material ResolveDefaultMaterial()
        {
            return PrepareMirrorMaterial(LoadMirrorMaterialTemplate(), CreateDefaultSettings().ReflectionBrightness);
        }

        public static Material PrepareMirrorMaterial(Material source, float reflectionBrightness = 1.62f)
        {
            if (source == null)
                return null;

            var prepared = new Material(source) { name = source.name + " (Avian Clean)" };
            ApplyCleanMirrorSurfaceProperties(prepared, reflectionBrightness);
            return prepared;
        }

        static Material LoadMirrorMaterialTemplate()
        {
#if UNITY_EDITOR
            var clean = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialAssetPath);
            if (clean != null)
                return clean;

            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialAssetPath);
            if (unlit != null)
                return unlit;
#endif
            var shader = Shader.Find("MirrorsAndPortals/Mirrors/MirrorSurfaceUnlit")
                         ?? Shader.Find("MirrorsAndPortals/Mirrors/MirrorSurfaceBakedLit");
            return shader != null ? new Material(shader) : null;
        }

        static void ApplyCleanMirrorSurfaceProperties(Material mat, float reflectionBrightness = 1.62f)
        {
            if (mat == null)
                return;

            var tint = ReflectionExposureTint(reflectionBrightness);

            // Demo "Mirror Unlit" ships with smudge albedo, normal maps, and refraction for stylized glass.
            if (mat.HasProperty("_Albedo"))
                mat.SetTexture("_Albedo", null);
            if (mat.HasProperty("_Normal"))
                mat.SetTexture("_Normal", null);
            if (mat.HasProperty("_Mask"))
                mat.SetTexture("_Mask", null);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", null);
            if (mat.HasProperty("_Metallic"))
                mat.SetTexture("_Metallic", null);

            if (mat.HasProperty("_Refraction"))
                mat.SetFloat("_Refraction", 0f);
            if (mat.HasProperty("_refraction"))
                mat.SetFloat("_refraction", 0f);
            if (mat.HasProperty("_Blur"))
                mat.SetFloat("_Blur", 0f);
            if (mat.HasProperty("_BlurFactor"))
                mat.SetFloat("_BlurFactor", 0f);
            if (mat.HasProperty("_NormalStrength"))
                mat.SetFloat("_NormalStrength", 0f);
            if (mat.HasProperty("_NormalToAlbedo"))
                mat.SetFloat("_NormalToAlbedo", 0f);
            if (mat.HasProperty("_Alpha"))
                mat.SetFloat("_Alpha", 1f);
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 1f);
            if (mat.HasProperty("_DistanceBlend"))
                mat.SetFloat("_DistanceBlend", 1f);
            if (mat.HasProperty("_Specular"))
                mat.SetFloat("_Specular", 0f);
            if (mat.HasProperty("_Tint"))
                mat.SetColor("_Tint", tint);
            if (mat.HasProperty("_ReflectionTint"))
                mat.SetColor("_ReflectionTint", tint);
            if (mat.HasProperty("_FadeColor"))
                mat.SetColor("_FadeColor", Color.black);
        }

        /// <summary>
        /// Gentle lift for reflection RTs that skip main-camera post exposure. Clamped to avoid blown highlights.
        /// </summary>
        static Color ReflectionExposureTint(float linearBoost)
        {
            var boost = Mathf.Clamp(linearBoost, 1f, 2f);
            var lift = 0.04f * (boost - 1f);
            var channel = Mathf.Min(boost + lift, 1.95f);
            return new Color(channel, channel, channel, 1f);
        }

        static Vector2 ResolveReflectionTextureSize(int longEdgePixels, Vector2 mirrorPanelSize)
        {
            var longEdge = Mathf.Clamp(longEdgePixels, 256, 2048);
            var width = Mathf.Max(mirrorPanelSize.x, 0.01f);
            var height = Mathf.Max(mirrorPanelSize.y, 0.01f);

            if (width >= height)
                return new Vector2(longEdge, Mathf.Clamp(Mathf.RoundToInt(longEdge * height / width), 256, 2048));

            return new Vector2(Mathf.Clamp(Mathf.RoundToInt(longEdge * width / height), 256, 2048), longEdge);
        }

        public static void CleanupLegacyMirrorSystems(Transform worldMirrorRoot)
        {
            if (worldMirrorRoot != null)
            {
                foreach (var child in worldMirrorRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "MirrorCamera")
                        continue;
                    Object.Destroy(child.gameObject);
                }
            }

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam == null || cam.name != "MirrorCamera")
                    continue;

                if (cam.targetTexture != null && cam.targetTexture.name.Contains("MirrorReflection"))
                    Object.Destroy(cam.gameObject);
            }

            foreach (var legacy in Object.FindObjectsByType<MirrorReflectionCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (legacy == null)
                    continue;

                var cam = legacy.GetComponent<Camera>();
                if (cam != null && cam.name == "MirrorCamera")
                    Object.Destroy(legacy.gameObject);
            }
        }

        public static MirrorRenderer ConfigureMirrorQuad(GameObject mirrorQuad, in Settings settings)
        {
            if (mirrorQuad == null)
                return null;

            CleanupLegacyMirrorSystems(mirrorQuad.transform.parent);

            var meshRenderer = mirrorQuad.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = mirrorQuad.AddComponent<MeshRenderer>();

            var meshFilter = mirrorQuad.GetComponent<MeshFilter>();
            if (meshFilter == null)
                meshFilter = mirrorQuad.AddComponent<MeshFilter>();

            if (meshFilter.sharedMesh == null)
            {
                var tempQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                meshFilter.sharedMesh = tempQuad.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tempQuad);
            }

            MirrorBodySetup.ConfigureMirrorPanelRenderer(meshRenderer);

            var sourceMaterial = settings.MirrorMaterial != null
                ? PrepareMirrorMaterial(settings.MirrorMaterial, settings.ReflectionBrightness)
                : PrepareMirrorMaterial(LoadMirrorMaterialTemplate(), settings.ReflectionBrightness);

            if (sourceMaterial == null)
            {
                Debug.LogError(
                    "[AvianVoice] Fragilem17 mirror material missing. Assign a MirrorsAndPortals material " +
                    "or reimport Assets/Fragilem17/Mirrors and reflections for VR.");
            }
            else
            {
                meshRenderer.sharedMaterial = sourceMaterial;
            }

            var surface = mirrorQuad.GetComponent<MirrorSurface>();
            var surfaceIsNew = surface == null;
            if (surfaceIsNew)
                surface = mirrorQuad.AddComponent<MirrorSurface>();
            else
                surface.enabled = false;

            surface.Material = sourceMaterial;
            surface.MyMeshRenderer = meshRenderer;
            surface.MyForwardTransform = mirrorQuad.transform;
            surface.maxRenderingDistance = Mathf.Max(5f, settings.MaxRenderingDistance);
            surface.fadeDistance = 0.05f;
            surface.maxBlend = 1f;
            surface.clippingPlaneOffset = 0.03f;
            surface.FadeColor = Color.black;

            if (!surfaceIsNew)
                surface.enabled = true;

            var renderer = mirrorQuad.GetComponent<MirrorRenderer>();
            if (renderer == null)
                renderer = mirrorQuad.AddComponent<MirrorRenderer>();

            EnsureRendererEvents(renderer);
            renderer.enabled = false;

            var panelSize = settings.MirrorPanelSize;
            if (panelSize.x <= 0.01f || panelSize.y <= 0.01f)
            {
                var scale = mirrorQuad.transform.lossyScale;
                panelSize = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }

            renderer.mirrorSurfaces = new System.Collections.Generic.List<MirrorSurface> { surface };
            renderer.recursions = 1;
            renderer.RenderTheseLayers = settings.ReflectionLayers;
            renderer.OpaqueTextureMode = CameraOverrideOption.Off;
            renderer.DepthTextureMode = CameraOverrideOption.Off;
            renderer.RenderShadows = false;
            renderer.RenderPostProcessing = false;
            renderer.textureSize = ResolveReflectionTextureSize(settings.TextureSize, panelSize);
            renderer.useScreenScaleFactor = false;
            renderer.screenScaleFactor = 0.5f;
            renderer.antiAliasing = settings.AntiAliasing;
            renderer.disablePixelLights = settings.DisablePixelLightsInReflection;
            renderer.framesNeededToUpdate = 0;
            renderer.UseOcclusionCulling = false;
            renderer.ExtendFrustumForCulling = Mathf.Clamp(settings.ExtendFrustumForCulling, 0.05f, 0.5f);
            renderer.PrewarmedTextures = 1;
            renderer.RenderSceneCamera = false;

            return renderer;
        }

        static void EnsureRendererEvents(MirrorRenderer renderer)
        {
            if (renderer == null)
                return;

            // Runtime AddComponent leaves UnityEvent fields null; Invoke() then NREs mid-frame in VR.
            if (renderer.onStartRendering == null)
                renderer.onStartRendering = new UnityEvent();
            if (renderer.onFinishedRendering == null)
                renderer.onFinishedRendering = new UnityEvent();
        }

        public static void SetMirrorRenderingActive(MirrorRenderer renderer, bool active)
        {
            if (renderer == null)
                return;

            if (active)
            {
                EnsureRendererEvents(renderer);
                // Let MirrorRenderer.OnEnable call PrewarmTextures once.
                // Calling PrewarmTextures again DestroyImmediate's GetTemporary RTs and leaves a black mirror.
                if (!renderer.enabled)
                    renderer.enabled = true;
            }
            else if (renderer.enabled)
            {
                renderer.enabled = false;
            }
        }
    }
}
