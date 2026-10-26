using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Pitch-match feedback on parrot or placeholder body (emission, base tint, brief light pulse).
    /// </summary>
    public class ParrotMatchVisuals : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer[] renderers = System.Array.Empty<Renderer>();
        MaterialPropertyBlock block;
        Color[] baseColors;
        Light pulseLight;
        float pulseTimer;

        public void BindFromScene()
        {
            renderers = ResolveRenderers();
            CacheBaseColors();
            EnsurePulseLight();
        }

        public void ApplyMatch(float match)
        {
            if (renderers == null || renderers.Length == 0)
                renderers = ResolveRenderers();

            match = Mathf.Clamp01(match);
            block ??= new MaterialPropertyBlock();
            if (baseColors == null || baseColors.Length != renderers.Length)
                CacheBaseColors();

            var glow = Color.Lerp(Color.black, Color.cyan * 2.5f, match);
            var tint = Color.Lerp(Color.white, new Color(0.55f, 0.95f, 1f), match * 0.85f);

            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;

                r.GetPropertyBlock(block);
                var mat = r.sharedMaterial;
                if (mat != null && mat.HasProperty(EmissionColorId))
                    block.SetColor(EmissionColorId, glow);
                if (mat != null && mat.HasProperty(BaseColorId))
                {
                    // Keep original albedo; only tint toward cyan as match rises.
                    var baseCol = i < baseColors.Length ? baseColors[i] : Color.white;
                    block.SetColor(BaseColorId, baseCol * tint);
                }

                r.SetPropertyBlock(block);
            }

            if (pulseLight != null)
                pulseLight.intensity = Mathf.Lerp(0f, 2.8f, match);
        }

        public void PulsePerfect()
        {
            pulseTimer = 0.55f;
            ApplyMatch(1f);
            if (pulseLight != null)
                pulseLight.intensity = 5f;
        }

        void Update()
        {
            if (pulseTimer <= 0f)
                return;

            pulseTimer -= Time.deltaTime;
            if (pulseLight != null && pulseTimer > 0f)
                pulseLight.intensity = Mathf.Lerp(0f, 4f, pulseTimer / 0.55f);
            else if (pulseLight != null)
                pulseLight.intensity = 0f;
        }

        void EnsurePulseLight()
        {
            if (pulseLight != null)
                return;

            var go = new GameObject("PitchMatchPulseLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.35f, 0.15f);
            pulseLight = go.AddComponent<Light>();
            pulseLight.type = LightType.Point;
            pulseLight.color = new Color(0.4f, 0.95f, 1f);
            pulseLight.range = 2.5f;
            pulseLight.intensity = 0f;
            pulseLight.shadows = LightShadows.None;
        }

        Renderer[] ResolveRenderers()
        {
            var selfSkinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (selfSkinned.Length > 0)
                return selfSkinned;

            var selfMesh = GetComponentsInChildren<MeshRenderer>(true);
            if (selfMesh.Length > 0)
                return selfMesh;

            var parrot = WorldPlacedBird.FindParrotInScene();
            if (parrot != null)
            {
                var skinned = parrot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skinned.Length > 0)
                    return skinned;

                var mesh = parrot.GetComponentsInChildren<MeshRenderer>(true);
                if (mesh.Length > 0)
                    return mesh;
            }

            var body = Object.FindAnyObjectByType<AvianBodyPlaceholder>();
            if (body != null && body.Chest != null)
            {
                var chest = body.Chest.GetComponent<Renderer>();
                if (chest != null)
                    return new[] { chest };
            }

            return System.Array.Empty<Renderer>();
        }

        void CacheBaseColors()
        {
            if (renderers == null || renderers.Length == 0)
                return;

            baseColors = new Color[renderers.Length];
            block ??= new MaterialPropertyBlock();
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterial == null)
                    continue;

                if (r.sharedMaterial.HasProperty(BaseColorId))
                    baseColors[i] = r.sharedMaterial.GetColor(BaseColorId);
                else
                    baseColors[i] = Color.white;
            }
        }
    }
}
