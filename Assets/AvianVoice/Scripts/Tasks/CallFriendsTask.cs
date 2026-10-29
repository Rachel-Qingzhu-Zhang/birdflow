using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
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
        float listenUntil;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
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
            EnsureSummonPlan();
            ResolveOrbitCenter();
            SpawnIdleCompanions();
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternA4x4);
            demonstrator?.RestartPattern();
            BeginListen();
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            CancelInvoke(nameof(PlaySuccessChirpDelayed));
            if (chorusRoutine != null)
                StopCoroutine(chorusRoutine);
            chorusRoutine = null;
            BirdExperienceAudio.Ensure()?.StopAll();
            ClearCompanions();
            SceneGuide.ClearPhase();
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

            if (analyzer == null)
                return;

            if (stepIndex >= melodyHz.Length && stage == SummonStage.Flock)
            {
                SceneGuide.SetPhase("Your friends are here.");
                return;
            }

            if (Time.time < listenUntil)
            {
                SceneGuide.SetPhase("Listen to the melody.");
                return;
            }

            SceneGuide.SetPhase("Your turn. Sing it back.");

            var target = melodyHz[Mathf.Min(stepIndex, melodyHz.Length - 1)];
            var v = analyzer.Current;
            var windowHz = Mathf.Max(toleranceHz, 70f);
            var pitchMatch = 0f;
            var nearNote = false;
            if (v.voiced > 0.5f && v.pitchHz > 0f)
            {
                var error = Mathf.Abs(v.pitchHz - target);
                pitchMatch = Mathf.Clamp01(1f - error / windowHz);
                nearNote = error <= windowHz;
            }

            var combined = pitchMatch;
            score = Mathf.Lerp(score, combined, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(combined);
            friendVisuals?.ApplyMatch(combined);

            if (nearNote)
                matchHold += Time.deltaTime;
            else
                matchHold = 0f;

            var holdNeeded = Mathf.Min(matchHoldSeconds, 0.32f);
            if (matchHold >= holdNeeded)
                AdvanceSummon(combined);
            else
                SetHud($"Sing {NoteLabel(target)} to call the birds");
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
            SummonNearestCompanions(1, noteHz, noteIndex);

            var perchedLeft = companions.Count(c => c != null && c.IsPerched);
            var melodyDone = melodyHz == null || stepIndex >= melodyHz.Length;
            if (melodyDone || perchedLeft == 0)
            {
                stage = SummonStage.Flock;
                if (!chorusComplete)
                {
                    if (chorusRoutine != null)
                        StopCoroutine(chorusRoutine);
                    chorusRoutine = StartCoroutine(CommunicateChorusRoutine());
                }
                SetHud("Your friends are here.");
            }
            else
            {
                stage = perchedLeft > 1 ? SummonStage.OneBird : SummonStage.ThreeBirds;
                SetHud("A friend flies in. Sing the next note.");
            }

            if (melodyHz != null && stepIndex < melodyHz.Length && perchedLeft > 0)
                BeginListen();

            taskManager?.ReportScore(stepScore);
        }

        void EnsureSummonPlan()
        {
            var count = Mathf.Clamp(initialCompanionCount, 1, 10);
            if (melodyHz != null && melodyHz.Length == count)
                return;

            var cycle = new[] { 330f, 392f, 440f, 494f, 523f, 587f, 659f };
            melodyHz = new float[count];
            for (var i = 0; i < count; i++)
                melodyHz[i] = cycle[i % cycle.Length];
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
                fanAngleDegrees = 16f
            };

            var count = Mathf.Clamp(initialCompanionCount, 1, 10);
            var template = BirdSceneRoles.Player != null ? BirdSceneRoles.Player : transform;

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
                audio.PlayMelodyNear(HearPoint(), true);
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

            var template = BirdSceneRoles.Player;
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

        void BeginListen()
        {
            PlayReference();
            var audio = BirdExperienceAudio.Ensure();
            var wait = 0.7f;
            if (stepIndex == 0 && audio != null && audio.IsReferencePlaying)
                wait = Mathf.Max(0.45f, audio.ReferenceSecondsRemaining + 0.08f);
            listenUntil = Time.time + wait;
            SceneGuide.SetPhase(stepIndex == 0 ? "Listen to the melody." : "Listen.");
        }

        Transform HearPoint()
        {
            if (BirdSceneRoles.Player != null)
                return BirdSceneRoles.Player;
            return Camera.main != null ? Camera.main.transform : transform;
        }

        void PlayReference()
        {
            var audio = BirdExperienceAudio.Ensure();
            if (stepIndex == 0 && audio != null && audio.HasMelodyClip)
            {
                audio.PlayMelodyNear(HearPoint(), true);
                return;
            }

            if (melodyHz == null || stepIndex >= melodyHz.Length)
                return;
            var cue = audio != null ? audio.CueSource : source;
            library?.PlayTeachingCue(cue, melodyHz[stepIndex], 1f);
        }

        void EnsureSource()
        {
            if (source != null)
                return;
            var bird = BirdSceneRoles.Player;
            var host = bird != null ? bird.gameObject : gameObject;
            source = host.GetComponent<AudioSource>() ?? host.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        void EnsureFriendVisuals()
        {
            if (friendVisuals != null)
                return;
            var bird = BirdSceneRoles.Player;
            var host = bird != null ? bird.gameObject : gameObject;
            friendVisuals = host.GetComponent<ParrotMatchVisuals>() ?? host.AddComponent<ParrotMatchVisuals>();
            friendVisuals.BindFromScene();
        }

        static string NoteLabel(float hz)
        {
            if (hz < 360f) return "E4";
            if (hz < 420f) return "G4";
            if (hz < 470f) return "A4";
            if (hz < 510f) return "B4";
            if (hz < 555f) return "C5";
            if (hz < 620f) return "D5";
            return "E5";
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
