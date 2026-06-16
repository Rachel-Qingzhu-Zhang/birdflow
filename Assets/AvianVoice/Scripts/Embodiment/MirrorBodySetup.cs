using AvianVoice.Core;
using AvianVoice.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// World-fixed mirror panel. A planar reflection camera (viewer pose mirrored across MirrorQuad)
    /// renders at life-size scale, matching the main camera field of view.
    /// </summary>
    public class MirrorBodySetup : MonoBehaviour
    {
        public const string WorldMirrorObjectName = "AvianWorldMirror";

        [SerializeField]
        Transform mirrorAnchor;

        [SerializeField]
        Transform birdBodyRoot;

        [SerializeField]
        [Tooltip("Long edge of the mirror reflection render target in pixels.")]
        int textureSize = 4096;

        [SerializeField]
        [Tooltip("MSAA samples for the mirror render target (1, 2, 4, or 8).")]
        int renderTextureAntiAliasing = 4;

        [SerializeField]
        bool matchViewerAspect = true;

        [SerializeField]
        LayerMask mirrorReflectionMask;

        [SerializeField]
        Material mirrorMaterial;

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
        float mirrorCameraClipDistance = 80f;

        [SerializeField]
        float mirrorDisplayBrightness = 1.15f;

        [SerializeField]
        float mirrorDisplayVignette = 0.03f;

        [SerializeField]
        float mirrorBirdFieldOfView = 38f;

        [SerializeField]
        float mirrorBirdCameraOffset = 0.35f;

        [SerializeField]
        float mirrorBirdLookHeight = 0.42f;

        [SerializeField]
        bool enablePerchFillLight = true;

        [SerializeField]
        float perchFillLightIntensity = 1.15f;

        [SerializeField]
        [Range(0.85f, 1.05f)]
        float perchFillLightWarmth = 0.97f;

        [SerializeField]
        float perchPointLightIntensity = 0.95f;

        [SerializeField]
        float perchPointLightRange = 9f;

        Camera mirrorCamera;
        RenderTexture renderTexture;
        GameObject mirrorQuad;
        Material mirrorDisplayMaterial;
        Transform worldMirrorRoot;
        Light perchFillLight;
        Light perchPointLight;
        bool built;
        bool mirrorVisible = true;
        bool worldMirrorAnchored;
        bool usingSceneHierarchyMirror;
        bool birdLayersEnsured;
        Vector3 anchoredPlaneNormal = Vector3.forward;

        static readonly int ReflectionVpId = Shader.PropertyToID("_ReflectionVP");
        static readonly int UseProjectedUvId = Shader.PropertyToID("_UseProjectedUv");

        public void SetBirdBodyRoot(Transform root)
        {
            birdBodyRoot = root;
        }

        public void SetMirrorActive(bool active)
        {
            mirrorVisible = active;
            if (!built && active)
                BuildMirror();

            if (mirrorQuad != null)
                mirrorQuad.SetActive(active);
            if (mirrorCamera != null)
                mirrorCamera.enabled = active;
            if (worldMirrorRoot != null)
                worldMirrorRoot.gameObject.SetActive(active);

            if (perchFillLight != null)
                perchFillLight.enabled = active && enablePerchFillLight;
            if (perchPointLight != null)
                perchPointLight.enabled = active && enablePerchFillLight;
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

        static int BirdMirrorLightMask => 1 << BirdSceneRoles.MirrorLayer;

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
            EnsureSceneBirdMirrorLayers();

            renderTexture = CreateReflectionRenderTexture();
            renderTexture.Create();

            if (useWorldPlacement)
                BuildWorldMirror();
            else
                BuildHeadMirror();

            built = true;
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

            EnsureMirrorCameraRig();
            ApplyMirrorMaterial(mirrorQuad != null ? mirrorQuad.GetComponent<MeshRenderer>() : null);
            SetMirrorDisplayLayers(worldMirrorRoot);

            if (mirrorQuad != null)
                mirrorQuad.layer = BirdSceneRoles.MirrorDisplayLayer;

            ConfigureMirrorVisuals(worldMirrorRoot);
            EnsureBirdFillLight();

            if (manualSceneMirror)
            {
                NormalizeMirrorRootScale(worldMirrorRoot, mirrorQuad != null ? mirrorQuad.transform : null);
                anchoredPlaneNormal = worldMirrorRoot.forward;
                worldMirrorAnchored = true;
                Debug.Log(
                    "[AvianVoice] Mirror uses exact scene Hierarchy transform at " +
                    worldMirrorRoot.position + ". No runtime repositioning.");
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

        void EnsureMirrorCameraRig()
        {
            var camTransform = worldMirrorRoot.Find("MirrorCamera");
            if (camTransform != null)
                mirrorCamera = camTransform.GetComponent<Camera>();

            if (mirrorCamera == null)
            {
                var camGo = new GameObject("MirrorCamera");
                camGo.transform.SetParent(worldMirrorRoot, false);
                mirrorCamera = camGo.AddComponent<Camera>();
            }

            if (mirrorCamera.GetComponent<MirrorReflectionCamera>() == null)
                mirrorCamera.gameObject.AddComponent<MirrorReflectionCamera>();

            ConfigureMirrorCamera(mirrorCamera);
            MirrorUrpCameraBootstrap.Configure(mirrorCamera);
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

            var camGo = new GameObject("MirrorCamera");
            camGo.transform.SetParent(mirrorAnchor, false);
            camGo.transform.localPosition = new Vector3(0f, 0.1f, -0.3f);
            camGo.transform.localRotation = Quaternion.identity;
            mirrorCamera = camGo.AddComponent<Camera>();
            if (mirrorCamera.GetComponent<MirrorReflectionCamera>() == null)
                mirrorCamera.gameObject.AddComponent<MirrorReflectionCamera>();
            ConfigureMirrorCamera(mirrorCamera);
            MirrorUrpCameraBootstrap.Configure(mirrorCamera);

            mirrorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirrorQuad.name = "MirrorQuad";
            mirrorQuad.transform.SetParent(mirrorAnchor, false);
            mirrorQuad.transform.localPosition = new Vector3(0f, 0.05f, 1.35f);
            mirrorQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mirrorQuad.transform.localScale = new Vector3(0.55f, 0.85f, 1f);
            Destroy(mirrorQuad.GetComponent<Collider>());
            ApplyMirrorMaterial(mirrorQuad.GetComponent<MeshRenderer>());
            ConfigureMirrorPanelRenderer(mirrorQuad.GetComponent<Renderer>());
            SetMirrorDisplayLayers(mirrorQuad.transform);
            mirrorCamera.gameObject.layer = BirdSceneRoles.MirrorDisplayLayer;
        }

        void ConfigureMirrorCamera(Camera cam)
        {
            cam.targetTexture = renderTexture;
            cam.cullingMask = mirrorReflectionMask;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = mirrorCameraClipDistance;
            cam.depth = -10;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.forceIntoRenderTexture = true;

            SyncMirrorCameraFromViewer(cam);
        }

        static void SyncMirrorCameraFromViewer(Camera mirrorCam)
        {
            PlanarReflectionUtility.SyncReflectionCameraFromViewer(mirrorCam);
        }

        RenderTexture CreateReflectionRenderTexture()
        {
            return PlanarReflectionUtility.CreateReflectionRenderTexture(
                textureSize,
                matchViewerAspect,
                renderTextureAntiAliasing);
        }

        void ApplyMirrorMaterial(MeshRenderer renderer)
        {
            if (renderer == null)
                return;

            Material mat;
            if (mirrorMaterial != null)
            {
                mat = new Material(mirrorMaterial);
            }
            else
            {
                var shader = Shader.Find("AvianVoice/MirrorRT")
                             ?? Shader.Find("Universal Render Pipeline/Unlit");
                mat = new Material(shader);
            }

            mat.mainTexture = renderTexture;
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", renderTexture);
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", renderTexture);
            if (mat.HasProperty("_Brightness"))
                mat.SetFloat("_Brightness", mirrorDisplayBrightness);
            if (mat.HasProperty("_Vignette"))
                mat.SetFloat("_Vignette", mirrorDisplayVignette);
            if (mat.HasProperty("_UseProjectedUv"))
                mat.SetFloat("_UseProjectedUv", 0f);

            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                Debug.LogError("[AvianVoice] Mirror shader missing. Assign AvianVoice/MirrorRT or add shader to Always Included Shaders.");

            renderer.material = mat;
            mirrorDisplayMaterial = renderer.material;
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

        void EnsureBirdFillLight()
        {
            if (!enablePerchFillLight || worldMirrorRoot == null)
                return;

            EnsurePerchLight(
                "MirrorPerchFillLight",
                ref perchFillLight,
                LightType.Directional);

            EnsurePerchLight(
                "MirrorPerchPointFill",
                ref perchPointLight,
                LightType.Point);

            UpdateBirdFillLight();
        }

        void EnsurePerchLight(string objectName, ref Light light, LightType type)
        {
            if (light != null)
                return;

            var existing = worldMirrorRoot.Find(objectName);
            if (existing != null)
                light = existing.GetComponent<Light>();

            if (light != null)
                return;

            var lightGo = new GameObject(objectName);
            lightGo.transform.SetParent(worldMirrorRoot, false);
            light = lightGo.AddComponent<Light>();
            light.type = type;
            light.shadows = LightShadows.None;
            light.cullingMask = BirdMirrorLightMask;
        }

        void UpdateBirdFillLight()
        {
            if (worldMirrorRoot == null)
                return;

            var enabled = mirrorVisible && enablePerchFillLight;
            var mirrorForward = worldMirrorRoot.forward;
            if (mirrorForward.sqrMagnitude < 0.01f)
                mirrorForward = Vector3.forward;

            if (perchFillLight != null)
            {
                perchFillLight.enabled = enabled;
                perchFillLight.intensity = perchFillLightIntensity;
                perchFillLight.color = new Color(1f, perchFillLightWarmth, perchFillLightWarmth * 0.9f);
                perchFillLight.cullingMask = BirdMirrorLightMask;

                var towardBirds = -mirrorForward;
                towardBirds.y = Mathf.Max(towardBirds.y, 0.08f);
                perchFillLight.transform.rotation = Quaternion.LookRotation(towardBirds.normalized, Vector3.up);
            }

            if (perchPointLight != null)
            {
                perchPointLight.enabled = enabled;
                perchPointLight.intensity = perchPointLightIntensity;
                perchPointLight.range = perchPointLightRange;
                perchPointLight.color = new Color(1f, 0.98f, 0.94f);
                perchPointLight.cullingMask = BirdMirrorLightMask;
                perchPointLight.transform.position = worldMirrorRoot.position + mirrorForward * 0.35f;
            }
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
            anchoredPlaneNormal = mirrorRot * Vector3.forward;
            ResetMirrorChildTransforms();

            worldMirrorAnchored = true;
            Debug.Log(
                "[AvianVoice] Mirror auto-placed at " + mirrorPos +
                " (between birds and XR Origin). Place AvianWorldMirror in the scene to override with Hierarchy edits.");
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
                "[AvianVoice] Mirror root non-uniform scale baked into MirrorQuad; root reset to (1,1,1) for correct reflection.");
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
            if (!mirrorVisible || mirrorCamera == null)
                return;

            if (useWorldPlacement)
            {
                if (!birdLayersEnsured)
                {
                    EnsureSceneBirdMirrorLayers();
                    PlanarReflectionUtility.EnsureBirdRenderersVisible();
                    birdLayersEnsured = true;
                }

                UpdatePlanarReflectionCamera();
                ConfigureMirrorVisuals(worldMirrorRoot);
                UpdateBirdFillLight();
            }
            else
                UpdateHeadMirrorCamera();
        }

        Transform ResolveMirrorSurface()
        {
            if (mirrorQuad != null)
                return mirrorQuad.transform;

            return worldMirrorRoot;
        }

        bool TryResolveMirrorPlane(out Vector3 planePos, out Vector3 planeNormal)
        {
            planePos = default;
            planeNormal = Vector3.forward;

            var surface = ResolveMirrorSurface();
            if (surface == null)
                return false;

            planePos = surface.position;
            planeNormal = surface.forward;

            var viewer = WorldPlacedBird.FindMainCameraTransform();
            if (viewer != null)
            {
                var toViewer = viewer.position - planePos;
                if (Vector3.Dot(planeNormal, toViewer) < 0f)
                    planeNormal = -planeNormal;
            }

            return true;
        }

        void UpdatePlanarReflectionCamera()
        {
            var viewerTransform = WorldPlacedBird.FindMainCameraTransform();
            var viewerCam = viewerTransform != null ? viewerTransform.GetComponent<Camera>() : null;
            if (viewerTransform == null || viewerCam == null)
                return;

            if (!TryResolveMirrorPlane(out var planePos, out var planeNormal))
                return;

            PlanarReflectionUtility.ApplyMirroredViewerMatrix(
                mirrorCamera,
                viewerCam,
                planePos,
                planeNormal);

            var reflectionVp = PlanarReflectionUtility.PrepareReflectionCameraMatrices(
                mirrorCamera,
                planePos,
                planeNormal,
                useObliqueClip: true);
            UpdateMirrorShaderMatrices(reflectionVp);
        }

        void UpdateMirrorShaderMatrices(Matrix4x4 reflectionVp)
        {
            EnsureMirrorDisplayMaterial();
            if (mirrorDisplayMaterial == null)
                return;

            if (mirrorDisplayMaterial.HasProperty(ReflectionVpId))
                mirrorDisplayMaterial.SetMatrix(ReflectionVpId, reflectionVp);
            if (mirrorDisplayMaterial.HasProperty(UseProjectedUvId))
                mirrorDisplayMaterial.SetFloat(UseProjectedUvId, 0f);

            if (mirrorDisplayMaterial.HasProperty("_MainTex"))
                mirrorDisplayMaterial.SetTexture("_MainTex", renderTexture);
        }

        void EnsureMirrorDisplayMaterial()
        {
            if (mirrorQuad == null)
                return;

            var renderer = mirrorQuad.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            if (mirrorDisplayMaterial != null
                && mirrorDisplayMaterial.shader != null
                && mirrorDisplayMaterial.shader.name.Contains("MirrorRT"))
                return;

            ApplyMirrorMaterial(renderer);
        }

        void UpdateHeadMirrorCamera()
        {
            var focus = ResolveFocusPoint();
            if (focus == null || mirrorAnchor == null)
                return;

            var pos = focus.Value;
            mirrorCamera.transform.position = pos + Vector3.up * 0.2f - mirrorAnchor.forward * 1.2f;
            mirrorCamera.transform.LookAt(pos + Vector3.up * 0.5f);
        }

        Vector3? ResolveFocusPoint()
        {
            if (BirdSceneRoles.Player != null && BirdSceneRoles.Friend != null)
                return (BirdSceneRoles.Player.position + BirdSceneRoles.Friend.position) * 0.5f;

            var friend = GameObject.Find(BirdSceneRoles.FriendName);
            var player = GameObject.Find(BirdSceneRoles.PlayerName);
            if (friend != null && player != null)
                return (friend.transform.position + player.transform.position) * 0.5f;

            if (birdBodyRoot != null)
                return birdBodyRoot.position;
            return null;
        }

        void OnDestroy()
        {
            if (renderTexture != null)
                renderTexture.Release();
        }

        static void EnsureSceneBirdMirrorLayers()
        {
            if (BirdSceneRoles.Friend != null)
                SetLayerRecursive(BirdSceneRoles.Friend.gameObject, BirdSceneRoles.MirrorLayer);
            if (BirdSceneRoles.Player != null)
                SetLayerRecursive(BirdSceneRoles.Player.gameObject, BirdSceneRoles.MirrorLayer);
            if (BirdSceneRoles.Friend == null && BirdSceneRoles.Player == null)
            {
                foreach (var role in Object.FindObjectsByType<BirdSceneRole>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (role != null)
                        SetLayerRecursive(role.gameObject, BirdSceneRoles.MirrorLayer);
                }
            }
        }

        static void SetMirrorDisplayLayers(Transform root)
        {
            if (root == null)
                return;

            SetLayerRecursive(root.gameObject, BirdSceneRoles.MirrorDisplayLayer);
            var mirrorCam = root.Find("MirrorCamera");
            if (mirrorCam != null)
                mirrorCam.gameObject.layer = BirdSceneRoles.MirrorDisplayLayer;
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
