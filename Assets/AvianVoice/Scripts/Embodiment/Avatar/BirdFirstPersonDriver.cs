using AvianVoice.BodyTracking;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// First-person parrot: the bird eyes sit on the XR camera, the head bone looks where the headset looks,
    /// the body yaw follows the tracked torso (shoulder line) or lazily follows the head without body tracking,
    /// and the wings lift with the tracked upper arms on top of the breath pose.
    /// Runs after ParrotFlightPresentation (10050) and before ParrotMainCtrlPin (32001).
    /// </summary>
    [DefaultExecutionOrder(31000)]
    public class BirdFirstPersonDriver : MonoBehaviour
    {
        [Tooltip("Eye point relative to the head bone, in the bird root frame (meters, +z forward, +y up).")]
        [SerializeField]
        Vector3 eyeOffsetFromHeadBone = new(0f, 0.025f, 0.045f);

        [Header("Body yaw without body tracking")]
        [SerializeField]
        float bodyYawFollowSpeed = 4f;

        [SerializeField]
        float bodyYawDeadzoneDegrees = 28f;

        [Header("Wings from tracked arms")]
        [SerializeField]
        bool driveWingsFromArms = true;

        [SerializeField]
        float wingSpreadMaxDegrees = 55f;

        [SerializeField]
        float armElevationStartDegrees = 20f;

        [SerializeField]
        float armElevationForFullSpread = 95f;

        [SerializeField]
        float wingSmoothing = 10f;

        AvatarEmbodimentController controller;
        Transform head;
        Transform leftWing;
        Transform rightWing;
        Quaternion headLocalFromRoot;
        Vector3 eyeOffsetInHead;
        bool restCaptured;
        float bodyYaw;
        bool yawInitialized;
        float leftSpread;
        float rightSpread;

        public bool UsesBodyTracking => controller != null && controller.TrackingToWorldValid;

        public void Configure(AvatarEmbodimentController owner)
        {
            controller = owner;
            ResolveBones();
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
            yawInitialized = false;
            if (!restCaptured)
                CaptureRest();
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        void LateUpdate()
        {
            Drive(Time.deltaTime);
        }

        void OnBeforeRender()
        {
            if (!isActiveAndEnabled)
                return;
            AlignEyeToCamera();
        }

        void ResolveBones()
        {
            if (ParrotEmbodimentWiring.TryResolve(transform, out var refs))
            {
                leftWing = refs.leftWing;
                rightWing = refs.rightWing;
            }

            foreach (var bone in GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "parrot_Head_bone")
                {
                    head = bone;
                    break;
                }
            }

            if (head == null)
                head = refs.neck != null ? refs.neck : transform;
        }

        void CaptureRest()
        {
            if (head == null)
                ResolveBones();
            if (head == null)
                return;

            headLocalFromRoot = Quaternion.Inverse(transform.rotation) * head.rotation;
            var eyeWorld = head.position + transform.rotation * eyeOffsetFromHeadBone;
            eyeOffsetInHead = head.InverseTransformPoint(eyeWorld);
            restCaptured = true;
        }

        void Drive(float deltaTime)
        {
            var cam = controller != null ? controller.XrCamera : null;
            if (cam == null || head == null)
                return;

            if (!restCaptured)
                CaptureRest();

            var camTransform = cam.transform;
            var camYaw = TrackingSpaceSolver.YawOf(camTransform.forward);
            if (!yawInitialized)
            {
                bodyYaw = camYaw;
                yawInitialized = true;
            }

            if (controller.TryGetTorsoYawWorld(out var torsoYaw))
            {
                bodyYaw = Mathf.LerpAngle(bodyYaw, torsoYaw, 1f - Mathf.Exp(-12f * deltaTime));
            }
            else
            {
                var delta = Mathf.DeltaAngle(bodyYaw, camYaw);
                if (Mathf.Abs(delta) > bodyYawDeadzoneDegrees)
                {
                    var target = camYaw - Mathf.Sign(delta) * bodyYawDeadzoneDegrees * 0.5f;
                    bodyYaw = Mathf.LerpAngle(bodyYaw, target, 1f - Mathf.Exp(-bodyYawFollowSpeed * deltaTime));
                }
            }

            transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
            head.rotation = camTransform.rotation * headLocalFromRoot;

            DriveWings(deltaTime);
            AlignEyeToCamera();
        }

        void DriveWings(float deltaTime)
        {
            if (!driveWingsFromArms || leftWing == null || rightWing == null)
                return;

            var targetLeft = 0f;
            var targetRight = 0f;
            if (controller.TryGetArmElevation(out var leftElevation, out var rightElevation))
            {
                targetLeft = Mathf.InverseLerp(armElevationStartDegrees, armElevationForFullSpread, leftElevation) * wingSpreadMaxDegrees;
                targetRight = Mathf.InverseLerp(armElevationStartDegrees, armElevationForFullSpread, rightElevation) * wingSpreadMaxDegrees;
            }

            var t = 1f - Mathf.Exp(-wingSmoothing * deltaTime);
            leftSpread = Mathf.Lerp(leftSpread, targetLeft, t);
            rightSpread = Mathf.Lerp(rightSpread, targetRight, t);
            if (leftSpread < 0.05f && rightSpread < 0.05f)
                return;

            // Lift around the body forward axis, on top of whatever the breath pose already set this frame.
            var forward = transform.forward;
            leftWing.rotation = Quaternion.AngleAxis(-leftSpread, forward) * leftWing.rotation;
            rightWing.rotation = Quaternion.AngleAxis(rightSpread, forward) * rightWing.rotation;
        }

        void AlignEyeToCamera()
        {
            var cam = controller != null ? controller.XrCamera : null;
            if (cam == null || head == null || !restCaptured)
                return;

            var eyeWorld = head.TransformPoint(eyeOffsetInHead);
            transform.position += cam.transform.position - eyeWorld;
        }
    }
}
