using AvianVoice.Core;
using AvianVoice.Physiology;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Parrot breath chest: scale Spine2/Spine3. Neck scale is counter-adjusted so the head stays stable.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class ParrotBreathEmbodiment : MonoBehaviour
    {
        [SerializeField]
        AvianVoiceSettings settings;

        [SerializeField]
        bool compensateNeckFromUpperChestScale = true;

        BreathSimulator breath;
        Transform chest;
        Transform chestMid;
        Transform neck;
        Vector3 chestRestScale = Vector3.one;
        Vector3 chestMidRestScale = Vector3.one;
        Vector3 neckRestScale = Vector3.one;
        float smoothedDepth;

        void Awake()
        {
            if (settings == null)
                settings = FindAvianSettings();
            breath = FindAnyObjectByType<BreathSimulator>();
            CacheBones();
            ParrotEmbodimentWiring.DisablePackIdleAnimation(GetComponent<Animator>());
        }

        void Start()
        {
            StartCoroutine(RefreshWingRestPoseNextFrame());
        }

        System.Collections.IEnumerator RefreshWingRestPoseNextFrame()
        {
            yield return null;
            FindAnyObjectByType<WingHandMapper>()?.RefreshRestPose();
        }

        void OnDisable()
        {
            RestoreChestScales();
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

        void ApplyBreathPose(float depth)
        {
            var emphasis = BreathEmphasis(depth);

            if (emphasis < 0.001f)
            {
                RestoreChestScales();
                return;
            }

            ApplyChestBone(chestMid, chestMidRestScale, ParrotChestBreathTuning.SecondaryChestInhaleScale, emphasis,
                out var midChestFactor);
            ApplyChestBone(chest, chestRestScale, ParrotChestBreathTuning.ChestInhaleScale, emphasis,
                out var upperChestFactor);
            CompensateNeckScale(midChestFactor * upperChestFactor);
        }

        void ApplyChestBone(
            Transform bone,
            Vector3 restScale,
            float amount,
            float emphasis,
            out float factor)
        {
            factor = 1f;
            if (bone == null || amount <= 0f)
                return;

            factor = ParrotChestBreathTuning.ComputeChestFactor(amount, emphasis);
            bone.localScale = restScale * factor;
        }

        void CompensateNeckScale(float combinedChestFactor)
        {
            if (!compensateNeckFromUpperChestScale || neck == null || combinedChestFactor <= 0.0001f)
                return;

            var inv = 1f / combinedChestFactor;
            neck.localScale = neckRestScale * inv;
        }

        void RestoreChestScales()
        {
            if (chestMid != null)
                chestMid.localScale = chestMidRestScale;
            if (chest != null)
                chest.localScale = chestRestScale;
            if (neck != null)
                neck.localScale = neckRestScale;
        }

        void CacheBones()
        {
            ParrotEmbodimentWiring.TryResolve(transform, out var refs);
            chest = refs.chest;
            chestMid = refs.secondaryChest;
            neck = refs.neck;

            if (chest != null)
                chestRestScale = chest.localScale;
            if (chestMid != null)
                chestMidRestScale = chestMid.localScale;
            if (neck != null)
                neckRestScale = neck.localScale;
        }

        static float BreathEmphasis(float depth) => ParrotChestBreathTuning.DepthToEmphasis(depth);

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
