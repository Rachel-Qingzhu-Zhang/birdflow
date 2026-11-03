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
    /// <summary>
    /// Same one-note call and response as singing. Each matched note brings one bird.
    /// The phrase repeats until every companion has flown in.
    /// Arrival is visual: no forest sting, no chorus, no extra chirp.
    /// </summary>
    public class CallFriendsTask : MonoBehaviour
    {
        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        VoiceAnalyzer analyzer;

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
        float listenUntil;
        float nextPromptAt;
        bool guideReady;
        bool responding;
        bool flockArrived;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            demonstrator = FindAnyObjectByType<BirdFriendDemonstrator>();
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
            listenUntil = 0f;
            nextPromptAt = 0f;
            guideReady = false;
            responding = false;
            flockArrived = false;
            sessionSeed = Random.Range(0, 100000);
            ResolveOrbitCenter();
            SpawnIdleCompanions();
            demonstrator?.SetPattern(BirdFriendDemonstrator.BreathPattern.PatternA4x4);
            demonstrator?.RestartPattern();
            BeginListen();
            SingingReplayButton.Show(ReplayCurrentNote);
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            ClearCompanions();
            SceneGuide.ClearPhase();
            SingingReplayButton.Hide();
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("call_friends"))
                return;

            if (AvianInput.WasReplayPressedThisFrame())
            {
                ReplayCurrentNote();
                return;
            }

            if (flockArrived)
            {
                AvianUiCopy.ShowSingingCue("friends", "");
                return;
            }

            if (!guideReady && nextPromptAt <= 0f)
                BeginListen();

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                AdvanceSummon(1f);
                Debug.Log("[AvianVoice] Call friends cheat. Step=" + stepIndex);
                return;
            }

            if (nextPromptAt > 0f)
            {
                if (Time.time >= nextPromptAt)
                {
                    nextPromptAt = 0f;
                    BeginListen();
                }

                return;
            }

            if (analyzer == null)
                return;

            if (Time.time < listenUntil)
            {
                responding = false;
                ShowCue("listen", CurrentNote());
                return;
            }

            if (!responding)
            {
                responding = true;
                BirdVoiceMapper.Find()?.SetFollowVoice(true);
            }

            var target = CurrentNote();
            var v = analyzer.Current;
            var pitchMatch = v.voiced > 0.5f
                ? BirdSongPhrase.Match(v.pitchHz, target, toleranceHz)
                : 0f;

            score = Mathf.Lerp(score, pitchMatch, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(pitchMatch);
            friendVisuals?.ApplyMatch(pitchMatch);
            ShowCue("call", target);

            if (pitchMatch >= 0.75f)
                matchHold += Time.deltaTime;
            else
                matchHold = 0f;

            if (matchHold >= matchHoldSeconds)
                AdvanceSummon(pitchMatch);
        }

        void AdvanceSummon(float stepScore)
        {
            matchHold = 0f;
            friendVisuals?.PulsePerfect();
            BirdVoiceMapper.Find()?.SetFollowVoice(false);

            var noteIndex = Mathf.Clamp(stepIndex, 0, BirdSongPhrase.Notes.Length - 1);
            stepIndex = (stepIndex + 1) % BirdSongPhrase.Notes.Length;
            SummonNearestCompanions(1, noteIndex);

            var perchedLeft = companions.Count(c => c != null && c.IsPerched);
            if (perchedLeft == 0)
            {
                flockArrived = true;
                responding = false;
                guideReady = true;
                nextPromptAt = 0f;
                ShowCue("friends", 0f);
            }
            else
                HoldForNextNote();

            taskManager?.ReportScore(stepScore);
        }

        void ReplayCurrentNote()
        {
            var notes = BirdSongPhrase.Notes;
            if (flockArrived)
            {
                var last = (stepIndex + notes.Length - 1) % notes.Length;
                BirdVoiceMapper.Find()?.PlayGuideNote(notes[last], BirdSongPhrase.GuideSeconds);
                return;
            }

            nextPromptAt = 0f;
            matchHold = 0f;
            responding = false;
            BeginListen();
        }

        void HoldForNextNote()
        {
            responding = false;
            guideReady = false;
            nextPromptAt = Time.time + BirdSongPhrase.PauseBetweenNotes;
            ShowCue("coming", 0f);
        }

        void BeginListen()
        {
            var mapper = BirdVoiceMapper.Find();
            if (mapper == null || stepIndex < 0 || stepIndex >= BirdSongPhrase.Notes.Length)
            {
                guideReady = false;
                return;
            }

            var note = CurrentNote();
            mapper.PlayGuideNote(note, BirdSongPhrase.GuideSeconds);
            listenUntil = Time.time + BirdSongPhrase.ListenSeconds;
            responding = false;
            guideReady = true;
            matchHold = 0f;
            ShowCue("listen", note);
        }

        float CurrentNote()
        {
            var notes = BirdSongPhrase.Notes;
            return notes[Mathf.Clamp(stepIndex, 0, notes.Length - 1)];
        }

        void ShowCue(string kind, float hz)
        {
            var name = hz > 0f ? BirdSongPhrase.NoteName(hz) : "";
            AvianUiCopy.ShowSingingCue(kind, name);
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(string.IsNullOrEmpty(name) ? kind : name);
        }

        void SummonNearestCompanions(int count, int noteIndex)
        {
            ResolveOrbitCenter();
            if (orbitCenter == null || count <= 0)
                return;

            var perched = companions
                .Where(c => c != null && c.IsPerched)
                .OrderBy(c => c.DistanceTo(orbitCenter.position))
                .Take(count)
                .ToList();

            for (var i = 0; i < perched.Count; i++)
                perched[i].RespondToCall(orbitCenter, noteIndex * 10 + i);
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

        void EnsureFriendVisuals()
        {
            if (friendVisuals != null)
                return;

            var bird = BirdSceneRoles.Player;
            var host = bird != null ? bird.gameObject : gameObject;
            friendVisuals = host.GetComponent<ParrotMatchVisuals>() ?? host.AddComponent<ParrotMatchVisuals>();
            friendVisuals.BindFromScene();
        }
    }
}
