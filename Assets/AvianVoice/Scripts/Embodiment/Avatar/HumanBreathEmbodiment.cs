using System.Globalization;
using AvianVoice.Physiology;
using AvianVoice.Tasks;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Chest breathing for the human avatar, the counterpart of the parrot's breath chest in ParrotBreathEmbodiment:
    /// a deep breath that is plain to see in the mirror (the chest comes forward by a few centimetres and the
    /// shoulders rise), so the breathing scene works with the human avatar as it does with the bird. Follows the same
    /// breath signal (BreathSimulator: the teacher's pattern) and is active in the breathing scene (Active Tasks)
    /// with a short fade in and out.
    ///
    /// The bone the neck and shoulders hang from (upper chest) is scaled: front-back by Depth Inhale and sideways by
    /// Width Inhale at full inhale. Its direct children get the inverse scale, so the neck, head and arms keep their
    /// size and the head stays where it was. The shoulder bones are lifted by Shoulder Rise. The axes are found from
    /// joint positions (towards the neck = up, shoulder to shoulder = sideways), so it works whatever the rig's bone
    /// axes are.
    /// Runs after the body-tracking retargeter (Update) and before HumanFirstPersonDriver places the eyes (31000).
    /// </summary>
    [DefaultExecutionOrder(30500)]
    public sealed class HumanBreathEmbodiment : MonoBehaviour
    {
        [Tooltip("Front-back expansion of the chest at full inhale (0.22 = 22 %, a few centimetres: a deep breath that shows " +
                 "in the mirror; it was 6 % until 2026-11-12, too little to see). The bird uses 40 %.")]
        [SerializeField, Range(0f, 0.3f)]
        float depthInhale = 0.22f;

        [Tooltip("Sideways expansion of the chest at full inhale: what shows most in the mirror, which is seen from the front.")]
        [SerializeField, Range(0f, 0.2f)]
        float widthInhale = 0.1f;

        [Tooltip("Lengthening along the spine at full inhale (the chest rises a little).")]
        [SerializeField, Range(0f, 0.1f)]
        float lengthInhale = 0.03f;

        [Tooltip("How far the shoulders rise at full inhale (m).")]
        [SerializeField, Range(0f, 0.03f)]
        float shoulderRise = 0.022f;

        [Tooltip("Task ids (scenes) in which the human avatar breathes. Empty = always.")]
        [SerializeField]
        string[] activeTasks = { "breath_sync" };

        [SerializeField]
        float fadeSeconds = 0.6f;

        [Tooltip("Smoothing of the breath depth signal (s).")]
        [SerializeField]
        float depthSmoothSeconds = 0.15f;

        Animator animator;
        Transform bone;
        Vector3 boneRestScale = Vector3.one;
        Transform[] children = new Transform[0];
        Vector3[] childRestScales = new Vector3[0];
        readonly Transform[] shoulders = new Transform[2];
        readonly Vector3[] shoulderRestPositions = new Vector3[2];
        float upSign = 1f;
        int upAxis = 1;
        int rightAxis;
        int forwardAxis = 2;
        bool resolved;
        bool applied;
        BreathSimulator breath;
        float smoothedDepth;
        float weight;
        float previewEmphasis = -1f;

        /// <summary>Chest bone being scaled (diagnostics).</summary>
        public Transform ChestBone => bone;

        /// <summary>Current emphasis 0..1 after the scene fade (diagnostics).</summary>
        public float Emphasis { get; private set; }

        void OnDisable()
        {
            Restore();
        }

        void LateUpdate()
        {
            if (!resolved && !Resolve())
                return;

            if (breath == null)
                breath = FindAnyObjectByType<BreathSimulator>();

            var dt = Time.deltaTime;
            var target = IsActiveScene() ? 1f : 0f;
            weight = Mathf.MoveTowards(weight, target, fadeSeconds <= 0f ? 1f : dt / fadeSeconds);

            var depth = breath != null ? breath.Current.depth : 0f;
            var k = depthSmoothSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt / depthSmoothSeconds);
            smoothedDepth = Mathf.Lerp(smoothedDepth, depth, k);

            var emphasis = previewEmphasis >= 0f ? previewEmphasis : ParrotChestBreathTuning.DepthToEmphasis(smoothedDepth) * weight;
            Emphasis = emphasis;
            if (emphasis <= 0.0005f)
            {
                Restore();
                return;
            }

            Apply(emphasis);
        }

        bool IsActiveScene()
        {
            if (activeTasks == null || activeTasks.Length == 0)
                return true;
            foreach (var id in activeTasks)
            {
                if (!string.IsNullOrEmpty(id) && TaskActivity.IsActive(id))
                    return true;
            }

            return false;
        }

        bool Resolve()
        {
            animator = GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
                return false;

            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            var leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            bone = neck != null ? neck.parent : null;
            if (bone == null)
                bone = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            if (bone == null || neck == null || leftArm == null || rightArm == null)
                return false;

            // Axes in the bone's own frame from joint positions (independent of the rig's bone axis conventions).
            var towardsNeck = bone.InverseTransformDirection(neck.position - bone.position);
            upAxis = DominantAxis(towardsNeck);
            upSign = towardsNeck[upAxis] >= 0f ? 1f : -1f;
            rightAxis = DominantAxis(bone.InverseTransformDirection(rightArm.position - leftArm.position), upAxis);
            forwardAxis = 3 - upAxis - rightAxis;

            boneRestScale = bone.localScale;
            children = new Transform[bone.childCount];
            childRestScales = new Vector3[bone.childCount];
            for (var i = 0; i < bone.childCount; i++)
            {
                children[i] = bone.GetChild(i);
                childRestScales[i] = children[i].localScale;
            }

            // Shoulder (clavicle) bones, wherever the rig hangs them (under the chest or under the neck).
            shoulders[0] = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            shoulders[1] = animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            for (var i = 0; i < shoulders.Length; i++)
                shoulderRestPositions[i] = shoulders[i] != null ? shoulders[i].localPosition : Vector3.zero;

            resolved = true;
            Debug.Log("[AvianVoice] Human breathing on '" + name + "': chest bone " + bone.name + " (axes up=" + "XYZ"[upAxis]
                      + " side=" + "XYZ"[rightAxis] + " depth=" + "XYZ"[forwardAxis] + "), " + children.Length
                      + " children compensated, depth +" + (depthInhale * 100f).ToString("0", CultureInfo.InvariantCulture)
                      + "% / width +" + (widthInhale * 100f).ToString("0", CultureInfo.InvariantCulture) + "% / shoulders +"
                      + (shoulderRise * 1000f).ToString("0", CultureInfo.InvariantCulture) + " mm at full inhale"
                      + (shoulders[0] != null && shoulders[1] != null ? "." : " (no shoulder bones: no shoulder rise)."));
            return true;
        }

        void Apply(float emphasis)
        {
            var s = Vector3.one;
            s[forwardAxis] = 1f + depthInhale * emphasis;
            s[rightAxis] = 1f + widthInhale * emphasis;
            s[upAxis] = 1f + lengthInhale * emphasis;
            bone.localScale = Vector3.Scale(boneRestScale, s);

            // Cancel the parent's scale in each child's own axes (first order: diagonal of R^T S R).
            for (var i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null)
                    continue;
                var r = Matrix4x4.Rotate(child.localRotation);
                var d = Vector3.zero;
                for (var axis = 0; axis < 3; axis++)
                {
                    var sum = 0f;
                    for (var j = 0; j < 3; j++)
                        sum += r[j, axis] * r[j, axis] * s[j];
                    d[axis] = sum;
                }

                child.localScale = new Vector3(childRestScales[i].x / d.x, childRestScales[i].y / d.y, childRestScales[i].z / d.z);
            }

            // Shoulders rise along the chest's up direction (world metres, whatever the scale of their parent).
            var up = Vector3.zero;
            up[upAxis] = upSign;
            var rise = bone.TransformDirection(up) * (shoulderRise * emphasis);
            for (var i = 0; i < shoulders.Length; i++)
            {
                var shoulder = shoulders[i];
                if (shoulder == null || shoulder.parent == null)
                    continue;
                shoulder.localPosition = shoulderRestPositions[i] + shoulder.parent.InverseTransformVector(rise);
            }

            applied = true;
        }

        void Restore()
        {
            if (!applied)
                return;
            if (bone != null)
                bone.localScale = boneRestScale;
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null)
                    children[i].localScale = childRestScales[i];
            }

            for (var i = 0; i < shoulders.Length; i++)
            {
                if (shoulders[i] != null)
                    shoulders[i].localPosition = shoulderRestPositions[i];
            }

            applied = false;
        }

        static int DominantAxis(Vector3 v, int exclude = -1)
        {
            var best = -1;
            var bestValue = -1f;
            for (var i = 0; i < 3; i++)
            {
                if (i == exclude)
                    continue;
                var value = Mathf.Abs(v[i]);
                if (value > bestValue)
                {
                    bestValue = value;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Test: measure the chest at exhale (0) and full inhale (1) on the baked skin, and how far the head and hands
        /// move because of it. Returns one line; leaves the pose as it was.
        /// </summary>
        public string Measure()
        {
            if (!resolved && !Resolve())
                return "not resolved (no Humanoid neck / arms)";

            var skinned = GetComponentInChildren<SkinnedMeshRenderer>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var lhand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var rhand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            var wasApplied = applied;
            var previous = Emphasis;

            Apply(0f);
            var rest = ChestExtent(skinned);
            var head0 = head.position; var l0 = lhand.position; var r0 = rhand.position; var headScale0 = head.lossyScale;
            var shoulder0 = shoulders[0] != null ? shoulders[0].position : Vector3.zero;
            Apply(1f);
            var full = ChestExtent(skinned);
            var head1 = head.position; var l1 = lhand.position; var r1 = rhand.position; var headScale1 = head.lossyScale;
            var shoulder1 = shoulders[0] != null ? shoulders[0].position : Vector3.zero;

            if (wasApplied && previous > 0f)
                Apply(previous);
            else
                Restore();

            var c = CultureInfo.InvariantCulture;
            return "chest depth " + (rest.x * 100f).ToString("0.0", c) + " -> " + (full.x * 100f).ToString("0.0", c) + " cm ("
                   + ((full.x / Mathf.Max(1e-4f, rest.x) - 1f) * 100f).ToString("+0.0;-0.0", c) + "%), width "
                   + (rest.y * 100f).ToString("0.0", c) + " -> " + (full.y * 100f).ToString("0.0", c) + " cm ("
                   + ((full.y / Mathf.Max(1e-4f, rest.y) - 1f) * 100f).ToString("+0.0;-0.0", c) + "%); shoulder moved "
                   + (Vector3.Distance(shoulder0, shoulder1) * 1000f).ToString("0.0", c) + " mm, head moved "
                   + (Vector3.Distance(head0, head1) * 1000f).ToString("0.0", c) + " mm, hands "
                   + (Vector3.Distance(l0, l1) * 1000f).ToString("0.0", c) + "/" + (Vector3.Distance(r0, r1) * 1000f).ToString("0.0", c)
                   + " mm, head size change " + ((headScale1.magnitude / Mathf.Max(1e-4f, headScale0.magnitude) - 1f) * 100f).ToString("+0.00;-0.00", c)
                   + "% (bone " + bone.name + ", neck " + (neck != null ? neck.name : "-") + ")";
        }

        /// <summary>Depth (along the avatar's forward) and width of the skin vertices weighted to the chest bone, in metres.</summary>
        Vector2 ChestExtent(SkinnedMeshRenderer skinned)
        {
            if (skinned == null || skinned.sharedMesh == null || !skinned.sharedMesh.isReadable)
                return Vector2.zero;

            var bones = skinned.bones;
            var index = System.Array.IndexOf(bones, bone);
            if (index < 0)
                return Vector2.zero;

            var baked = new Mesh();
            skinned.BakeMesh(baked, true);
            var vertices = baked.vertices;
            var weights = skinned.sharedMesh.boneWeights;
            var forward = transform.forward;
            var right = transform.right;
            float fMin = float.MaxValue, fMax = float.MinValue, rMin = float.MaxValue, rMax = float.MinValue;
            for (var i = 0; i < vertices.Length && i < weights.Length; i++)
            {
                var w = weights[i];
                var wb = (w.boneIndex0 == index ? w.weight0 : 0f) + (w.boneIndex1 == index ? w.weight1 : 0f)
                         + (w.boneIndex2 == index ? w.weight2 : 0f) + (w.boneIndex3 == index ? w.weight3 : 0f);
                if (wb < 0.5f)
                    continue;
                var p = skinned.transform.TransformPoint(vertices[i]);
                var f = Vector3.Dot(p, forward);
                var r = Vector3.Dot(p, right);
                fMin = Mathf.Min(fMin, f); fMax = Mathf.Max(fMax, f);
                rMin = Mathf.Min(rMin, r); rMax = Mathf.Max(rMax, r);
            }

            Destroy(baked);
            return fMax < fMin ? Vector2.zero : new Vector2(fMax - fMin, rMax - rMin);
        }

        /// <summary>Hold a fixed emphasis (0..1) for tests; negative = follow the breath signal.</summary>
        public void SetPreviewEmphasis(float emphasis) => previewEmphasis = emphasis;
    }

    /// <summary>Adds <see cref="HumanBreathEmbodiment"/> to every human avatar the embodiment controller creates.</summary>
    public sealed class HumanBreathInstaller : MonoBehaviour
    {
        AvatarEmbodimentController controller;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            var controller = FindAnyObjectByType<AvatarEmbodimentController>();
            if (controller != null && controller.GetComponent<HumanBreathInstaller>() == null)
                controller.gameObject.AddComponent<HumanBreathInstaller>();
        }

        void Update()
        {
            if (controller == null)
                controller = GetComponent<AvatarEmbodimentController>();
            var root = controller != null ? controller.HumanRoot : null;
            if (root != null && root.GetComponent<HumanBreathEmbodiment>() == null)
                root.AddComponent<HumanBreathEmbodiment>();
        }
    }
}
