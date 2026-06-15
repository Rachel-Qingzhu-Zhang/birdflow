#if UNITY_EDITOR
using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    [CustomEditor(typeof(XrRigTaskStartPoses))]
    public class XrRigTaskStartPosesEditor : UnityEditor.Editor
    {
        int selectedTaskIndex;

        public override void OnInspectorGUI()
        {
            var poses = (XrRigTaskStartPoses)target;

            EditorGUILayout.LabelField("Per-Task Start Pose (keys 1-6)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Move XR Origin in Scene view.\n" +
                "2. Pick a task below.\n" +
                "3. Click Capture Pose For Selected Task.\n" +
                "4. Repeat for tasks 1-6, then Save scene (Ctrl+S).",
                MessageType.Info);

            selectedTaskIndex = EditorGUILayout.Popup(
                "Select Task",
                selectedTaskIndex,
                XrRigTaskStartPoses.TaskLabels);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Capture Pose For Selected Task", GUILayout.Height(28f)))
                CaptureTask(poses, XrRigTaskStartPoses.TaskIds[selectedTaskIndex]);

            if (GUILayout.Button("Apply Selected Task Pose", GUILayout.Height(28f)))
            {
                Undo.RecordObject(poses.transform, "Apply task start pose");
                poses.ApplyForTask(XrRigTaskStartPoses.TaskIds[selectedTaskIndex]);
                EditorUtility.SetDirty(poses);
            }
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                var manager = Object.FindAnyObjectByType<TaskManager>();
                if (manager != null && GUILayout.Button("Capture Pose For Active Play Task"))
                    CaptureTask(poses, manager.CurrentTaskId);
            }

            var all = poses.GetAllTaskPoses();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Configured Tasks", EditorStyles.boldLabel);
            for (var i = 0; i < all.Length; i++)
            {
                var entry = all[i];
                var status = entry.configured
                    ? $"pos {entry.worldPosition}, rot Y {entry.worldRotation.eulerAngles.y:0}°"
                    : "not set";
                EditorGUILayout.LabelField(XrRigTaskStartPoses.TaskLabels[i], status);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
            DrawDefaultInspector();
        }

        static void CaptureTask(XrRigTaskStartPoses poses, string taskId)
        {
            Undo.RecordObject(poses, "Capture task start pose");
            poses.CaptureForTask(taskId);
            EditorUtility.SetDirty(poses);
            var index = System.Array.IndexOf(XrRigTaskStartPoses.TaskIds, taskId);
            var label = index >= 0 ? XrRigTaskStartPoses.TaskLabels[index] : taskId;
            Debug.Log("[AvianVoice] Captured XR start pose for " + label + ". Save scene (Ctrl+S).");
        }
    }
}
#endif
