using System.Collections;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Small parrot flock member: orbits the two scene parrots with pack fly animation.
    /// </summary>
    [DefaultExecutionOrder(32002)]
    public class FlockParrotBehaviour : MonoBehaviour
    {
        public const float DefaultFlockScale = 0.6f;

        [SerializeField]
        float flockScale = DefaultFlockScale;

        [SerializeField]
        float orbitRadius = 2.55f;

        [SerializeField]
        float orbitRadiusStep = 0.38f;

        [SerializeField]
        float orbitSpeed = 26f;

        [SerializeField]
        float bobAmplitude = 0.08f;

        [SerializeField]
        float flightHeight = 0.55f;

        [SerializeField]
        float takeoffDuration = 0.35f;

        Transform templateBird;
        int memberIndex;
        float orbitAngle;
        float orbitRadiusForMember;
        float orbitSpeedForMember;
        float bobPhase;
        Animator animator;
        Coroutine flightRoutine;

        public void ConfigureScale(float scale)
        {
            flockScale = Mathf.Max(0.1f, scale);
        }

        public void Initialize(Transform template, int index, float lateralSpawnOffset)
        {
            templateBird = template;
            memberIndex = index;
            orbitRadiusForMember = orbitRadius + memberIndex * orbitRadiusStep;
            orbitSpeedForMember = orbitSpeed + memberIndex * 3.5f;
            orbitAngle = index * 62f + lateralSpawnOffset * 18f + Random.Range(-8f, 8f);
            bobPhase = Random.Range(0f, Mathf.PI * 2f);

            FlockParrotSetup.PrepareInstance(transform, templateBird);
            animator = GetComponent<Animator>();
            transform.localScale = Vector3.one * flockScale;
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            ApplyOrbitPose();
            BeginFlightAnimation();
        }

        void BeginFlightAnimation()
        {
            if (animator == null)
                return;

            if (flightRoutine != null)
                StopCoroutine(flightRoutine);
            flightRoutine = StartCoroutine(FlightEnterRoutine());
        }

        IEnumerator FlightEnterRoutine()
        {
            FlockParrotSetup.ResetFlightBools(animator);
            animator.speed = 1f;
            SetBool("idle", false);
            SetBool("takeoff", true);

            yield return new WaitForSeconds(takeoffDuration);

            SetBool("takeoff", false);
            SetBool("glide", false);
            SetBool("fly", true);
            if (animator.runtimeAnimatorController != null)
                animator.Play("fly", 0, Random.Range(0f, 1f));
            flightRoutine = null;
        }

        void LateUpdate()
        {
            ApplyOrbitPose();
        }

        void ApplyOrbitPose()
        {
            if (!BirdSceneRoles.TryGetFlockOrbitCenter(out var center))
            {
                if (templateBird != null)
                    center = templateBird.position;
                else
                    return;
            }

            orbitAngle += orbitSpeedForMember * Time.deltaTime;
            var rad = orbitAngle * Mathf.Deg2Rad;
            var bob = Mathf.Sin(Time.time * 2.1f + bobPhase) * bobAmplitude;
            var offset = new Vector3(
                Mathf.Cos(rad) * orbitRadiusForMember,
                flightHeight + bob,
                Mathf.Sin(rad) * orbitRadiusForMember);
            transform.position = center + offset;

            var tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            if (tangent.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        }

        void OnDestroy()
        {
            if (flightRoutine != null)
                StopCoroutine(flightRoutine);
        }

        void SetBool(string param, bool value)
        {
            if (animator != null)
                animator.SetBool(param, value);
        }
    }
}
