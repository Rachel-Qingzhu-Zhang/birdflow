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
        float forwardSpeed = 1.75f;

        [SerializeField]
        float verticalSpeedMid = 0.7f;

        [SerializeField]
        float verticalSpeedHigh = 2.1f;

        [SerializeField]
        float glideDescentMin = 0.21f;

        [SerializeField]
        float glideDescentSpeed = 1.05f;

        CharacterController controller;
        ParrotFlightPresentation parrotFlight;
        XrRigPoseSnapshot rigSnapshot;
        Transform flightHeading;
        float score;
        bool glideActive;
        ParrotBreathEmbodiment playerBreathEmbodiment;

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
            ResolveFlightHeading();
            ResolveParrotFlightPresentation();
            SetFriendCompanionVisible(false);
            SetPlayerBreathEmbodimentEnabled(false);
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
            SetFriendCompanionVisible(true);
            SetPlayerBreathEmbodimentEnabled(true);
            rigSnapshot?.RestorePose();
            glideActive = false;
        }

        bool IsActiveTask() => TaskActivity.IsActive("flying") || TaskActivity.IsActive("flight");

        void ResolveFlightHeading()
        {
            if (WorldPlacedBird.TryFindFlightAnchor(out var anchor, out _) && anchor != null)
                flightHeading = anchor;
            else
                flightHeading = transform;
        }

        void ResolveParrotFlightPresentation()
        {
            var parrot = BirdSceneRoles.Player != null
                ? BirdSceneRoles.Player
                : BirdSceneRoles.Friend != null
                    ? BirdSceneRoles.Friend
                    : WorldPlacedBird.FindParrotInScene();
            if (parrot == null)
                return;

            parrotFlight = parrot.GetComponent<ParrotFlightPresentation>();
            if (parrotFlight == null)
                parrotFlight = parrot.gameObject.AddComponent<ParrotFlightPresentation>();
        }

        void SetFriendCompanionVisible(bool visible)
        {
            if (BirdSceneRoles.Friend == null)
                return;

            if (BirdSceneRoles.Player != null && BirdSceneRoles.Friend == BirdSceneRoles.Player)
                return;

            foreach (var renderer in BirdSceneRoles.Friend.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                renderer.enabled = visible;
            }

        }

        void SetPlayerBreathEmbodimentEnabled(bool enabled)
        {
            if (BirdSceneRoles.Player == null)
                return;

            if (playerBreathEmbodiment == null)
                playerBreathEmbodiment = BirdSceneRoles.Player.GetComponent<ParrotBreathEmbodiment>();

            if (playerBreathEmbodiment != null)
                playerBreathEmbodiment.enabled = enabled;
        }

        bool ShouldSnapRigToGround()
        {
            var taskPoses = GetComponent<XrRigTaskStartPoses>();
            if (taskPoses != null && taskPoses.HasConfiguredPose("flying"))
                return false;

            var parrot = BirdSceneRoles.Friend ?? WorldPlacedBird.FindParrotInScene();
            if (parrot == null)
                return true;
            return Vector3.Distance(parrot.position, transform.position) < 12f;
        }

        void SnapToGroundIfNeeded()
        {
            var origin = transform.position;
            var mask = Physics.DefaultRaycastLayers;
            if (!Physics.Raycast(origin + Vector3.up * 2f, Vector3.down, out var hit, 50f, mask, QueryTriggerInteraction.Ignore))
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
            var heading = flightHeading != null ? flightHeading : transform;
            var forward = heading.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = heading.forward;
            forward.Normalize();

            var move = forward * forwardSpeed + Vector3.up * verticalSpeed;
            controller.Move(move * Time.deltaTime);

            var stability = b.isInhaling ? b.depth : 1f - b.depth;
            score = Mathf.Lerp(score, stability, Time.deltaTime);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordBreathing(stability);

            UpdateParrotGlide(stability, b.depth);
        }

        float ComputeVerticalSpeed(float depth, bool inhaling)
        {
            if (inhaling)
            {
                if (depth < 0.5f)
                    return Mathf.Lerp(0f, verticalSpeedMid, depth / 0.5f);
                return Mathf.Lerp(verticalSpeedMid, verticalSpeedHigh, (depth - 0.5f) / 0.5f);
            }

            return -Mathf.Lerp(glideDescentMin, glideDescentSpeed, 1f - depth);
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
