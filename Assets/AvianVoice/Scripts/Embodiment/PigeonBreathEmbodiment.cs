using AvianVoice.Core;
using AvianVoice.Physiology;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Breath-driven chest scale for Pigeon_PBR. Wings are handled by WingHandMapper only.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class PigeonBreathEmbodiment : MonoBehaviour
    {
        [SerializeField]
        AvianVoiceSettings settings;

        [Header("Chest (scale, not twist)")]
        [SerializeField]
        bool useChestScale = true;

        [SerializeField]
        float chestInhaleScale = 0.12f;

        [SerializeField]
        float neckInhaleScale = 0.06f;

        [Header("Optional subtle chest lift (degrees)")]
        [SerializeField]
        bool useChestPitch = true;

        [SerializeField]
        float chestPitchMax = 4f;

        BreathSimulator breath;
        Transform chest;
        Transform neck;

        Vector3 chestRestScale = Vector3.one;
        Vector3 neckRestScale = Vector3.one;
        Quaternion chestRestRotation;
        Quaternion neckRestRotation;
        float smoothedDepth;

        void Awake()
        {
            if (settings == null)
                settings = FindAvianSettings();
            breath = FindAnyObjectByType<BreathSimulator>();
            CacheRestPose();
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        public void CacheRestPose()
        {
            PigeonEmbodimentWiring.TryResolve(transform, out var refs);
            chest = refs.chest;
            neck = refs.secondaryChest;

            if (chest != null)
            {
                chestRestScale = chest.localScale;
                chestRestRotation = chest.localRotation;
            }

            if (neck != null)
            {
                neckRestScale = neck.localScale;
                neckRestRotation = neck.localRotation;
            }
        }

        void LateUpdate()
        {
            if (breath == null)
                breath = FindAnyObjectByType<BreathSimulator>();
            if (settings == null)
                settings = FindAvianSettings();
            if (settings == null || breath == null)
                return;

            var smooth = 1f - Mathf.Exp(-settings.paramSmooth * 3f * Time.deltaTime);
            smoothedDepth = Mathf.Lerp(smoothedDepth, breath.Current.depth, smooth);
            ApplyBreathPose(smoothedDepth);
        }

        void OnBeforeRender()
        {
            if (settings == null || breath == null)
                return;
            ApplyBreathPose(smoothedDepth);
        }

        void ApplyBreathPose(float depth)
        {
            var emphasis = BreathEmphasis(depth);
            if (emphasis < 0.001f)
                return;

            if (chest != null)
            {
                if (useChestScale)
                {
                    var scale = 1f + emphasis * chestInhaleScale;
                    chest.localScale = chestRestScale * scale;
                }

                if (useChestPitch)
                {
                    var pitch = -emphasis * chestPitchMax;
                    chest.localRotation = chestRestRotation * Quaternion.Euler(pitch, 0f, 0f);
                }
            }

            if (neck != null && useChestScale)
            {
                var scale = 1f + emphasis * neckInhaleScale;
                neck.localScale = neckRestScale * scale;
            }
        }

        static float BreathEmphasis(float depth) => Mathf.Pow(Mathf.Clamp01(depth), 0.55f);

        static AvianVoiceSettings FindAvianSettings()
        {
            foreach (var asset in Resources.FindObjectsOfTypeAll<AvianVoiceSettings>())
            {
                if (asset != null && asset.name.Contains("AvianVoiceSettings"))
                    return asset;
            }

            return null;
        }
    }
}
