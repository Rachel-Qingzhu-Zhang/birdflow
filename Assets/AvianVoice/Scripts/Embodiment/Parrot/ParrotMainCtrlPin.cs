using System.Collections.Generic;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Fly clips translate parrot_Main_ctrl and COM_ctrl. Pin them after animator so the mesh stays put.
    /// </summary>
    [DefaultExecutionOrder(32001)]
    public class ParrotMainCtrlPin : MonoBehaviour
    {
        readonly List<PoseSnapshot> snapshots = new();
        bool pinEnabled;

        struct PoseSnapshot
        {
            public Transform bone;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        void Awake()
        {
            CaptureRestPose();
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        public void SetPinEnabled(bool enabled)
        {
            pinEnabled = enabled;
            if (enabled)
                CaptureRestPose();
        }

        public void CaptureRestPose()
        {
            snapshots.Clear();
            foreach (var t in EnumeratePinTargets())
            {
                if (t == null)
                    continue;
                snapshots.Add(new PoseSnapshot
                {
                    bone = t,
                    localPosition = t.localPosition,
                    localRotation = t.localRotation
                });
            }
        }

        void LateUpdate()
        {
            ApplyPin();
        }

        void OnBeforeRender()
        {
            ApplyPin();
        }

        void ApplyPin()
        {
            if (!pinEnabled)
                return;

            foreach (var snap in snapshots)
            {
                if (snap.bone == null)
                    continue;
                snap.bone.localPosition = snap.localPosition;
                snap.bone.localRotation = snap.localRotation;
            }
        }

        static IEnumerable<Transform> EnumeratePinTargets(Transform root)
        {
            ParrotEmbodimentWiring.TryResolve(root, out var refs);
            if (refs.mainCtrl != null)
                yield return refs.mainCtrl;
            if (refs.comCtrl != null)
                yield return refs.comCtrl;
        }

        IEnumerable<Transform> EnumeratePinTargets()
        {
            return EnumeratePinTargets(transform);
        }
    }
}
