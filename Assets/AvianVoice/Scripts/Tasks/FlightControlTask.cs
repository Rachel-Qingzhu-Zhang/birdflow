using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using UnityEngine;
using UnityEngine.XR;

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
        float altitudeSpeed = 2.2f;

        [SerializeField]
        float minAltitude = -40f;

        [SerializeField]
        float maxAltitude = 40f;

        [SerializeField]
        float maxHeightAboveWater = 3.4f;

        [SerializeField]
        float minHeightAboveWater = 1.15f;

        [SerializeField]
        // > 1 zooms the reflection center so the near-camera bird reads larger on the pond.
        float flightReflectionScaleMultiplier = 1.55f;

        CharacterController controller;
        ParrotFlightPresentation parrotFlight;
        XrRigPoseSnapshot rigSnapshot;
        Vector3 lockedTravelForward = Vector3.forward;
        float score;
        bool glideActive;
        bool flightSessionActive;
        float cheatClimbTimer;
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
            CaptureLockedTravelForward();
            ResolveParrotFlightPresentation();
            SetFriendCompanionVisible(false);
            SetPlayerBreathEmbodimentEnabled(false);
            parrotFlight?.BeginFlightPresentation();
            PondWaterReflection.SetAllReflectionScaleMultiplier(flightReflectionScaleMultiplier);

            if (ShouldSnapRigToGround())
                SnapToGroundIfNeeded();
            score = 0f;
            glideActive = false;
            flightSessionActive = true;
            cheatClimbTimer = 0f;
            Debug.Log("[AvianVoice] Flying: hold Space to climb, release to glide down. E/Q or stick still nudge height.");
        }

        void OnDisable()
        {
            // Record once on exit so Reflection scores are not inflated by per-frame peaks.
            if (flightSessionActive)
                SessionScoreCache.RecordBreathing(score);
            flightSessionActive = false;

            PondWaterReflection.RestoreAllReflectionScales();
            parrotFlight?.EndFlightPresentation();
            SetFriendCompanionVisible(true);
            SetPlayerBreathEmbodimentEnabled(true);
            rigSnapshot?.RestorePose();
            glideActive = false;
        }

        bool IsActiveTask() => TaskActivity.IsActive("flying") || TaskActivity.IsActive("flight");

        void CaptureLockedTravelForward()
        {
            // Use XR Origin yaw only — not Camera Offset / HMD look direction.
            lockedTravelForward = transform.forward;
            lockedTravelForward.y = 0f;
            if (lockedTravelForward.sqrMagnitude < 0.0001f)
                lockedTravelForward = Vector3.forward;
            lockedTravelForward.Normalize();
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
            var p = transform.position;
            p.y = groundedY;
            if (controller != null)
            {
                controller.enabled = false;
                transform.position = p;
                controller.enabled = true;
            }
            else
            {
                transform.position = p;
            }
        }

        void Update()
        {
            if (!enabled || !IsActiveTask())
                return;

            if (AvianInput.WasTaskCheatPressedThisFrame())
            {
                score = 1f;
                cheatClimbTimer = 0.85f;
                taskManager?.ReportScore(score);
                SessionScoreCache.RecordBreathing(1f);
                BirdExperienceAudio.Ensure()?.PlayForestReward();
                Debug.Log("[AvianVoice] Flying cheat: score 1, brief climb.");
            }

            if (controller == null)
                return;

            // Keep travel axis on the XR Origin yaw. HMD look does not steer movement.
            // Thumbstick snap-turn rotates the origin, which updates this forward.
            var travelForward = transform.forward;
            travelForward.y = 0f;
            if (travelForward.sqrMagnitude < 0.0001f)
                travelForward = lockedTravelForward;
            travelForward.Normalize();
            lockedTravelForward = travelForward;

            var verticalSpeed = ResolveVerticalSpeed();
            if (cheatClimbTimer > 0f)
            {
                cheatClimbTimer -= Time.deltaTime;
                verticalSpeed = Mathf.Max(verticalSpeed, altitudeSpeed);
            }

            var move = travelForward * forwardSpeed + Vector3.up * verticalSpeed;
            controller.Move(move * Time.deltaTime);
            ClampAltitude();

            if (breath != null)
            {
                var b = breath.Current;
                var stability = b.isInhaling ? b.depth : 1f - b.depth;
                score = Mathf.Lerp(score, stability, Time.deltaTime);
                UpdateParrotGlide();
            }

            taskManager?.ReportScore(score);
        }

        float ResolveVerticalSpeed()
        {
            var axis = AvianInput.GetFlightAltitudeAxis();
            if (Mathf.Abs(axis) > 0.01f)
                return axis * altitudeSpeed;

            // VR: left/right stick Y still nudges height when the player asks for it.
            if (TryReadThumbstickY(out var stickY) && Mathf.Abs(stickY) > 0.25f)
                return Mathf.Clamp(stickY, -1f, 1f) * altitudeSpeed;

            return BreathVerticalMeters();
        }

        /// <summary>
        /// Inhale climbs (about 1 m/s at half breath, tuned cap at full breath).
        /// Exhale descends. The cap is the existing altitudeSpeed so the pond band stays comfortable.
        /// </summary>
        float BreathVerticalMeters()
        {
            if (breath == null)
                return 0f;

            var frame = breath.Current;
            if (frame.isInhaling)
            {
                if (frame.depth <= 0.5f)
                    return Mathf.Lerp(0f, 1f, frame.depth / 0.5f);
                return Mathf.Lerp(1f, altitudeSpeed, (frame.depth - 0.5f) / 0.5f);
            }

            return -Mathf.Lerp(0.35f, 1.35f, 1f - frame.depth);
        }

        void ClampAltitude()
        {
            var p = transform.position;
            var clampedY = Mathf.Clamp(p.y, minAltitude, maxAltitude);

            var pond = UnityEngine.Object.FindAnyObjectByType<PondWaterReflection>();
            if (pond != null)
            {
                var waterY = pond.transform.position.y;
                clampedY = Mathf.Clamp(clampedY, waterY + minHeightAboveWater, waterY + maxHeightAboveWater);
            }

            if (Mathf.Approximately(clampedY, p.y))
                return;

            p.y = clampedY;
            controller.enabled = false;
            transform.position = p;
            controller.enabled = true;
        }

        static bool TryReadThumbstickY(out float y)
        {
            y = 0f;
            if (TryReadNodeY(XRNode.RightHand, out y))
                return true;
            return TryReadNodeY(XRNode.LeftHand, out y);
        }

        static bool TryReadNodeY(XRNode node, out float y)
        {
            y = 0f;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
                return false;
            if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var stick))
                return false;
            y = stick.y;
            return Mathf.Abs(y) > 0.0001f;
        }

        void UpdateParrotGlide()
        {
            if (parrotFlight == null || breath == null)
                return;

            var frame = breath.Current;
            var wantGlide = !frame.isInhaling || frame.depth < 0.22f;
            if (wantGlide == glideActive)
                return;

            glideActive = wantGlide;
            parrotFlight.SetGlide(wantGlide);
        }
    }
}
