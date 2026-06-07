#if UNITY_EDITOR
using AvianVoice.Audio;
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    [CustomEditor(typeof(BirdCallLibrary))]
    public class BirdCallLibraryEditor : UnityEditor.Editor
    {
        const float LongClipWarningSeconds = 0.5f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var library = (BirdCallLibrary)target;
            var so = serializedObject;
            var clips = so.FindProperty("clips");
            if (clips == null || !clips.isArray)
                return;

            for (var i = 0; i < clips.arraySize; i++)
            {
                var element = clips.GetArrayElementAtIndex(i);
                var clipProp = element.FindPropertyRelative("clip");
                var labelProp = element.FindPropertyRelative("label");
                var clip = clipProp.objectReferenceValue as AudioClip;
                if (clip == null || clip.length <= LongClipWarningSeconds)
                    continue;

                var label = labelProp.stringValue;
                EditorGUILayout.HelpBox(
                    $"Clip '{label}' is {clip.length:0.00}s. Transform mode works best with short chirps (0.1–0.4s). " +
                    "Long ambience layers cause noisy overlap when retriggered.",
                    MessageType.Warning);
            }
        }
    }
}
#endif
