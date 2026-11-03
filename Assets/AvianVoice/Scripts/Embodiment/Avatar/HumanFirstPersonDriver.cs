using AvianVoice.BodyTracking;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// First-person human avatar. With body tracking the backend retargets the tracked skeleton onto the
    /// Humanoid rig and this driver places the avatar root at the tracking-space origin, so the avatar head
    /// coincides with the headset. Without body tracking the avatar stands with arms down, head on the camera.
    /// </summary>
    [DefaultExecutionOrder(31000)]
    public class HumanFirstPersonDriver : MonoBehaviour
    {
        [Tooltip("Eye point relative to the head bone in the avatar root frame (meters). Used without body tracking.")]
        [SerializeField]
        Vector3 eyeOffsetFromHeadBone = new(0f, 0.07f, 0.09f);

        [Tooltip("Humanoid muscle value for Arm Down-Up without body tracking (-1 = arms at the sides).")]
        [SerializeField]
        float fallbackArmDownUp = -0.9f;

        AvatarEmbodimentController controller;
        IHumanoidRetargetingHandle handle;
        Animator animator;
        Transform headBone;
        HumanPoseHandler poseHandler;
        HumanPose humanPose;
        Vector3 tPoseBodyPosition;
        Quaternion tPoseBodyRotation = Quaternion.identity;
        Quaternion headLocalFromRoot = Quaternion.identity;
        Vector3 eyeOffsetInHead;
        bool restCaptured;
        int leftArmMuscle = -1;
        int rightArmMuscle = -1;
        bool trackedLastFrame;

        public bool IsTracked =>
            handle != null && handle.IsDriving && controller != null && controller.TrackingToWorldValid;

        public void Configure(AvatarEmbodimentController owner, IBodyTrackingBackend backend, bool hideHead)
        {
            controller = owner;
            animator = GetComponent<Animator>();

            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                poseHandler = new HumanPoseHandler(animator.avatar, transform);
                humanPose = new HumanPose();
                FindArmMuscles();
                ForceTPose();
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                CaptureRest();

                if (backend != null)
                    handle = backend.AttachHumanoidRetargeting(animator);
            }

            if (hideHead && GetComponent<FirstPersonHeadHider>() == null)
                gameObject.AddComponent<FirstPersonHeadHider>();
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
            handle?.SetEnabled(true);
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            handle?.SetEnabled(false);
        }

        void OnDestroy()
        {
            handle?.Dispose();
            handle = null;
            poseHandler?.Dispose();
            poseHandler = null;
        }

        void LateUpdate()
        {
            var cam = controller != null ? controller.XrCamera : null;
            if (cam == null)
                return;

            if (IsTracked)
            {
                PlaceAtTrackingOrigin();
                trackedLastFrame = true;
                return;
            }

            trackedLastFrame = false;
            ApplyFallbackPose();
            AlignHeadToCamera(cam);
        }

        void OnBeforeRender()
        {
            if (!isActiveAndEnabled)
                return;

            var cam = controller != null ? controller.XrCamera : null;
            if (cam == null)
                return;

            if (trackedLastFrame && IsTracked)
                PlaceAtTrackingOrigin();
            else
                AlignHeadToCamera(cam);
        }

        void PlaceAtTrackingOrigin()
        {
            var pose = controller.TrackingToWorld;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        void AlignHeadToCamera(Camera cam)
        {
            var camTransform = cam.transform;
            var yaw = Quaternion.Euler(0f, TrackingSpaceSolver.YawOf(camTransform.forward), 0f);
            transform.rotation = yaw;

            if (headBone == null || !restCaptured)
            {
                // Rough placement: stand under the camera.
                transform.position = camTransform.position + Vector3.down * 1.6f;
                return;
            }

            headBone.rotation = camTransform.rotation * headLocalFromRoot;
            var eyeWorld = headBone.TransformPoint(eyeOffsetInHead);
            transform.position += camTransform.position - eyeWorld;
        }

        void ForceTPose()
        {
            if (poseHandler == null)
                return;

            poseHandler.GetHumanPose(ref humanPose);
            if (humanPose.muscles != null)
            {
                for (var i = 0; i < humanPose.muscles.Length; i++)
                    humanPose.muscles[i] = 0f;
            }

            tPoseBodyPosition = humanPose.bodyPosition;
            tPoseBodyRotation = humanPose.bodyRotation;
            poseHandler.SetHumanPose(ref humanPose);
        }

        void ApplyFallbackPose()
        {
            if (poseHandler == null || humanPose.muscles == null)
                return;

            for (var i = 0; i < humanPose.muscles.Length; i++)
                humanPose.muscles[i] = 0f;
            if (leftArmMuscle >= 0)
                humanPose.muscles[leftArmMuscle] = fallbackArmDownUp;
            if (rightArmMuscle >= 0)
                humanPose.muscles[rightArmMuscle] = fallbackArmDownUp;
            humanPose.bodyPosition = tPoseBodyPosition;
            humanPose.bodyRotation = tPoseBodyRotation;
            poseHandler.SetHumanPose(ref humanPose);
        }

        void CaptureRest()
        {
            if (headBone == null)
                return;

            headLocalFromRoot = Quaternion.Inverse(transform.rotation) * headBone.rotation;
            var eyeWorld = headBone.position + transform.rotation * eyeOffsetFromHeadBone;
            eyeOffsetInHead = headBone.InverseTransformPoint(eyeWorld);
            restCaptured = true;
        }

        void FindArmMuscles()
        {
            var names = HumanTrait.MuscleName;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == "Left Arm Down-Up")
                    leftArmMuscle = i;
                else if (names[i] == "Right Arm Down-Up")
                    rightArmMuscle = i;
            }
        }
    }
}
