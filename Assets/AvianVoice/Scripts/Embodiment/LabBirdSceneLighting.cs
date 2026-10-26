using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Perch-task bird readability: close point lights (URP additional lights) + albedo/emission lift.
    /// Extra directional lights are unreliable in URP (only one main directional), so we avoid them.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public class LabBirdSceneLighting : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        float keyPointIntensity = 5.5f;

        [SerializeField]
        float fillPointIntensity = 3.8f;

        [SerializeField]
        float rimPointIntensity = 2.6f;

        [SerializeField]
        float pointRange = 7f;

        [SerializeField]
        float emissionLift = 0.35f;

        [SerializeField]
        float ambientBoost = 0.7f;

        [SerializeField]
        Color keyColor = new(1f, 0.97f, 0.92f);

        [SerializeField]
        Color fillColor = new(0.85f, 0.92f, 1f);

        [SerializeField]
        Color rimColor = new(1f, 0.9f, 0.85f);

        Light keyLight;
        Light fillLight;
        Light rimLight;
        bool lightingActive;
        float savedAmbientIntensity = 1f;
        bool ambientSaved;
        readonly List<Renderer> birdRenderers = new();
        readonly List<bool> savedReceiveShadows = new();
        MaterialPropertyBlock propertyBlock;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public static LabBirdSceneLighting Ensure()
        {
            var existing = Object.FindAnyObjectByType<LabBirdSceneLighting>();
            if (existing != null)
                return existing;

            var host = GameObject.Find("TaskRoot") ?? new GameObject("TaskRoot");
            return host.GetComponent<LabBirdSceneLighting>() ?? host.AddComponent<LabBirdSceneLighting>();
        }

        public static void SetPerchLightingActive(bool active)
        {
            var lighting = Ensure();
            lighting.SetActive(active);
        }

        public void SetActive(bool active)
        {
            if (lightingActive == active)
            {
                if (active)
                {
                    RefreshBirdRenderers();
                    UpdateLightPlacement();
                    ApplyMaterialLift();
                }

                return;
            }

            lightingActive = active;
            EnsureLights();
            ApplyEnabled();

            if (active)
            {
                SaveAndBoostAmbient();
                RefreshBirdRenderers();
                ApplyShadowBypass(true);
                UpdateLightPlacement();
                ApplyMaterialLift();
            }
            else
            {
                ClearMaterialLift();
                ApplyShadowBypass(false);
                RestoreAmbient();
            }
        }

        void LateUpdate()
        {
            if (!lightingActive)
                return;

            UpdateLightPlacement();
            ApplyMaterialLift();
        }

        void OnDisable()
        {
            if (!lightingActive)
                return;

            ClearMaterialLift();
            ApplyShadowBypass(false);
            RestoreAmbient();
            lightingActive = false;
            ApplyEnabled();
        }

        void EnsureLights()
        {
            keyLight = EnsureLight(ref keyLight, "LabBirdKeyPoint", LightType.Point);
            fillLight = EnsureLight(ref fillLight, "LabBirdFillPoint", LightType.Point);
            rimLight = EnsureLight(ref rimLight, "LabBirdRimPoint", LightType.Point);

            Configure(keyLight, keyPointIntensity, keyColor, pointRange);
            Configure(fillLight, fillPointIntensity, fillColor, pointRange * 1.15f);
            Configure(rimLight, rimPointIntensity, rimColor, pointRange);
        }

        Light EnsureLight(ref Light cached, string objectName, LightType type)
        {
            if (cached != null)
                return cached;

            var existing = transform.Find(objectName);
            if (existing != null)
            {
                cached = existing.GetComponent<Light>() ?? existing.gameObject.AddComponent<Light>();
                cached.type = type;
                return cached;
            }

            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            cached = go.AddComponent<Light>();
            cached.type = type;
            return cached;
        }

        static void Configure(Light light, float intensity, Color color, float range)
        {
            if (light == null)
                return;

            light.type = LightType.Point;
            light.intensity = intensity;
            light.color = color;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.cullingMask = ~0;
        }

        void ApplyEnabled()
        {
            if (keyLight != null)
                keyLight.enabled = lightingActive;
            if (fillLight != null)
                fillLight.enabled = lightingActive;
            if (rimLight != null)
                rimLight.enabled = lightingActive;
        }

        void UpdateLightPlacement()
        {
            EnsureLights();

            var focus = ResolveFocus();
            var view = WorldPlacedBird.FindMainCameraTransform();
            var viewPos = view != null ? view.position : focus + Vector3.forward * 2f;
            var toBirds = focus - viewPos;
            if (toBirds.sqrMagnitude < 0.001f)
                toBirds = Vector3.forward;
            toBirds.Normalize();

            // Key: slightly above and in front of the birds (viewer side).
            keyLight.transform.position = focus - toBirds * 1.1f + Vector3.up * 1.35f;
            // Fill: opposite side / overhead to kill canopy shadows.
            fillLight.transform.position = focus + toBirds * 0.35f + Vector3.up * 1.8f;
            // Rim: behind birds for silhouette separation.
            rimLight.transform.position = focus + toBirds * 1.4f + Vector3.up * 0.55f;
        }

        void RefreshBirdRenderers()
        {
            birdRenderers.Clear();
            CollectBirdRenderers(BirdSceneRoles.Friend);
            CollectBirdRenderers(BirdSceneRoles.Player);
        }

        void CollectBirdRenderers(Transform bird)
        {
            if (bird == null)
                return;

            foreach (var renderer in bird.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                if (renderer.gameObject.name is "PlayerMarker" or "FriendMarker")
                    continue;
                // Never touch mirror panel / display-layer meshes.
                if (renderer.gameObject.layer == BirdSceneRoles.MirrorDisplayLayer)
                    continue;
                if (renderer.GetComponentInParent<Fragilem17.MirrorsAndPortals.MirrorRenderer>() != null)
                    continue;
                birdRenderers.Add(renderer);
            }
        }

        void ApplyShadowBypass(bool bypass)
        {
            if (bypass)
            {
                savedReceiveShadows.Clear();
                for (var i = 0; i < birdRenderers.Count; i++)
                {
                    var renderer = birdRenderers[i];
                    if (renderer == null)
                    {
                        savedReceiveShadows.Add(true);
                        continue;
                    }

                    savedReceiveShadows.Add(renderer.receiveShadows);
                    renderer.receiveShadows = false;
                    if (renderer is SkinnedMeshRenderer skinned)
                        skinned.updateWhenOffscreen = true;
                }

                return;
            }

            for (var i = 0; i < birdRenderers.Count; i++)
            {
                var renderer = birdRenderers[i];
                if (renderer == null)
                    continue;
                if (i < savedReceiveShadows.Count)
                    renderer.receiveShadows = savedReceiveShadows[i];
            }

            savedReceiveShadows.Clear();
        }

        void ApplyMaterialLift()
        {
            if (birdRenderers.Count == 0)
                RefreshBirdRenderers();

            for (var i = 0; i < birdRenderers.Count; i++)
            {
                var renderer = birdRenderers[i];
                if (renderer == null)
                    continue;

                var mat = renderer.sharedMaterial;
                if (mat == null)
                    continue;

                // Only lift emission here so ParrotMatchVisuals can still tint _BaseColor.
                if (!mat.HasProperty(EmissionColorId))
                {
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    continue;
                }

                propertyBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                var emission = propertyBlock.HasColor(EmissionColorId)
                    ? propertyBlock.GetColor(EmissionColorId)
                    : mat.GetColor(EmissionColorId);
                var lift = new Color(emissionLift, emissionLift * 0.95f, emissionLift * 0.9f, 1f);
                propertyBlock.SetColor(EmissionColorId, emission + lift);
                renderer.SetPropertyBlock(propertyBlock);
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }
        }

        void ClearMaterialLift()
        {
            for (var i = 0; i < birdRenderers.Count; i++)
            {
                var renderer = birdRenderers[i];
                if (renderer == null)
                    continue;
                renderer.SetPropertyBlock(null);
            }
        }

        void SaveAndBoostAmbient()
        {
            if (!ambientSaved)
            {
                savedAmbientIntensity = RenderSettings.ambientIntensity;
                ambientSaved = true;
            }

            RenderSettings.ambientIntensity = Mathf.Max(savedAmbientIntensity, 1f) + ambientBoost;
        }

        void RestoreAmbient()
        {
            if (!ambientSaved)
                return;

            RenderSettings.ambientIntensity = savedAmbientIntensity;
            ambientSaved = false;
        }

        static Vector3 ResolveFocus()
        {
            if (BirdSceneRoles.TryGetFlockOrbitCenter(out var center))
                return center + Vector3.up * 0.4f;

            if (BirdSceneRoles.Friend != null)
                return BirdSceneRoles.Friend.position + Vector3.up * 0.45f;

            if (BirdSceneRoles.Player != null)
                return BirdSceneRoles.Player.position + Vector3.up * 0.45f;

            return Vector3.zero;
        }
    }
}
