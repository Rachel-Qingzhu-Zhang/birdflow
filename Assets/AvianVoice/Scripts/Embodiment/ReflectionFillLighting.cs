using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Soft fill lights that only appear in reflection camera passes (birds + nearby perch geometry).
    /// Does not brighten the direct VR view.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReflectionFillLighting : MonoBehaviour
    {
        const string KeyLightName = "ReflectionKeyFill";
        const string PointLightName = "ReflectionPointFill";
        const string RimLightName = "ReflectionRimFill";

        [SerializeField]
        float keyIntensity = 2.15f;

        [SerializeField]
        [Range(0.85f, 1.05f)]
        float keyWarmth = 0.98f;

        [SerializeField]
        float pointIntensity = 1.8f;

        [SerializeField]
        float pointRange = 14f;

        [SerializeField]
        float rimIntensity = 1.05f;

        Light keyLight;
        Light pointLight;
        Light rimLight;
        bool active;

        public static ReflectionFillLighting Ensure(Transform parent)
        {
            if (parent == null)
                return null;

            var existing = parent.GetComponent<ReflectionFillLighting>();
            if (existing != null)
                return existing;

            return parent.gameObject.AddComponent<ReflectionFillLighting>();
        }

        public void SetFillActive(bool enabled)
        {
            active = enabled;
            ApplyEnabledState();
        }

        public void UpdateForMirror(Transform mirrorRoot, Vector3 focusPoint)
        {
            EnsureLights(mirrorRoot);
            if (!active)
                return;

            var mirrorForward = mirrorRoot.forward.sqrMagnitude > 0.01f ? mirrorRoot.forward : Vector3.forward;
            var towardFocus = focusPoint - mirrorRoot.position;
            if (towardFocus.sqrMagnitude < 0.01f)
                towardFocus = -mirrorForward;
            towardFocus.Normalize();

            var keyDirection = towardFocus;
            keyDirection.y = Mathf.Max(keyDirection.y, 0.12f);
            keyLight.transform.rotation = Quaternion.LookRotation(keyDirection.normalized, Vector3.up);

            pointLight.transform.position = Vector3.Lerp(mirrorRoot.position + mirrorForward * 0.4f, focusPoint, 0.55f);

            var rimDirection = Vector3.Reflect(-towardFocus, mirrorForward);
            rimDirection.y = Mathf.Max(rimDirection.y, 0.05f);
            rimLight.transform.rotation = Quaternion.LookRotation(rimDirection.normalized, Vector3.up);
        }

        public void UpdateForPond(Transform pondSurface, Vector3 focusPoint)
        {
            EnsureLights(pondSurface);
            if (!active)
                return;

            var planeNormal = pondSurface.up.sqrMagnitude > 0.01f ? pondSurface.up : Vector3.up;
            var towardFocus = focusPoint - pondSurface.position;
            if (towardFocus.sqrMagnitude < 0.01f)
                towardFocus = planeNormal;
            towardFocus.Normalize();

            var keyDirection = (towardFocus + planeNormal * 0.65f).normalized;
            keyLight.transform.rotation = Quaternion.LookRotation(keyDirection, Vector3.up);

            pointLight.transform.position = Vector3.Lerp(
                pondSurface.position + planeNormal * 1.2f,
                focusPoint,
                0.45f);

            var rimDirection = (towardFocus - planeNormal * 0.35f).normalized;
            rimLight.transform.rotation = Quaternion.LookRotation(rimDirection, Vector3.up);
        }

        void EnsureLights(Transform parent)
        {
            keyLight = EnsureLight(parent, KeyLightName, ref keyLight, LightType.Directional);
            pointLight = EnsureLight(parent, PointLightName, ref pointLight, LightType.Point);
            rimLight = EnsureLight(parent, RimLightName, ref rimLight, LightType.Directional);

            var warmKey = new Color(1f, keyWarmth, keyWarmth * 0.92f);
            var neutralPoint = new Color(1f, 0.99f, 0.95f);
            var coolRim = new Color(0.95f, 0.98f, 1f);

            ConfigureLight(keyLight, keyIntensity, warmKey, BirdOnlyLightMask, 0f);
            ConfigureLight(pointLight, pointIntensity, neutralPoint, NearbyReflectionLightMask, pointRange);
            ConfigureLight(rimLight, rimIntensity, coolRim, BirdOnlyLightMask, 0f);

            ApplyEnabledState();
        }

        static Light EnsureLight(Transform parent, string objectName, ref Light cached, LightType type)
        {
            if (cached != null)
                return cached;

            var existing = parent.Find(objectName);
            if (existing != null)
                cached = existing.GetComponent<Light>();

            if (cached != null)
                return cached;

            var lightGo = new GameObject(objectName);
            lightGo.transform.SetParent(parent, false);
            cached = lightGo.AddComponent<Light>();
            cached.type = type;
            return cached;
        }

        static void ConfigureLight(Light light, float intensity, Color color, int cullingMask, float range)
        {
            if (light == null)
                return;

            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.intensity = intensity;
            light.color = color;
            light.cullingMask = cullingMask;
            if (light.type == LightType.Point)
                light.range = range;
        }

        static int BirdOnlyLightMask => 1 << BirdSceneRoles.MirrorLayer;

        static int NearbyReflectionLightMask =>
            ~((1 << BirdSceneRoles.MirrorDisplayLayer) | (1 << BirdSceneRoles.UiLayer));

        void ApplyEnabledState()
        {
            if (keyLight != null)
                keyLight.enabled = active;
            if (pointLight != null)
                pointLight.enabled = active;
            if (rimLight != null)
                rimLight.enabled = active;
        }

        public static Vector3 ResolveDefaultFocusPoint()
        {
            if (BirdSceneRoles.TryGetFlockOrbitCenter(out var center))
                return center + Vector3.up * 0.35f;

            var player = BirdSceneRoles.Player;
            if (player != null)
                return player.position + Vector3.up * 0.35f;

            var viewer = WorldPlacedBird.FindMainCameraTransform();
            return viewer != null ? viewer.position : Vector3.zero;
        }
    }
}
