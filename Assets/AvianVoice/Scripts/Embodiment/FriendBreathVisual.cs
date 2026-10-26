using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Applies bird friend demonstrator breath depth to friend parrot chest bones.
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

        void Awake()
        {
            if (settings == null)
                settings = FindAvianSettings();
            demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            CacheBones();
            ParrotEmbodimentWiring.DisablePackIdleAnimation(GetComponent<Animator>());
        }

        void LateUpdate()
        {
            if (demonstrator == null)
                demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            if (settings == null || demonstrator == null)
                return;

            var smooth = 1f - Mathf.Exp(-settings.paramSmooth * 3f * Time.deltaTime);
            smoothedDepth = Mathf.Lerp(smoothedDepth, demonstrator.CurrentDepth, smooth);
            ApplyBreathPose(smoothedDepth);
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
