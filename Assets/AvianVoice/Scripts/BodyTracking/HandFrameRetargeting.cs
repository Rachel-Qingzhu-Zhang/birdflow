using System.Collections.Generic;
using UnityEngine;

namespace AvianVoice.BodyTracking
{
    /// <summary>
    /// Rig-independent retargeting of the forearm, wrist and finger rotations from a tracked skeleton onto a
    /// Humanoid avatar.
    ///
    /// Meta's OVRUnityHumanoidSkeletonRetargeter matches every bone only by its direction (a FromToRotation from the
    /// avatar bone to the tracked bone). The roll about the bone stays whatever the avatar had in its T-pose, so when
    /// the avatar's rest palm faces another way than the tracked skeleton's bind pose, the hand comes out rotated and
    /// the fingers bend about the wrong axes (crumpled hands). This class gives every bone a full frame built from
    /// joint positions only (forward = towards the next joint of the chain, up = a roll reference from the hand, see
    /// <see cref="TryFrame"/>) and maps the source's bind-pose frame onto the avatar's rest frame, so the result does
    /// not depend on how each rig's bone axes are set up. Wrist and palm follow the tracked hand exactly; forearm and
    /// finger rolls assume both reference poses have no axial twist relative to the hand (neutral T / bind poses).
    ///
    /// Usage: <see cref="Calibrate"/> once with the source bind pose and the avatar rest pose (same space, e.g. the
    /// avatar root), then every frame set <c>avatarBone.rotation = sourceBone.rotation * correction</c> in
    /// <see cref="ApplyOrder"/> (parents first).
    /// </summary>
    public sealed class HandFrameRetargeting
    {
        /// <summary>One joint of a pose snapshot.</summary>
        public struct Joint
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        /// <summary>Joint positions and rotations of one skeleton in one shared frame (for example avatar root space).</summary>
        public sealed class Snapshot
        {
            public readonly Dictionary<HumanBodyBones, Joint> joints = new();

            /// <summary>End points after the distal finger bones (fingertips). Optional.</summary>
            public readonly Dictionary<HumanBodyBones, Vector3> tips = new();

            public void Set(HumanBodyBones bone, Vector3 position, Quaternion rotation)
            {
                joints[bone] = new Joint { position = position, rotation = rotation };
            }

            public bool Has(HumanBodyBones bone) => joints.ContainsKey(bone);

            public void Clear()
            {
                joints.Clear();
                tips.Clear();
            }
        }

        static readonly HumanBodyBones[] Fingers =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal,
        };

        /// <summary>Bones handled here, parents before children (apply rotations in this order).</summary>
        public static readonly HumanBodyBones[] ApplyOrder = BuildApplyOrder();

        readonly Dictionary<HumanBodyBones, Quaternion> corrections = new();
        readonly Dictionary<HumanBodyBones, float> directionOnlyRollError = new();

        public bool IsCalibrated => corrections.Count > 0;

        public int BoneCount => corrections.Count;

        /// <summary>
        /// Per bone, the rotation (degrees) a direction-only alignment (the SDK's method) would have been off at the
        /// bind pose. Large values mean the two rigs' rest poses disagree about the roll of that bone.
        /// </summary>
        public IReadOnlyDictionary<HumanBodyBones, float> DirectionOnlyRollError => directionOnlyRollError;

        public static bool IsFinger(HumanBodyBones bone)
        {
            return bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.RightLittleDistal;
        }

        public static bool IsLeft(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.LeftLowerArm || bone == HumanBodyBones.LeftHand ||
                   (bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.LeftLittleDistal);
        }

        public static bool IsDistal(HumanBodyBones bone)
        {
            return IsFinger(bone) && (bone - HumanBodyBones.LeftThumbProximal) % 3 == 2;
        }

