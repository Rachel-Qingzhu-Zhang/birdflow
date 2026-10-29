using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Disables the pack Parrot.controller idle loop in lab tasks; restores it only for flight glide clips.
    /// </summary>
    [DisallowMultipleComponent]
    public class ParrotPackAnimatorGate : MonoBehaviour
    {
        RuntimeAnimatorController savedController;
        Animator animator;

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
                savedController = animator.runtimeAnimatorController;
        }

        public void DisableIdleAnimation()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (animator == null)
                return;

            if (savedController == null && animator.runtimeAnimatorController != null)
                savedController = animator.runtimeAnimatorController;

            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            animator.enabled = false;
        }

        public void RestoreForFlight()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (animator == null)
                return;

            CacheSavedController();
            animator.enabled = true;
            if (savedController != null)
                animator.runtimeAnimatorController = savedController;

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        public void InheritSavedControllerFrom(ParrotPackAnimatorGate source)
        {
            if (source == null)
                return;

            source.CacheSavedController();
            if (source.savedController != null)
                savedController = source.savedController;
        }

        public void CacheSavedController()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (savedController == null && animator != null && animator.runtimeAnimatorController != null)
                savedController = animator.runtimeAnimatorController;
        }
    }
}
