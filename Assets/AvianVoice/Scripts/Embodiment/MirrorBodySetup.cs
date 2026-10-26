using AvianVoice.Core;
using AvianVoice.UI;
using Fragilem17.MirrorsAndPortals;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// World-fixed mirror panel using Fragilem17 Mirrors and reflections for VR (URP + XR stereo).
    /// </summary>
    public class MirrorBodySetup : MonoBehaviour
    {
        public const string WorldMirrorObjectName = "AvianWorldMirror";

        [SerializeField]
        Transform mirrorAnchor;

        [SerializeField]
        Transform birdBodyRoot;

        [SerializeField]
        [Tooltip("Reflection texture long edge (pixels). Lower to 1024 if VR stutters when turning your head.")]
        int textureSize = 1280;

        [SerializeField]
        [Tooltip("MSAA on the reflection texture. Set None if mirror tasks drop frames.")]
        MirrorRenderer.AA mirrorAntiAliasing = MirrorRenderer.AA.Low;

        [SerializeField]
        [Range(1f, 2f)]
        [Tooltip("Brightens the mirror reflection (does not affect the rest of the scene).")]
        float mirrorReflectionBrightness = 1.62f;

        [SerializeField]
        [Range(0.05f, 0.5f)]
        [Tooltip("Widen the VR reflection frustum. Raise if peripheral mirror view looks flat or clipped.")]
        float mirrorFrustumExtension = 0.25f;

        [SerializeField]
        [Tooltip("When on, Fragilem17 reflection cameras skip additional lights (more stable mirror RT). Main view still uses MirrorFillLight.")]
        bool mirrorDisablePixelLights = true;

        [SerializeField]
        [Tooltip("Optional Fragilem17 material override. Leave empty for a clean clinical mirror (no smudges or refraction).")]
        Material fragilem17MirrorMaterial;

        [SerializeField]
        LayerMask mirrorReflectionMask;

        [SerializeField]
        bool useWorldPlacement = true;

        [Tooltip("When AvianWorldMirror exists in the scene, keep its Hierarchy position/rotation/scale.")]
        [SerializeField]
        bool respectSceneMirrorTransform = true;

        [SerializeField]
        Transform sceneMirrorOverride;

        [SerializeField]
        float worldMirrorAheadMeters = AvianLabSceneLayout.MirrorAheadMeters;

        [SerializeField]
        float worldMirrorHeight = AvianLabSceneLayout.MirrorPanelHeightMeters;

        [SerializeField]
        Vector2 worldMirrorSize = new(1.5f, 2.2f);

        [SerializeField]
        float mirrorMaxRenderingDistance = 80f;

        [SerializeField]
        bool enableReflectionFillLight = true;

        GameObject mirrorQuad;
        MirrorRenderer fragilem17Renderer;
        Transform worldMirrorRoot;
        ReflectionFillLighting reflectionFillLighting;
        bool built;
        bool mirrorVisible;
        bool worldMirrorAnchored;
        bool usingSceneHierarchyMirror;

        public void SetBirdBodyRoot(Transform root)
        {
            birdBodyRoot = root;
        }

        public void SetMirrorActive(bool active)
        {
            mirrorVisible = active;
            if (!built && active)
                BuildMirror();

            // Never deactivate AvianWorldMirror / MirrorQuad — that tears down Fragilem17 RTs.
            if (worldMirrorRoot != null && !worldMirrorRoot.gameObject.activeSelf)
                worldMirrorRoot.gameObject.SetActive(true);

            if (mirrorQuad != null && !mirrorQuad.activeSelf)
                mirrorQuad.SetActive(true);

            if (active)
                EnsureFragilem17Bound();

            SetMirrorVisualsEnabled(active);

            if (fragilem17Renderer != null)
            {
                if (active)
                {
                    // One clean OnEnable → PrewarmTextures cycle.
                    if (fragilem17Renderer.enabled)
                        fragilem17Renderer.enabled = false;
                    Fragilem17MirrorBridge.SetMirrorRenderingActive(fragilem17Renderer, true);
                }
                else
                {
                    Fragilem17MirrorBridge.SetMirrorRenderingActive(fragilem17Renderer, false);
                }
            }

            if (reflectionFillLighting != null)
                reflectionFillLighting.SetFillActive(active && enableReflectionFillLight);
        }

        void EnsureFragilem17Bound()
        {
            if (mirrorQuad == null)
                return;

            var meshRenderer = mirrorQuad.GetComponent<MeshRenderer>();
            var needsBind = fragilem17Renderer == null
                            || meshRenderer == null
                            || meshRenderer.sharedMaterial == null
                            || !IsFragilem17MirrorMaterial(meshRenderer.sharedMaterial)
                            || mirrorQuad.GetComponent<MirrorSurface>() == null
                            || mirrorQuad.GetComponent<MirrorRenderer>() == null;

            if (needsBind)
                fragilem17Renderer = ConfigureFragilem17OnQuad(mirrorQuad);
            else if (fragilem17Renderer == null)
                fragilem17Renderer = mirrorQuad.GetComponent<MirrorRenderer>();

            if (worldMirrorRoot != null)
                SetMirrorDisplayLayers(worldMirrorRoot);
            else
                mirrorQuad.layer = BirdSceneRoles.MirrorDisplayLayer;
        }

        void SetMirrorVisualsEnabled(bool active)
        {
            if (mirrorQuad != null)
            {
                var quadRenderer = mirrorQuad.GetComponent<MeshRenderer>();
                if (quadRenderer != null)
                    quadRenderer.enabled = active;
            }

            var frame = worldMirrorRoot != null ? worldMirrorRoot.Find("MirrorFrame") : null;
            if (frame == null)
                return;

            var frameRenderer = frame.GetComponent<MeshRenderer>();
            if (frameRenderer != null)
                frameRenderer.enabled = active;
            else
                frame.gameObject.SetActive(active);
        }

        static bool IsFragilem17MirrorMaterial(Material mat)
        {
            if (mat == null || mat.shader == null)
                return false;

            var shaderName = mat.shader.name;
            return shaderName.IndexOf("MirrorsAndPortals", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || mat.HasProperty("_TexLeft")
                   || mat.HasProperty("_TexRight");
        }

        public static void ConfigureMirrorPanelRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        public void Initialize(AvianVoiceSettings voiceSettings)
        {
            if (voiceSettings != null && !voiceSettings.mirrorEnabled)
            {
                enabled = false;
                return;
            }

            if (!useWorldPlacement)
                EnsureMirrorAnchorOnHead();

            BuildMirror();
        }

        public void EnsureMirrorAnchorOnHead()
        {
            if (mirrorAnchor != null && mirrorAnchor != transform)
                return;

            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name != "Camera Offset")
                    continue;
                mirrorAnchor = child;
                return;
            }

            if (Camera.main != null)
                mirrorAnchor = Camera.main.transform;
        }

        void BuildMirror()
        {
            if (built)
                return;

            AvianHudLayerSetup.ApplyToXrRig();
            mirrorReflectionMask = PlanarReflectionUtility.BuildDefaultReflectionMask();
            PlanarReflectionUtility.EnsureBirdRenderersVisible();
            Fragilem17MirrorBridge.CleanupLegacyMirrorSystems(ResolveSceneMirrorRoot());

            if (fragilem17MirrorMaterial == null)
                fragilem17MirrorMaterial = Fragilem17MirrorBridge.ResolveDefaultMaterial();

            if (useWorldPlacement)
                BuildWorldMirror();
            else
                BuildHeadMirror();

            Fragilem17MirrorBridge.SetMirrorRenderingActive(fragilem17Renderer, mirrorVisible);
            ValidateMainCameraForFragilem17();
            built = true;
        }

        static void ValidateMainCameraForFragilem17()
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[AvianVoice] No MainCamera — Fragilem17 mirrors will stay blank.");
                return;
            }

            var uac = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (uac != null && uac.renderType == CameraRenderType.Overlay)
            {
                Debug.LogWarning(
                    "[AvianVoice] MainCamera is a URP Overlay camera. Fragilem17 mirrors require a Base camera. " +
                    "Set the XR eye camera to Base in Universal Additional Camera Data.");
            }
        }

        void BuildWorldMirror()
        {
            worldMirrorRoot = ResolveSceneMirrorRoot();
            usingSceneHierarchyMirror = worldMirrorRoot != null;
            var manualSceneMirror = IsManualSceneMirror();

            if (worldMirrorRoot == null)
            {
                worldMirrorRoot = new GameObject(WorldMirrorObjectName).transform;
                CreateWorldMirrorVisuals(worldMirrorRoot, createNewVisuals: true);
            }
            else
            {
                mirrorQuad = worldMirrorRoot.Find("MirrorQuad")?.gameObject;
                if (mirrorQuad == null)
                    CreateWorldMirrorVisuals(worldMirrorRoot, createNewVisuals: true);
            }

            fragilem17Renderer = ConfigureFragilem17OnQuad(mirrorQuad);
            SetMirrorDisplayLayers(worldMirrorRoot);

            if (mirrorQuad != null)
                mirrorQuad.layer = BirdSceneRoles.MirrorDisplayLayer;

            ConfigureMirrorVisuals(worldMirrorRoot);
            CleanupLegacyFillLights();
            EnsureBirdFillLight();

            if (manualSceneMirror)
            {
                NormalizeMirrorRootScale(worldMirrorRoot, mirrorQuad != null ? mirrorQuad.transform : null);
                Debug.Log(
                    "[AvianVoice] Mirror uses Fragilem17 at scene transform " +
                    worldMirrorRoot.position + ".");
                return;
            }

            AnchorWorldMirrorOnce();
        }

        bool IsManualSceneMirror()
        {
            if (!usingSceneHierarchyMirror || worldMirrorRoot == null)
                return false;

            if (worldMirrorRoot.GetComponent<WorldMirrorScenePlacement>() != null)
                return true;

            return respectSceneMirrorTransform;
        }

        Transform ResolveSceneMirrorRoot()
        {
            if (sceneMirrorOverride != null)
                return sceneMirrorOverride;

            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == WorldMirrorObjectName)
                    return t;
            }

            return null;
        }

        void CreateWorldMirrorVisuals(Transform root, bool createNewVisuals)
        {
            mirrorQuad = root.Find("MirrorQuad")?.gameObject;
            if (mirrorQuad == null && createNewVisuals)
            {
                mirrorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mirrorQuad.name = "MirrorQuad";
                mirrorQuad.transform.SetParent(root, false);
                mirrorQuad.transform.localPosition = Vector3.zero;
                mirrorQuad.transform.localRotation = Quaternion.identity;
                mirrorQuad.transform.localScale = new Vector3(worldMirrorSize.x, worldMirrorSize.y, 1f);
                Destroy(mirrorQuad.GetComponent<Collider>());
                ConfigureMirrorPanelRenderer(mirrorQuad.GetComponent<Renderer>());
            }

            var frame = root.Find("MirrorFrame")?.gameObject;
            if (frame == null && createNewVisuals)
            {
                frame = GameObject.CreatePrimitive(PrimitiveType.Quad);
                frame.name = "MirrorFrame";
                frame.transform.SetParent(root, false);
                frame.transform.localPosition = new Vector3(0f, 0f, -0.04f);
                frame.transform.localRotation = Quaternion.identity;
                frame.transform.localScale = new Vector3(worldMirrorSize.x + 0.18f, worldMirrorSize.y + 0.18f, 1f);
                Destroy(frame.GetComponent<Collider>());
                ApplyFrameMaterial(frame.GetComponent<MeshRenderer>());
                ConfigureMirrorPanelRenderer(frame.GetComponent<Renderer>());
            }

            SetMirrorDisplayLayers(root);
        }

        void BuildHeadMirror()
        {
            if (mirrorAnchor == null)
                mirrorAnchor = transform;

            mirrorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirrorQuad.name = "MirrorQuad";
            mirrorQuad.transform.SetParent(mirrorAnchor, false);
            mirrorQuad.transform.localPosition = new Vector3(0f, 0.05f, 1.35f);
            mirrorQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mirrorQuad.transform.localScale = new Vector3(0.55f, 0.85f, 1f);
            Destroy(mirrorQuad.GetComponent<Collider>());
            ConfigureMirrorPanelRenderer(mirrorQuad.GetComponent<Renderer>());
            SetMirrorDisplayLayers(mirrorQuad.transform);
            fragilem17Renderer = ConfigureFragilem17OnQuad(mirrorQuad);
        }

        MirrorRenderer ConfigureFragilem17OnQuad(GameObject quad)
        {
            if (quad == null)
                return null;

            var settings = Fragilem17MirrorBridge.CreateDefaultSettings();
            settings.TextureSize = textureSize;
            settings.MirrorPanelSize = worldMirrorSize;
            settings.AntiAliasing = mirrorAntiAliasing;
            settings.ReflectionBrightness = mirrorReflectionBrightness;
            settings.ExtendFrustumForCulling = mirrorFrustumExtension;
            settings.DisablePixelLightsInReflection = mirrorDisablePixelLights;
            settings.MaxRenderingDistance = mirrorMaxRenderingDistance;
            settings.ReflectionLayers = mirrorReflectionMask;
            settings.MirrorMaterial = fragilem17MirrorMaterial;

            if (!useWorldPlacement && quad != null)
            {
                var scale = quad.transform.localScale;
                settings.MirrorPanelSize = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }

            return Fragilem17MirrorBridge.ConfigureMirrorQuad(quad, settings);
        }

        void ConfigureMirrorVisuals(Transform root)
        {
            if (root == null)
                return;

            foreach (var childName in new[] { "MirrorQuad", "MirrorFrame" })
            {
                var child = root.Find(childName);
                if (child != null)
                    ConfigureMirrorPanelRenderer(child.GetComponent<Renderer>());
            }
        }

        void CleanupLegacyFillLights()
        {
            if (worldMirrorRoot == null)
                return;

            foreach (var childName in new[] { "MirrorPerchFillLight", "MirrorPerchPointFill" })
            {
                var legacy = worldMirrorRoot.Find(childName);
                if (legacy != null)
                    Destroy(legacy.gameObject);
            }
        }

        void EnsureBirdFillLight()
        {
            if (!enableReflectionFillLight || worldMirrorRoot == null)
                return;

            reflectionFillLighting = ReflectionFillLighting.Ensure(worldMirrorRoot);
            reflectionFillLighting.SetFillActive(mirrorVisible);
            UpdateReflectionFillLight();
        }

        void UpdateReflectionFillLight()
        {
            if (worldMirrorRoot == null || reflectionFillLighting == null)
                return;

            reflectionFillLighting.SetFillActive(mirrorVisible && enableReflectionFillLight);
            reflectionFillLighting.UpdateForMirror(
                worldMirrorRoot,
                ReflectionFillLighting.ResolveDefaultFocusPoint());
        }

        static void ApplyFrameMaterial(MeshRenderer renderer)
        {
            if (renderer == null)
                return;

            var frameShader = Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Sprites/Default");
            if (frameShader == null)
                return;

            var frameMat = new Material(frameShader);
            frameMat.color = new Color(0.15f, 0.72f, 0.95f, 1f);
            renderer.material = frameMat;
            ConfigureMirrorPanelRenderer(renderer);
        }

        void AnchorWorldMirrorOnce()
        {
            if (worldMirrorRoot == null || worldMirrorAnchored)
                return;

            if (!AvianLabSceneLayout.TryResolveXrOrigin(out var xrOrigin, out var viewForward))
            {
                Debug.LogWarning("[AvianVoice] Mirror anchor needs XR Origin in the scene.");
                return;
            }

            var viewerEye = AvianLabSceneLayout.ViewerEyePoint(xrOrigin);
            var mirrorPos = AvianLabSceneLayout.ResolveMirrorPositionNearBirds(
                xrOrigin,
                viewForward,
                worldMirrorAheadMeters,
                worldMirrorHeight);
            var mirrorRot = AvianLabSceneLayout.ResolveMirrorRotation(mirrorPos, viewerEye);

            worldMirrorRoot.SetPositionAndRotation(mirrorPos, mirrorRot);
            worldMirrorRoot.localScale = Vector3.one;
            ResetMirrorChildTransforms();

            worldMirrorAnchored = true;
            Debug.Log(
                "[AvianVoice] Mirror auto-placed at " + mirrorPos +
                " (Fragilem17). Place AvianWorldMirror in the scene to override with Hierarchy edits.");
        }

        void NormalizeMirrorRootScale(Transform root, Transform quad)
        {
            if (root == null || quad == null)
                return;

            var rootScale = root.localScale;
            if (Mathf.Approximately(rootScale.x, 1f)
                && Mathf.Approximately(rootScale.y, 1f)
                && Mathf.Approximately(rootScale.z, 1f))
                return;

            var quadLocal = quad.localScale;
            quad.localScale = new Vector3(
                quadLocal.x * rootScale.x,
                quadLocal.y * rootScale.y,
                quadLocal.z * rootScale.z);
            root.localScale = Vector3.one;
            Debug.Log(
                "[AvianVoice] Mirror root non-uniform scale baked into MirrorQuad; root reset to (1,1,1).");
        }

        void ResetMirrorChildTransforms()
        {
            if (mirrorQuad != null)
                mirrorQuad.transform.localRotation = Quaternion.identity;

            var frame = worldMirrorRoot.Find("MirrorFrame");
            if (frame != null)
            {
                frame.localPosition = new Vector3(0f, 0f, -0.04f);
                frame.localRotation = Quaternion.identity;
            }
        }

        void LateUpdate()
        {
            if (!mirrorVisible || !useWorldPlacement)
                return;

            UpdateReflectionFillLight();
        }

        static void SetMirrorDisplayLayers(Transform root)
        {
            if (root == null)
                return;

            SetLayerRecursive(root.gameObject, BirdSceneRoles.MirrorDisplayLayer);
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
