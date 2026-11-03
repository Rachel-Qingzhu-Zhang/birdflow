using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AvianVoice.Embodiment.Avatars
{
    /// <summary>
    /// Hides head / hair material slots of a skinned avatar for the first-person (headset) camera only,
    /// by swapping those slots to an invisible material right before that camera renders and restoring them
    /// afterwards. Reflection cameras (mirror, pond) still render the full head.
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

        void OnEnable()
        {
            Build();
            if (slots.Count == 0 || invisible == null)
                return;

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            Camera.onPreRender += OnLegacyCameraPreRender;
            Camera.onPostRender += OnLegacyCameraPostRender;
            hooked = true;
        }

        void OnDisable()
        {
            if (hooked)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
                RenderPipelineManager.endCameraRendering -= OnEndCamera;
                Camera.onPreRender -= OnLegacyCameraPreRender;
                Camera.onPostRender -= OnLegacyCameraPostRender;
                hooked = false;
            }

            Swap(false);
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

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (IsFirstPersonCamera(cam))
                Swap(true);
        }

        void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (swapped)
                Swap(false);
        }

        // Built-in pipeline only (named to avoid the MonoBehaviour OnPreRender/OnPostRender messages).
        void OnLegacyCameraPreRender(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline == null && IsFirstPersonCamera(cam))
                Swap(true);
        }

        void OnLegacyCameraPostRender(Camera cam)
        {
            if (GraphicsSettings.currentRenderPipeline == null && swapped)
                Swap(false);
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
