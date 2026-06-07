using System.Collections.Generic;
using AvianVoice.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace AvianVoice.Embodiment
{
    public class WingHandMapper : MonoBehaviour
    {
        [SerializeField]
        float simulatedFlapSpeed = 2f;

        AvianVoiceSettings settings;
        PlatformProfile platformProfile;
        Transform leftWing;
        Transform rightWing;
        Transform headAnchor;

        XRHandSubsystem handSubsystem;
        Quaternion leftRestRotation;
        Quaternion rightRestRotation;
        float simulatedPhase;
        float smoothedBreathDepth;
        float exhaleTremorPhase;
        BreathFrame breathFrame;
        float breathWingSpreadScale = 1f;
        float breathWingPitchScale = 1f;

        public void Initialize(
            AvianVoiceSettings voiceSettings,
            PlatformProfile profile,
            Transform leftWingBone,
            Transform rightWingBone,
            Transform head)
        {
            settings = voiceSettings;
            platformProfile = profile;
            leftWing = leftWingBone;
            rightWing = rightWingBone;
            headAnchor = head;

            if (leftWing != null)
                leftRestRotation = leftWing.localRotation;
            if (rightWing != null)
                rightRestRotation = rightWing.localRotation;

            TryGetHandSubsystem();
        }

        public void ApplyBreath(BreathFrame breath)
        {
            breathFrame = breath;
        }

        public void RefreshRestPose()
        {
            if (leftWing != null)
                leftRestRotation = leftWing.localRotation;
            if (rightWing != null)
                rightRestRotation = rightWing.localRotation;
        }

        public void SetBreathWingScales(float spreadScale, float pitchScale)
        {
            breathWingSpreadScale = Mathf.Max(0f, spreadScale);
            breathWingPitchScale = pitchScale;
        }

        void LateUpdate()
        {
            if (settings == null || !settings.wingMappingEnabled)
                return;

            var dt = Time.deltaTime;
            var smooth = 1f - Mathf.Exp(-settings.paramSmooth * dt);
            smoothedBreathDepth = Mathf.Lerp(smoothedBreathDepth, breathFrame.depth, smooth);

            if (ShouldUseSimulatedWings())
            {
                ApplySimulatedWings();
                return;
            }

            if (handSubsystem == null && !TryGetHandSubsystem())
                return;

            ApplyHandWing(handSubsystem.leftHand, leftWing, leftRestRotation, true);
            ApplyHandWing(handSubsystem.rightHand, rightWing, rightRestRotation, false);
        }

        void ApplySimulatedWings()
        {
            if (UsesParrotWingRig())
                return;

            if (settings.breathDrivesWings)
            {
                ApplyBreathWing(leftWing, leftRestRotation, true);
                ApplyBreathWing(rightWing, rightRestRotation, false);
                return;
            }

            simulatedPhase += Time.deltaTime * simulatedFlapSpeed;
            var flap = Mathf.Sin(simulatedPhase) * 25f * settings.wingRotationScale;
            ApplyWingRotation(leftWing, leftRestRotation, 0f, flap);
            ApplyWingRotation(rightWing, rightRestRotation, 0f, -flap);
        }

        void ApplyHandWing(XRHand hand, Transform wing, Quaternion rest, bool isLeft)
        {
            if (!hand.isTracked || wing == null)
                return;

            var wristJoint = hand.GetJoint(XRHandJointID.Wrist);
            if (!wristJoint.TryGetPose(out var wristPose))
                return;

            var wristRot = wristPose.rotation;
            if (headAnchor != null)
                wristRot = Quaternion.Inverse(headAnchor.rotation) * wristRot;

            var euler = wristRot.eulerAngles;
            var pitch = Mathf.DeltaAngle(0f, euler.x) * settings.wingRotationScale;
            var roll = Mathf.DeltaAngle(0f, euler.z) * settings.wingRotationScale * (isLeft ? 1f : -1f);

            if (settings.breathDrivesWings && !UsesParrotWingRig())
            {
                var breathEuler = BreathWingEuler(isLeft);
                pitch += breathEuler.x;
                roll += breathEuler.z;
            }

            ApplyWingRotation(wing, rest, pitch, roll);
        }

        void ApplyBreathWing(Transform wing, Quaternion rest, bool isLeft)
        {
            if (wing == null)
                return;

            var euler = BreathWingEuler(isLeft);
            ApplyWingRotation(wing, rest, euler.x, euler.z);
        }

        Vector3 BreathWingEuler(bool isLeft)
        {
            var spreadSign = isLeft ? 1f : -1f;
            var emphasis = smoothedBreathDepth * (0.55f + 0.45f * smoothedBreathDepth);
            var spread = emphasis * settings.wingBreathSpreadMax * settings.wingRotationScale * breathWingSpreadScale;
            spread = Mathf.Clamp(spread, -55f, 55f);
            var pitch = emphasis * settings.wingBreathPitchOffset * breathWingPitchScale;

            var tremor = 0f;
            if (!breathFrame.isInhaling && breathFrame.depth > 0.15f)
            {
                exhaleTremorPhase += Time.deltaTime * 5f;
                tremor = Mathf.Sin(exhaleTremorPhase) * settings.wingExhaleTremorMax * breathFrame.depth;
            }
            else
            {
                exhaleTremorPhase = 0f;
            }

            return new Vector3(pitch, 0f, spreadSign * spread + spreadSign * tremor);
        }

        static void ApplyWingRotation(Transform wing, Quaternion rest, float pitch, float roll)
        {
            wing.localRotation = rest * Quaternion.Euler(pitch, 0f, roll);
        }

        bool UsesParrotWingRig()
        {
            return leftWing != null && leftWing.name.StartsWith("parrot_");
        }

        bool ShouldUseSimulatedWings()
        {
            if (platformProfile != null)
                return platformProfile.UseSimulatedHands;

#if UNITY_ANDROID
            return false;
#else
            return true;
#endif
        }

        bool TryGetHandSubsystem()
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0)
                return false;

            handSubsystem = subsystems[0];
            if (!handSubsystem.running)
                handSubsystem.Start();
            return true;
        }
    }
}
