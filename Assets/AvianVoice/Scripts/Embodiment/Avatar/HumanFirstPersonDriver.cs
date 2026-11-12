using AvianVoice.BodyTracking;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// First-person human avatar. With body tracking the backend retargets the tracked skeleton onto the
    /// Humanoid rig (rotations, plus the hips position; the avatar keeps its own proportions) and this driver
    /// places the avatar root at the tracking-space origin and then shifts the whole body so the avatar's
    /// eyes sit on the headset. Without body tracking the avatar stands with arms down, head on the camera.
    ///
    /// The retargeter takes one snapshot of the rig's T-pose when it starts; this driver re-poses the rig
    /// into T-pose right before that (the Avatar's stored T-pose) and never writes bones while the retargeter
    /// is driving them. When tracking data stops, retargeting is paused (the SDK would keep writing stale
    /// joints) and the fallback pose takes over.
    /// </summary>
    [DefaultExecutionOrder(31000)]
    public class HumanFirstPersonDriver : MonoBehaviour
    {
        [Tooltip("Eye point relative to the head bone in the avatar root frame (meters), measured in T-pose.")]
        [SerializeField]
        Vector3 eyeOffsetFromHeadBone = new(0f, 0.07f, 0.09f);

        [Tooltip("Humanoid muscle value for Arm Down-Up without body tracking (-1 = arms at the sides).")]
        [SerializeField]
        float fallbackArmDownUp = -0.9f;

        [Tooltip("With body tracking: shift the whole avatar so its eyes sit on the headset (the tracked hips keep the " +
                 "avatar's own proportions, so its head would otherwise sit at the avatar's height, not the user's).")]
        [SerializeField]
        bool alignEyesToCameraWhileTracked = true;

        [Tooltip("How much of the horizontal part of that shift to apply (1 = eyes exactly on the camera, 0 = vertical only).")]
        [SerializeField, Range(0f, 1f)]
        float trackedEyeHorizontalWeight = 1f;

        [Tooltip("Pause retargeting (and use the fallback pose) when no tracking data arrived for this long.")]
        [SerializeField]
        float noDataPauseSeconds = 0.5f;

        AvatarEmbodimentController controller;
        IHumanoidRetargetingHandle handle;
        Animator animator;
        Transform headBone;
        HumanPoseHandler poseHandler;
        HumanPose humanPose;
        Vector3 tPoseBodyPosition;
        Quaternion tPoseBodyRotation = Quaternion.identity;
        float[] tPoseMuscles;
        Quaternion headLocalFromRoot = Quaternion.identity;
        Vector3 eyeOffsetInHead;
        bool restCaptured;
        int leftArmMuscle = -1;
        int rightArmMuscle = -1;
        bool trackedLastFrame;
        bool retargetingActive = true;
        float noDataSince = -1f;
        int restPoseCount;

        /// <summary>Tracked joints are being applied and the tracking space is solved.</summary>
        public bool IsTracked =>
            handle != null && handle.IsDriving && controller != null && controller.TrackingToWorldValid;

        /// <summary>The SDK writes the bones (tracking data present), even if the world alignment is not solved yet.</summary>
        public bool IsRetargeting => handle != null && handle.IsDriving;

        public bool RetargetingPaused => handle != null && !retargetingActive;

        /// <summary>Distance between the avatar's eye point and the camera after this frame's placement (diagnostics).</summary>
        public float EyeError { get; private set; }

        public void Configure(AvatarEmbodimentController owner, IBodyTrackingBackend backend, bool hideHead, Vector3? eyeOffset = null)
        {
            controller = owner;
            animator = GetComponent<Animator>();
            if (eyeOffset.HasValue)
                eyeOffsetFromHeadBone = eyeOffset.Value;

            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                poseHandler = new HumanPoseHandler(animator.avatar, transform);
                humanPose = new HumanPose();
                FindArmMuscles();
                ForceTPose();
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                CaptureRest();

                if (backend != null)
                    handle = backend.AttachHumanoidRetargeting(animator, ForceTPose);
            }

            if (hideHead && GetComponent<FirstPersonHeadHider>() == null)
                gameObject.AddComponent<FirstPersonHeadHider>();
        }

        public string Describe()
        {
            return "retargeting=" + (handle == null ? "none" : handle.IsDriving ? "driving" : handle.HasTrackingData ? "data" : "no data")
                   + (RetargetingPaused ? " (paused)" : "")
                   + " tracked=" + IsTracked + " eyeErr=" + EyeError.ToString("0.00") + " tposes=" + restPoseCount
                   + (handle != null ? " | " + handle.DescribeHands() : "");
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
            retargetingActive = true;
            noDataSince = -1f;
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

            UpdateRetargetingGate();

            if (IsTracked)
            {
                PlaceAtTrackingOrigin();
                if (alignEyesToCameraWhileTracked)
                    AlignEyeToCamera(cam);
                trackedLastFrame = true;
                return;
            }

            trackedLastFrame = false;
            if (IsRetargeting)
            {
                // The SDK poses the body but the tracking-space solution is not there yet: do not touch bones,
                // stand in the shared tracking space and put the eyes on the camera.
                PlaceInSharedTrackingSpace();
                AlignEyeToCamera(cam);
                return;
            }

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
            {
                PlaceAtTrackingOrigin();
                if (alignEyesToCameraWhileTracked)
                    AlignEyeToCamera(cam);
            }
            else if (IsRetargeting)
            {
                AlignEyeToCamera(cam);
            }
            else
            {
                AlignHeadToCamera(cam);
            }
        }

        /// <summary>Pause the SDK retargeter while no tracking data arrives (it would keep writing stale joints).</summary>
        void UpdateRetargetingGate()
        {
            if (handle == null)
                return;

            if (handle.HasTrackingData)
            {
                noDataSince = -1f;
                if (!retargetingActive)
                {
                    retargetingActive = true;
                    handle.SetRetargetingActive(true);
                }
                return;
            }

            if (noDataSince < 0f)
                noDataSince = Time.unscaledTime;
            if (retargetingActive && Time.unscaledTime - noDataSince >= noDataPauseSeconds)
            {
                retargetingActive = false;
                handle.SetRetargetingActive(false);
            }
        }

        void PlaceAtTrackingOrigin()
        {
            var pose = controller.TrackingToWorld;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        void PlaceInSharedTrackingSpace()
        {
            var space = controller.TrackingSpace;
            if (space != null)
                transform.SetPositionAndRotation(space.position, space.rotation);
        }

        /// <summary>Shift the whole avatar (bones untouched) so its eye point sits on the camera.</summary>
        void AlignEyeToCamera(Camera cam)
        {
            if (headBone == null || !restCaptured)
                return;

            var eyeWorld = headBone.TransformPoint(eyeOffsetInHead);
            var delta = cam.transform.position - eyeWorld;
            EyeError = delta.magnitude;
            if (trackedEyeHorizontalWeight < 1f)
            {
                var horizontal = new Vector3(delta.x, 0f, delta.z) * trackedEyeHorizontalWeight;
                delta = Vector3.up * delta.y + horizontal;
            }

            transform.position += delta;
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
            var delta = camTransform.position - eyeWorld;
            EyeError = 0f;
            transform.position += delta;
        }

        /// <summary>
        /// The Avatar's own T-pose (<see cref="HumanoidTPose"/>: arms horizontal, elbows and knees straight). This is
        /// what the retargeter must see when it records the rig. All Humanoid muscles at 0 is not a T-pose: it
        /// leaves the arms about 40 degrees down and forward and the elbows and knees bent; it is only used when the
        /// Avatar stores no skeleton.
        /// </summary>
        void ForceTPose()
        {
            if (poseHandler == null)
                return;

            if (HumanoidTPose.Apply(animator) == 0)
            {
                poseHandler.GetHumanPose(ref humanPose);
                if (humanPose.muscles != null)
                {
                    for (var i = 0; i < humanPose.muscles.Length; i++)
                        humanPose.muscles[i] = 0f;
                }

                if (restPoseCount > 0)
                {
                    humanPose.bodyPosition = tPoseBodyPosition;
                    humanPose.bodyRotation = tPoseBodyRotation;
                }

                poseHandler.SetHumanPose(ref humanPose);
            }

            if (restPoseCount == 0)
            {
                poseHandler.GetHumanPose(ref humanPose);
                tPoseBodyPosition = humanPose.bodyPosition;
                tPoseBodyRotation = humanPose.bodyRotation;
                tPoseMuscles = humanPose.muscles != null ? (float[])humanPose.muscles.Clone() : null;
            }

            restPoseCount++;
        }

        /// <summary>Without tracking: the T-pose with the arms lowered (elbows and knees stay straight).</summary>
        void ApplyFallbackPose()
        {
            if (poseHandler == null || humanPose.muscles == null)
                return;

            for (var i = 0; i < humanPose.muscles.Length; i++)
                humanPose.muscles[i] = tPoseMuscles != null && i < tPoseMuscles.Length ? tPoseMuscles[i] : 0f;
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
