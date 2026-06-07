using AvianVoice.Audio;
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
        VoiceAnalyzer analyzer;

        [SerializeField]
        BreathSimulator breath;

        [SerializeField]
        float pitchHeightScale = 8f;

        [SerializeField]
        float forwardSpeed = 3f;

        [SerializeField]
        float glideStabilityThreshold = 0.75f;

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
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
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
            Debug.Log("[AvianVoice] Flight Control: hum pitch to rise/fall, hold Space (~half breath) to fly steady. Parrot flies beside you.");
        }

        void OnDisable()
        {
            parrotFlight?.EndFlightPresentation();
            rigSnapshot?.RestorePose();
            glideActive = false;
        }

        bool IsActiveTask()
        {
            var tm = FindAnyObjectByType<TaskManager>();
            return TaskActivity.IsActive("flight");
        }

        void ResolveParrotFlightPresentation()
        {
            var parrot = WorldPlacedBird.FindParrotInScene();
            if (parrot == null)
                return;

            parrotFlight = parrot.GetComponent<ParrotFlightPresentation>();
            if (parrotFlight == null)
                parrotFlight = parrot.gameObject.AddComponent<ParrotFlightPresentation>();
        }

        bool ShouldSnapRigToGround()
        {
            var parrot = WorldPlacedBird.FindParrotInScene();
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

            if (analyzer == null || breath == null)
                return;

            var v = analyzer.Current;
            var b = breath.Current;

            var pitchNorm = v.pitchHz > 0f ? Mathf.InverseLerp(80f, 800f, v.pitchHz) : 0.5f;
            var targetY = pitchNorm * pitchHeightScale;
            var stability = 1f - Mathf.Abs(b.depth - 0.5f);
            var move = Vector3.forward * forwardSpeed + Vector3.up * (targetY - transform.position.y) * stability;

            controller.Move(move * Time.deltaTime);
            score = Mathf.Lerp(score, stability * v.loudness, Time.deltaTime);
            taskManager?.ReportScore(score);

            UpdateParrotGlide(stability, b.depth);
        }

        void UpdateParrotGlide(float stability, float breathDepth)
        {
            if (parrotFlight == null)
                return;

            var wantGlide = stability >= glideStabilityThreshold && breathDepth > 0.25f;
            if (wantGlide == glideActive)
                return;

            glideActive = wantGlide;
            parrotFlight.SetGlide(true);
        }
    }
}
