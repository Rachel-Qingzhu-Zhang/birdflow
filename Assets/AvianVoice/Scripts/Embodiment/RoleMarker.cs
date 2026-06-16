using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Colored sphere above a bird to distinguish player vs friend roles.
    /// </summary>
    public class RoleMarker : MonoBehaviour
    {
        public enum Role
        {
            Player,
            Friend
        }

        [SerializeField]
        Role role = Role.Player;

        [SerializeField]
        float sphereWorldDiameter = 0.196f;

        [SerializeField]
        float heightAboveHead = 0.252f;

        [SerializeField]
        Color playerColor = new(0.35f, 0.55f, 1f);

        [SerializeField]
        Color friendColor = new(1f, 0.45f, 0.1f);

        GameObject sphere;

        public Role BirdRole => role;

        public void Configure(Role birdRole)
        {
            role = birdRole;
            ApplyMarker();
        }

        void LateUpdate()
        {
            if (sphere == null)
                return;

            var head = sphere.transform.parent;
            if (head == null)
                return;

            var lossy = Mathf.Max(head.lossyScale.x, head.lossyScale.y, head.lossyScale.z, 0.001f);
            var targetLocal = sphereWorldDiameter / lossy;
            if (!Mathf.Approximately(sphere.transform.localScale.x, targetLocal))
                sphere.transform.localScale = Vector3.one * targetLocal;
        }

        void ApplyMarker()
        {
            var head = ResolveHeadAnchor(transform) ?? transform;
            CleanupOrphanMarkers(head);

            if (sphere == null)
            {
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(sphere.GetComponent<Collider>());
            }

            sphere.name = role == Role.Player ? "PlayerMarker" : "FriendMarker";
            sphere.transform.SetParent(head, false);
            sphere.transform.localPosition = Vector3.up * heightAboveHead;
            sphere.transform.localRotation = Quaternion.identity;
            sphere.SetActive(true);
            ApplyColor();

            var lossy = Mathf.Max(head.lossyScale.x, head.lossyScale.y, head.lossyScale.z, 0.001f);
            sphere.transform.localScale = Vector3.one * (sphereWorldDiameter / lossy);
        }

        void CleanupOrphanMarkers(Transform head)
        {
            for (var i = head.childCount - 1; i >= 0; i--)
            {
                var child = head.GetChild(i);
                if (child.name is not ("PlayerMarker" or "FriendMarker"))
                    continue;
                if (sphere == null && child.gameObject.name == (role == Role.Player ? "PlayerMarker" : "FriendMarker"))
                    sphere = child.gameObject;
                else if (sphere == null || child.gameObject != sphere)
                    Destroy(child.gameObject);
            }
        }

        static Transform ResolveHeadAnchor(Transform birdRoot)
        {
            if (birdRoot == null)
                return null;

            if (ParrotEmbodimentWiring.TryResolve(birdRoot, out var refs))
            {
                if (refs.neck != null)
                {
                    var head = refs.neck.Find("parrot_Head_bone");
                    if (head != null)
                        return head;
                    return refs.neck;
                }
            }

            return birdRoot;
        }

        void OnDestroy()
        {
            if (sphere != null)
                Destroy(sphere);
        }

        void ApplyColor()
        {
            if (sphere == null)
                return;

            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            var mat = new Material(shader);
            mat.color = role == Role.Player ? playerColor : friendColor;
            renderer.material = mat;
            sphere.layer = 0;
        }
    }
}
