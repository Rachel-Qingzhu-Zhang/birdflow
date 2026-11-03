using System.Collections.Generic;
using System.Reflection;
using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Hides the XR rig's own hand drawings (XR Hands HandVisualizer meshes and joints, interactor line
    /// visuals, anything rendered under "Left Hand" / "Right Hand") while
    /// <see cref="ParticipantInputPolicy.HandsVisible"/> is false. Hand tracking keeps running: the avatar
    /// body (wings or human arms) is what the participant sees instead.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class ParticipantHandVisuals : MonoBehaviour
    {
        static readonly string[] HandObjectNames = { "Left Hand", "Right Hand", "Hand Visualizer" };

        readonly List<Renderer> hiddenRenderers = new();
        readonly List<Behaviour> hiddenVisualizers = new();
        Transform rigRoot;
        float nextScan;
        bool applied;

        public int HiddenRendererCount => hiddenRenderers.Count;

        public void Configure(Transform xrRigRoot)
        {
            rigRoot = xrRigRoot;
            nextScan = 0f;
        }

        void OnEnable()
        {
            ParticipantInputPolicy.HandsVisibleChanged += OnPolicyChanged;
        }

        void OnDisable()
        {
            ParticipantInputPolicy.HandsVisibleChanged -= OnPolicyChanged;
            Restore();
        }

        void OnPolicyChanged(bool visible)
        {
            if (visible)
                Restore();
            else
                nextScan = 0f;
        }

        void LateUpdate()
        {
            if (ParticipantInputPolicy.HandsVisible)
            {
                if (applied)
                    Restore();
                return;
            }

            // Hand meshes are spawned lazily by the visualizer, so keep scanning while hidden.
            if (Time.unscaledTime < nextScan)
                return;

            nextScan = Time.unscaledTime + 0.5f;
            Hide();
        }

        void Hide()
        {
            if (rigRoot == null)
                return;

            foreach (var behaviour in rigRoot.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour == null || !behaviour.enabled || behaviour.GetType().Name != "HandVisualizer")
                    continue;

                SetVisualizerFlags(behaviour, false);
                behaviour.enabled = false;
                hiddenVisualizers.Add(behaviour);
            }

            // Hand meshes may be spawned anywhere under the rig, so hide every renderer whose object or
            // ancestor (below the rig root) is a hand object.
            foreach (var renderer in rigRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !IsUnderHandObject(renderer.transform))
                    continue;

                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }

            applied = true;
        }

        bool IsUnderHandObject(Transform t)
        {
            while (t != null && t != rigRoot)
            {
                if (IsHandObject(t))
                    return true;
                t = t.parent;
            }

            return false;
        }

        void Restore()
        {
            foreach (var renderer in hiddenRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }

            foreach (var behaviour in hiddenVisualizers)
            {
                if (behaviour == null)
                    continue;
                SetVisualizerFlags(behaviour, true);
                behaviour.enabled = true;
            }

            hiddenRenderers.Clear();
            hiddenVisualizers.Clear();
            applied = false;
        }

        static bool IsHandObject(Transform t)
        {
            foreach (var name in HandObjectNames)
            {
                if (t.name == name)
                    return true;
            }

            return t.name.IndexOf("hand", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void SetVisualizerFlags(Behaviour visualizer, bool value)
        {
            var type = visualizer.GetType();
            foreach (var property in new[] { "drawMeshes", "drawJoints" })
            {
                var info = type.GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
                if (info != null && info.CanWrite && info.PropertyType == typeof(bool))
                    info.SetValue(visualizer, value);
            }
        }
    }
}
