using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// Puts a Humanoid rig into its rest pose for retargeting: the T-pose stored in its Avatar for the arms, legs and
    /// fingers (arms horizontal, elbows and knees straight), with the torso (pelvis, spine, chest, neck, head,
    /// collarbones) taken from <see cref="TorsoSource"/>.
    ///
    /// All Humanoid muscles at 0 is NOT a T-pose: muscle zero leaves the arms about 40 degrees down and forward, the
    /// elbows and knees bent. A retargeter that records muscle zero as the rig's T-pose (the Meta SDK takes one such
    /// snapshot) starts every arm and leg bone from that bent pose.
    /// </summary>
    public static class HumanoidTPose
    {
        public enum TorsoSource
        {
            /// <summary>The torso as stored with the Avatar's T-pose (the pose the Humanoid was configured in).</summary>
            StoredTPose,

            /// <summary>Torso, neck, head and collarbone muscles at 0 (Unity's normalised neutral); pelvis as stored.</summary>
            MuscleZero,

            /// <summary>The pose the mesh was skinned in (the modeller's posture), as the hands use it.</summary>
            SkinBindPose,
        }

        /// <summary>Torso bones, parents first.</summary>
        static readonly HumanBodyBones[] TorsoBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
        };

        static readonly HumanBodyBones[] MuscleZeroBones =
        {
            HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.Jaw, HumanBodyBones.LeftEye, HumanBodyBones.RightEye,
            HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder,
        };

        /// <summary>
        /// Writes the Avatar's T-pose rotations into every transform below the animator (the root object itself and
        /// all positions are left as they are), then the torso from <paramref name="torso"/>. Returns the number of
        /// bones set from the stored T-pose, 0 when the Avatar has no stored skeleton (nothing is changed then).
        /// </summary>
        public static int Apply(Animator animator, TorsoSource torso = TorsoSource.SkinBindPose)
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

            if (count > 0 && torso != TorsoSource.StoredTPose)
            {
                // The limbs hang off the torso: keep their T-pose directions in the world while the torso changes.
                var limbs = new List<(Transform bone, Quaternion rotation)>();
                foreach (var limb in LimbRoots)
                {
                    var t = animator.GetBoneTransform(limb);
                    if (t != null)
                        limbs.Add((t, t.rotation));
                }

                if (torso == TorsoSource.MuscleZero)
                    MuscleZeroTorso(animator);
                else
                    BindPoseTorso(animator);

                foreach (var (bone, rotation) in limbs)
                    bone.rotation = rotation;
            }

            return count;
        }

        static readonly HumanBodyBones[] LimbRoots =
        {
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
        };

        /// <summary>
        /// Torso bones turned to their skin bind pose orientation (world rotations, parents first; the caller puts
        /// the limbs back to their T-pose directions), and everything below the head (jaw, eyes, eyelids, brows) set
        /// to its bind pose relative to its parent: the stored pose of the Rocketbox bodies has the mouth open, which
        /// nothing drives at runtime.
        /// </summary>
        static void BindPoseTorso(Animator animator)
        {
            var bindWorld = SkinBindWorld(animator);
            if (bindWorld.Count == 0)
                return;

            foreach (var bone in TorsoBones)
            {
                var t = animator.GetBoneTransform(bone);
                if (t != null && bindWorld.TryGetValue(t, out var m))
                    t.rotation = m.rotation;
            }

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head == null)
                return;

            var queue = new Queue<Transform>();
            queue.Enqueue(head);
            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                bindWorld.TryGetValue(parent, out var parentBind);
                var parentHasBind = bindWorld.ContainsKey(parent);
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (parentHasBind && bindWorld.TryGetValue(child, out var childBind))
                    {
                        var local = parentBind.inverse * childBind;
                        child.localPosition = local.GetColumn(3);
                        child.localRotation = local.rotation;
                    }

                    queue.Enqueue(child);
                }
            }
        }

        /// <summary>World matrix of every skinned bone in the pose the mesh was skinned in.</summary>
        static Dictionary<Transform, Matrix4x4> SkinBindWorld(Animator animator)
        {
            var bindWorld = new Dictionary<Transform, Matrix4x4>();
            foreach (var skinned in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skinned.sharedMesh;
                if (mesh == null)
                    continue;
                var bones = skinned.bones;
                var bindposes = mesh.bindposes;
                var meshToWorld = skinned.transform.localToWorldMatrix;
                for (var i = 0; i < bones.Length && i < bindposes.Length; i++)
                {
                    if (bones[i] != null && !bindWorld.ContainsKey(bones[i]))
                        bindWorld[bones[i]] = meshToWorld * bindposes[i].inverse;
                }
            }

            return bindWorld;
        }

        static void MuscleZeroTorso(Animator animator)
        {
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null)
                return;

            // A HumanPose round trip may move and turn the pelvis (and drifts it when the root is far from the
            // origin): the pelvis stays as stored, only the bones above it change.
            var hipsPosition = hips.localPosition;
            var hipsRotation = hips.localRotation;
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                for (var i = 0; i < pose.muscles.Length; i++)
                {
                    if (Array.IndexOf(MuscleZeroBones, (HumanBodyBones)HumanTrait.BoneFromMuscle(i)) >= 0)
                        pose.muscles[i] = 0f;
                }

                handler.SetHumanPose(ref pose);
            }

            hips.localPosition = hipsPosition;
            hips.localRotation = hipsRotation;
        }
    }
}
