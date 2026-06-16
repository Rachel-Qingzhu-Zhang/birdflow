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
        float flyToDuration = 1.2f;

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

            orbitAngle += orbitSpeedForMember * Time.deltaTime;
            var rad = orbitAngle * Mathf.Deg2Rad;
            var bob = Mathf.Sin(Time.time * 2.1f + bobPhase) * orbitBobAmplitude;
            var center = orbitCenter.position + Vector3.up * 0.5f;
            var offset = new Vector3(
                Mathf.Cos(rad) * orbitRadiusForMember,
                flightHeight + bob,
                Mathf.Sin(rad) * orbitRadiusForMember);
            transform.position = center + offset;

            var tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            if (tangent.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
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

            yield return new WaitForSeconds(takeoffDuration);

            if (animator != null)
            {
                animator.SetBool("takeoff", false);
                animator.SetBool("fly", true);
                if (animator.runtimeAnimatorController != null)
                    animator.Play("fly", 0, Random.Range(0f, 1f));
            }

            var start = transform.position;
            var entryAngle = orbitAngle * Mathf.Deg2Rad;
            var center = orbitCenter.position + Vector3.up * 0.5f;
            var entryOffset = new Vector3(
                Mathf.Cos(entryAngle) * orbitRadiusForMember,
                flightHeight,
                Mathf.Sin(entryAngle) * orbitRadiusForMember);
            var target = center + entryOffset;

            var elapsed = 0f;
            while (elapsed < flyToDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / flyToDuration);
                transform.position = Vector3.Lerp(start, target, t);

                var moveDir = target - start;
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
            chirpSource.maxDistance = 18f;
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
