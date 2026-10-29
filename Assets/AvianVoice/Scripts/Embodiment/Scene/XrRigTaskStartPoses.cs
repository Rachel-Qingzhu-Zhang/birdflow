using System;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Per-task XR Origin start poses for Avian_Lab (keys 1-6). Capture each task pose in the Editor,
    /// then Play / task switch restores the matching camera rig position.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-450)]
    public class XrRigTaskStartPoses : MonoBehaviour
    {
        public static readonly string[] TaskIds =
        {
            "meet",
            "breath_sync",
            "flying",
            "singing",
            "call_friends",
            "reflection"
        };

        public static readonly string[] TaskLabels =
        {
            "1 Meet Your Bird",
            "2 Breath Sync",
            "3 Flying",
            "4 Singing Branch",
            "5 Call Friends",
            "6 Reflection"
        };

        [Serializable]
        public struct TaskPose
        {
            public string taskId;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public bool configured;
        }

        [SerializeField]
        TaskPose[] taskPoses = Array.Empty<TaskPose>();

        [SerializeField]
        bool applyBootTaskOnPlay = true;

        [SerializeField]
        string bootTaskId = "meet";

        [SerializeField]
        bool skipAutoSceneLayout;

        [Header("Flying start (task 3, rig origin relative to the pond surface)")]
        [Tooltip("Rig origin height above the water when entering Flying. Lower = closer to the reflection.")]
        [SerializeField]
        float flyingHeightAboveWater = 1.3f;

        [Tooltip("A captured flying pose outside [min, max] is snapped to flyingHeightAboveWater.")]
        [SerializeField]
        float flyingMinHeightAboveWater = 0.6f;

        [SerializeField]
        float flyingMaxHeightAboveWater = 2.4f;

        [Tooltip("Rig pitch on enter (degrees, positive looks down).")]
        [SerializeField]
        float flyingPitchDegrees = 18f;

        [SerializeField]
        float flyingBackFromPondCenter = 3.5f;

        [SerializeField]
        Color gizmoColor = new(0.2f, 0.85f, 1f, 0.85f);

        public bool SkipAutoSceneLayout => skipAutoSceneLayout;

        public static bool ShouldSkipAutoSceneLayout()
        {
            if (WorldMirrorScenePlacement.HasManualLayoutInScene())
                return true;

            foreach (var poses in UnityEngine.Object.FindObjectsByType<XrRigTaskStartPoses>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (poses != null && poses.skipAutoSceneLayout)
                    return true;
            }

            return false;
        }

        void Awake()
        {
            EnsureTaskSlots();
            if (applyBootTaskOnPlay)
                ApplyForTask(bootTaskId);
        }

        public bool HasConfiguredPose(string taskId)
        {
            EnsureTaskSlots();
            var index = IndexOfTask(taskId);
            return index >= 0 && taskPoses[index].configured;
        }

        public void CaptureForTask(string taskId)
        {
            EnsureTaskSlots();
            var index = IndexOfTask(taskId);
            if (index < 0)
                return;

            var entry = taskPoses[index];
            entry.worldPosition = transform.position;
            entry.worldRotation = transform.rotation;
            entry.configured = true;
            taskPoses[index] = entry;
        }

        public void SetTaskPose(string taskId, Vector3 worldPosition, Quaternion worldRotation)
        {
            EnsureTaskSlots();
            var index = IndexOfTask(taskId);
            if (index < 0)
                return;

            var entry = taskPoses[index];
            entry.worldPosition = worldPosition;
            entry.worldRotation = worldRotation;
            entry.configured = true;
            taskPoses[index] = entry;
        }

        public void ApplyForTask(string taskId)
        {
            EnsureTaskSlots();
            var index = IndexOfTask(taskId);
            if (index < 0 || !taskPoses[index].configured)
                return;

            var position = taskPoses[index].worldPosition;
            var rotation = taskPoses[index].worldRotation;
            if (taskId == "flying")
                ClampFlyingPoseNearPond(ref position, ref rotation);

            ApplyPose(position, rotation);
        }

        void ClampFlyingPoseNearPond(ref Vector3 position, ref Quaternion rotation)
        {
            var pond = UnityEngine.Object.FindAnyObjectByType<PondWaterReflection>();
            if (pond == null)
                return;

            var water = pond.transform.position;
            // Keep flyer close to the surface: reflection of an FPV bird scales ~1/(2H).
            position.x = water.x;
            position.z = water.z - flyingBackFromPondCenter;
            var height = position.y - water.y;
            if (height > flyingMaxHeightAboveWater || height < flyingMinHeightAboveWater)
                position.y = water.y + flyingHeightAboveWater;

            var euler = rotation.eulerAngles;
            // Keep the origin upright so CharacterController.Move can glide. Look-down is on the camera.
            rotation = Quaternion.Euler(0f, euler.y, 0f);
        }

        public void ApplyPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            transform.SetPositionAndRotation(worldPosition, worldRotation);
            ApplyFlyingCameraLookDown();

            if (cc != null)
                cc.enabled = true;
        }

        void ApplyFlyingCameraLookDown()
        {
            if (!TaskActivityIsFlying())
                return;

            Camera cam = null;
            foreach (var candidate in GetComponentsInChildren<Camera>(true))
            {
                if (candidate != null && candidate.targetTexture == null)
                {
                    cam = candidate;
                    break;
                }
            }

            if (cam == null)
                return;

            cam.transform.localRotation = Quaternion.Euler(flyingPitchDegrees, 0f, 0f);
        }

        static bool TaskActivityIsFlying()
        {
            return AvianVoice.Tasks.TaskActivity.IsActive("flying")
                   || AvianVoice.Tasks.TaskActivity.IsActive("flight");
        }

        public TaskPose GetTaskPose(string taskId)
        {
            EnsureTaskSlots();
            var index = IndexOfTask(taskId);
            return index >= 0 ? taskPoses[index] : default;
        }

        public TaskPose[] GetAllTaskPoses()
        {
            EnsureTaskSlots();
            return taskPoses;
        }

        void EnsureTaskSlots()
        {
            if (taskPoses != null && taskPoses.Length == TaskIds.Length)
                return;

            var rebuilt = new TaskPose[TaskIds.Length];
            for (var i = 0; i < TaskIds.Length; i++)
            {
                rebuilt[i].taskId = TaskIds[i];
                if (taskPoses != null)
                {
                    for (var j = 0; j < taskPoses.Length; j++)
                    {
                        if (taskPoses[j].taskId != TaskIds[i])
                            continue;
                        rebuilt[i] = taskPoses[j];
                        rebuilt[i].taskId = TaskIds[i];
                        break;
                    }
                }
            }

            taskPoses = rebuilt;
        }

        int IndexOfTask(string taskId)
        {
            for (var i = 0; i < TaskIds.Length; i++)
            {
                if (TaskIds[i] == taskId)
                    return i;
            }

            return -1;
        }

        void OnDrawGizmosSelected()
        {
            if (taskPoses == null)
                return;

            for (var i = 0; i < taskPoses.Length; i++)
            {
                if (!taskPoses[i].configured)
                    continue;

                Gizmos.color = gizmoColor;
                var eye = taskPoses[i].worldPosition + Vector3.up * 1.45f;
                Gizmos.DrawWireSphere(eye, 0.1f);
                var forward = taskPoses[i].worldRotation * Vector3.forward;
                Gizmos.DrawLine(eye, eye + forward * 0.6f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(eye + Vector3.up * 0.15f, (i + 1).ToString());
#endif
            }
        }
    }
}
