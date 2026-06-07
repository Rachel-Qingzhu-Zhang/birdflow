using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Resolves bone and mesh references on ANIMALS FULL PACK Pigeon_PBR rigs.
    /// </summary>
    public static class PigeonEmbodimentWiring
    {
        public struct RigRefs
        {
            public Transform leftWing;
            public Transform rightWing;
            public Transform leftForearm;
            public Transform rightForearm;
            public Transform chest;
            public Transform secondaryChest;
            public Transform head;
            public SkinnedMeshRenderer skinMesh;
            public Animator animator;
        }

        public static bool IsPigeonRoot(Transform root)
        {
            if (root == null)
                return false;

            var n = root.name;
            return n == "Pigeon_PBR" || n == "Pigeon" || n.StartsWith("Pigeon_");
        }

        public static bool TryResolve(Transform birdRoot, out RigRefs refs)
        {
            refs = default;
            if (birdRoot == null)
                return false;

            refs.animator = birdRoot.GetComponent<Animator>();
            refs.skinMesh = birdRoot.GetComponentInChildren<SkinnedMeshRenderer>(true);

            foreach (var bone in birdRoot.GetComponentsInChildren<Transform>(true))
            {
                switch (bone.name)
                {
                    case "PIGEON_ L UpperArm":
                        refs.leftWing = bone;
                        break;
                    case "PIGEON_ R UpperArm":
                        refs.rightWing = bone;
                        break;
                    case "PIGEON_ L Forearm":
                        refs.leftForearm = bone;
                        break;
                    case "PIGEON_ R Forearm":
                        refs.rightForearm = bone;
                        break;
                    case "PIGEON_ Spine":
                        refs.chest = bone;
                        break;
                    case "PIGEON_ Neck":
                        refs.secondaryChest = bone;
                        break;
                    case "PIGEON_ Head":
                        refs.head = bone;
                        break;
                }
            }

            if (refs.chest == null)
                refs.chest = FindBoneByName(birdRoot, "PIGEON_ Spine");
            if (refs.secondaryChest == null)
                refs.secondaryChest = FindBoneByName(birdRoot, "PIGEON_ Neck");
            if (refs.leftWing == null)
                refs.leftWing = FindBoneByName(birdRoot, "PIGEON_ L UpperArm");
            if (refs.rightWing == null)
                refs.rightWing = FindBoneByName(birdRoot, "PIGEON_ R UpperArm");
            if (refs.head == null)
                refs.head = FindBoneByName(birdRoot, "PIGEON_ Head");

            return refs.leftWing != null && refs.rightWing != null && refs.chest != null;
        }

        public static void DisablePackAnimations(Animator animator)
        {
            if (animator == null)
                return;

            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.enabled = false;
        }

        public static void ApplyAnimatorDefaults(Animator animator, bool disablePackAnimator = true)
        {
            if (animator == null)
                return;

            if (disablePackAnimator)
            {
                DisablePackAnimations(animator);
                return;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
