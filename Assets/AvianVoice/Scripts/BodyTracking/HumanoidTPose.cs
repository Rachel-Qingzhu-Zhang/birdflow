using System.Collections.Generic;
using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// Puts a Humanoid rig into the T-pose stored in its Avatar (the pose the Humanoid configuration was made in:
    /// arms horizontal, elbows and knees straight).
    ///
    /// This is NOT the pose of all Humanoid muscles at 0: muscle zero is the middle of each muscle's range, which
    /// leaves the arms about 40 degrees down and forward, the elbows and knees bent. A retargeter that records
    /// muscle zero as the rig's T-pose (the Meta SDK takes one such snapshot) turns every arm bone from that bent
    /// pose onto the tracker's T-pose by the shortest rotation, which twists the upper arms about their own axis.
    /// </summary>
    public static class HumanoidTPose
    {
        /// <summary>
        /// Writes the Avatar's T-pose rotations into every transform below the animator (the root object itself
        /// and all positions are left as they are). Returns the number of bones set, 0 when the Avatar has no
        /// stored skeleton.
        /// </summary>
        public static int Apply(Animator animator)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return 0;

            var skeleton = animator.avatar.humanDescription.skeleton;
            if (skeleton == null || skeleton.Length == 0)
                return 0;

            var rotations = new Dictionary<string, Quaternion>(skeleton.Length);
            foreach (var bone in skeleton)
                rotations[bone.name] = bone.rotation;

            var count = 0;
            var root = animator.transform;
            var stack = new Stack<Transform>();
            for (var i = 0; i < root.childCount; i++)
                stack.Push(root.GetChild(i));
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (rotations.TryGetValue(t.name, out var rotation))
                {
                    t.localRotation = rotation;
                    count++;
                }

                for (var i = 0; i < t.childCount; i++)
                    stack.Push(t.GetChild(i));
            }

            return count;
        }
    }
}