        /// <summary>
        /// Compute the per-bone corrections. Both snapshots must be in the same space. Bones missing on either side
        /// are skipped. Returns false when neither hand could be calibrated.
        /// </summary>
        public bool Calibrate(Snapshot sourceBind, Snapshot targetRest, bool includeForearm, bool includeFingers)
        {
            corrections.Clear();
            directionOnlyRollError.Clear();
            if (sourceBind == null || targetRest == null)
                return false;

            foreach (var bone in ApplyOrder)
            {
                if (!includeForearm && (bone == HumanBodyBones.LeftLowerArm || bone == HumanBodyBones.RightLowerArm))
                    continue;
                if (!includeFingers && IsFinger(bone))
                    continue;
                if (!sourceBind.joints.TryGetValue(bone, out var s) || !targetRest.joints.TryGetValue(bone, out var t))
                    continue;
                if (!TryFrame(sourceBind, bone, out var sourceFrame) || !TryFrame(targetRest, bone, out var targetFrame))
                    continue;

                corrections[bone] = Quaternion.Inverse(s.rotation) * sourceFrame * Quaternion.Inverse(targetFrame) * t.rotation;

                // What the SDK's direction-only match would give at the bind pose, compared with the full frame.
                var directionOnlyFrame = Quaternion.FromToRotation(targetFrame * Vector3.forward, sourceFrame * Vector3.forward) * targetFrame;
                directionOnlyRollError[bone] = Quaternion.Angle(directionOnlyFrame, sourceFrame);
            }

            return corrections.Count > 0;
        }

        public bool TryGetCorrection(HumanBodyBones bone, out Quaternion correction)
        {
            return corrections.TryGetValue(bone, out correction);
        }

        /// <summary>The SDK's direction-only correction for the same bone (for comparisons and diagnostics).</summary>
        public static bool TryDirectionOnlyCorrection(Snapshot sourceBind, Snapshot targetRest, HumanBodyBones bone, out Quaternion correction)
        {
            correction = Quaternion.identity;
            if (!sourceBind.joints.TryGetValue(bone, out var s) || !targetRest.joints.TryGetValue(bone, out var t))
                return false;
            if (!TryFrame(sourceBind, bone, out var sourceFrame) || !TryFrame(targetRest, bone, out var targetFrame))
                return false;

            correction = Quaternion.Inverse(s.rotation) *
                         Quaternion.FromToRotation(targetFrame * Vector3.forward, sourceFrame * Vector3.forward) * t.rotation;
            return true;
        }

