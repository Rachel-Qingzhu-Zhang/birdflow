using System.Collections;
using AvianVoice.Audio;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    public enum CallFriendsCompanionPhase
    {
        Perched,
        FlyingToPlayer,
        Orbiting
    }

    /// <summary>
    /// Call Friends flock member: idle perch in front of player, then fly in and orbit on call.
    /// </summary>
    [DefaultExecutionOrder(32001)]
    public class CallFriendsCompanionBehaviour : MonoBehaviour
    {
        [SerializeField]
        float flockScale = FlockParrotBehaviour.DefaultFlockScale;

        [SerializeField]
        float perchBobAmplitude = 0.03f;

        [SerializeField]
        float orbitRadius = 2.6f;

        [SerializeField]
        float orbitRadiusStep = 0.38f;

        [SerializeField]
        float orbitSpeed = 24f;

        [SerializeField]
        float orbitBobAmplitude = 0.08f;

        [SerializeField]
        float flightHeight = 0.55f;

        [SerializeField]
        float takeoffDuration = 0.35f;

        [SerializeField]
        float flyToDuration = 3.4f;

        Transform templateBird;
        Transform orbitCenter;
        int memberIndex;
        int responseIndex;
        float orbitAngle;
        float orbitRadiusForMember;
        float orbitSpeedForMember;
        float bobPhase;
        float chirpHz;
        Vector3 perchedBasePosition;
        Quaternion perchedBaseRotation;
        CallFriendsCompanionPhase phase = CallFriendsCompanionPhase.Perched;
        Animator animator;
        AudioSource chirpSource;
        Coroutine motionRoutine;

        public CallFriendsCompanionPhase Phase => phase;
        public bool IsPerched => phase == CallFriendsCompanionPhase.Perched;

        public void ConfigureScale(float scale)
        {
            flockScale = Mathf.Max(0.1f, scale);
        }

        public void InitializePerched(
            Transform template,
            int index,
            Vector3 worldPos,
            Quaternion facing,
            float scale)
        {
            templateBird = template;
            memberIndex = index;
            flockScale = Mathf.Max(0.1f, scale);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);
            perchedBasePosition = worldPos;
            perchedBaseRotation = facing;
            phase = CallFriendsCompanionPhase.Perched;

            FlockParrotSetup.PrepareInstance(transform, templateBird);
            animator = GetComponent<Animator>();
            transform.localScale = Vector3.one * flockScale;
            transform.SetPositionAndRotation(worldPos, facing);

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.updateWhenOffscreen = true;

            BeginIdleAnimation();
            EnsureChirpSource();
        }

        public float DistanceTo(Vector3 point)
        {
            return Vector3.Distance(transform.position, point);
        }

        public void RespondToCall(Transform center, float noteHz, BirdCallLibrary library, int responseIdx)
        {
            if (phase != CallFriendsCompanionPhase.Perched)
                return;

            orbitCenter = center;
            chirpHz = noteHz;
            responseIndex = responseIdx;
            orbitRadiusForMember = orbitRadius + memberIndex * orbitRadiusStep;
            orbitSpeedForMember = orbitSpeed + memberIndex * 3.5f;
            orbitAngle = memberIndex * 62f + responseIdx * 28f + Random.Range(-8f, 8f);

            if (motionRoutine != null)
                StopCoroutine(motionRoutine);
            motionRoutine = StartCoroutine(FlyToOrbitRoutine(library));
        }

        public void PlayAnswerCall(BirdCallLibrary library, float delaySeconds)
        {
            if (library == null || chirpHz <= 0f)
                return;
            Invoke(nameof(PlayChirpEcho), delaySeconds);
        }

        void LateUpdate()
        {
            if (phase == CallFriendsCompanionPhase.Perched)
                ApplyPerchedBob();
            else if (phase == CallFriendsCompanionPhase.Orbiting)
                ApplyOrbitPose();
        }

        void ApplyPerchedBob()
        {
            var bob = Mathf.Sin(Time.time * 1.8f + bobPhase) * perchBobAmplitude;
            transform.position = perchedBasePosition + Vector3.up * bob;
            transform.rotation = perchedBaseRotation;
        }

        void ApplyOrbitPose()
        {
            if (orbitCenter == null)
                return;

            var slot = ViewSlotPosition(GatherDistance());
            var sway = Mathf.Sin(Time.time * 0.8f + bobPhase) * 0.12f;
            var right = orbitCenter.right;
            right.y = 0f;
            if (right.sqrMagnitude > 0.0001f)
                right.Normalize();
            transform.position = slot + right * sway + Vector3.up * Mathf.Sin(Time.time * 2.1f + bobPhase) * orbitBobAmplitude;

            var toView = orbitCenter.position - transform.position;
            toView.y = 0f;
            if (toView.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toView.normalized, Vector3.up);
        }

        Vector3 ViewSlotPosition(float distance)
        {
            if (orbitCenter == null)
                return transform.position;

            var forward = orbitCenter.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward);
            var lateral = Mathf.Lerp(-1.4f, 1.4f, (memberIndex % 7) / 6f);
            lateral += Mathf.Sin(Time.time * 0.45f + memberIndex) * 0.1f;
            var height = Mathf.Lerp(0.35f, 1.15f, (memberIndex % 5) / 4f);
            return orbitCenter.position + forward * distance + right * lateral + Vector3.up * height;
        }

        float GatherDistance()
        {
            return 5f;
        }

        void BeginIdleAnimation()
        {
            if (animator == null)
                return;

            FlockParrotSetup.ResetFlightBools(animator);
            animator.speed = 1f;
            animator.SetBool("idle", true);
        }

        IEnumerator FlyToOrbitRoutine(BirdCallLibrary library)
        {
            phase = CallFriendsCompanionPhase.FlyingToPlayer;

            if (animator != null)
            {
                FlockParrotSetup.ResetFlightBools(animator);
                animator.SetBool("idle", false);
                animator.SetBool("takeoff", true);
            }

            yield return new WaitForSeconds(takeoffDuration + memberIndex * 0.16f);

            if (animator != null)
            {
                animator.SetBool("takeoff", false);
                animator.SetBool("fly", true);
                if (animator.runtimeAnimatorController != null)
                    animator.Play("fly", 0, Random.Range(0f, 1f));
            }

            var start = transform.position;
            var target = ViewSlotPosition(GatherDistance());
            var travel = Vector3.Distance(start, target);
            var duration = Mathf.Clamp(travel / 3.6f, flyToDuration, 6.2f);

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                var liveTarget = ViewSlotPosition(GatherDistance());
                if (elapsed < duration * 0.55f)
                    target = liveTarget;
                transform.position = Vector3.Lerp(start, target, t);

                var moveDir = target - transform.position;
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(moveDir.normalized, Vector3.up);

                yield return null;
            }

            phase = CallFriendsCompanionPhase.Orbiting;
            motionRoutine = null;

            var chirpDelay = responseIndex * 0.12f;
            PlayAnswerCall(library, chirpDelay);
        }

        void EnsureChirpSource()
        {
            if (chirpSource != null)
                return;

            chirpSource = GetComponent<AudioSource>();
            if (chirpSource == null)
                chirpSource = gameObject.AddComponent<AudioSource>();
            chirpSource.playOnAwake = false;
            chirpSource.spatialBlend = 0.75f;
            chirpSource.volume = 0.85f;
            chirpSource.minDistance = 0.5f;
            chirpSource.maxDistance = 36f;
        }

        void PlayChirpEcho()
        {
            if (chirpSource == null || chirpHz <= 0f)
                return;

            var lib = FindAnyObjectByType<BirdCallLibrary>();
            lib?.PlayNearest(chirpSource, chirpHz, 0.85f, null);
        }

        void OnDestroy()
        {
            if (motionRoutine != null)
                StopCoroutine(motionRoutine);
            CancelInvoke(nameof(PlayChirpEcho));
        }
    }
}
