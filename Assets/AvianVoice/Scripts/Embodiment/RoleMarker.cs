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
        float sphereWorldDiameter = 0.28f;

        [SerializeField]
        float heightAboveHead = 0.32f;

        [SerializeField]
        Color playerColor = new(0.35f, 0.75f, 1f);

        [SerializeField]
        Color friendColor = new(1f, 0.55f, 0.12f);

        GameObject sphere;
        bool markerVisible = true;

        public Role BirdRole => role;

        public void Configure(Role birdRole)
        {
            role = birdRole;
            ApplyMarker();
        }

        public void SetMarkerVisible(bool visible)
        {
            markerVisible = visible;
            if (sphere != null)
                sphere.SetActive(visible);
        }

        public static void SetMarkersVisibleOnBird(Transform birdRoot, bool visible)
        {
            if (birdRoot == null)
                return;

            foreach (var marker in birdRoot.GetComponentsInChildren<RoleMarker>(true))
                marker.SetMarkerVisible(visible);
        }

        public static void SetAllMarkersVisible(bool visible)
        {
            foreach (var marker in Object.FindObjectsByType<RoleMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (marker != null)
                    marker.SetMarkerVisible(visible);
            }
        }

        /// <summary>
        /// Perch tasks (1/2/6) show markers; flight/pond tasks hide them.
        /// </summary>
        public static void SetMarkersForTask(string taskId)
        {
            var show = taskId is "meet" or "breath_sync" or "reflection" or "lab";
            EnsureMarkersExist();
            SetAllMarkersVisible(show);
        }

        public static void EnsureMarkersExist()
        {
            AttachIfNeeded(BirdSceneRoles.Friend, Role.Friend);
            AttachIfNeeded(BirdSceneRoles.Player, Role.Player);
        }

        static void AttachIfNeeded(Transform bird, Role birdRole)
        {
            if (bird == null)
                return;

            var marker = bird.GetComponent<RoleMarker>() ?? bird.gameObject.AddComponent<RoleMarker>();
            marker.Configure(birdRole);
            marker.SetMarkerVisible(true);
        }

        void LateUpdate()
        {
            if (sphere == null || !markerVisible)
                return;

            var head = sphere.transform.parent;
            if (head == null)
                return;

            var lossy = Mathf.Max(head.lossyScale.x, head.lossyScale.y, head.lossyScale.z);
            if (lossy < 0.05f)
            {
                sphere.SetActive(false);
                return;
            }

            sphere.SetActive(true);
            var targetLocal = Mathf.Clamp(sphereWorldDiameter / lossy, 0.01f, 2.5f);
            if (!Mathf.Approximately(sphere.transform.localScale.x, targetLocal))
                sphere.transform.localScale = Vector3.one * targetLocal;

            // Keep Default layer so the main XR camera always sees the markers.
            if (sphere.layer != 0)
                sphere.layer = 0;
        }

        void ApplyMarker()
        {
            StripMisplacedMarkers();
            sphere = null;

            var head = ResolveHeadAnchor(transform) ?? transform;

            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());

            sphere.name = role == Role.Player ? "PlayerMarker" : "FriendMarker";
            sphere.transform.SetParent(head, false);
            sphere.transform.localPosition = Vector3.up * heightAboveHead;
            sphere.transform.localRotation = Quaternion.identity;
            sphere.SetActive(true);
            ApplyColor();

            var lossy = Mathf.Max(head.lossyScale.x, head.lossyScale.y, head.lossyScale.z, 0.05f);
            sphere.transform.localScale = Vector3.one * (sphereWorldDiameter / lossy);
        }

        void StripMisplacedMarkers()
        {
            foreach (var child in transform.GetComponentsInChildren<Transform>(true))
            {
                if (child.name is "PlayerMarker" or "FriendMarker" or "FriendBreathCue")
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

            var color = role == Role.Player ? playerColor : friendColor;
            // Unlit + emission so markers stay readable even when scene lighting is dark.
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color")
                         ?? Shader.Find("Sprites/Default");
            if (shader == null)
                return;

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.8f);
            }

            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            sphere.layer = 0;
            sphere.SetActive(markerVisible);
        }
    }
}
