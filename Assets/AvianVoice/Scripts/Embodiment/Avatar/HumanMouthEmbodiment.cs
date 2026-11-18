using System;
using System.Collections.Generic;
using System.Globalization;
using AvianVoice.Audio;
using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>What to do with the inside of the mouth, which shows once the jaw opens.</summary>
    public enum MouthInteriorShading
    {
        /// <summary>Darken it on plain bodies (no texture on the face: the mannequin); leave textured faces as modelled.</summary>
        Auto,
        Always,
        Never
    }

    /// <summary>
    /// The human avatar opens its mouth while the participant vocalizes: the counterpart of the parrot's beak
    /// (BeakVocalFX), so the avatar in the mirror answers the voice with either body.
    ///
    /// What moves: the jaw bone (Humanoid Jaw, else a bone under the head named like one) turns about the head's
    /// left-right axis. The opening is set as a lip gap in metres, not as an angle: the jaw joint sits in a
    /// different place on every rig, so the angle for the same gap is worked out per body from how far the chin
    /// lies in front of the joint.
    ///
    /// What makes it look natural:
    /// - Gate: the mouth opens only while the analyzer calls the sound voiced, the same gate as the feedback
    ///   voice, so breathing and room noise never move it.
    /// - Amount: the quietest voiced sound already opens the lips part of the way (people do not sing through
    ///   closed lips) and loudness adds the rest; full voice is a sung "ah", not a shout.
    /// - Timing: eased both ways (no snapping), opening faster than closing, and a dropout shorter than
    ///   Close Hold Seconds does not close the mouth, so it does not flap on a held note.
    /// - Inside of the mouth: the mannequin body has a mouth cavity with the skin material, which would show as a
    ///   pale patch between the lips. Those triangles (found as the faces that are walled in on all sides while the
    ///   mouth is closed) get a dark copy of the face material. Textured bodies with teeth and a painted mouth are
    ///   left as they are.
    ///
    /// <see cref="Prepare"/> must run before the body is first shown (AvatarEmbodimentController calls it when it
    /// creates the body): FirstPersonHeadHider splits the meshes on its first enable and has to see the mouth
    /// interior as part of the head, so it stays hidden from the headset camera like the rest of the head.
    ///
    /// Runs after HumanFirstPersonDriver (31000), whose fallback pose rewrites every Humanoid bone, the jaw included.
    /// </summary>
    [DefaultExecutionOrder(31400)]
    public sealed class HumanMouthEmbodiment : MonoBehaviour
    {
        [Tooltip("How far the lower lip drops at full voice (m). 0.011 = a sung note (0.014 already reads as a wide 'ah' on " +
                 "the mannequin, whose mouth has no teeth to fill it). The jaw angle follows from each body's jaw joint " +
                 "and skin weights.")]
        [SerializeField, Range(0f, 0.03f)]
        float lipOpening = 0.011f;

        [Tooltip("Share of that opening at the quietest voiced sound; loudness adds the rest.")]
        [SerializeField, Range(0f, 1f)]
        float softVoiceShare = 0.45f;

        [Tooltip("Analyzer loudness (0-1) at which the mouth is fully open. Sung notes in the study sit between 0.4 and 1.")]
        [SerializeField, Range(0.15f, 1f)]
        float fullLoudness = 0.7f;

        [Tooltip("Analyzer loudness of the quietest voiced sound (voiced threshold x loudness gain, about 0.1).")]
        [SerializeField, Range(0f, 0.5f)]
        float quietLoudness = 0.1f;

        [Tooltip("Easing time when the mouth opens (s).")]
        [SerializeField, Range(0f, 0.5f)]
        float openSeconds = 0.07f;

        [Tooltip("Easing time when the mouth closes (s): slower than opening, as a jaw relaxes.")]
        [SerializeField, Range(0f, 0.5f)]
        float closeSeconds = 0.14f;

        [Tooltip("A gap in the voice shorter than this does not start closing the mouth (s).")]
        [SerializeField, Range(0f, 0.3f)]
        float closeHoldSeconds = 0.06f;

        [Tooltip("Upper limit of the jaw bone's angle, whatever the rig's jaw joint and skin weights give (degrees).")]
        [SerializeField, Range(0f, 25f)]
        float maxJawDegrees = 18f;

        [Header("Inside of the mouth")]
        [SerializeField]
        MouthInteriorShading interiorShading = MouthInteriorShading.Auto;

        [Tooltip("Brightness of the inside of the mouth relative to the face colour (a shadowed cavity).")]
        [SerializeField, Range(0f, 1f)]
        float interiorBrightness = 0.16f;

        const float DefaultReach = 0.09f;

        Transform jaw;
        Quaternion restLocalRotation = Quaternion.identity;
        Vector3 axisInParent = Vector3.right;
        float reach = DefaultReach;
        bool prepared;
        VoiceAnalyzer analyzer;
        float opening;
        float openingVelocity;
        float heldTarget;
        float lastVoicedTime = float.NegativeInfinity;
        float preview = -1f;
        VoiceFrame lastVoice;

        SkinnedMeshRenderer shadedRenderer;
        Mesh shadedMesh;
        Material interiorMaterial;
        int interiorTriangles;
        string interiorReport = "not checked";

        /// <summary>Jaw bone being turned; null when the body has none (the mouth then stays closed).</summary>
        public Transform JawBone => jaw;

        /// <summary>Opening now, 0 (closed) to 1 (full voice).</summary>
        public float Opening => opening;

        /// <summary>Jaw angle at full voice for this body (degrees).</summary>
        public float FullJawDegrees => Mathf.Min(maxJawDegrees, Mathf.Rad2Deg * lipOpening / Mathf.Max(0.02f, reach));

        public float LipOpeningMeters => lipOpening;
        public float SoftVoiceShare => softVoiceShare;
        public float OpenSeconds => openSeconds;
        public float CloseSeconds => closeSeconds;
        public float CloseHoldSeconds => closeHoldSeconds;

        /// <summary>Number of triangles given the dark mouth material (0 when the mouth is left as modelled).</summary>
        public int InteriorTriangleCount => interiorTriangles;

        /// <summary>
        /// Find the jaw, measure the body and shade the inside of the mouth. Call before the body is first shown
        /// (before FirstPersonHeadHider builds). Returns false when the body has no jaw that moves skin.
        /// </summary>
        public bool Prepare()
        {
            if (prepared)
                return jaw != null;
            prepared = true;

            var animator = GetComponent<Animator>();
            Transform head = null;
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
            }

            if (head == null)
            {
                Debug.Log("[AvianVoice] Human mouth on '" + name + "': no Humanoid head bone; the mouth stays closed.");
                return false;
            }

            if (jaw == null)
                jaw = FindJawByName(head);
            if (jaw == null || jaw.parent == null)
            {
                jaw = null;
                Debug.Log("[AvianVoice] Human mouth on '" + name + "': no jaw bone under " + head.name + "; the mouth stays closed.");
                return false;
            }

            // The skinned mesh the jaw moves most of: the face.
            SkinnedMeshRenderer face = null;
            var faceJawWeight = 0.5f;
            foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var weight = TotalBoneWeight(skinned, jaw);
                if (weight > faceJawWeight)
                {
                    faceJawWeight = weight;
                    face = skinned;
                }
            }

            if (face == null)
            {
                var detail = "";
                foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var m = skinned.sharedMesh;
                    detail += " [" + skinned.name + ": " + (m == null ? "no mesh" : (m.isReadable ? "readable" : "not readable") + ", "
                              + m.vertexCount + " vertices, " + skinned.bones.Length + " bones, jaw bone index "
                              + Array.IndexOf(skinned.bones, jaw)) + "]";
                }

                Debug.Log("[AvianVoice] Human mouth on '" + name + "': jaw bone " + jaw.name + " moves no skin; the mouth stays closed." + detail);
                jaw = null;
                return false;
            }

            // Rest pose, turning axis and joint position from the pose the mesh was skinned in (mouth closed),
            // whatever pose the body is in right now.
            var right = transform.right;
            var up = transform.up;
            var forward = transform.forward;
            Vector3 pivot;
            if (TryBindWorld(face, jaw.parent, out var parentBind) && TryBindWorld(face, jaw, out var jawBind))
            {
                restLocalRotation = (parentBind.inverse * jawBind).rotation;
                axisInParent = parentBind.inverse.MultiplyVector(right).normalized;
                pivot = jawBind.GetColumn(3);
            }
            else
            {
                restLocalRotation = jaw.localRotation;
                axisInParent = jaw.parent.InverseTransformDirection(right).normalized;
                pivot = jaw.position;
            }

            var mesh = face.sharedMesh;
            var readable = mesh != null && mesh.isReadable;
            if (readable)
            {
                var chin = MouthGeometry.FarthestJawPoint(face, jaw, pivot, forward, out var chinWeight);
                if (chin.HasValue)
                {
                    var arm = chin.Value - pivot;
                    // The skin follows the jaw bone only by its weight (the MakeHuman body shares the chin half
                    // and half with the head bone), so the lever that counts is distance x weight.
                    reach = Mathf.Max(0.02f, Vector3.Dot(arm, forward) * chinWeight);
                    // Opening must take the chin down: check the turning direction on the body itself.
                    var moved = Quaternion.AngleAxis(1f, right) * arm - arm;
                    if (Vector3.Dot(moved, up) > 0f)
                        axisInParent = -axisInParent;
                }
            }

            ShadeInterior(face, head, right, up, forward, readable);

            Debug.Log("[AvianVoice] Human mouth on '" + name + "': jaw bone " + jaw.name + ", lever of the chin skin about the jaw joint "
                      + (reach * 1000f).ToString("0", CultureInfo.InvariantCulture) + " mm (distance x skin weight); full voice opens the lips "
                      + (lipOpening * 1000f).ToString("0", CultureInfo.InvariantCulture) + " mm = "
                      + FullJawDegrees.ToString("0.0", CultureInfo.InvariantCulture) + " deg; inside of the mouth: " + interiorReport + ".");
            return true;
        }

        void ShadeInterior(SkinnedMeshRenderer face, Transform head, Vector3 right, Vector3 up, Vector3 forward, bool readable)
        {
            if (interiorShading == MouthInteriorShading.Never)
            {
                interiorReport = "left as modelled (shading off)";
                return;
            }

            if (!readable)
            {
                interiorReport = "left as modelled (mesh not readable)";
                return;
            }

            if (interiorShading == MouthInteriorShading.Auto && HasTexture(face))
            {
                interiorReport = "left as modelled (textured face)";
                return;
            }

            var hider = GetComponent<FirstPersonHeadHider>();
            if (hider != null && hider.SplitMeshCount > 0)
            {
                // Too late: the head hider has already split this mesh, and a sub-mesh added now would not be hidden
                // from the headset camera.
                interiorReport = "left as modelled (Prepare ran after the first-person head hider)";
                Debug.LogWarning("[AvianVoice] Human mouth on '" + name + "': Prepare() must run before the body is first shown.");
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = MouthGeometry.SplitInterior(face, head, jaw, right, up, forward, interiorBrightness);
            watch.Stop();
            if (result.mesh == null)
            {
                interiorReport = "nothing found to darken";
                return;
            }

            shadedRenderer = face;
            shadedMesh = result.mesh;
            interiorMaterial = result.material;
            interiorTriangles = result.triangles;
            var c = CultureInfo.InvariantCulture;
            interiorReport = result.triangles + " triangles darkened, a region " + (result.size.x * 1000f).ToString("0", c) + " mm wide x "
                             + (result.size.y * 1000f).ToString("0", c) + " high x " + (result.size.z * 1000f).ToString("0", c)
                             + " deep (brightness " + interiorBrightness.ToString("0.00", c) + ", found in " + watch.ElapsedMilliseconds + " ms)";
        }

        static bool HasTexture(Renderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null && material.mainTexture != null)
                    return true;
            }

            return false;
        }

        void OnDisable()
        {
            opening = 0f;
            openingVelocity = 0f;
            heldTarget = 0f;
            lastVoicedTime = float.NegativeInfinity;
            if (jaw != null)
                jaw.localRotation = restLocalRotation;
        }

        void OnDestroy()
        {
            Release();
        }

        /// <summary>Destroy the runtime mesh and material (the body is going away). Editor tests call this themselves.</summary>
        public void Release()
        {
            DestroyRuntimeObject(shadedMesh);
            DestroyRuntimeObject(interiorMaterial);
            shadedMesh = null;
            interiorMaterial = null;
        }

        static void DestroyRuntimeObject(UnityEngine.Object o)
        {
            if (o == null)
                return;
            if (Application.isPlaying)
                Destroy(o);
            else
                DestroyImmediate(o);
        }

        void LateUpdate()
        {
            if (!prepared)
                Prepare();
            if (jaw == null)
                return;

            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            lastVoice = analyzer != null ? analyzer.Current : default;

            float target;
            if (preview >= 0f)
            {
                target = preview;
            }
            else
            {
                target = TargetOpening(lastVoice);
                if (target > 0f)
                {
                    lastVoicedTime = Time.time;
                    heldTarget = target;
                }
                else if (Time.time - lastVoicedTime < closeHoldSeconds)
                {
                    target = heldTarget;
                }
            }

            var smoothTime = target > opening ? openSeconds : closeSeconds;
            opening = smoothTime <= 0f
                ? target
                : Mathf.SmoothDamp(opening, target, ref openingVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            opening = Mathf.Clamp01(opening);
            Apply(opening);
        }

        /// <summary>Opening (0-1) the voice asks for right now: 0 unless voiced, then the soft-voice share plus loudness.</summary>
        public float TargetOpening(VoiceFrame voice)
        {
            if (voice.voiced <= 0.5f)
                return 0f;

            var level = Mathf.InverseLerp(quietLoudness, Mathf.Max(quietLoudness + 0.01f, fullLoudness), Mathf.Clamp01(voice.loudness));
            level = level * level * (3f - 2f * level);
            return Mathf.Lerp(softVoiceShare, 1f, level);
        }

        void Apply(float amount)
        {
            jaw.localRotation = Quaternion.AngleAxis(amount * FullJawDegrees, axisInParent) * restLocalRotation;
        }

        /// <summary>Editor tests: pose the jaw at a fixed opening (0 = closed, 1 = full voice) right now, outside Play.</summary>
        public bool PoseNow(float amount)
        {
            if (!Prepare())
                return false;
            Apply(Mathf.Clamp01(amount));
            return true;
        }

        /// <summary>Hold a fixed opening (0..1) for tests; negative = follow the voice.</summary>
        public void SetPreview(float amount) => preview = amount;

        /// <summary>Diagnostics: the mouth as it stands this frame.</summary>
        public string DescribeNow()
        {
            if (!prepared)
                return "not prepared";
            if (jaw == null)
                return "no jaw bone (mouth stays closed)";

            var c = CultureInfo.InvariantCulture;
            return "opening " + opening.ToString("0.00", c) + " (lips " + (opening * FullJawDegrees * Mathf.Deg2Rad * reach * 1000f).ToString("0.0", c)
                   + " mm, jaw " + (opening * FullJawDegrees).ToString("0.0", c) + " deg of " + FullJawDegrees.ToString("0.0", c)
                   + "), voice " + (lastVoice.voiced > 0.5f ? "voiced" : "silent") + " loudness " + lastVoice.loudness.ToString("0.00", c)
                   + (preview >= 0f ? ", held for a test" : "") + ", jaw bone " + jaw.name + ", inside of the mouth: " + interiorReport;
        }

        static Transform FindJawByName(Transform head)
        {
            // Breadth first: the jaw itself comes before bones that hang from it (lips, tongue).
            var queue = new Queue<Transform>();
            queue.Enqueue(head);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                if (t != head && t.name.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
                for (var i = 0; i < t.childCount; i++)
                    queue.Enqueue(t.GetChild(i));
            }

            return null;
        }

        /// <summary>Skin weight on a bone and the bones under it, summed over the mesh (in vertices).</summary>
        static float TotalBoneWeight(SkinnedMeshRenderer skinned, Transform bone)
        {
            var mesh = skinned != null ? skinned.sharedMesh : null;
            if (mesh == null || !mesh.isReadable)
                return 0f;

            var mask = MouthGeometry.BoneMask(skinned, bone);
            if (mask == null)
                return 0f;

            var sum = 0f;
            foreach (var w in mesh.boneWeights)
                sum += MouthGeometry.Weight(w, mask);
            return sum;
        }

        /// <summary>World matrix of a bone in the pose the mesh was skinned in, for the renderer where it stands now.</summary>
        static bool TryBindWorld(SkinnedMeshRenderer skinned, Transform bone, out Matrix4x4 world)
        {
            world = Matrix4x4.identity;
            var mesh = skinned.sharedMesh;
            if (mesh == null)
                return false;

            var index = Array.IndexOf(skinned.bones, bone);
            var bindposes = mesh.bindposes;
            if (index < 0 || index >= bindposes.Length)
                return false;

            world = skinned.transform.localToWorldMatrix * bindposes[index].inverse;
            return true;
        }
    }

    /// <summary>Mesh work for <see cref="HumanMouthEmbodiment"/>: where the chin is, and which triangles are inside the mouth.</summary>
    static class MouthGeometry
    {
        /// <summary>A face is inside the mouth when at least this many of the 14 directions around it are blocked.</summary>
        const int MinBlockedRays = 12;
        const float RayLength = 0.25f;
        const float SurfaceOffset = 0.0005f;

        public struct InteriorResult
        {
            public Mesh mesh;
            public Material material;
            public int triangles;

            /// <summary>Extent of the darkened triangles: width, height, depth (m). A mouth cavity is a few centimetres each way.</summary>
            public Vector3 size;
        }

        struct Triangle
        {
            public int subMesh;
            public int index;
            public Vector3 a;
            public Vector3 edge1;
            public Vector3 edge2;
            public Vector3 center;
            public float radius;
            public bool candidate;
        }

        /// <summary>True for the bones of <paramref name="skinned"/> that are <paramref name="root"/> or hang from it; null when none.</summary>
        public static bool[] BoneMask(SkinnedMeshRenderer skinned, Transform root)
        {
            var bones = skinned.bones;
            var mask = new bool[bones.Length];
            var any = false;
            for (var i = 0; i < bones.Length; i++)
            {
                mask[i] = bones[i] != null && (bones[i] == root || bones[i].IsChildOf(root));
                any |= mask[i];
            }

            return any ? mask : null;
        }

        public static float Weight(BoneWeight w, bool[] mask)
        {
            var sum = 0f;
            if (w.boneIndex0 < mask.Length && mask[w.boneIndex0]) sum += w.weight0;
            if (w.boneIndex1 < mask.Length && mask[w.boneIndex1]) sum += w.weight1;
            if (w.boneIndex2 < mask.Length && mask[w.boneIndex2]) sum += w.weight2;
            if (w.boneIndex3 < mask.Length && mask[w.boneIndex3]) sum += w.weight3;
            return sum;
        }

        /// <summary>
        /// The skin point the jaw moves most (chin / lower lip): the largest distance in front of the jaw joint x
        /// skin weight on the jaw, in the bind pose. <paramref name="weight"/> is that point's weight on the jaw.
        /// </summary>
        public static Vector3? FarthestJawPoint(SkinnedMeshRenderer skinned, Transform jaw, Vector3 pivot, Vector3 forward, out float weight)
        {
            weight = 0f;
            var mask = BoneMask(skinned, jaw);
            var mesh = skinned.sharedMesh;
            if (mask == null || mesh == null)
                return null;

            var vertices = mesh.vertices;
            var weights = mesh.boneWeights;
            var toWorld = skinned.transform.localToWorldMatrix;
            Vector3? best = null;
            var bestReach = float.NegativeInfinity;
            for (var i = 0; i < vertices.Length && i < weights.Length; i++)
            {
                var w = Weight(weights[i], mask);
                if (w <= 0f)
                    continue;
                var p = toWorld.MultiplyPoint3x4(vertices[i]);
                var r = Vector3.Dot(p - pivot, forward) * w;
                if (r > bestReach)
                {
                    bestReach = r;
                    best = p;
                    weight = w;
                }
            }

            return best;
        }

        /// <summary>
        /// Move the triangles inside the mouth into a sub-mesh of their own with a dark copy of the face material.
        /// "Inside" = walled in: with the mouth closed (the pose the mesh was skinned in), rays from the face in 14
        /// directions (the 6 axes and the 8 diagonals) almost all hit the head. Skin on the outside sees open space
        /// over half of them; the cavity behind the lips sees none except through the slit between the lips. Only
        /// the lower face is looked at (around the skin the jaw moves), which also keeps the cost down.
        /// The renderer gets a runtime copy of the mesh; the asset is untouched.
        /// </summary>
        public static InteriorResult SplitInterior(SkinnedMeshRenderer skinned, Transform head, Transform jaw,
            Vector3 right, Vector3 up, Vector3 forward, float brightness)
        {
            var result = new InteriorResult();
            var mesh = skinned.sharedMesh;
            var headMask = BoneMask(skinned, head);
            var jawMask = BoneMask(skinned, jaw);
            if (mesh == null || headMask == null || jawMask == null)
                return result;

            var weights = mesh.boneWeights;
            var local = mesh.vertices;
            var localNormals = mesh.normals;
            if (weights.Length != local.Length)
                return result;

            // Skin "on the jaw": at least half the largest jaw weight found (weights are shared with the head bone on
            // some bodies, so no vertex need reach 0.5).
            var jawThreshold = 0f;
            for (var i = 0; i < weights.Length; i++)
                jawThreshold = Mathf.Max(jawThreshold, Weight(weights[i], jawMask));
            jawThreshold *= 0.5f;
            if (jawThreshold <= 0f)
                return result;

            var toWorld = skinned.transform.localToWorldMatrix;
            var points = new Vector3[local.Length];
            var onHead = new bool[local.Length];
            float rMin = float.MaxValue, rMax = float.MinValue, uMin = float.MaxValue, uMax = float.MinValue;
            for (var i = 0; i < local.Length; i++)
            {
                points[i] = toWorld.MultiplyPoint3x4(local[i]);
                onHead[i] = Weight(weights[i], headMask) >= 0.5f;
                if (Weight(weights[i], jawMask) < jawThreshold)
                    continue;
                var r = Vector3.Dot(points[i], right);
                var u = Vector3.Dot(points[i], up);
                rMin = Mathf.Min(rMin, r); rMax = Mathf.Max(rMax, r);
                uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u);
            }

            if (rMax < rMin)
                return result;

            // The lower face: the jaw-moved skin, a little wider, and up to the roof of the mouth above it. Only that
            // region is used to block rays: a face inside the mouth is blocked by the walls of the mouth itself (and,
            // for the directions behind it, by itself), so nothing farther away is needed to recognise it.
            rMin -= 0.01f; rMax += 0.01f; uMin -= 0.01f; uMax += 0.05f;
            const float margin = 0.03f;

            var subMeshCount = mesh.subMeshCount;
            var subMeshTriangles = new int[subMeshCount][];
            var list = new List<Triangle>();
            for (var s = 0; s < subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                    continue;

                var tris = mesh.GetTriangles(s);
                subMeshTriangles[s] = tris;
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    var a = points[tris[t]];
                    var b = points[tris[t + 1]];
                    var c = points[tris[t + 2]];
                    var center = (a + b + c) / 3f;
                    var r = Vector3.Dot(center, right);
                    var u = Vector3.Dot(center, up);
                    if (r < rMin - margin || r > rMax + margin || u < uMin - margin || u > uMax + margin)
                        continue;

                    list.Add(new Triangle
                    {
                        subMesh = s, index = t, a = a, edge1 = b - a, edge2 = c - a, center = center,
                        radius = Mathf.Sqrt(Mathf.Max((a - center).sqrMagnitude, Mathf.Max((b - center).sqrMagnitude, (c - center).sqrMagnitude))),
                        candidate = r >= rMin && r <= rMax && u >= uMin && u <= uMax
                                    && onHead[tris[t]] && onHead[tris[t + 1]] && onHead[tris[t + 2]]
                    });
                }
            }

            var rays = new List<Vector3>(14);
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    for (var z = -1; z <= 1; z++)
                    {
                        var parts = Mathf.Abs(x) + Mathf.Abs(y) + Mathf.Abs(z);
                        if (parts == 1 || parts == 3)
                            rays.Add((right * x + up * y + forward * z).normalized);
                    }
                }
            }

            var triangles = list.ToArray();
            var grid = new TriangleGrid(triangles);
            var allowedMisses = rays.Count - MinBlockedRays;
            var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var hi = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var walledIn = new List<int>();
            var order = new Vector3[rays.Count];
            var interior = new bool[subMeshCount][];
            var perSubMesh = new int[subMeshCount];
            var found = 0;
            for (var i = 0; i < triangles.Length; i++)
            {
                var tri = triangles[i];
                if (!tri.candidate)
                    continue;

                var normal = Vector3.Cross(tri.edge1, tri.edge2);
                if (normal.sqrMagnitude < 1e-16f)
                    continue;
                normal.Normalize();
                if (localNormals.Length == local.Length)
                {
                    var tris = subMeshTriangles[tri.subMesh];
                    var shading = toWorld.MultiplyVector(localNormals[tris[tri.index]] + localNormals[tris[tri.index + 1]] + localNormals[tris[tri.index + 2]]);
                    if (Vector3.Dot(shading, normal) < 0f)
                        normal = -normal;
                }

                // Outward directions first: skin on the outside is ruled out after a few rays.
                rays.CopyTo(order);
                Array.Sort(order, (p, q) => Vector3.Dot(q, normal).CompareTo(Vector3.Dot(p, normal)));

                var origin = tri.center + normal * SurfaceOffset;
                var misses = 0;
                for (var k = 0; k < order.Length && misses <= allowedMisses; k++)
                {
                    if (!grid.Blocked(origin, order[k]))
                        misses++;
                }

                if (misses <= allowedMisses)
                    walledIn.Add(i);
            }

            // Other walled-in places lie in the same region (ear canals, nostrils): keep the largest connected patch,
            // which is the mouth.
            foreach (var i in LargestPatch(walledIn, triangles, subMeshTriangles, points))
            {
                var tri = triangles[i];
                interior[tri.subMesh] ??= new bool[subMeshTriangles[tri.subMesh].Length / 3];
                interior[tri.subMesh][tri.index / 3] = true;
                perSubMesh[tri.subMesh]++;
                found++;
                var along = new Vector3(Vector3.Dot(tri.center, right), Vector3.Dot(tri.center, up), Vector3.Dot(tri.center, forward));
                lo = Vector3.Min(lo, along);
                hi = Vector3.Max(hi, along);
            }

            if (found == 0)
                return result;

            var copy = UnityEngine.Object.Instantiate(mesh);
            copy.name = mesh.name + " (mouth interior)";
            copy.subMeshCount = subMeshCount + 1;
            var inside = new List<int>(found * 3);
            var kept = new List<int>();
            var source = 0;
            for (var s = 0; s < subMeshCount; s++)
            {
                if (interior[s] == null)
                    continue;
                if (perSubMesh[s] > perSubMesh[source])
                    source = s;

                var tris = subMeshTriangles[s];
                kept.Clear();
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    var target = interior[s][t / 3] ? inside : kept;
                    target.Add(tris[t]); target.Add(tris[t + 1]); target.Add(tris[t + 2]);
                }

                copy.SetTriangles(kept, s, false);
            }

            copy.SetTriangles(inside, subMeshCount, false);
            copy.bounds = mesh.bounds;

            var materials = new List<Material>(skinned.sharedMaterials);
            while (materials.Count < subMeshCount)
                materials.Add(null);
            var face = source < materials.Count ? materials[source] : null;
            var dark = DarkCopy(face, brightness);
            materials.Add(dark);

            skinned.sharedMesh = copy;
            skinned.sharedMaterials = materials.ToArray();
            result.mesh = copy;
            result.material = dark;
            result.triangles = found;
            result.size = hi - lo;
            return result;
        }

        /// <summary>
        /// The largest group of the given triangles that hang together (sharing a corner position; corners are
        /// compared by position because the importer splits vertices at texture seams).
        /// </summary>
        static List<int> LargestPatch(List<int> chosen, Triangle[] triangles, int[][] subMeshTriangles, Vector3[] points)
        {
            var parent = new int[chosen.Count];
            for (var i = 0; i < parent.Length; i++)
                parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            var firstAtCorner = new Dictionary<Vector3Int, int>();
            for (var i = 0; i < chosen.Count; i++)
            {
                var tri = triangles[chosen[i]];
                var indices = subMeshTriangles[tri.subMesh];
                for (var corner = 0; corner < 3; corner++)
                {
                    var p = points[indices[tri.index + corner]];
                    // 0.01 mm cells: the same corner always lands in the same cell, neighbours never do.
                    var key = new Vector3Int(Mathf.RoundToInt(p.x * 1e5f), Mathf.RoundToInt(p.y * 1e5f), Mathf.RoundToInt(p.z * 1e5f));
                    if (firstAtCorner.TryGetValue(key, out var other))
                        parent[Find(i)] = Find(other);
                    else
                        firstAtCorner[key] = i;
                }
            }

            var sizes = new Dictionary<int, int>();
            var best = -1;
            for (var i = 0; i < chosen.Count; i++)
            {
                var root = Find(i);
                sizes.TryGetValue(root, out var n);
                sizes[root] = n + 1;
                if (best < 0 || sizes[root] > sizes[best])
                    best = root;
            }

            var patch = new List<int>();
            for (var i = 0; i < chosen.Count; i++)
            {
                if (Find(i) == best)
                    patch.Add(chosen[i]);
            }

            return patch;
        }

        /// <summary>The face material, darker and without shine: reads as the shadow inside a mouth under any light.</summary>
        static Material DarkCopy(Material face, float brightness)
        {
            var dark = face != null ? new Material(face) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            dark.name = (face != null ? face.name : "face") + " (mouth interior)";
            var color = dark.HasProperty("_BaseColor") ? dark.GetColor("_BaseColor") : dark.HasProperty("_Color") ? dark.GetColor("_Color") : Color.gray;
            color = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
            if (dark.HasProperty("_BaseColor"))
                dark.SetColor("_BaseColor", color);
            if (dark.HasProperty("_Color"))
                dark.SetColor("_Color", color);
            if (dark.HasProperty("_Smoothness"))
                dark.SetFloat("_Smoothness", 0f);
            if (dark.HasProperty("_SpecularHighlights"))
            {
                dark.SetFloat("_SpecularHighlights", 0f);
                dark.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }

            if (dark.HasProperty("_EnvironmentReflections"))
            {
                dark.SetFloat("_EnvironmentReflections", 0f);
                dark.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }

            return dark;
        }

        /// <summary>
        /// Triangles in a uniform grid, for "does this ray hit anything". Each triangle is listed in every cell its
        /// bounding box, grown by half a cell, touches; a ray is walked in steps of a quarter cell and tests the
        /// triangles of each cell it steps into, so a triangle the ray crosses is always found.
        /// </summary>
        sealed class TriangleGrid
        {
            const float Cell = 0.015f;

            readonly Triangle[] triangles;
            readonly List<int>[] cells;
            readonly Vector3 min;
            readonly int nx, ny, nz;

            public TriangleGrid(Triangle[] source)
            {
                triangles = source;
                var lo = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var hi = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var tri in triangles)
                {
                    Bounds(tri, out var tLo, out var tHi);
                    lo = Vector3.Min(lo, tLo);
                    hi = Vector3.Max(hi, tHi);
                }

                if (triangles.Length == 0)
                    lo = hi = Vector3.zero;
                min = lo - Vector3.one * Cell;
                var size = hi - lo + Vector3.one * (2f * Cell);
                nx = Mathf.Max(1, Mathf.CeilToInt(size.x / Cell));
                ny = Mathf.Max(1, Mathf.CeilToInt(size.y / Cell));
                nz = Mathf.Max(1, Mathf.CeilToInt(size.z / Cell));
                cells = new List<int>[nx * ny * nz];
                var half = Vector3.one * (Cell * 0.5f);
                for (var i = 0; i < triangles.Length; i++)
                {
                    Bounds(triangles[i], out var tLo, out var tHi);
                    Index(tLo - half, out var x0, out var y0, out var z0);
                    Index(tHi + half, out var x1, out var y1, out var z1);
                    for (var x = x0; x <= x1; x++)
                    {
                        for (var y = y0; y <= y1; y++)
                        {
                            for (var z = z0; z <= z1; z++)
                            {
                                var c = (z * ny + y) * nx + x;
                                (cells[c] ??= new List<int>()).Add(i);
                            }
                        }
                    }
                }
            }

            static void Bounds(in Triangle tri, out Vector3 lo, out Vector3 hi)
            {
                var b = tri.a + tri.edge1;
                var c = tri.a + tri.edge2;
                lo = Vector3.Min(tri.a, Vector3.Min(b, c));
                hi = Vector3.Max(tri.a, Vector3.Max(b, c));
            }

            void Index(Vector3 p, out int x, out int y, out int z)
            {
                x = Mathf.Clamp(Mathf.FloorToInt((p.x - min.x) / Cell), 0, nx - 1);
                y = Mathf.Clamp(Mathf.FloorToInt((p.y - min.y) / Cell), 0, ny - 1);
                z = Mathf.Clamp(Mathf.FloorToInt((p.z - min.z) / Cell), 0, nz - 1);
            }

            /// <summary>Does the ray hit any triangle (either side) within <see cref="RayLength"/>?</summary>
            public bool Blocked(Vector3 origin, Vector3 direction)
            {
                const float step = Cell * 0.25f;
                var last = -1;
                for (var t = 0f; t <= RayLength + step; t += step)
                {
                    var p = origin + direction * t;
                    var fx = (p.x - min.x) / Cell;
                    var fy = (p.y - min.y) / Cell;
                    var fz = (p.z - min.z) / Cell;
                    if (fx < 0f || fy < 0f || fz < 0f || fx >= nx || fy >= ny || fz >= nz)
                    {
                        // Outside the grid: nothing here; once the ray has left it after being inside, nothing ahead either.
                        if (last >= 0)
                            return false;
                        continue;
                    }

                    var c = ((int)fz * ny + (int)fy) * nx + (int)fx;
                    if (c == last)
                        continue;
                    last = c;
                    var items = cells[c];
                    if (items == null)
                        continue;
                    for (var k = 0; k < items.Count; k++)
                    {
                        if (Hits(origin, direction, triangles[items[k]]))
                            return true;
                    }
                }

                return false;
            }

            static bool Hits(Vector3 origin, Vector3 direction, in Triangle tri)
            {
                // Bounding sphere first.
                var toCenter = tri.center - origin;
                var along = Vector3.Dot(toCenter, direction);
                if (along < -tri.radius || along > RayLength + tri.radius)
                    return false;
                if (toCenter.sqrMagnitude - along * along > tri.radius * tri.radius)
                    return false;

                // Moller-Trumbore.
                var p = Vector3.Cross(direction, tri.edge2);
                var det = Vector3.Dot(tri.edge1, p);
                if (det > -1e-12f && det < 1e-12f)
                    return false;
                var inv = 1f / det;
                var s = origin - tri.a;
                var u = Vector3.Dot(s, p) * inv;
                if (u < 0f || u > 1f)
                    return false;
                var q = Vector3.Cross(s, tri.edge1);
                var v = Vector3.Dot(direction, q) * inv;
                if (v < 0f || u + v > 1f)
                    return false;
                var distance = Vector3.Dot(tri.edge2, q) * inv;
                return distance > 1e-5f && distance <= RayLength;
            }
        }
    }
}
