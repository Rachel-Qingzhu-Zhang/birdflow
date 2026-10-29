using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    public class BirdBodyController : MonoBehaviour
    {
        AvianVoiceSettings settings;
        Animator animator;
        SkinnedMeshRenderer beakMesh;
        int beakBlendIndex = -1;
        Transform chestTransform;
        Transform secondaryChestTransform;

        float breathDepth;
        float wingTremor;
        float beakOpen;
        float vocalLevel;
        Vector3 chestBaseScale = Vector3.one;
        Vector3 secondaryChestBaseScale = Vector3.one;

        static readonly int BreathDepthId = Animator.StringToHash("BreathDepth");
        static readonly int WingTremorId = Animator.StringToHash("WingTremor");
        static readonly int VocalId = Animator.StringToHash("Vocal");
        static readonly int ExhaleId = Animator.StringToHash("Exhale");

        public float CrossModalIntensity { get; private set; }

        public void Initialize(
            AvianVoiceSettings voiceSettings,
            Animator birdAnimator,
            SkinnedMeshRenderer beakRenderer,
            int blendIndex,
            Transform chest,
            Transform secondaryChest = null)
        {
            settings = voiceSettings;
            animator = birdAnimator;
            beakMesh = beakRenderer;
            beakBlendIndex = blendIndex;
            chestTransform = chest;
            secondaryChestTransform = secondaryChest;

            if (chestTransform != null)
                chestBaseScale = chestTransform.localScale;
            if (secondaryChestTransform != null)
                secondaryChestBaseScale = secondaryChestTransform.localScale;
        }

        public void SetSecondaryChest(Transform secondaryChest)
        {
            secondaryChestTransform = secondaryChest;
            if (secondaryChestTransform != null)
                secondaryChestBaseScale = secondaryChestTransform.localScale;
        }

        public void Apply(VoiceFrame voice, BreathFrame breath)
        {
            if (settings == null)
                return;

            var targetBreath = breath.depth;
            var targetVocal = voice.loudness * voice.voiced;
            var targetTremor = breath.phase > 0.6f ? voice.loudness * 0.5f : 0f;
            var pitchNorm = voice.pitchHz > 0f
                ? Mathf.InverseLerp(settings.minPitchHz, settings.maxPitchHz, voice.pitchHz)
                : 0f;

            CrossModalIntensity = breath.depth * voice.loudness * Mathf.Lerp(0.5f, 1f, pitchNorm);

            var dt = Time.deltaTime;
            var smooth = 1f - Mathf.Exp(-settings.paramSmooth * dt);
            breathDepth = Mathf.Lerp(breathDepth, targetBreath, smooth);
            beakOpen = Mathf.Lerp(beakOpen, targetVocal, smooth);
            wingTremor = Mathf.Lerp(wingTremor, targetTremor, smooth);
            vocalLevel = Mathf.Lerp(vocalLevel, targetVocal, smooth);

            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                if (HasParameter(BreathDepthId))
                    animator.SetFloat(BreathDepthId, breathDepth);
                if (HasParameter(VocalId))
                    animator.SetFloat(VocalId, vocalLevel);
                if (HasParameter(WingTremorId))
                    animator.SetFloat(WingTremorId, wingTremor);
                if (HasParameter(ExhaleId))
                    animator.SetBool(ExhaleId, !breath.isInhaling && breath.depth > 0.2f);
            }

            if (beakMesh != null && beakBlendIndex >= 0)
                beakMesh.SetBlendShapeWeight(beakBlendIndex, beakOpen * 100f);

            var breathEmphasis = BreathEmphasis(breathDepth);

            if (chestTransform != null && CanScaleTransformForBreath(chestTransform))
            {
                var scale = 1f + breathEmphasis * settings.chestScaleMax;
                chestTransform.localScale = chestBaseScale * scale;
            }

            if (secondaryChestTransform != null && settings.secondaryChestScaleMax > 0f &&
                CanScaleTransformForBreath(secondaryChestTransform))
            {
                var scale = 1f + breathEmphasis * settings.secondaryChestScaleMax;
                secondaryChestTransform.localScale = secondaryChestBaseScale * scale;
            }
        }

        static float BreathEmphasis(float depth)
        {
            return Mathf.Pow(Mathf.Clamp01(depth), 0.55f);
        }

        static bool CanScaleTransformForBreath(Transform t) => false;

        bool HasParameter(int id)
        {
            if (animator == null)
                return false;
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == id)
                    return true;
            }
            return false;
        }
    }
}
