using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Applies bird friend demonstrator breath depth to friend parrot chest bones.
    /// During song performance the chest expands and the body bobs slightly instead of staying planted.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class FriendBreathVisual : MonoBehaviour
    {
        [SerializeField]
        AvianVoiceSettings settings;

        BirdFriendDemonstrator demonstrator;
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
        }

        void Awake()
        {
            if (settings == null)
                settings = FindAvianSettings();
            demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            CacheBones();
            ParrotEmbodimentWiring.DisablePackIdleAnimation(GetComponent<Animator>());
        }

        void OnEnable()
        {
            floatRestCaptured = false;
        }

        void OnDisable()
        {
            RestoreWorldFloat();
        }

        void LateUpdate()
        {
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            if (performanceActive)
            {
                ApplyPerformancePose();
                return;
            }

            RestoreWorldFloat();
            if (settings == null || demonstrator == null)
                return;

            var smooth = 1f - Mathf.Exp(-settings.paramSmooth * 3f * Time.deltaTime);
            smoothedDepth = Mathf.Lerp(smoothedDepth, demonstrator.CurrentDepth, smooth);
            ApplyBreathPose(smoothedDepth);
        }

        void ApplyPerformancePose()
        {
            var smooth = 1f - Mathf.Exp(-10f * Time.deltaTime);
            smoothedPerformance = Mathf.Lerp(smoothedPerformance, performanceChest, smooth);
            if (smoothedPerformance < 0.02f && !performanceFlap)
            {
                RestoreChestScales();
                RestoreWorldFloat();
                return;
            }

            ApplyChestBone(chestMid, chestMidRestScale, ParrotChestBreathTuning.SecondaryChestInhaleScale,
                smoothedPerformance, out var midFactor);
            ApplyChestBone(chest, chestRestScale, ParrotChestBreathTuning.ChestInhaleScale, smoothedPerformance,
                out var upperFactor);
            CompensateNeck(midFactor * upperFactor);

            if (performanceFlap)
            {
                ParrotBreathWingPose.Apply(
                    leftWing, rightWing, leftWingRest, rightWingRest,
                    0.95f, true, Time.time);
            }
            else if (smoothedPerformance > 0.25f)
            {
                ParrotBreathWingPose.Apply(
                    leftWing, rightWing, leftWingRest, rightWingRest,
                    0.4f + smoothedPerformance * 0.2f, true, Time.time * 0.7f);
            }
            else
            {
                ParrotBreathWingPose.Restore(leftWing, rightWing, leftWingRest, rightWingRest);
            }

            ApplyWorldFloat(smoothedPerformance);
        }

        void ApplyBreathPose(float depth)
        {
            var emphasis = ParrotChestBreathTuning.DepthToEmphasis(depth);
            if (emphasis < 0.001f)
            {
                RestoreChestScales();
                return;
            }

            ApplyChestBone(chestMid, chestMidRestScale, ParrotChestBreathTuning.SecondaryChestInhaleScale, emphasis,
                out var midFactor);
            ApplyChestBone(chest, chestRestScale, ParrotChestBreathTuning.ChestInhaleScale, emphasis,
                out var upperFactor);
            CompensateNeck(midFactor * upperFactor);
            var inhaling = demonstrator == null || demonstrator.IsInhaling;
            ParrotBreathWingPose.Apply(
                leftWing, rightWing, leftWingRest, rightWingRest,
                depth, inhaling, Time.time);
        }

        void ApplyWorldFloat(float energy)
        {
            if (energy < 0.05f)
            {
                RestoreWorldFloat();
                return;
            }

            if (!floatRestCaptured)
            {
                floatRestPosition = transform.position;
                floatRestCaptured = true;
            }

            var height = BirdPresenceMotion.FloatHeight(energy, Time.time, 0.05f, 0.022f, 1.15f);
            transform.position = floatRestPosition + Vector3.up * height;
        }

        void RestoreWorldFloat()
        {
            if (!floatRestCaptured)
                return;
            transform.position = floatRestPosition;
            floatRestCaptured = false;
        }

        void ApplyChestBone(Transform bone, Vector3 rest, float amount, float emphasis, out float factor)
        {
            factor = 1f;
            if (bone == null || amount <= 0f)
                return;
            factor = ParrotChestBreathTuning.ComputeChestFactor(amount, emphasis);
            bone.localScale = rest * factor;
        }

        void CompensateNeck(float combined)
        {
            if (neck == null || combined <= 0.0001f)
                return;
            neck.localScale = neckRestScale * (1f / combined);
        }

        void RestoreChestScales()
        {
            if (chestMid != null)
                chestMid.localScale = chestMidRestScale;
            if (chest != null)
                chest.localScale = chestRestScale;
            if (neck != null)
                neck.localScale = neckRestScale;
            ParrotBreathWingPose.Restore(leftWing, rightWing, leftWingRest, rightWingRest);
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
