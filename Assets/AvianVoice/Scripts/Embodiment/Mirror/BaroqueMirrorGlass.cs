using Fragilem17.MirrorsAndPortals;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Puts the live Fragilem17 reflection on the baroque mirror's glass mesh and hides the flat stand-in panels.
    /// The gold and wood frame stay as modeled. Only the fake mirror card is replaced.
    /// </summary>
    public static class BaroqueMirrorGlass
    {
        const string FacingChildName = "MirrorFacing";

        public static bool TryBind(
            Transform flatMirrorRoot,
            in Fragilem17MirrorBridge.Settings settings,
            out MirrorRenderer mirrorRenderer,
            out MeshRenderer glassRenderer)
        {
            mirrorRenderer = null;
            glassRenderer = FindGlassRenderer(flatMirrorRoot);
            if (glassRenderer == null)
                return false;

            var facing = EnsureFacing(glassRenderer, flatMirrorRoot);
            glassRenderer.gameObject.layer = BirdSceneRoles.MirrorDisplayLayer;
            MirrorBodySetup.ConfigureMirrorPanelRenderer(glassRenderer);

            var glassSettings = settings;
            glassSettings.MirrorPanelSize = PlanarSize(glassRenderer.bounds.size, facing.forward);

            mirrorRenderer = Fragilem17MirrorBridge.ConfigureMirrorQuad(glassRenderer.gameObject, glassSettings);
            var surface = glassRenderer.GetComponent<MirrorSurface>();
            if (surface != null)
            {
                surface.MyForwardTransform = facing;
                surface.clippingPlaneOffset = 0.02f;
            }

            HideFlatPanels(flatMirrorRoot);
            Debug.Log(
                "[AvianVoice] Baroque mirror glass is reflective at " + glassRenderer.bounds.center +
                ". Flat MirrorQuad and MirrorFrame are hidden.");
            return mirrorRenderer != null;
        }

        public static void HideFlatPanels(Transform flatMirrorRoot)
        {
            if (flatMirrorRoot == null)
                return;

            HidePanel(flatMirrorRoot.Find("MirrorQuad"));
            HidePanel(flatMirrorRoot.Find("MirrorFrame"));
        }

        public static void SetGlassVisible(MeshRenderer glassRenderer, bool visible)
        {
            if (glassRenderer != null)
                glassRenderer.enabled = visible;
        }

        /// <summary>
        /// Shows or hides the whole baroque mirror, frame included.
        /// The reflective glass alone is not the object people see.
        /// </summary>
        public static void SetModelVisible(bool visible)
        {
            var glass = FindGlassRenderer(null);
            if (glass == null)
                return;

            var model = glass.transform;
            while (model.parent != null && model.parent.name != MirrorBodySetup.WorldMirrorObjectName)
                model = model.parent;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.gameObject.name == FacingChildName)
                    continue;
                renderer.enabled = visible;
            }
        }

        static void HidePanel(Transform panel)
        {
            if (panel == null)
                return;

            var meshRenderer = panel.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = false;

            var surface = panel.GetComponent<MirrorSurface>();
            if (surface != null)
                surface.enabled = false;

            var mirrorRenderer = panel.GetComponent<MirrorRenderer>();
            if (mirrorRenderer != null)
                mirrorRenderer.enabled = false;
        }

        static MeshRenderer FindGlassRenderer(Transform flatMirrorRoot)
        {
            MeshRenderer best = null;
            var bestDistance = float.PositiveInfinity;
            var origin = flatMirrorRoot != null ? flatMirrorRoot.position : Vector3.zero;

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!IsFakeMirrorGlass(renderer))
                    continue;
                if (flatMirrorRoot != null && renderer.transform.IsChildOf(flatMirrorRoot))
                    continue;

                var distance = (renderer.bounds.center - origin).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = renderer;
            }

            return best;
        }

        static bool IsFakeMirrorGlass(MeshRenderer renderer)
        {
            if (renderer == null)
                return false;

            if (MaterialNameContains(renderer, "Realistic_Mirror")
                || MaterialNameContains(renderer, "AR3DMat_Procedural"))
                return true;

            // glTF node Object_7 is the glass card. Confirm it belongs to this frame via the gold material.
            if (renderer.gameObject.name != "Object_7")
                return false;

            var root = renderer.transform.root;
            foreach (var other in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (MaterialNameContains(other, "Gold_material"))
                    return true;
            }

            return false;
        }

        static bool MaterialNameContains(MeshRenderer renderer, string token)
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material != null
                    && material.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static Transform EnsureFacing(MeshRenderer glass, Transform flatMirrorRoot)
        {
            var facing = glass.transform.Find(FacingChildName);
            if (facing == null)
            {
                var facingObject = new GameObject(FacingChildName);
                facing = facingObject.transform;
                facing.SetParent(glass.transform, false);
            }

            var normal = ResolveViewerNormal(glass, flatMirrorRoot);
            var up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            var halfThickness = HalfThicknessAlong(glass.bounds.size, normal);
            var front = glass.bounds.center + normal * Mathf.Max(0f, halfThickness - 0.004f);
            facing.SetPositionAndRotation(front, Quaternion.LookRotation(normal, up));
            return facing;
        }

        /// <summary>
        /// Fragilem17 treats forward as the side the viewer stands on.
        /// </summary>
        static Vector3 ResolveViewerNormal(MeshRenderer glass, Transform flatMirrorRoot)
        {
            var normal = ThinWorldAxis(glass);
            var preferred = flatMirrorRoot != null ? flatMirrorRoot.forward : normal;
            if (preferred.sqrMagnitude < 0.0001f)
                preferred = Vector3.forward;
            if (Vector3.Dot(normal, preferred) < 0f)
                normal = -normal;
            return normal.normalized;
        }

        static Vector3 ThinWorldAxis(MeshRenderer glass)
        {
            var size = glass.bounds.size;
            var thin = 0;
            if (size.y < size[thin])
                thin = 1;
            if (size.z < size[thin])
                thin = 2;

            var next = thin == 0 ? 1 : 0;
            var other = 3 - thin - next;
            if (size[other] < size[next])
                next = other;

            if (size[next] > 0.0001f && size[thin] / size[next] < 0.45f)
                return thin == 0 ? Vector3.right : thin == 1 ? Vector3.up : Vector3.forward;

            var filter = glass.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                return Vector3.forward;

            var extents = mesh.bounds.extents;
            var localThin = 0;
            if (extents.y < extents[localThin])
                localThin = 1;
            if (extents.z < extents[localThin])
                localThin = 2;
            var localAxis = localThin == 0 ? Vector3.right : localThin == 1 ? Vector3.up : Vector3.forward;
            var world = glass.transform.TransformDirection(localAxis);
            return world.sqrMagnitude > 0.0001f ? world.normalized : Vector3.forward;
        }

        static float HalfThicknessAlong(Vector3 worldSize, Vector3 normal)
        {
            var axis = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            return 0.5f * Vector3.Dot(worldSize, axis);
        }

        static Vector2 PlanarSize(Vector3 worldSize, Vector3 normal)
        {
            var absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            float width;
            float height;
            if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
            {
                width = worldSize.z;
                height = worldSize.y;
            }
            else if (absNormal.y >= absNormal.z)
            {
                width = worldSize.x;
                height = worldSize.z;
            }
            else
            {
                width = worldSize.x;
                height = worldSize.y;
            }

            return new Vector2(Mathf.Max(width, 0.2f), Mathf.Max(height, 0.2f));
        }
    }
}
