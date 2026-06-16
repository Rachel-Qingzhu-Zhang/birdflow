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
        float perchForwardMin = 4f;

        [SerializeField]
        float perchForwardMax = 9f;

        [SerializeField]
        float perchLateralSpread = 5f;

        [SerializeField]
        float perchHeightMin = 0.4f;

        [SerializeField]
        float perchHeightMax = 1.6f;

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
        int sessionSeed;
        SummonStage stage = SummonStage.None;
        Coroutine chorusRoutine;
        bool chorusComplete;

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
            stage = SummonStage.None;
            chorusComplete = false;
            sessionSeed = Random.Range(0, 100000);
            ResolveOrbitCenter();
            SpawnIdleCompanions();
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternA4x4);
            demonstrator?.RestartPattern();
            PlayReference();
            SetHud("Call friends: sing with Pattern A breath.");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            CancelInvoke(nameof(PlaySuccessChirpDelayed));
            if (chorusRoutine != null)
                StopCoroutine(chorusRoutine);
            chorusRoutine = null;
            ClearCompanions();
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("call_friends"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                AdvanceSummon(1f);
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
            var combined = pitchMatch * 0.6f + breathSync * 0.4f;
            score = Mathf.Lerp(score, combined, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(pitchMatch);
            SessionScoreCache.RecordSynchronization(breathSync);
            friendVisuals?.ApplyMatch(combined);

            if (pitchMatch > 0.7f && breathSync >= breathSyncMin)
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
            Invoke(nameof(PlaySuccessChirpDelayed), 0.2f);

            var noteIndex = stepIndex;
            stepIndex++;
            var noteHz = melodyHz[Mathf.Clamp(noteIndex, 0, melodyHz.Length - 1)];
            var summonCount = birdsPerNote != null && noteIndex < birdsPerNote.Length
                ? birdsPerNote[noteIndex]
                : 1;

            if (stepIndex == 1)
            {
                stage = SummonStage.OneBird;
                SummonNearestCompanions(summonCount, noteHz, noteIndex);
            }
            else if (stepIndex == 2)
            {
                stage = SummonStage.ThreeBirds;
                SummonNearestCompanions(summonCount, noteHz, noteIndex);
                SummonAllRemaining(noteHz);
                stage = SummonStage.Flock;
                if (!chorusComplete)
                {
                    if (chorusRoutine != null)
                        StopCoroutine(chorusRoutine);
                    chorusRoutine = StartCoroutine(CommunicateChorusRoutine());
                }
                SetHud("Flock arrived — friends communicate!");
            }

            if (stepIndex < melodyHz.Length)
                PlayReference();

            taskManager?.ReportScore(stepScore);
        }

        void PlaySuccessChirpDelayed()
        {
            successChirp?.PlaySuccessBurst();
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
                fanAngleDegrees = 150f
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
            yield return new WaitForSeconds(1.5f);

            for (var note = 0; note < melodyHz.Length; note++)
            {
                library?.PlayNearest(source, melodyHz[note], 1f, 0f);
                var idx = 0;
                foreach (var companion in companions)
                {
                    if (companion == null || companion.IsPerched)
                        continue;
                    companion.PlayAnswerCall(library, 0.08f + idx * 0.12f);
                    idx++;
                }
                yield return new WaitForSeconds(0.6f);
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
            if (stepIndex >= melodyHz.Length)
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

        static string NoteLabel(float hz) => hz < 360f ? "E4" : "G4";

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
