using System.Collections.Generic;
using System.Reflection;
using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Hides everything the XR rig itself draws for the participant's hands and controllers while
    /// <see cref="ParticipantInputPolicy.HandsVisible"/> is false: XR Hands HandVisualizer meshes and joints, the
    /// controller models, the poke / pinch pointers and the interactor rays (Near-Far and Teleport interactor lines),
    /// i.e. every renderer under a "... Hand" or "... Controller" object of the rig. Tracking keeps running: the avatar
    /// body (wings or human arms) is what the participant sees instead.
    ///
    /// The ray drawers (XRI CurveVisualController / XRInteractorLineVisual) switch their LineRenderer back on whenever
    /// the ray finds something, so they are disabled too, not only their renderers (otherwise a ray flashes until the
    /// next scan).
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class ParticipantHandVisuals : MonoBehaviour
    {
        static readonly string[] HandObjectNames = { "Left Hand", "Right Hand", "Hand Visualizer" };

        /// <summary>XR Interaction Toolkit behaviours that draw rays, reticles and pointers.</summary>
        static readonly string[] VisualBehaviourTypes =
        {
            "HandVisualizer", "CurveVisualController", "XRInteractorLineVisual", "XRInteractorReticleVisual",
        };

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
                if (behaviour == null || !behaviour.enabled || !IsVisualBehaviour(behaviour))
                    continue;
                if (behaviour.GetType().Name != "HandVisualizer" && !IsUnderHandOrController(behaviour.transform))
                    continue;

                SetVisualizerFlags(behaviour, false);
                behaviour.enabled = false;
                hiddenVisualizers.Add(behaviour);
            }

            // Hand meshes may be spawned anywhere under the rig, so hide every renderer whose object or
            // ancestor (below the rig root) is a hand or controller object.
            foreach (var renderer in rigRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled || !IsUnderHandOrController(renderer.transform))
                    continue;

                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }

            applied = true;
        }

        static bool IsVisualBehaviour(Behaviour behaviour)
        {
            var name = behaviour.GetType().Name;
            foreach (var type in VisualBehaviourTypes)
            {
                if (name == type)
                    return true;
            }

            return false;
        }

        bool IsUnderHandOrController(Transform t)
        {
            while (t != null && t != rigRoot)
            {
                if (IsHandOrControllerObject(t))
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

        static bool IsHandOrControllerObject(Transform t)
        {
            foreach (var name in HandObjectNames)
            {
                if (t.name == name)
                    return true;
            }

            return t.name.IndexOf("hand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   t.name.IndexOf("controller", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