        /// <summary>
        /// Frame of a bone: forward along the bone (towards the next joint, or the fingertip), with a roll that does
        /// not depend on how bent the joints are in the reference pose:
        /// <list type="bullet">
        /// <item>forearm, wrist and the first segment of each finger: up = the palm normal. For a segment bending about
        /// its own flexion axis (perpendicular to the segment and the palm normal) or spreading about the palm normal,
        /// this frame turns exactly with the bone;</item>
        /// <item>middle and distal segments: the parent segment's frame carried along the joint bend (the shortest
        /// rotation from the parent's direction to this one), which turns exactly with a hinge joint and never
        /// degenerates, even for a segment curled perpendicular to the palm;</item>
        /// <item>a distal bone without a fingertip on this rig: the middle segment's frame carried through the rig's own
        /// bone axes (consecutive finger bones of a rig share their axis convention). Prefer giving fingertips, e.g.
        /// from <see cref="ComputeFingertips"/>.</item>
        /// </list>
        /// The roll is thereby defined as "no axial twist relative to the hand" in the reference pose, which holds for
        /// neutral T-poses and bind poses.
        /// </summary>
        public static bool TryFrame(Snapshot pose, HumanBodyBones bone, out Quaternion frame)
        {
            frame = Quaternion.identity;
            if (!pose.joints.TryGetValue(bone, out var joint))
                return false;

            var isSegmentAfterFirst = IsFinger(bone) && !IsProximal(bone);
            if (isSegmentAfterFirst)
            {
                var parent = bone - 1;
                if (!pose.joints.TryGetValue(parent, out var p) || !TryFrame(pose, parent, out var parentFrame))
                    return false;

                if (!TryDirection(pose, bone, out var dir))
                {
                    if (!IsDistal(bone))
                        return false;
                    frame = joint.rotation * (Quaternion.Inverse(p.rotation) * parentFrame);
                    return true;
                }

                var parentDir = parentFrame * Vector3.forward;
                frame = Quaternion.FromToRotation(parentDir, dir) * parentFrame;
                return true;
            }

            if (!TryDirection(pose, bone, out var direction))
                return false;
            if (!TryPalmNormal(pose, IsLeft(bone), out var normal, out var across))
                return false;

            var up = normal;
            if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.95f)
                up = across;
            if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.95f)
                return false;

            frame = Quaternion.LookRotation(direction, up);
            return true;
        }

        /// <summary>
        /// Fill a snapshot of an avatar's hands in the pose its mesh was skinned in (from the skin bind poses), in
        /// <paramref name="root"/> space. This is the avatar reference to calibrate against: the Humanoid muscle-zero
        /// "T-pose" is Unity's normalisation and can twist finger bones about their own axis (MakeHuman's fingers come
        /// out turned), which joint positions cannot reveal and which a calibration would then carry into every frame.
        /// Bones that are not skinned keep their current transform. Returns how many bones came from bind poses.
        /// </summary>
        public static int SnapshotFromSkinBindPose(Animator animator, Transform root, IEnumerable<HumanBodyBones> bones,
            IReadOnlyDictionary<HumanBodyBones, Vector3> fingertips, Snapshot snapshot)
        {
            snapshot.Clear();
            if (animator == null || root == null)
                return 0;

            var bindWorld = new Dictionary<Transform, Matrix4x4>();
            foreach (var skinned in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skinned.sharedMesh;
                if (mesh == null)
                    continue;
                var skinBones = skinned.bones;
                var bindposes = mesh.bindposes;
                var meshToWorld = skinned.transform.localToWorldMatrix;
                for (var i = 0; i < skinBones.Length && i < bindposes.Length; i++)
                {
                    if (skinBones[i] != null && !bindWorld.ContainsKey(skinBones[i]))
                        bindWorld[skinBones[i]] = meshToWorld * bindposes[i].inverse;
                }
            }

            var rootInv = Quaternion.Inverse(root.rotation);
            var fromBind = 0;
            foreach (var bone in bones)
            {
                var t = animator.GetBoneTransform(bone);
                if (t == null)
                    continue;

                Matrix4x4 world;
                if (bindWorld.TryGetValue(t, out var m))
                {
                    world = m;
                    fromBind++;
                }
                else
                {
                    world = t.localToWorldMatrix;
                }

                snapshot.Set(bone, root.InverseTransformPoint(world.GetColumn(3)), rootInv * world.rotation);
                if (fingertips != null && fingertips.TryGetValue(bone, out var localTip))
                    snapshot.tips[bone] = root.InverseTransformPoint(world.MultiplyPoint3x4(localTip));
            }

            return fromBind;
        }

        /// <summary>Bones a hand snapshot needs: <see cref="ApplyOrder"/> plus the palm reference joints.</summary>
        public static readonly HumanBodyBones[] SnapshotBones = BuildSnapshotBones();

        static HumanBodyBones[] BuildSnapshotBones()
        {
            var list = new List<HumanBodyBones>(ApplyOrder);
            foreach (var b in new[]
                     {
                         HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal,
                         HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal,
                         HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal
                     })
            {
                if (!list.Contains(b))
                    list.Add(b);
            }

            return list.ToArray();
        }

        public static bool IsProximal(HumanBodyBones bone)
        {
            return IsFinger(bone) && (bone - HumanBodyBones.LeftThumbProximal) % 3 == 0;
        }

        /// <summary>
        /// Fingertip points of an avatar taken from its mesh (distal bones often have no child joint, and when they
        /// do it is not always at the tip): the centre of the vertices skinned mostly to each distal bone, pushed out
        /// to twice its distance from the joint, in the distal bone's local space (rigid with the bone). Needs
        /// readable meshes. Keyed by the Humanoid distal bone.
        /// </summary>
        public static Dictionary<HumanBodyBones, Vector3> ComputeFingertips(Animator animator)
        {
            var result = new Dictionary<HumanBodyBones, Vector3>();
            if (animator == null || !animator.isHuman)
                return result;

            var sums = new Dictionary<Transform, Vector3>();
            var counts = new Dictionary<Transform, int>();
            var wanted = new Dictionary<Transform, HumanBodyBones>();
            foreach (var bone in ApplyOrder)
            {
                if (!IsDistal(bone))
                    continue;
                var t = animator.GetBoneTransform(bone);
                if (t != null)
                    wanted[t] = bone;
            }

            if (wanted.Count == 0)
                return result;

            foreach (var skinned in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skinned.sharedMesh;
                if (mesh == null || !mesh.isReadable)
                    continue;

                var bones = skinned.bones;
                var bindposes = mesh.bindposes;
                var vertices = mesh.vertices;
                var weights = mesh.boneWeights;
                if (weights.Length != vertices.Length || bindposes.Length != bones.Length)
                    continue;

                for (var i = 0; i < vertices.Length; i++)
                {
                    var w = weights[i];
                    if (w.weight0 < 0.6f || w.boneIndex0 >= bones.Length)
                        continue;
                    var b = bones[w.boneIndex0];
                    if (b == null || !wanted.ContainsKey(b))
                        continue;

                    // Bind pose: mesh space -> bone local space.
                    var local = bindposes[w.boneIndex0].MultiplyPoint3x4(vertices[i]);
                    sums[b] = sums.TryGetValue(b, out var s) ? s + local : local;
                    counts[b] = counts.TryGetValue(b, out var c) ? c + 1 : 1;
                }
            }

            foreach (var pair in wanted)
            {
                if (counts.TryGetValue(pair.Key, out var n) && n > 0)
                    result[pair.Value] = sums[pair.Key] / n * 2f;
            }

            return result;
        }

        public static bool IsThumb(HumanBodyBones bone)
        {
            return (bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.LeftThumbDistal) ||
                   (bone >= HumanBodyBones.RightThumbProximal && bone <= HumanBodyBones.RightThumbDistal);
        }

        /// <summary>
        /// Unit vector from this joint towards the next joint of its chain; for a distal bone towards its fingertip
        /// (false when the rig has no fingertip joint).
        /// </summary>
        public static bool TryDirection(Snapshot pose, HumanBodyBones bone, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!pose.joints.TryGetValue(bone, out var joint))
                return false;

            Vector3 delta;
            if (IsDistal(bone))
            {
                if (!pose.tips.TryGetValue(bone, out var tip))
                    return false;
                delta = tip - joint.position;
            }
            else
            {
                var next = NextInChain(bone);
                if (!pose.joints.TryGetValue(next, out var child))
                    return false;
                delta = child.position - joint.position;
            }

            if (delta.sqrMagnitude < 1e-10f)
                return false;

            direction = delta.normalized;
            return true;
        }

        // ------------------------------------------------------------------ forearm twist

        /// <summary>Angle (degrees, -180..180) of the part of <paramref name="q"/> that turns about the unit <paramref name="axis"/> (swing-twist split).</summary>
        public static float TwistAngle(Quaternion q, Vector3 axis)
        {
            var projection = q.x * axis.x + q.y * axis.y + q.z * axis.z;
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(projection, q.w) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Shares the axial twist between elbow and hand (turning the wrist, pronation / supination) out along the
        /// forearm. In a real arm the radius turns about the ulna, so the skin twists gradually from the elbow to the
        /// wrist; a rig with one forearm bone and no twist bones (MakeHuman, Rocketbox Adult_01, the Armature) puts all
        /// of it into whichever joint the tracker does: Meta's body skeleton carries it in its wrist-twist joint, so
        /// the avatar's wrist skin wrings like a sweet wrapper. Here the forearm bone is turned about its own axis to
        /// carry <paramref name="share"/> of the total twist and the hand is put back to its world rotation, so the
        /// hand still matches the tracking exactly.
        /// <paramref name="elbowRest"/> = forearm rotation relative to the upper arm, <paramref name="wristRest"/> =
        /// hand relative to the forearm, both in a pose without twist (the tracker's bind pose). Returns the total
        /// twist in degrees.
        /// </summary>
        public static float DistributeForearmTwist(Transform upperArm, Transform forearm, Transform hand,
            Quaternion elbowRest, Quaternion wristRest, float share)
        {
            if (upperArm == null || forearm == null || hand == null)
                return 0f;

            var handWorld = hand.rotation;
            var toHand = hand.position - forearm.position;
            if (toHand.sqrMagnitude < 1e-10f)
                return 0f;

            // The forearm axis in the forearm's own frame, and in the hand's frame of the untwisted pose.
            var axisInForearm = (Quaternion.Inverse(forearm.rotation) * toHand).normalized;
            var axisInHand = Quaternion.Inverse(wristRest) * axisInForearm;

            var elbow = Quaternion.Inverse(elbowRest) * (Quaternion.Inverse(upperArm.rotation) * forearm.rotation);
            var wrist = Quaternion.Inverse(wristRest) * (Quaternion.Inverse(forearm.rotation) * handWorld);
            var atElbow = TwistAngle(elbow, axisInForearm);
            var total = Mathf.DeltaAngle(0f, atElbow + TwistAngle(wrist, axisInHand));

            forearm.rotation *= Quaternion.AngleAxis(Mathf.Clamp01(share) * total - atElbow, axisInForearm);
            hand.rotation = handWorld;
            return total;
        }

        /// <summary>
        /// Palm normal n = (wrist -> middle base) x (little base -> index base), and the across-the-palm unit axis
        /// (little -> index). The same formula on both skeletons gives the same side of the palm for the same hand.
        /// </summary>
        public static bool TryPalmNormal(Snapshot pose, bool left, out Vector3 normal, out Vector3 across)
        {
            normal = Vector3.zero;
            across = Vector3.zero;
            var hand = left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var middle = left ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal;
            var index = left ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal;
            var little = left ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal;
            var ring = left ? HumanBodyBones.LeftRingProximal : HumanBodyBones.RightRingProximal;

            if (!pose.joints.TryGetValue(hand, out var w) || !pose.joints.TryGetValue(middle, out var m) ||
                !pose.joints.TryGetValue(index, out var i))
                return false;
            if (!pose.joints.TryGetValue(little, out var l) && !pose.joints.TryGetValue(ring, out l))
                return false;

            var along = m.position - w.position;
            across = i.position - l.position;
            normal = Vector3.Cross(along, across);
            if (normal.sqrMagnitude < 1e-12f || across.sqrMagnitude < 1e-12f)
                return false;

            normal.Normalize();
            across.Normalize();
            return true;
        }

        public static HumanBodyBones NextInChain(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.LeftUpperArm: return HumanBodyBones.LeftLowerArm;
                case HumanBodyBones.LeftLowerArm: return HumanBodyBones.LeftHand;
                case HumanBodyBones.LeftHand: return HumanBodyBones.LeftMiddleProximal;
                case HumanBodyBones.RightUpperArm: return HumanBodyBones.RightLowerArm;
                case HumanBodyBones.RightLowerArm: return HumanBodyBones.RightHand;
                case HumanBodyBones.RightHand: return HumanBodyBones.RightMiddleProximal;
            }

            return IsFinger(bone) && !IsDistal(bone) ? bone + 1 : HumanBodyBones.LastBone;
        }

        static HumanBodyBones[] BuildApplyOrder()
        {
            var order = new List<HumanBodyBones>
            {
                HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand
            };
            order.AddRange(Fingers); // proximal, intermediate, distal per finger: parents first
            return order.ToArray();
        }
    }
}
