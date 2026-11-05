using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Hides the avatar's own head for the first-person (headset) camera only, so the participant never sees
    /// the inside of the head, the beak or the hair in front of the eyes, while reflection cameras (mirror,
    /// pond) still render the full head and the face shows in the mirror.
    ///
    /// Two mechanisms, both per camera:
    /// 1. Geometry: every skinned mesh is split so the triangles weighted to the head bone (and its children:
    ///    beak, eyes, jaw) get their own sub-mesh with the same material; that sub-mesh is swapped to an
    ///    invisible material while the first-person camera renders. Works for any avatar with a head bone
    ///    (Humanoid Head, or a bone named like one of Head Bone Names), whatever its materials are.
    /// 2. Materials: slots whose material name contains one of Material Keywords (head, hair, eyelash ...) are
    ///    hidden too (textured avatars whose head is its own material). Non-skinned renderers parented under the
    ///    head bone (separate eyeballs, teeth) are disabled for that camera.
    ///
    /// The Fragilem17 mirror renders its reflection inside the main camera's beginCameraRendering callback
    /// (RenderSingleCamera, no camera events of its own), so the head must still be visible when that callback
    /// runs. Delegates run in subscription order and the mirror re-subscribes whenever it is re-enabled (scene
    /// changes), so this component re-subscribes every frame to stay last in the chain: the reflection is drawn
    /// with the head, then the head is hidden, then the headset view renders. Nested cameras that do raise
    /// events (SubmitRenderRequest mirrors, reflection probes) get the head back while they render.
    /// </summary>
    public class FirstPersonHeadHider : MonoBehaviour
    {
        const string InvisibleShaderName = "AvianVoice/Invisible";

        [Tooltip("Material slots whose material name contains one of these words are hidden for the first-person camera.")]
        [SerializeField]
        string[] materialKeywords = { "head", "opacity", "hair", "eyelash" };

        [Header("Geometry split")]
        [Tooltip("Split skinned meshes so the head-weighted triangles can be hidden on their own (needs readable meshes).")]
        [SerializeField]
        bool splitHeadGeometry = true;

        [Tooltip("Head bone. Empty = the Humanoid Head bone of the Animator, else the first child named like one of Head Bone Names.")]
        [SerializeField]
        Transform headBone;

        [SerializeField]
        string[] headBoneNames = { "parrot_Head_bone", "Head", "head", "mixamorig:Head", "Bip01 Head" };

        [Tooltip("A vertex belongs to the head when at least this much of its skin weight is on the head bone or its children.")]
        [SerializeField, Range(0.05f, 1f)]
        float headWeightThreshold = 0.5f;

        struct Slot
        {
            public Renderer renderer;
            public int index;
            public Material original;
        }

        struct SplitMesh
        {
            public SkinnedMeshRenderer renderer;
            public Mesh original;
            public Mesh split;
        }

        readonly List<Slot> slots = new();
        readonly List<Renderer> headRenderers = new();
        readonly List<SplitMesh> splitMeshes = new();
        Material invisible;
        bool built;
        bool hooked;
        bool swapped;
        bool firstPersonRendering;

        /// <summary>True while the head is swapped out / disabled (debug / tests).</summary>
        public bool HeadHidden => swapped;
        public int HiddenSlotCount => slots.Count;
        public int HiddenRendererCount => headRenderers.Count;
        public int SplitMeshCount => splitMeshes.Count;
        public Transform HeadBone => headBone;

        /// <summary>Set the head bone before the first enable (optional; it is found automatically otherwise).</summary>
        public void Configure(Transform head)
        {
            headBone = head;
            if (built)
            {
                Swap(false);
                built = false;
                Build();
            }
        }

        void OnEnable()
        {
            if (!built)
                Build();
            if (slots.Count == 0 && headRenderers.Count == 0)
                return;

            Hook();
        }

        void OnDisable()
        {
            Unhook();
            firstPersonRendering = false;
            Swap(false);
        }

        void LateUpdate()
        {
            // Move to the end of the render-event chain, after any mirror that (re)subscribed this frame.
            if (hooked)
            {
                Unhook();
                Hook();
            }
        }

        void Hook()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            Camera.onPreRender += OnLegacyCameraPreRender;
            Camera.onPostRender += OnLegacyCameraPostRender;
            hooked = true;
        }

        void Unhook()
        {
            if (!hooked)
                return;
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            Camera.onPreRender -= OnLegacyCameraPreRender;
            Camera.onPostRender -= OnLegacyCameraPostRender;
            hooked = false;
        }

        void OnDestroy()
        {
            Swap(false);
            foreach (var split in splitMeshes)
            {
                if (split.renderer != null && split.renderer.sharedMesh == split.split)
                    split.renderer.sharedMesh = split.original;
                if (split.split != null)
                    Destroy(split.split);
            }
            splitMeshes.Clear();
            if (invisible != null)
                Destroy(invisible);
        }

        // ---------------------------------------------------------------- build

        void Build()
        {
            built = true;
            slots.Clear();
            headRenderers.Clear();

            if (invisible == null)
            {
                var shader = Shader.Find(InvisibleShaderName);
                if (shader != null)
                    invisible = new Material(shader) { name = "AvianInvisible (runtime)" };
                else
                    Debug.LogWarning("[AvianVoice] Shader '" + InvisibleShaderName + "' not found; head stays visible in first person.");
            }

            if (headBone == null)
                headBone = FindHeadBone();

            if (splitHeadGeometry && headBone != null)
            {
                foreach (var skinned in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    SplitHead(skinned);
            }

            // Material-name slots (after the split so the duplicated head slots are seen too; a slot already
            // registered by the split is not added twice).
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || !Matches(material.name) || HasSlot(renderer, i))
                        continue;

                    slots.Add(new Slot { renderer = renderer, index = i, original = material });
                }
            }

            // Whole renderers under the head bone (separate eyes, teeth ...), skinned ones excluded: those are split.
            if (headBone != null)
            {
                foreach (var renderer in headBone.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null || renderer is SkinnedMeshRenderer)
                        continue;
                    if (ParrotEmbodimentWiring.IsRigControlObject(renderer.gameObject))
                        continue;
                    headRenderers.Add(renderer);
                }
            }

            if (invisible == null)
                slots.Clear();

            Debug.Log("[AvianVoice] First-person head hider on '" + name + "': head bone " + (headBone != null ? headBone.name : "none")
                      + ", split meshes " + splitMeshes.Count + ", hidden slots " + slots.Count + ", hidden renderers " + headRenderers.Count);
        }

        bool HasSlot(Renderer renderer, int index)
        {
            foreach (var slot in slots)
            {
                if (slot.renderer == renderer && slot.index == index)
                    return true;
            }

            return false;
        }

        Transform FindHeadBone()
        {
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                    return head;
            }

            if (headBoneNames == null)
                return null;

            var all = GetComponentsInChildren<Transform>(true);
            foreach (var wanted in headBoneNames)
            {
                if (string.IsNullOrEmpty(wanted))
                    continue;
                foreach (var t in all)
                {
                    if (t.name == wanted)
                        return t;
                }
            }

            foreach (var wanted in headBoneNames)
            {
                if (string.IsNullOrEmpty(wanted))
                    continue;
                foreach (var t in all)
                {
                    if (string.Equals(t.name, wanted, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }

            return null;
        }

        /// <summary>
        /// Move the head-weighted triangles of every sub-mesh into an extra sub-mesh (same material) so they can
        /// be hidden on their own. The renderer gets a runtime copy of the mesh; the asset is untouched.
        /// </summary>
        void SplitHead(SkinnedMeshRenderer skinned)
        {
            var mesh = skinned != null ? skinned.sharedMesh : null;
            if (mesh == null || skinned.bones == null || skinned.bones.Length == 0)
                return;
            if (ParrotEmbodimentWiring.IsRigControlObject(skinned.gameObject))
                return;
            if (!mesh.isReadable)
            {
                Debug.LogWarning("[AvianVoice] Head hider: mesh '" + mesh.name + "' is not readable (enable Read/Write on the model); " +
                                 "its head cannot be split off and may show in first person.");
                return;
            }

            var bones = skinned.bones;
            var headBoneMask = new bool[bones.Length];
            var any = false;
            for (var i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                headBoneMask[i] = bone != null && (bone == headBone || bone.IsChildOf(headBone));
                any |= headBoneMask[i];
            }
            if (!any)
                return;

            var weights = mesh.boneWeights;
            if (weights == null || weights.Length != mesh.vertexCount)
                return;

            var headVertex = new bool[weights.Length];
            for (var i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                var headWeight = 0f;
                if (w.boneIndex0 < headBoneMask.Length && headBoneMask[w.boneIndex0]) headWeight += w.weight0;
                if (w.boneIndex1 < headBoneMask.Length && headBoneMask[w.boneIndex1]) headWeight += w.weight1;
                if (w.boneIndex2 < headBoneMask.Length && headBoneMask[w.boneIndex2]) headWeight += w.weight2;
                if (w.boneIndex3 < headBoneMask.Length && headBoneMask[w.boneIndex3]) headWeight += w.weight3;
                headVertex[i] = headWeight >= headWeightThreshold;
            }

            var subMeshCount = mesh.subMeshCount;
            var bodyTriangles = new List<int[]>(subMeshCount);
            var headTriangles = new List<(int subMesh, List<int> tris)>();
            var body = new List<int>();
            var head = new List<int>();
            for (var s = 0; s < subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles)
                {
                    bodyTriangles.Add(null);
                    continue;
                }

                var tris = mesh.GetTriangles(s);
                body.Clear();
                head.Clear();
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    var a = tris[t];
                    var b = tris[t + 1];
                    var c = tris[t + 2];
                    var count = (headVertex[a] ? 1 : 0) + (headVertex[b] ? 1 : 0) + (headVertex[c] ? 1 : 0);
                    if (count >= 2)
                    {
                        head.Add(a); head.Add(b); head.Add(c);
                    }
                    else
                    {
                        body.Add(a); body.Add(b); body.Add(c);
                    }
                }

                if (head.Count == 0)
                {
                    bodyTriangles.Add(null);
                    continue;
                }

                bodyTriangles.Add(body.ToArray());
                headTriangles.Add((s, new List<int>(head)));
            }

            if (headTriangles.Count == 0)
                return;

            var copy = Instantiate(mesh);
            copy.name = mesh.name + " (head split)";
            copy.subMeshCount = subMeshCount + headTriangles.Count;
            for (var s = 0; s < subMeshCount; s++)
            {
                if (bodyTriangles[s] != null)
                    copy.SetTriangles(bodyTriangles[s], s, false);
            }

            var materials = new List<Material>(skinned.sharedMaterials);
            while (materials.Count < subMeshCount)
                materials.Add(null);
            for (var k = 0; k < headTriangles.Count; k++)
            {
                var (sourceSubMesh, tris) = headTriangles[k];
                var index = subMeshCount + k;
                copy.SetTriangles(tris, index, false);
                var material = sourceSubMesh < materials.Count ? materials[sourceSubMesh] : null;
                materials.Add(material);
                slots.Add(new Slot { renderer = skinned, index = index, original = material });
            }

            copy.bounds = mesh.bounds;
            skinned.sharedMesh = copy;
            skinned.sharedMaterials = materials.ToArray();
            splitMeshes.Add(new SplitMesh { renderer = skinned, original = mesh, split = copy });
        }

        bool Matches(string materialName)
        {
            if (string.IsNullOrEmpty(materialName) || materialKeywords == null)
                return false;

            foreach (var keyword in materialKeywords)
            {
                if (!string.IsNullOrEmpty(keyword)
                    && materialName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        // ---------------------------------------------------------------- per camera

        static bool IsFirstPersonCamera(Camera cam)
        {
            if (cam == null || cam.targetTexture != null || cam.cameraType != CameraType.Game)
                return false;
            if (cam.name.IndexOf("Reflection", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return cam.stereoEnabled || cam.CompareTag("MainCamera") || cam == Camera.main;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam) => BeginCamera(cam);

        void OnEndCamera(ScriptableRenderContext context, Camera cam) => EndCamera(cam);

        // Built-in pipeline only (named to avoid the MonoBehaviour OnPreRender/OnPostRender messages).
        void OnLegacyCameraPreRender(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
                BeginCamera(cam);
        }

        void OnLegacyCameraPostRender(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
                EndCamera(cam);
        }

        void BeginCamera(Camera cam)
        {
            if (IsFirstPersonCamera(cam))
            {
                firstPersonRendering = true;
                Swap(true);
            }
            else if (firstPersonRendering)
            {
                // A camera rendering inside the first-person render (mirror via SubmitRenderRequest, probes): show the head.
                Swap(false);
            }
        }

        void EndCamera(Camera cam)
        {
            if (IsFirstPersonCamera(cam))
            {
                firstPersonRendering = false;
                Swap(false);
            }
            else if (firstPersonRendering)
            {
                Swap(true);
            }
        }

        void Swap(bool hide)
        {
            if (swapped == hide)
                return;

            foreach (var slot in slots)
            {
                if (slot.renderer == null)
                    continue;

                var materials = slot.renderer.sharedMaterials;
                if (slot.index >= materials.Length)
                    continue;

                materials[slot.index] = hide ? invisible : slot.original;
                slot.renderer.sharedMaterials = materials;
            }

            foreach (var renderer in headRenderers)
            {
                if (renderer != null)
                    renderer.enabled = !hide;
            }

            swapped = hide;
        }
    }
}
