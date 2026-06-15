using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Simple pond fish patrol between random waypoints below the water surface.
    /// </summary>
    public class PondFishSwim : MonoBehaviour
    {
        [SerializeField]
        Vector3 pondCenter;

        [SerializeField]
        Vector2 pondRadius = new(8f, 7f);

        [SerializeField]
        float swimDepth = 1.2f;

        [SerializeField]
        float speed = 0.8f;

        [SerializeField]
        int waypointCount = 4;

        [SerializeField]
        float wiggleAmplitude = 12f;

        [SerializeField]
        float wiggleFrequency = 2.5f;

        [SerializeField]
        float facingYawOffset;

        Vector3[] waypoints;
        int waypointIndex;
        float phaseOffset;

        public void Configure(
            Vector3 center,
            Vector2 radius,
            float depth,
            float swimSpeed,
            int seed,
            float yawOffset = 0f)
        {
            pondCenter = center;
            pondRadius = radius;
            swimDepth = depth;
            speed = swimSpeed;
            phaseOffset = seed * 0.37f;
            facingYawOffset = yawOffset;
            BuildWaypoints(seed);
        }

        void Awake()
        {
            if (waypoints == null || waypoints.Length == 0)
                BuildWaypoints(gameObject.GetInstanceID());
        }

        void BuildWaypoints(int seed)
        {
            var rng = new System.Random(seed);
            waypointCount = Mathf.Max(2, waypointCount);
            waypoints = new Vector3[waypointCount];

            for (var i = 0; i < waypointCount; i++)
            {
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var radiusScale = 0.35f + (float)rng.NextDouble() * 0.55f;
                waypoints[i] = new Vector3(
                    pondCenter.x + Mathf.Cos(angle) * pondRadius.x * radiusScale,
                    pondCenter.y,
                    pondCenter.z + Mathf.Sin(angle) * pondRadius.y * radiusScale);
            }

            waypointIndex = 0;
            transform.position = waypoints[0];
        }

        void Update()
        {
            if (waypoints == null || waypoints.Length == 0)
                return;

            var target = waypoints[waypointIndex];
            var toTarget = target - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.04f)
            {
                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                target = waypoints[waypointIndex];
                toTarget = target - transform.position;
                toTarget.y = 0f;
            }

            var step = speed * Time.deltaTime;
            var next = Vector3.MoveTowards(transform.position, target, step);
            next.y = pondCenter.y + Mathf.Sin((Time.time + phaseOffset) * wiggleFrequency) * 0.05f;
            transform.position = next;

            if (toTarget.sqrMagnitude > 0.0001f)
            {
                var facing = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                var wiggle = Quaternion.Euler(
                    0f,
                    Mathf.Sin((Time.time + phaseOffset) * wiggleFrequency) * wiggleAmplitude,
                    0f);
                transform.rotation = facing
                    * Quaternion.Euler(0f, facingYawOffset, 0f)
                    * wiggle;
            }
        }
    }
}
