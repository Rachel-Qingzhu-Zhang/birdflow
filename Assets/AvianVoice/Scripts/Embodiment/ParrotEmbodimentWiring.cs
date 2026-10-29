using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Parrot rig: skinned mesh follows *_bone chain under parrot_Main_ctrl → COM_ctrl.
    /// Sibling *_ctrl objects under the root are IK handles and do not deform the mesh.
    /// </summary>
    public static class ParrotEmbodimentWiring
    {
        public struct RigRefs
        {
            public Transform leftWing;
            public Transform rightWing;
            public Transform leftUlna;
            public Transform rightUlna;
            public Transform chest;
            public Transform secondaryChest;
            public Transform neck;
            public Transform mainCtrl;
            public Transform comCtrl;
            public Animator animator;
        }

        public static bool TryResolve(Transform birdRoot, out RigRefs refs)
        {
            refs = default;
            if (birdRoot == null)
                return false;

            refs.animator = birdRoot.GetComponent<Animator>();

            foreach (var bone in birdRoot.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "parrot_Main_ctrl":
                        refs.mainCtrl = bone;
                        break;
                    case "parrot_COM_ctrl":
                        refs.comCtrl = bone;
                        break;
                    case "parrot_l_Humerus_bone":
                        refs.leftWing = bone;
                        break;
                    case "parrot_r_Humerus_bone":
                        refs.rightWing = bone;
                        break;
                    case "parrot_l_Ulna_bone":
                        refs.leftUlna = bone;
                        break;
                    case "parrot_r_Ulna_bone":
                        refs.rightUlna = bone;
                        break;
                    case "parrot_Spine3_bone":
                        refs.chest = bone;
                        break;
                    case "parrot_Spine2_bone":
                        refs.secondaryChest = bone;
                        break;
                    case "parrot_Spine1_bone":
                        if (refs.chest == null)
                            refs.chest = bone;
                        break;
                    case "parrot_Neck_bone":
                        refs.neck = bone;
                        break;
                }
            }

            if (refs.chest == null)
                refs.chest = FindBoneByName(birdRoot, "parrot_Spine3_bone");
            if (refs.secondaryChest == null)
                refs.secondaryChest = FindBoneByName(birdRoot, "parrot_Spine2_bone");

            return refs.leftWing != null && refs.rightWing != null && refs.chest != null;
        }

        public static ParrotPackAnimatorGate EnsureAnimatorGate(Transform birdRoot)
        {
            if (birdRoot == null)
                return null;

            return birdRoot.GetComponent<ParrotPackAnimatorGate>()
                   ?? birdRoot.gameObject.AddComponent<ParrotPackAnimatorGate>();
        }

        public static void ApplyAnimatorMeditationDefaults(Animator animator)
        {
            if (animator == null)
                return;

            animator.applyRootMotion = false;
            EnsureAnimatorGate(animator.transform).DisableIdleAnimation();
        }

        public static void RestorePackAnimatorForFlight(Animator animator)
        {
            if (animator == null)
                return;

            EnsureAnimatorGate(animator.transform).RestoreForFlight();
        }

        public static void DisablePackIdleAnimation(Animator animator)
        {
            if (animator == null)
                return;

            EnsureAnimatorGate(animator.transform).DisableIdleAnimation();
        }

        /// <summary>
        /// Foot / IK handles in the pack FBX ship with white box meshes. Keep the skinned body only.
        /// </summary>
        public static void HideRigControlMeshes(Transform birdRoot)
        {
            if (birdRoot == null)
                return;

            foreach (var renderer in birdRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null || IsSkinned(renderer))
                    continue;
                if (!IsRigControlObject(renderer.gameObject))
                    continue;
                renderer.enabled = false;
                renderer.forceRenderingOff = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public static bool IsRigControlObject(GameObject go)
        {
            if (go == null)
                return false;
            var n = go.name;
            return n.EndsWith("_ctrl") || n.EndsWith("_hlp") || n.EndsWith("_loc");
        }

        static bool IsSkinned(Renderer renderer) => renderer is SkinnedMeshRenderer;

        public static void DisableConflictingParrotDemoScript(Transform birdRoot)
        {
            if (birdRoot == null)
                return;

            foreach (var behaviour in birdRoot.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null)
                    continue;
                if (behaviour.GetType().Name == "Parrot")
                    behaviour.enabled = false;
            }
        }

        static Transform FindBoneByName(Transform birdRoot, string boneName)
        {
            foreach (var bone in birdRoot.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == boneName)
                    return bone;
            }
            return null;
        }
    }
}
