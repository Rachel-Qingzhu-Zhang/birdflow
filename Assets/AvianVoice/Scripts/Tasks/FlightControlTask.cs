using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using UnityEngine;
using UnityEngine.XR;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// Flying: the bird glides forward on its own. Voice controls altitude:
    /// louder or higher singing climbs, silence glides slowly down toward the pond.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FlightControlTask : MonoBehaviour
    {
        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        VoiceAnalyzer analyzer;

        [SerializeField]
        float forwardSpeed = 1.75f;

        [SerializeField]
        float altitudeSpeed = 2.2f;

        [SerializeField]
        float minAltitude = -40f;

        [SerializeField]
        float maxAltitude = 40f;

        [Header("Height band above the pond (rig origin)")]
        [SerializeField]
        float maxHeightAboveWater = 2.6f;

        [SerializeField]
        float minHeightAboveWater = 0.6f;

        [Header("Voice altitude")]
        [Tooltip("Loudness (0-1) below which a voiced frame does not climb.")]
        [SerializeField]
        float climbLoudnessMin = 0.15f;

        [Tooltip("Human pitch (Hz) mapped to no pitch bonus.")]
        [SerializeField]
        float lowPitchHz = 120f;

        [Tooltip("Human pitch (Hz) mapped to full pitch bonus.")]
        [SerializeField]
        float highPitchHz = 600f;

        [Tooltip("0 = loudness only, 1 = pitch only.")]
        [Range(0f, 1f)]
        [SerializeField]
        float pitchWeight = 0.5f;

        [Tooltip("Descent speed (m/s) while silent.")]
        [SerializeField]
        float glideDescentSpeed = 0.45f;

        [Tooltip("Seconds to ease vertical speed toward the voice target.")]
        [SerializeField]
        float verticalSmoothing = 0.35f;

        [SerializeField]
        // > 1 zooms the reflection center so the near-camera bird reads larger on the pond.
        float flightReflectionScaleMultiplier = 1.55f;

        CharacterController controller;
        ParrotFlightPresentation parrotFlight;
        XrRigPoseSnapshot rigSnapshot;
        Vector3 lockedTravelForward = Vector3.forward;
        float score;
        float verticalVelocity;
        float lastPitchHz;
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
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            ResolveParrotFlightPresentation();
        }

        void OnEnable()
        {
            if (!IsActiveTask())
                return;

            if (controller == null)
                controller = GetComponent<CharacterController>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();

            rigSnapshot?.SavePose();
            CaptureLockedTravelForward();
            ResolveParrotFlightPresentation();
            SetPlayerBreathEmbodimentEnabled(false);
            parrotFlight?.BeginFlightPresentation();
            PondWaterReflection.SetAllReflectionScaleMultiplier(flightReflectionScaleMultiplier);

            if (ShouldSnapRigToGround())
                SnapToGroundIfNeeded();
            score = 0f;
            verticalVelocity = 0f;
            lastPitchHz = 0f;
            glideActive = false;
            flightSessionActive = true;
            cheatClimbTimer = 0f;
            Debug.Log("[AvianVoice] Flying: sing louder or higher to climb, stay quiet to glide down. E/Q or stick still nudge height.");
        }

        void OnDisable()
        {
            // Record once on exit so Reflection scores are not inflated by per-frame peaks.
            if (flightSessionActive)
                SessionScoreCache.RecordVoice(score);
            flightSessionActive = false;

            PondWaterReflection.RestoreAllReflectionScales();
            parrotFlight?.EndFlightPresentation();
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
                : WorldPlacedBird.FindParrotInScene();
            if (parrot == null)
                return;

            parrotFlight = parrot.GetComponent<ParrotFlightPresentation>();
            if (parrotFlight == null)
                parrotFlight = parrot.gameObject.AddComponent<ParrotFlightPresentation>();
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

        void KeepOriginUpright()
        {
            var euler = transform.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(euler.x, 0f)) < 0.5f && Mathf.Abs(Mathf.DeltaAngle(euler.z, 0f)) < 0.5f)
                return;

            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }

        bool ShouldSnapRigToGround()
        {
            var taskPoses = GetComponent<XrRigTaskStartPoses>();
            if (taskPoses != null && taskPoses.HasConfiguredPose("flying"))
                return false;

            var parrot = WorldPlacedBird.FindParrotInScene();
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
                SessionScoreCache.RecordVoice(1f);
                BirdExperienceAudio.Ensure()?.PlayForestReward();
                Debug.Log("[AvianVoice] Flying cheat: score 1, brief climb.");
            }

            if (controller == null)
                return;

            KeepOriginUpright();

            // Keep travel axis on the XR Origin yaw. HMD look does not steer movement.
            // Thumbstick snap-turn rotates the origin, which updates this forward.
            var travelForward = transform.forward;
            travelForward.y = 0f;
            if (travelForward.sqrMagnitude < 0.0001f)
                travelForward = lockedTravelForward;
            travelForward.Normalize();
            lockedTravelForward = travelForward;

            var voice = analyzer != null ? analyzer.Current : default;
            var isVoiced = voice.voiced > 0.5f && voice.loudness >= climbLoudnessMin;
            var verticalSpeed = ResolveVerticalSpeed(voice, isVoiced);
            if (cheatClimbTimer > 0f)
            {
                cheatClimbTimer -= Time.deltaTime;
                verticalSpeed = Mathf.Max(verticalSpeed, altitudeSpeed);
            }

            var move = travelForward * forwardSpeed + Vector3.up * verticalSpeed;
            var before = transform.position;
            controller.Move(move * Time.deltaTime);
            if ((transform.position - before).sqrMagnitude < 0.00001f && move.sqrMagnitude > 0.01f)
            {
                controller.enabled = false;
                transform.position = before + travelForward * (forwardSpeed * Time.deltaTime) + Vector3.up * 0.12f;
                controller.enabled = true;
            }

            ClampAltitude();

            UpdateScore(voice, isVoiced);
            UpdateParrotGlide(isVoiced);
            taskManager?.ReportScore(score);
        }

        float ResolveVerticalSpeed(VoiceFrame voice, bool isVoiced)
        {
            var axis = AvianInput.GetFlightAltitudeAxis();
            if (Mathf.Abs(axis) > 0.01f)
            {
                verticalVelocity = axis * altitudeSpeed;
                return verticalVelocity;
            }

            // VR: left/right stick Y still nudges height when the player asks for it.
            if (TryReadThumbstickY(out var stickY) && Mathf.Abs(stickY) > 0.25f)
            {
                verticalVelocity = Mathf.Clamp(stickY, -1f, 1f) * altitudeSpeed;
                return verticalVelocity;
            }

            var target = VoiceVerticalTarget(voice, isVoiced);
            var k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, verticalSmoothing));
            verticalVelocity = Mathf.Lerp(verticalVelocity, target, k);
            return verticalVelocity;
        }

        float VoiceVerticalTarget(VoiceFrame voice, bool isVoiced)
        {
            if (!isVoiced)
                return -glideDescentSpeed;

            var loud = Mathf.InverseLerp(climbLoudnessMin, 1f, voice.loudness);
            var pitch = 0f;
            if (voice.pitchHz > 0f && highPitchHz > lowPitchHz)
            {
                pitch = Mathf.InverseLerp(
                    Mathf.Log(lowPitchHz),
                    Mathf.Log(highPitchHz),
                    Mathf.Log(voice.pitchHz));
            }

            var drive = Mathf.Lerp(loud, pitch, pitchWeight);
            // Any clear voice at least holds altitude; stronger / higher climbs faster.
            return Mathf.Lerp(0.15f, altitudeSpeed, drive);
        }

        void UpdateScore(VoiceFrame voice, bool isVoiced)
        {
            float frameScore;
            if (isVoiced && voice.pitchHz > 0f)
            {
                var steadiness = 1f;
                if (lastPitchHz > 0f)
                {
                    var semitones = Mathf.Abs(12f * Mathf.Log(voice.pitchHz / lastPitchHz, 2f));
                    steadiness = Mathf.Clamp01(1f - semitones / 3f);
                }

                lastPitchHz = voice.pitchHz;
                frameScore = Mathf.Lerp(0.6f, 1f, steadiness);
            }
            else
            {
                lastPitchHz = 0f;
                // Quiet gliding is part of flight, so it keeps a modest score.
                frameScore = 0.4f;
            }

            score = Mathf.Lerp(score, frameScore, Time.deltaTime * 0.5f);
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
            verticalVelocity = 0f;
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

        void UpdateParrotGlide(bool isVoiced)
        {
            if (parrotFlight == null)
                return;

            if (glideActive)
                return;

            glideActive = true;
            parrotFlight.SetGlide(false);
        }
    }
}
