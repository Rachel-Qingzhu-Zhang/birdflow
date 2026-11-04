using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Hides head / hair material slots of a skinned avatar for the first-person (headset) camera only,
    /// by swapping those slots to an invisible material right before that camera renders and restoring them
    /// afterwards. Reflection cameras (mirror, pond) still render the full head, so the face shows in the mirror.
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

        struct Slot
        {
            public Renderer renderer;
            public int index;
            public Material original;
        }

        readonly List<Slot> slots = new();
        Material invisible;
        bool hooked;
        bool swapped;
        bool firstPersonRendering;

        /// <summary>True while the head slots are swapped to the invisible material (debug / tests).</summary>
        public bool HeadHidden => swapped;
        public int HiddenSlotCount => slots.Count;

        void OnEnable()
        {
            Build();
            if (slots.Count == 0 || invisible == null)
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
            if (invisible != null)
                Destroy(invisible);
        }

        void Build()
        {
            slots.Clear();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || !Matches(material.name))
                        continue;

                    slots.Add(new Slot { renderer = renderer, index = i, original = material });
                }
            }

            if (invisible == null)
            {
                var shader = Shader.Find(InvisibleShaderName);
                if (shader != null)
                {
                    invisible = new Material(shader) { name = "AvianInvisible (runtime)" };
                }
                else
                {
                    Debug.LogWarning("[AvianVoice] Shader '" + InvisibleShaderName + "' not found; head stays visible in first person.");
                }
            }
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

            swapped = hide;
        }
    }
}
