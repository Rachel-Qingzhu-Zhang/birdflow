using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using UnityEngine;

namespace AvianVoice.Tasks
{
    [RequireComponent(typeof(CharacterController))]
    public class FlightControlTask : MonoBehaviour
    {
        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        BreathSimulator breath;

        [SerializeField]
        float forwardSpeed = 2.5f;

        [SerializeField]
        float verticalSpeedMid = 1f;

        [SerializeField]
        float verticalSpeedHigh = 3f;

        [SerializeField]
        float glideDescentSpeed = 1.5f;

        CharacterController controller;
        ParrotFlightPresentation parrotFlight;
        XrRigPoseSnapshot rigSnapshot;
        float score;
        bool glideActive;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            rigSnapshot = GetComponent<XrRigPoseSnapshot>() ?? gameObject.AddComponent<XrRigPoseSnapshot>();
        }

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (breath == null)
                breath = FindAnyObjectByType<BreathSimulator>();
            ResolveParrotFlightPresentation();
        }

        void OnEnable()
        {
            if (!IsActiveTask())
                return;

            if (controller == null)
                controller = GetComponent<CharacterController>();

            rigSnapshot?.SavePose();
            ResolveParrotFlightPresentation();
            parrotFlight?.BeginFlightPresentation();

            if (ShouldSnapRigToGround())
                SnapToGroundIfNeeded();
            score = 0f;
            glideActive = false;
            Debug.Log("[AvianVoice] Flying: deep inhale to rise, exhale to glide down.");
        }

        void OnDisable()
        {
            parrotFlight?.EndFlightPresentation();
            rigSnapshot?.RestorePose();
            glideActive = false;
        }

        bool IsActiveTask() => TaskActivity.IsActive("flying") || TaskActivity.IsActive("flight");

        void ResolveParrotFlightPresentation()
        {
            var parrot = BirdSceneRoles.Friend != null ? BirdSceneRoles.Friend : WorldPlacedBird.FindParrotInScene();
            if (parrot == null)
                return;

            parrotFlight = parrot.GetComponent<ParrotFlightPresentation>();
            if (parrotFlight == null)
                parrotFlight = parrot.gameObject.AddComponent<ParrotFlightPresentation>();
        }

        bool ShouldSnapRigToGround()
        {
            var parrot = BirdSceneRoles.Friend ?? WorldPlacedBird.FindParrotInScene();
            if (parrot == null)
                return true;
            return Vector3.Distance(parrot.position, transform.position) < 12f;
        }

        void SnapToGroundIfNeeded()
        {
            var origin = transform.position;
            if (!Physics.Raycast(origin + Vector3.up * 2f, Vector3.down, out var hit, 50f))
                return;

            var groundedY = hit.point.y;
            if (controller != null)
            {
                var p = transform.position;
                p.y = groundedY;
                controller.enabled = false;
                transform.position = p;
                controller.enabled = true;
            }
            else
            {
                var p = transform.position;
                p.y = groundedY;
                transform.position = p;
            }
        }

        void Update()
        {
            if (!enabled || !IsActiveTask())
                return;

            if (breath == null)
                return;

            var b = breath.Current;
            var verticalSpeed = ComputeVerticalSpeed(b.depth, b.isInhaling);
            var move = Vector3.forward * forwardSpeed + Vector3.up * verticalSpeed;
            controller.Move(move * Time.deltaTime);

            var stability = b.isInhaling ? b.depth : 1f - b.depth;
            score = Mathf.Lerp(score, stability, Time.deltaTime);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordBreathing(stability);

            UpdateParrotGlide(stability, b.depth);
        }

        static float ComputeVerticalSpeed(float depth, bool inhaling)
        {
            if (inhaling)
            {
                if (depth < 0.5f)
                    return Mathf.Lerp(0f, 1f, depth / 0.5f);
                return Mathf.Lerp(1f, 3f, (depth - 0.5f) / 0.5f);
            }

            return -Mathf.Lerp(0.3f, 1.5f, 1f - depth);
        }

        void UpdateParrotGlide(float stability, float breathDepth)
        {
            if (parrotFlight == null)
                return;

            var wantGlide = stability >= 0.5f && breathDepth > 0.2f;
            if (wantGlide == glideActive)
                return;

            glideActive = wantGlide;
            parrotFlight.SetGlide(true);
        }
    }
}
