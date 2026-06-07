using AvianVoice.Core;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Renders a mirror view of the bird body for body-ownership illusion.
    /// </summary>
    public class MirrorBodySetup : MonoBehaviour
    {
        [SerializeField]
        Transform mirrorAnchor;

        [SerializeField]
        Transform birdBodyRoot;

        [SerializeField]
        int textureSize = 512;

        [SerializeField]
        LayerMask birdLayerMask;

        [SerializeField]
        Material mirrorMaterial;

        Camera mirrorCamera;
        RenderTexture renderTexture;
        GameObject mirrorQuad;
        bool built;

        public void SetBirdBodyRoot(Transform root)
        {
            birdBodyRoot = root;
        }

        public void Initialize(AvianVoiceSettings voiceSettings)
        {
            if (voiceSettings != null && !voiceSettings.mirrorEnabled)
            {
                enabled = false;
                return;
            }

            BuildMirror();
        }

        void BuildMirror()
        {
            if (built)
                return;

            if (mirrorAnchor == null)
                mirrorAnchor = transform;

            renderTexture = new RenderTexture(textureSize, textureSize, 16);
            renderTexture.Create();

            var camGo = new GameObject("MirrorCamera");
            camGo.transform.SetParent(mirrorAnchor, false);
            camGo.transform.localPosition = new Vector3(0f, 0.1f, -0.3f);
            camGo.transform.localRotation = Quaternion.identity;

            mirrorCamera = camGo.AddComponent<Camera>();
            mirrorCamera.targetTexture = renderTexture;
            mirrorCamera.clearFlags = CameraClearFlags.SolidColor;
            mirrorCamera.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 1f);
            mirrorCamera.cullingMask = birdLayerMask;
            mirrorCamera.nearClipPlane = 0.05f;
            mirrorCamera.farClipPlane = 5f;

            mirrorQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirrorQuad.name = "MirrorQuad";
            mirrorQuad.transform.SetParent(mirrorAnchor, false);
            mirrorQuad.transform.localPosition = new Vector3(0f, 1.2f, 1.5f);
            mirrorQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            mirrorQuad.transform.localScale = new Vector3(0.8f, 1.2f, 1f);
            Destroy(mirrorQuad.GetComponent<Collider>());

            var renderer = mirrorQuad.GetComponent<MeshRenderer>();
            if (mirrorMaterial != null)
            {
                renderer.material = new Material(mirrorMaterial);
            }
            else
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            renderer.material.mainTexture = renderTexture;

            if (birdBodyRoot != null && birdBodyRoot.GetComponentInParent<WorldPlacedBird>() == null)
            {
                SetLayerRecursive(birdBodyRoot.gameObject, LayerMaskToLayer(birdLayerMask));
            }

            built = true;
        }

        void LateUpdate()
        {
            if (mirrorCamera == null || birdBodyRoot == null)
                return;

            mirrorCamera.transform.position = birdBodyRoot.position + Vector3.up * 0.2f - mirrorAnchor.forward * 1.2f;
            mirrorCamera.transform.LookAt(birdBodyRoot.position + Vector3.up * 0.5f);
        }

        void OnDestroy()
        {
            if (renderTexture != null)
                renderTexture.Release();
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            if (layer < 0)
                return;
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        static int LayerMaskToLayer(LayerMask mask)
        {
            var value = mask.value;
            if (value == 0)
                return -1;
            for (var i = 0; i < 32; i++)
            {
                if ((value & (1 << i)) != 0)
                    return i;
            }
            return -1;
        }
    }
}
