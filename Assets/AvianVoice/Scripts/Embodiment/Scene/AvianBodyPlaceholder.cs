using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Procedural placeholder bird rig until a rigged model is imported.
    /// </summary>
    public class AvianBodyPlaceholder : MonoBehaviour
    {
        [SerializeField]
        Transform head;

        [SerializeField]
        Transform chest;

        [SerializeField]
        Transform leftWing;

        [SerializeField]
        Transform rightWing;

        [SerializeField]
        Transform beak;

        [SerializeField]
        SkinnedMeshRenderer beakRenderer;

        public Transform Head => head;
        public Transform Chest => chest;
        public Transform LeftWing => leftWing;
        public Transform RightWing => rightWing;
        public Transform Beak => beak;
        public SkinnedMeshRenderer BeakRenderer => beakRenderer;

        public static AvianBodyPlaceholder Create(Transform parent)
        {
            var root = new GameObject("AvianBody_Placeholder");
            root.transform.SetParent(parent, false);

            var body = root.AddComponent<AvianBodyPlaceholder>();
            body.Build();
            return body;
        }

        void Build()
        {
            chest = CreatePart("Chest", transform, new Vector3(0f, 1.1f, 0.15f), new Vector3(0.35f, 0.3f, 0.4f), new Color(0.45f, 0.35f, 0.2f));
            head = CreatePart("Head", chest, new Vector3(0f, 0.35f, 0.2f), new Vector3(0.2f, 0.2f, 0.22f), new Color(0.35f, 0.28f, 0.15f));
            beak = CreatePart("Beak", head, new Vector3(0f, 0f, 0.25f), new Vector3(0.06f, 0.05f, 0.18f), new Color(0.9f, 0.7f, 0.1f));
            leftWing = CreatePart("Wing_L", chest, new Vector3(-0.35f, 0.05f, -0.05f), new Vector3(0.08f, 0.45f, 0.25f), new Color(0.3f, 0.4f, 0.55f));
            rightWing = CreatePart("Wing_R", chest, new Vector3(0.35f, 0.05f, -0.05f), new Vector3(0.08f, 0.45f, 0.25f), new Color(0.3f, 0.4f, 0.55f));

            transform.gameObject.AddComponent<Animator>();
        }

        static Transform CreatePart(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = color;
                renderer.material = mat;
            }

            return go.transform;
        }
    }
}
