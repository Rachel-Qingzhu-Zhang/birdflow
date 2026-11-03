using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    /// <summary>
    /// The friend bird sings one note. After a short silence, the player sings it back
    /// in the same synth voice. Matching a note leads to the next note, with no reward sting.
    /// </summary>
    public class SingingBranchTask : MonoBehaviour
    {
        [SerializeField]
        TaskManager taskManager;

        [SerializeField]
        VoiceAnalyzer analyzer;

        [SerializeField]
        float toleranceHz = 40f;

        [SerializeField]
        float matchHoldSeconds = 0.45f;

        [SerializeField]
        bool enableDebugCheat = true;

        ParrotMatchVisuals friendVisuals;
        FriendBreathVisual friendPose;
        ParrotBreathEmbodiment playerPose;
        TaskFlowUI taskUi;
        BirdFriendDemonstrator demonstratorPause;
        int stepIndex;
        float flapUntil;
        float friendSingUntil;
        float listenUntil;
        float nextPromptAt;
        bool guideReady;
        bool responding;
        bool playerVoiced;
        bool pitchCorrect;
        float matchHold;
        float score;

        void Start()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            EnsureFriendVisuals();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive("singing"))
                return;

            stepIndex = 0;
            matchHold = 0f;
            score = 0f;
            flapUntil = 0f;
            friendSingUntil = 0f;
            listenUntil = 0f;
            nextPromptAt = 0f;
            guideReady = false;
            responding = false;
            pitchCorrect = false;
            playerVoiced = false;
            BirdSceneRoles.SetFriendVisible(true);
            BirdSceneRoles.EnsurePlayerBirdVisible();
            demonstratorPause = FindAnyObjectByType<BirdFriendDemonstrator>();
            demonstratorPause?.SetTeachingActive(false);
            BindPoses();
            EnsureFriendVisuals();
            BeginListen();
            SingingReplayButton.Show(this);
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            friendPose?.ClearPerformancePose();
            playerPose?.ClearPerformancePose();
            BirdSceneRoles.SetFriendVisible(false);
            demonstratorPause?.SetTeachingActive(true);
            SceneGuide.ClearPhase();
            SingingReplayButton.Hide();
        }

        void LateUpdate()
        {
            if (!enabled || !TaskActivity.IsActive("singing"))
                return;

            BindPoses();
            var friendSinging = Time.time < friendSingUntil;
            var flap = pitchCorrect || Time.time < flapUntil;
            friendPose?.SetPerformancePose(friendSinging ? 0.92f : 0.08f, flap);
            playerPose?.SetPerformancePose(playerVoiced ? 0.5f : 0.08f, false);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("singing"))
                return;

            if (!guideReady && nextPromptAt <= 0f && stepIndex < BirdSongPhrase.Notes.Length)
                BeginListen();

            if (AvianInput.WasReplayPressedThisFrame())
            {
                ReplayMelody();
                return;
            }

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                AdvanceStep(1f);
                Debug.Log("[AvianVoice] Singing cheat.");
                return;
            }

            if (nextPromptAt > 0f)
            {
                playerVoiced = false;
                pitchCorrect = false;
                if (Time.time >= nextPromptAt)
                {
                    nextPromptAt = 0f;
                    BeginListen();
                }

                return;
            }

            if (stepIndex >= BirdSongPhrase.Notes.Length)
                return;

            if (Time.time < listenUntil)
            {
                playerVoiced = false;
                pitchCorrect = false;
                responding = false;
                ShowCue("listen", CurrentNote());
                return;
            }

            if (!responding)
            {
                responding = true;
                BirdVoiceMapper.Find()?.SetFollowVoice(true);
            }

            if (analyzer == null)
                return;

            var target = CurrentNote();
            var v = analyzer.Current;
            var pitchMatch = v.voiced > 0.5f
                ? BirdSongPhrase.Match(v.pitchHz, target, toleranceHz)
                : 0f;

            playerVoiced = v.voiced > 0.5f;
            pitchCorrect = pitchMatch >= 0.75f;
            score = Mathf.Lerp(score, pitchMatch, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(pitchMatch);
            friendVisuals?.ApplyMatch(pitchMatch);
            ShowCue("sing", target);

            if (pitchCorrect)
                matchHold += Time.deltaTime;
            else
                matchHold = 0f;

            if (matchHold >= matchHoldSeconds)
                AdvanceStep(pitchMatch);
        }

        void AdvanceStep(float stepScore)
        {
            matchHold = 0f;
            flapUntil = Time.time + 1.5f;
            stepIndex++;
            taskManager?.ReportScore(stepScore);
            if (stepIndex >= BirdSongPhrase.Notes.Length)
            {
                responding = false;
                guideReady = true;
                nextPromptAt = 0f;
                BirdVoiceMapper.Find()?.SetFollowVoice(false);
                friendVisuals?.PulsePerfect();
                ShowCue("done", 0f);
                return;
            }

            friendVisuals?.PulsePerfect();
            HoldForNextNote();
        }

        public void ReplayMelody()
        {
            if (stepIndex >= BirdSongPhrase.Notes.Length)
                stepIndex = 0;

            nextPromptAt = 0f;
            matchHold = 0f;
            BeginListen();
        }

        void HoldForNextNote()
        {
            responding = false;
            guideReady = false;
            playerVoiced = false;
            BirdVoiceMapper.Find()?.SetFollowVoice(false);
            nextPromptAt = Time.time + BirdSongPhrase.PauseBetweenNotes;
            ShowCue("next", 0f);
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
            friendSingUntil = Time.time + BirdSongPhrase.GuideSeconds;
            responding = false;
            guideReady = true;
            matchHold = 0f;
            ShowCue("listen", note);
        }

        float CurrentNote()
        {
            var notes = BirdSongPhrase.Notes;
            var index = Mathf.Clamp(stepIndex, 0, notes.Length - 1);
            return notes[index];
        }

        void ShowCue(string kind, float hz)
        {
            var name = hz > 0f ? BirdSongPhrase.NoteName(hz) : "";
            AvianUiCopy.ShowSingingCue(kind, name);
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(kind == "done"
                ? "Melody complete."
                : (string.IsNullOrEmpty(name) ? "Listen for the next note." : name));
        }

        void BindPoses()
        {
            if (friendPose == null && BirdSceneRoles.Friend != null)
                friendPose = BirdSceneRoles.Friend.GetComponent<FriendBreathVisual>();
            if (playerPose == null && BirdSceneRoles.Player != null)
                playerPose = BirdSceneRoles.Player.GetComponent<ParrotBreathEmbodiment>();
        }

        void EnsureFriendVisuals()
        {
            if (friendVisuals != null)
                return;
            var bird = BirdSceneRoles.Friend != null ? BirdSceneRoles.Friend : BirdSceneRoles.Player;
            var host = bird != null ? bird.gameObject : gameObject;
            friendVisuals = host.GetComponent<ParrotMatchVisuals>() ?? host.AddComponent<ParrotMatchVisuals>();
            friendVisuals.BindFromScene();
        }
    }
}
