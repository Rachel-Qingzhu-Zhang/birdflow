using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Physiology;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Parrot breath chest: scale Spine2/Spine3. Neck scale is counter-adjusted so the head stays stable.
    /// Microphone energy adds a slight extra chest and a small vertical bob.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class ParrotBreathEmbodiment : MonoBehaviour
    {
        [SerializeField]
        AvianVoiceSettings settings;

        [SerializeField]
        bool compensateNeckFromUpperChestScale = true;

        BreathSimulator breath;
        VoiceAnalyzer analyzer;
        ParrotFlightPresentation flight;
        Transform chest;
        Transform chestMid;
        Transform neck;
        Transform leftWing;
        Transform rightWing;
        Vector3 chestRestScale = Vector3.one;
        Vector3 chestMidRestScale = Vector3.one;
        Vector3 neckRestScale = Vector3.one;
        Quaternion leftWingRest = Quaternion.identity;
        Quaternion rightWingRest = Quaternion.identity;
        float smoothedDepth;
        bool performanceActive;
        float performanceChest;
        bool performanceFlap;
        float smoothedPerformance;
        float smoothedVocal;
        Vector3 floatRestPosition;
        bool floatRestCaptured;

        public void SetPerformancePose(float chestEmphasis, bool flapWings)
        {
            performanceActive = true;
            performanceChest = Mathf.Clamp01(chestEmphasis);
            performanceFlap = flapWings;
        }

        public void ClearPerformancePose()
        {
            if (!performanceActive)
                return;
            performanceActive = false;
            performanceFlap = false;
            smoothedPerformance = 0f;
            RestoreChestScales();
            RestoreWorldFloat();
            ParrotBreathWingPose.Restore(leftWing, rightWing, leftWingRest, rightWingRest);
        }

        void Awake()
        {
            if (settings == null)
                settings = FindAvianSettings();
            breath = FindAnyObjectByType<BreathSimulator>();
            analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            flight = GetComponent<ParrotFlightPresentation>();
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
            RestoreWorldFloat();
            ParrotBreathWingPose.Restore(leftWing, rightWing, leftWingRest, rightWingRest);
        }

        void LateUpdate()
        {
            if (breath == null)
                breath = FindAnyObjectByType<BreathSimulator>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            if (flight == null)
                flight = GetComponent<ParrotFlightPresentation>();
            if (settings == null)
                settings = FindAvianSettings();

            var vocalSmooth = 1f - Mathf.Exp(-14f * Time.deltaTime);
            var vocalTarget = analyzer != null ? BirdPresenceMotion.VocalEnergy(analyzer.Current) : 0f;
            smoothedVocal = Mathf.Lerp(smoothedVocal, vocalTarget, vocalSmooth);

            if (performanceActive)
            {
                ApplyPerformancePose();
                ApplyWorldFloat(Mathf.Max(smoothedPerformance, smoothedVocal), 0.02f, 0.012f, 1.4f);
                return;
            }

            if (settings == null || breath == null)
            {
                ApplyWorldFloat(smoothedVocal, 0.018f, 0.01f, 1.35f);
                return;
            }

            var smooth = 1f - Mathf.Exp(-settings.paramSmooth * 3f * Time.deltaTime);
            smoothedDepth = Mathf.Lerp(smoothedDepth, breath.Current.depth, smooth);
            ApplyBreathPose(smoothedDepth, smoothedVocal);
            ApplyWorldFloat(smoothedVocal, 0.018f, 0.01f, 1.35f);
        }

        void ApplyPerformancePose()
        {
            var smooth = 1f - Mathf.Exp(-10f * Time.deltaTime);
            smoothedPerformance = Mathf.Lerp(smoothedPerformance, performanceChest, smooth);
            if (smoothedPerformance < 0.02f && !performanceFlap && smoothedVocal < 0.02f)
            {
                RestoreChestScales();
                ParrotBreathWingPose.Restore(leftWing, rightWing, leftWingRest, rightWingRest);
                return;
            }

            var chestEnergy = Mathf.Max(smoothedPerformance, smoothedVocal * 0.7f);
            ApplyChestBone(chestMid, chestMidRestScale, ParrotChestBreathTuning.SecondaryChestInhaleScale,
                chestEnergy, out var midChestFactor);
            ApplyChestBone(chest, chestRestScale, ParrotChestBreathTuning.ChestInhaleScale, chestEnergy,
                out var upperChestFactor);
            CompensateNeckScale(midChestFactor * upperChestFactor);
            if (performanceFlap)
            {
                ParrotBreathWingPose.Apply(
                    leftWing, rightWing, leftWingRest, rightWingRest,
                    0.95f, true, Time.time);
            }
            else if (smoothedVocal > 0.05f)
            {
                ParrotBreathWingPose.Apply(
                    leftWing, rightWing, leftWingRest, rightWingRest,
                    0.28f + smoothedVocal * 0.35f, true, Time.time);
            }
            else
            {
                ParrotBreathWingPose.Restore(leftWing, rightWing, leftWingRest, rightWingRest);
            }
        }

        void ApplyBreathPose(float depth, float vocal)
        {
            var emphasis = BreathEmphasis(depth) + vocal * 0.22f;

            if (emphasis < 0.001f)
            {
                RestoreChestScales();
                var idleAnimator = GetComponent<Animator>();
                if (idleAnimator == null || !idleAnimator.enabled)
                    ParrotBreathWingPose.Restore(leftWing, rightWing, leftWingRest, rightWingRest);
                return;
            }

            ApplyChestBone(chestMid, chestMidRestScale, ParrotChestBreathTuning.SecondaryChestInhaleScale, emphasis,
                out var midChestFactor);
            ApplyChestBone(chest, chestRestScale, ParrotChestBreathTuning.ChestInhaleScale, emphasis,
                out var upperChestFactor);
            CompensateNeckScale(midChestFactor * upperChestFactor);
            var packAnimator = GetComponent<Animator>();
            if (packAnimator == null || !packAnimator.enabled)
            {
                var wingDepth = Mathf.Clamp01(depth + vocal * 0.4f);
                ParrotBreathWingPose.Apply(
                    leftWing, rightWing, leftWingRest, rightWingRest,
                    wingDepth, breath != null && breath.Current.isInhaling, Time.time);
            }
        }

        void ApplyWorldFloat(float energy, float liftMeters, float bobMeters, float bobHz)
        {
            if (flight != null && flight.IsFlying)
            {
                RestoreWorldFloat();
                return;
            }

            if (energy < 0.02f)
            {
                RestoreWorldFloat();
                return;
            }

            if (!floatRestCaptured)
            {
                floatRestPosition = transform.position;
                floatRestCaptured = true;
            }

            var height = BirdPresenceMotion.FloatHeight(energy, Time.time, liftMeters, bobMeters, bobHz);
            transform.position = floatRestPosition + Vector3.up * height;
        }

        void RestoreWorldFloat()
        {
            if (!floatRestCaptured)
                return;
            transform.position = floatRestPosition;
            floatRestCaptured = false;
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
            leftWing = refs.leftWing;
            rightWing = refs.rightWing;

            if (chest != null)
                chestRestScale = chest.localScale;
            if (chestMid != null)
                chestMidRestScale = chestMid.localScale;
            if (neck != null)
                neckRestScale = neck.localScale;
            if (leftWing != null)
                leftWingRest = leftWing.localRotation;
            if (rightWing != null)
                rightWingRest = rightWing.localRotation;
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
