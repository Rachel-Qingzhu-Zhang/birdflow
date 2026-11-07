#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace AvianVoice.Editor
{
    /// <summary>Editor Game view helpers (the GameView class is internal, so this goes through reflection).</summary>
    public static class AvianGameViewUtility
    {
        public static EditorWindow FindGameView()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (type == null)
                return null;
            foreach (var window in Resources.FindObjectsOfTypeAll(type))
            {
                if (window is EditorWindow editorWindow)
                    return editorWindow;
            }

            return null;
        }

        /// <summary>Maximize / restore the Game view (no other editor window repaints while it is maximized).</summary>
        public static bool SetMaximized(bool maximized)
        {
            var view = FindGameView();
            if (view == null)
                return false;
            if (view.maximized != maximized)
                view.maximized = maximized;
            return view.maximized == maximized;
        }
    }
}
#endif
