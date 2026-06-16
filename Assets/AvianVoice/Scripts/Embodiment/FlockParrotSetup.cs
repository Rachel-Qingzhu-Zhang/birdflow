using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Shared setup for cloned flock parrots (strip scene roles, wire animator, layer).
    /// </summary>
    public static class FlockParrotSetup
    {
        public static void PrepareInstance(Transform bird, Transform templateBird)
        {
            ParrotEmbodimentWiring.DisableConflictingParrotDemoScript(bird);
            StripFriendOnlyComponents(bird);
            StripRoleMarkerSpheres(bird);

            var animator = bird.GetComponent<Animator>();
            var gate = ParrotEmbodimentWiring.EnsureAnimatorGate(bird);
            if (templateBird != null)
            {
                var sourceGate = templateBird.GetComponent<ParrotPackAnimatorGate>();
                gate.InheritSavedControllerFrom(sourceGate);
            }

            var rigPin = bird.GetComponent<ParrotMainCtrlPin>() ?? bird.gameObject.AddComponent<ParrotMainCtrlPin>();
            rigPin.CaptureRestPose();
            rigPin.SetPinEnabled(true);

            if (animator != null)
            {
                ParrotEmbodimentWiring.RestorePackAnimatorForFlight(animator);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            SetLayerRecursive(bird.gameObject, BirdSceneRoles.MirrorLayer);
        }

        public static void StripFriendOnlyComponents(Transform bird)
        {
            var worldGuard = bird.GetComponent<WorldPlacedBird>();
            if (worldGuard != null)
                Object.Destroy(worldGuard);

            foreach (var role in bird.GetComponents<BirdSceneRole>())
                Object.Destroy(role);
            foreach (var marker in bird.GetComponents<RoleMarker>())
                Object.Destroy(marker);
            foreach (var breath in bird.GetComponents<FriendBreathVisual>())
                Object.Destroy(breath);
            foreach (var breath in bird.GetComponents<ParrotBreathEmbodiment>())
                Object.Destroy(breath);
            foreach (var cue in bird.GetComponents<FriendTeachingCue>())
                Object.Destroy(cue);
            foreach (var visuals in bird.GetComponents<ParrotMatchVisuals>())
                Object.Destroy(visuals);
        }

        public static void StripRoleMarkerSpheres(Transform root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name is "PlayerMarker" or "FriendMarker" or "FriendBreathCue")
                    Object.Destroy(child.gameObject);
            }
        }

        public static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        public static void ResetFlightBools(Animator animator)
        {
            if (animator == null)
                return;

            animator.SetBool("walk", false);
            animator.SetBool("fly", false);
            animator.SetBool("glide", false);
            animator.SetBool("takeoff", false);
            animator.SetBool("landing", false);
            animator.SetBool("flyleft", false);
            animator.SetBool("flyright", false);
            animator.SetBool("turnleft", false);
            animator.SetBool("turnright", false);
            animator.SetBool("dance", false);
            animator.SetBool("eat", false);
            animator.SetBool("jump", false);
            animator.SetBool("idle", false);
        }
    }
}
