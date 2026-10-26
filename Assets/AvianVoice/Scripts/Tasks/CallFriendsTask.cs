using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class CallFriendsTask : MonoBehaviour
    {
        enum SummonStage
        {
            None,
            OneBird,
            ThreeBirds,
            Flock
        }

        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        VoiceAnalyzer analyzer;

        [SerializeField]
        BreathSimulator breath;

        [SerializeField]
        BirdCallLibrary library;

        [SerializeField]
        AudioSource source;

        [SerializeField]
        SuccessChirpBurst successChirp;

        [SerializeField]
        GameObject flockBirdPrefab;

        [SerializeField]
        float flockScale = FlockParrotBehaviour.DefaultFlockScale;

        [SerializeField]
        int initialCompanionCount = 7;

        [SerializeField]
        float perchForwardMin = 30f;

        [SerializeField]
        float perchForwardMax = 30f;

        [SerializeField]
        float perchLateralSpread = 4f;

        [SerializeField]
        float perchHeightMin = 1.8f;

        [SerializeField]
        float perchHeightMax = 4.6f;

        [SerializeField]
        int[] birdsPerNote = { 1, 2 };

        [SerializeField]
        float[] melodyHz = { 330f, 392f };

        [SerializeField]
        float toleranceHz = 40f;

        [SerializeField]
        float matchHoldSeconds = 0.45f;

        [SerializeField]
        float breathSyncMin = 0.55f;

        [SerializeField]
        bool enableDebugCheat = true;

        BirdFriendDemonstrator demonstrator;
        ParrotMatchVisuals friendVisuals;
        TaskFlowUI taskUi;
        readonly List<CallFriendsCompanionBehaviour> companions = new();
        Transform orbitCenter;
        int stepIndex;
        float matchHold;
        float score;
        float stablePitch;
        bool imitateMelody;
        int sessionSeed;
        SummonStage stage = SummonStage.None;
        Coroutine chorusRoutine;
        bool chorusComplete;
        bool friendHomeSaved;
        bool friendHoldingView;
        Vector3 friendHomePosition;
        Quaternion friendHomeRotation;
        Vector3 friendHomeScale;
        Coroutine friendFlightRoutine;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            if (breath == null)
                breath = FindAnyObjectByType<BreathSimulator>();
            if (library == null)
                library = FindAnyObjectByType<BirdCallLibrary>();
            if (successChirp == null)
                successChirp = FindAnyObjectByType<SuccessChirpBurst>();
            demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
            EnsureSource();
            EnsureFriendVisuals();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("call_friends"))
                return;

            ClearCompanions();
            stepIndex = 0;
            matchHold = 0f;
            score = 0f;
            stablePitch = 0f;
            imitateMelody = BirdExperienceAudio.Ensure()?.HasMelodyClip == true;
            stage = SummonStage.None;
            chorusComplete = false;
            sessionSeed = Random.Range(0, 100000);
            EnsureSummonPlan();
            RememberFriendHome();
            ResolveOrbitCenter();
            SpawnIdleCompanions();
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternA4x4);
            demonstrator?.RestartPattern();
            PlayReference();
            SetHud("Imitate the melody with Pattern A breath. One bird, then three, then the flock.");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            CancelInvoke(nameof(PlaySuccessChirpDelayed));
            if (chorusRoutine != null)
                StopCoroutine(chorusRoutine);
            chorusRoutine = null;
            RestoreFriendHome();
            BirdExperienceAudio.Ensure()?.StopAll();
            ClearCompanions();
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("call_friends"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                AdvanceSummon(1f);
                Debug.Log("[AvianVoice] Call friends cheat. Step=" + stepIndex);
                return;
            }

            if (analyzer == null || breath == null || demonstrator == null)
                return;

            if (stepIndex >= melodyHz.Length && stage == SummonStage.Flock)
            {
                SetHud("Flock gathered! Friends say goodbye soon — press 6 for Reflection.");
                return;
            }

            var target = melodyHz[Mathf.Min(stepIndex, melodyHz.Length - 1)];
            var v = analyzer.Current;
            var pitchMatch = 0f;
            if (v.voiced > 0.5f && v.pitchHz > 0f)
            {
                var error = Mathf.Abs(v.pitchHz - target);
                pitchMatch = Mathf.Clamp01(1f - error / toleranceHz);
            }

            var breathSync = BreathSyncEvaluator.Evaluate(breath.Current, demonstrator);
            var stable = false;
            if (v.voiced > 0.5f && v.pitchHz > 90f && v.pitchHz < 950f)
            {
                if (stablePitch <= 0f)
                    stablePitch = v.pitchHz;
                stable = Mathf.Abs(v.pitchHz - stablePitch) <= 55f;
                if (!stable)
                    stablePitch = v.pitchHz;
            }
            else
            {
                stablePitch = 0f;
            }

            var combined = pitchMatch * 0.6f + breathSync * 0.4f;
            if (imitateMelody && stable)
                combined = Mathf.Max(combined, 0.55f + breathSync * 0.45f);
            score = Mathf.Lerp(score, combined, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(Mathf.Max(pitchMatch, imitateMelody && stable ? 0.8f : 0f));
            SessionScoreCache.RecordSynchronization(breathSync);
            friendVisuals?.ApplyMatch(combined);

            var targetHit = pitchMatch > 0.7f;
            var imitateHit = imitateMelody && stable;
            if ((targetHit || imitateHit) && breathSync >= breathSyncMin)
                matchHold += Time.deltaTime;
            else
                matchHold = 0f;

            if (matchHold >= matchHoldSeconds)
                AdvanceSummon(combined);
            else
                SetHud($"Call {NoteLabel(target)} + breath sync {breathSync * 100f:0}%");
        }

        void AdvanceSummon(float stepScore)
        {
            matchHold = 0f;
            friendVisuals?.PulsePerfect();
            if (BirdExperienceAudio.Ensure()?.PlayForestReward() != true)
                Invoke(nameof(PlaySuccessChirpDelayed), 0.2f);

            var noteIndex = melodyHz == null || melodyHz.Length == 0
                ? 0
                : Mathf.Clamp(stepIndex, 0, melodyHz.Length - 1);
            var noteHz = melodyHz != null && melodyHz.Length > 0 ? melodyHz[noteIndex] : 440f;
            stepIndex++;

            var flying = companions.Count(c => c != null && !c.IsPerched);
            if (melodyHz != null && stepIndex >= melodyHz.Length)
            {
                stage = SummonStage.Flock;
                SummonAllRemaining(noteHz);
                if (!chorusComplete)
                {
                    if (chorusRoutine != null)
                        StopCoroutine(chorusRoutine);
                    chorusRoutine = StartCoroutine(CommunicateChorusRoutine());
                }
                SetHud("Flock arrived — friends communicate!");
            }
            else if (flying <= 0)
            {
                stage = SummonStage.OneBird;
                BeginFriendApproach();
                var firstWave = birdsPerNote != null && birdsPerNote.Length > 0
                    ? Mathf.Max(1, birdsPerNote[0])
                    : 1;
                SummonNearestCompanions(firstWave, noteHz, noteIndex);
                SetHud("Your bird friend flies in. Imitate the melody again.");
            }
            else
            {
                stage = SummonStage.ThreeBirds;
                var need = Mathf.Max(1, 3 - flying);
                SummonNearestCompanions(need, noteHz, noteIndex);
                SetHud("Three friends are here. Sing once more to call the flock.");
            }

            if (melodyHz != null && stepIndex < melodyHz.Length)
                PlayReference();

            taskManager?.ReportScore(stepScore);
        }

        void EnsureSummonPlan()
        {
            if (melodyHz == null || melodyHz.Length < 3)
                melodyHz = new[] { 330f, 392f, 440f };
        }

        void PlaySuccessChirpDelayed()
        {
            successChirp?.PlaySuccessBurst();
        }

        void RememberFriendHome()
        {
            var friend = BirdSceneRoles.Friend;
            if (friend == null)
                return;

            friendHomePosition = friend.position;
            friendHomeRotation = friend.rotation;
            friendHomeScale = friend.localScale;
            friendHomeSaved = true;
            friendHoldingView = false;
        }

        void BeginFriendApproach()
        {
            var friend = BirdSceneRoles.Friend;
            if (friend == null || friendFlightRoutine != null || friendHoldingView)
                return;

            var animator = friend.GetComponent<Animator>();
            ParrotEmbodimentWiring.RestorePackAnimatorForFlight(animator);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                FlockParrotSetup.ResetFlightBools(animator);
                animator.speed = 1f;
                animator.SetBool("takeoff", true);
            }

            foreach (var renderer in friend.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.updateWhenOffscreen = true;

            friendFlightRoutine = StartCoroutine(FriendApproachRoutine(friend, animator));
        }

        IEnumerator FriendApproachRoutine(Transform friend, Animator animator)
        {
            var view = ResolveViewTransform();
            var distantStart = FriendViewPoint(18f, 2.8f);
            var gather = FriendViewPoint(6.2f, 0.25f);
            if (view != null)
            {
                var side = view.right;
                side.y = 0f;
                if (side.sqrMagnitude > 0.0001f)
                    distantStart += side.normalized * 2.6f;
            }

            var inbound = gather - distantStart;
            inbound.y = 0f;
            var startRot = inbound.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(inbound.normalized, Vector3.up)
                : friend.rotation;
            friend.SetPositionAndRotation(distantStart, startRot);

            yield return new WaitForSeconds(0.28f);
            if (animator != null)
            {
                animator.SetBool("takeoff", false);
                animator.SetBool("fly", true);
                animator.Play("fly", 0, 0f);
            }

            var start = friend.position;
            var elapsed = 0f;
            const float duration = 4.6f;
            while (elapsed < duration && friend != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, duration));
                var target = FriendViewPoint(6.2f, 0.25f);
                friend.position = Vector3.Lerp(start, target, t);
                var travel = target - start;
                travel.y = 0f;
                if (travel.sqrMagnitude > 0.001f)
                {
                    var face = Quaternion.LookRotation(travel.normalized, Vector3.up);
                    friend.rotation = Quaternion.Slerp(startRot, face, t);
                }

                yield return null;
            }

            if (animator != null)
            {
                animator.SetBool("fly", false);
                animator.SetBool("glide", true);
            }

            friendHoldingView = true;
            friendFlightRoutine = null;
        }

        void LateUpdate()
        {
            if (!enabled || !friendHoldingView || !TaskActivity.IsActive("call_friends"))
                return;

            var friend = BirdSceneRoles.Friend;
            if (friend == null)
                return;

            var target = FriendViewPoint(6.4f, 0.35f);
            var follow = 1f - Mathf.Exp(-6f * Time.deltaTime);
            friend.position = Vector3.Lerp(friend.position, target, follow);

            var view = ResolveViewTransform();
            if (view == null)
                return;

            var toView = view.position - friend.position;
            toView.y = 0f;
            if (toView.sqrMagnitude > 0.001f)
            {
                var face = Quaternion.LookRotation(toView.normalized, Vector3.up);
                friend.rotation = Quaternion.Slerp(friend.rotation, face, follow);
            }
        }

        void RestoreFriendHome()
        {
            friendHoldingView = false;
            if (friendFlightRoutine != null)
            {
                StopCoroutine(friendFlightRoutine);
                friendFlightRoutine = null;
            }

            var friend = BirdSceneRoles.Friend;
            if (friend == null || !friendHomeSaved)
                return;

            friend.SetPositionAndRotation(friendHomePosition, friendHomeRotation);
            friend.localScale = friendHomeScale;
            ParrotEmbodimentWiring.DisablePackIdleAnimation(friend.GetComponent<Animator>());
        }

        static Vector3 FriendViewPoint(float distance, float height)
        {
            var view = ResolveViewTransform();
            if (view == null)
                return Vector3.zero;

            var forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();
            return view.position + forward * distance + Vector3.up * height;
        }

        static Transform ResolveViewTransform()
        {
            if (CallFriendsCompanionLayout.TryGetPlayerViewAnchor(out var anchor, out _))
                return anchor;
            return Camera.main != null ? Camera.main.transform : null;
        }

        void SpawnIdleCompanions()
        {
            if (!CallFriendsCompanionLayout.TryGetPlayerViewAnchor(out var anchor, out var flatForward))
                return;

            var settings = new CallFriendsCompanionLayout.PerchSettings
            {
                forwardMin = perchForwardMin,
                forwardMax = perchForwardMax,
                lateralSpread = perchLateralSpread,
                heightMin = perchHeightMin,
                heightMax = perchHeightMax,
                fanAngleDegrees = 16f
            };

            var count = Mathf.Clamp(initialCompanionCount, 1, 10);
            var template = BirdSceneRoles.Friend != null ? BirdSceneRoles.Friend : transform;

            for (var i = 0; i < count; i++)
            {
                var bird = CreateCompanionInstance();
                if (bird == null)
                    continue;

                var pos = CallFriendsCompanionLayout.SamplePerchedPosition(
                    anchor, flatForward, i, count, sessionSeed, settings);
                var facing = CallFriendsCompanionLayout.FacingToward(anchor, pos, i, sessionSeed);

                bird.name = $"CallFriend_{i + 1}";
                var behaviour = bird.GetComponent<CallFriendsCompanionBehaviour>()
                                  ?? bird.AddComponent<CallFriendsCompanionBehaviour>();
                behaviour.ConfigureScale(flockScale);
                behaviour.InitializePerched(template, i, pos, facing, flockScale);
                companions.Add(behaviour);
            }
        }

        void SummonNearestCompanions(int count, float noteHz, int noteIndex)
        {
            ResolveOrbitCenter();
            if (orbitCenter == null)
                return;

            var perched = companions
                .Where(c => c != null && c.IsPerched)
                .OrderBy(c => c.DistanceTo(orbitCenter.position))
                .Take(count)
                .ToList();

            for (var i = 0; i < perched.Count; i++)
                perched[i].RespondToCall(orbitCenter, noteHz, library, noteIndex * 10 + i);
        }

        void SummonAllRemaining(float noteHz)
        {
            ResolveOrbitCenter();
            if (orbitCenter == null)
                return;

            var remaining = companions.Where(c => c != null && c.IsPerched).ToList();
            for (var i = 0; i < remaining.Count; i++)
                remaining[i].RespondToCall(orbitCenter, noteHz, library, 20 + i);
        }

        IEnumerator CommunicateChorusRoutine()
        {
            yield return new WaitForSeconds(0.45f);

            var audio = BirdExperienceAudio.Ensure();
            if (audio != null && audio.HasMelodyClip)
            {
                audio.PlayMelody(true);
            }
            else if (melodyHz != null)
            {
                for (var note = 0; note < melodyHz.Length; note++)
                {
                    library?.PlayNearest(source, melodyHz[note], 1f, 0f);
                    yield return new WaitForSeconds(0.55f);
                }
            }

            var idx = 0;
            foreach (var companion in companions)
            {
                if (companion == null || companion.IsPerched)
                    continue;
                companion.PlayAnswerCall(library, 0.05f + idx * 0.1f);
                idx++;
            }

            chorusComplete = true;
            chorusRoutine = null;
            SetHud("Flock gathered! Friends say goodbye soon — press 6 for Reflection.");
        }

        GameObject CreateCompanionInstance()
        {
            if (flockBirdPrefab != null)
                return Instantiate(flockBirdPrefab);

            var template = BirdSceneRoles.Friend;
            if (template == null)
                return null;

            var clone = Instantiate(template.gameObject);
            clone.SetActive(false);
            clone.name = "CallFriend";
            return clone;
        }

        void ResolveOrbitCenter()
        {
            if (CallFriendsCompanionLayout.TryGetPlayerViewAnchor(out var anchor, out _))
                orbitCenter = anchor;
            else if (BirdSceneRoles.Player != null)
                orbitCenter = BirdSceneRoles.Player;
            else
                orbitCenter = transform;
        }

        void ClearCompanions()
        {
            foreach (var companion in companions)
            {
                if (companion != null)
                    Destroy(companion.gameObject);
            }
            companions.Clear();
        }

        void PlayReference()
        {
            var audio = BirdExperienceAudio.Ensure();
            if (audio != null && audio.HasMelodyClip)
            {
                audio.PlayMelody(true);
                return;
            }

            if (melodyHz == null || stepIndex >= melodyHz.Length)
                return;
            library?.PlayNearest(source, melodyHz[stepIndex], 1f, 0f);
        }

        void EnsureSource()
        {
            if (source != null)
                return;
            var friend = BirdSceneRoles.Friend;
            var host = friend != null ? friend.gameObject : gameObject;
            source = host.GetComponent<AudioSource>() ?? host.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        void EnsureFriendVisuals()
        {
            if (friendVisuals != null)
                return;
            var friend = BirdSceneRoles.Friend;
            var host = friend != null ? friend.gameObject : gameObject;
            friendVisuals = host.GetComponent<ParrotMatchVisuals>() ?? host.AddComponent<ParrotMatchVisuals>();
            friendVisuals.BindFromScene();
        }

        static string NoteLabel(float hz)
        {
            if (hz < 360f) return "E4";
            if (hz < 420f) return "G4";
            return "A4";
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
