using System.Collections;
using UnityEngine;

namespace AvianVoice.Embodiment
{
    /// <summary>
    /// Flight: parrot on Camera Offset, glide pose (no fly root drift), pin Main_ctrl + COM_ctrl.
    /// </summary>
    [DefaultExecutionOrder(10050)]
    public class ParrotFlightPresentation : MonoBehaviour
    {
        [SerializeField]
        Vector3 anchorLocalOffset = new(0f, -0.12f, 1.4f);

        [SerializeField]
        // Y=0: back toward player in flight; lab/world uses ~180° to face the player.
        Vector3 anchorLocalEuler = new(0f, 0f, 0f);

        [SerializeField]
        float takeoffDuration = 0.5f;

        [SerializeField]
        float landingDuration = 0.45f;

        Animator animator;
        WorldPlacedBird worldGuard;
        ParrotMainCtrlPin rigPin;
        Transform flightAnchor;
        Coroutine flightRoutine;
        bool flightActive;
        float savedAnimatorSpeed = 1f;

        void Awake()
        {
            animator = GetComponent<Animator>();
            worldGuard = GetComponent<WorldPlacedBird>();
            rigPin = GetComponent<ParrotMainCtrlPin>() ?? gameObject.AddComponent<ParrotMainCtrlPin>();
        }

        public void BeginFlightPresentation()
        {
            if (flightActive)
                return;

            flightActive = true;
            rigPin.CaptureRestPose();
            rigPin.SetPinEnabled(true);

            worldGuard?.SaveWorldPose();
            worldGuard?.SetFlightCameraParenting(true);
            TryAttachToFlightAnchor();

            if (animator != null)
            {
                savedAnimatorSpeed = animator.speed;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            if (flightRoutine != null)
                StopCoroutine(flightRoutine);
            flightRoutine = StartCoroutine(FlightEnterRoutine());
        }

        public void EndFlightPresentation()
        {
            if (!flightActive)
                return;

            flightActive = false;
            rigPin.SetPinEnabled(false);

            if (animator != null)
                animator.speed = savedAnimatorSpeed;

            if (flightRoutine != null)
                StopCoroutine(flightRoutine);

            worldGuard?.SetFlightCameraParenting(false);
            if (transform.parent != null)
                transform.SetParent(null, true);

            worldGuard?.RestoreWorldPose();
            flightRoutine = StartCoroutine(FlightExitRoutine());
        }

        void LateUpdate()
        {
            if (!flightActive)
                return;

            if (flightAnchor == null)
                TryAttachToFlightAnchor();

            if (flightAnchor == null)
                return;

            if (transform.parent != flightAnchor)
            {
                worldGuard?.SetFlightCameraParenting(true);
                transform.SetParent(flightAnchor, false);
            }

            ApplyAnchorLocalPose();
        }

        void TryAttachToFlightAnchor()
        {
            if (!WorldPlacedBird.TryFindFlightAnchor(out flightAnchor, out _))
                return;

            transform.SetParent(flightAnchor, false);
            ApplyAnchorLocalPose();
            Debug.Log("[AvianVoice] Flight parrot attached to '" + flightAnchor.name + "'.");
        }

        void ApplyAnchorLocalPose()
        {
            transform.localPosition = anchorLocalOffset;
            transform.localRotation = Quaternion.Euler(anchorLocalEuler);
        }

        IEnumerator FlightEnterRoutine()
        {
            ResetFlightBools();
            if (animator != null)
                animator.speed = savedAnimatorSpeed;

            SetBool("idle", false);
            SetBool("takeoff", true);

            yield return new WaitForSeconds(takeoffDuration);

            SetBool("takeoff", false);
            SetBool("fly", false);
            SetBool("glide", true);

            if (animator != null)
            {
                animator.Play("glide", 0, 0f);
                yield return null;
                animator.speed = 0f;
            }

            flightRoutine = null;
        }

        IEnumerator FlightExitRoutine()
        {
            if (animator != null)
                animator.speed = savedAnimatorSpeed;

            SetBool("fly", false);
            SetBool("glide", false);
            SetBool("takeoff", false);
            SetBool("landing", true);

            yield return new WaitForSeconds(landingDuration);

            SetBool("landing", false);
            SetBool("idle", true);
            worldGuard?.SaveWorldPose();
            flightRoutine = null;
        }

        public void SetGlide(bool glide)
        {
            if (!flightActive || animator == null)
                return;

            SetBool("glide", glide || flightActive);
        }

        void ResetFlightBools()
        {
            SetBool("walk", false);
            SetBool("fly", false);
            SetBool("glide", false);
            SetBool("takeoff", false);
            SetBool("landing", false);
            SetBool("flyleft", false);
            SetBool("flyright", false);
            SetBool("turnleft", false);
            SetBool("turnright", false);
            SetBool("dance", false);
            SetBool("eat", false);
            SetBool("jump", false);
        }

        void SetBool(string param, bool value)
        {
            if (animator != null)
                animator.SetBool(param, value);
        }
    }
}
