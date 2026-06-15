using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Spawns koi fish models that patrol below the pond surface.
    /// </summary>
    [ExecuteAlways]
    public class PondFishSpawner : MonoBehaviour
    {
        public const string DefaultFishModelPath =
            "Assets/AvianVoice/models/koi-fish/source/koi_fish.fbx";

        [SerializeField]
        GameObject fishPrefab;

        [SerializeField]
        int fishCount = 8;

        [SerializeField]
        Vector2 pondRadius = new(8f, 7f);

        [SerializeField]
        float swimDepth = 1.2f;

        [SerializeField]
        float minSpeed = 0.55f;

        [SerializeField]
        float maxSpeed = 1.1f;

        [SerializeField]
        Vector3 fishScale = Vector3.one * 0.28f;

        [SerializeField]
        Vector3 modelEulerOffset = new(0f, -90f, 0f);

        [SerializeField]
        float swimFacingYawOffset = 180f;

        [SerializeField]
        float scaleVariation = 0.12f;

        [SerializeField]
        Transform pondCenterOverride;

        bool spawned;

        void Reset()
        {
            TryAssignDefaultFishPrefab();
        }

        void Start()
        {
            if (Application.isPlaying)
                SpawnFish();
        }

        public void SpawnFish()
        {
            if (fishPrefab == null)
            {
                Debug.LogWarning(
                    "[AvianVoice] PondFishSpawner: assign fishPrefab (koi_fish.fbx) on " + name + ".");
                return;
            }

            if (spawned && transform.childCount >= fishCount)
                return;

            ClearFish();
            var center = ResolvePondCenter();

            for (var i = 0; i < fishCount; i++)
            {
                var fishRoot = new GameObject($"PondFish_{i + 1}");
                fishRoot.transform.SetParent(transform, false);

                var model = Instantiate(fishPrefab, fishRoot.transform);
                model.name = "KoiModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(modelEulerOffset);

                var scaleJitter = 1f + Random.Range(-scaleVariation, scaleVariation);
                model.transform.localScale = fishScale * scaleJitter;

                StripColliders(model);
                DisableRigidbodies(model);

                var swim = fishRoot.AddComponent<PondFishSwim>();
                var speed = Mathf.Lerp(minSpeed, maxSpeed, (i + 1f) / (fishCount + 1f));
                swim.Configure(center, pondRadius, center.y, speed, i * 17 + 3, swimFacingYawOffset);
            }

            spawned = true;
        }

        public void ClearFish()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }

            spawned = false;
        }

        void TryAssignDefaultFishPrefab()
        {
#if UNITY_EDITOR
            if (fishPrefab != null)
                return;

            fishPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFishModelPath);
#endif
        }

        static void StripColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }
        }

        static void DisableRigidbodies(GameObject root)
        {
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body == null)
                    continue;
                body.isKinematic = true;
                body.useGravity = false;
            }
        }

        Vector3 ResolvePondCenter()
        {
            if (pondCenterOverride != null)
                return pondCenterOverride.position;

            var pondRoot = transform.parent;
            if (pondRoot != null)
                return pondRoot.TransformPoint(new Vector3(0f, swimDepth, 0f));

            return transform.position;
        }

        void OnDrawGizmosSelected()
        {
            var center = ResolvePondCenter();
            Gizmos.color = new Color(0.2f, 0.6f, 0.9f, 0.35f);
            Gizmos.DrawWireSphere(center, 0.25f);
            Gizmos.DrawLine(
                new Vector3(center.x - pondRadius.x, center.y, center.z),
                new Vector3(center.x + pondRadius.x, center.y, center.z));
            Gizmos.DrawLine(
                new Vector3(center.x, center.y, center.z - pondRadius.y),
                new Vector3(center.x, center.y, center.z + pondRadius.y));
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            TryAssignDefaultFishPrefab();
        }
#endif
    }
}
