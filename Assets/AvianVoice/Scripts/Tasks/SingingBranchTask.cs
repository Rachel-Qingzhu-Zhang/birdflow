using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Tasks
{
    public class SingingBranchTask : MonoBehaviour
    {
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
        float[] melodyHz = { 330f, 392f, 440f, 494f };

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
        int stepIndex;
        float flapUntil;
        float friendSingUntil;
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
            if (library == null)
                library = FindAnyObjectByType<BirdCallLibrary>();
            if (successChirp == null)
                successChirp = FindAnyObjectByType<SuccessChirpBurst>();
            EnsureSource();
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
            pitchCorrect = false;
            playerVoiced = false;
            BirdSceneRoles.SetFriendVisible(true);
            BirdSceneRoles.EnsurePlayerBirdVisible();
            demonstratorPause = FindAnyObjectByType<BirdFriendDemonstrator>();
            demonstratorPause?.SetTeachingActive(false);
            BindPoses();
            EnsureFriendVisuals();
            PlayStepReference();
            SetHud("Listen, then hold the same note.");
        }

        BirdFriendDemonstrator demonstratorPause;

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            friendPose?.ClearPerformancePose();
            playerPose?.ClearPerformancePose();
            BirdSceneRoles.SetFriendVisible(false);
            demonstratorPause?.SetTeachingActive(true);
            BirdExperienceAudio.Ensure()?.StopAll();
        }

        void LateUpdate()
        {
            if (!enabled || !TaskActivity.IsActive("singing"))
                return;

            BindPoses();
            var audio = BirdExperienceAudio.Ensure();
            if (audio != null && audio.IsReferencePlaying)
                friendSingUntil = Mathf.Max(friendSingUntil, Time.time + 0.2f);
            var friendSinging = Time.time < friendSingUntil;
            var flap = pitchCorrect || Time.time < flapUntil;
            friendPose?.SetPerformancePose(friendSinging ? 0.92f : 0.08f, flap);
            playerPose?.SetPerformancePose(playerVoiced ? 0.5f : 0.08f, false);
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive("singing"))
                return;

            if (enableDebugCheat && AvianInput.WasTaskCheatPressedThisFrame())
            {
                AdvanceStep(1f);
                Debug.Log("[AvianVoice] Singing cheat.");
                return;
            }

            if (analyzer == null || stepIndex >= melodyHz.Length)
                return;

            var v = analyzer.Current;
            var target = melodyHz[stepIndex];
            var pitchMatch = 0f;
            if (v.voiced > 0.5f && v.pitchHz > 0f)
            {
                var error = Mathf.Abs(v.pitchHz - target);
                pitchMatch = Mathf.Clamp01(1f - error / toleranceHz);
            }

            playerVoiced = v.voiced > 0.5f;
            pitchCorrect = pitchMatch >= 0.75f;
            var combined = pitchMatch;
            score = Mathf.Lerp(score, combined, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(combined);
            friendVisuals?.ApplyMatch(combined);

            if (pitchCorrect)
                matchHold += Time.deltaTime;
            else
                matchHold = 0f;

            if (matchHold >= matchHoldSeconds)
                AdvanceStep(combined);
            else
                SetHud($"Hold {NoteLabel(target)} — pitch {v.pitchHz:0}/{target:0} Hz");
        }

        void AdvanceStep(float stepScore)
        {
            matchHold = 0f;
            flapUntil = Time.time + 1.5f;
            stepIndex++;
            if (stepIndex >= melodyHz.Length)
            {
                if (BirdExperienceAudio.Ensure()?.PlayForestReward() != true)
                    successChirp?.PlaySuccessBurst();
                friendVisuals?.PulsePerfect();
                SetHud("Melody complete!");
                return;
            }

            PlayStepReference();
            SetHud($"Next: hold {NoteLabel(melodyHz[stepIndex])}.");
            taskManager?.ReportScore(stepScore);
        }

        void PlayStepReference()
        {
            if (stepIndex >= melodyHz.Length)
                return;

            var target = melodyHz[stepIndex];
            var audio = BirdExperienceAudio.Ensure();
            var singer = BirdSceneRoles.Friend != null ? BirdSceneRoles.Friend : BirdSceneRoles.Player;
            var rootHz = melodyHz[0] > 1f ? melodyHz[0] : target;
            if (audio != null && audio.HasPitchClip)
                audio.PlayPitchStepNear(singer, stepIndex, melodyHz.Length, target / rootHz);

            var cue = audio != null ? audio.CueSource : NoteSource(singer);
            library?.PlayTeachingCue(cue, target, 1f);
            var phrase = audio != null && audio.IsReferencePlaying
                ? Mathf.Max(1.15f, audio.ReferenceSecondsRemaining)
                : 1.15f;
            friendSingUntil = Time.time + phrase;
            friendVisuals?.PulsePerfect();
        }

        AudioSource NoteSource(Transform singer)
        {
            var host = singer != null ? singer : transform;
            var existing = host.Find("FriendNote");
            var go = existing != null ? existing.gameObject : new GameObject("FriendNote");
            if (existing == null)
            {
                go.transform.SetParent(host, false);
                go.transform.localPosition = new Vector3(0f, 0.2f, 0.05f);
            }

            var note = go.GetComponent<AudioSource>() ?? go.AddComponent<AudioSource>();
            note.playOnAwake = false;
            note.spatialBlend = 0f;
            note.dopplerLevel = 0f;
            note.volume = 1f;
            return note;
        }

        void EnsureSource()
        {
            if (source != null)
                return;
            var bird = BirdSceneRoles.Player;
            var host = bird != null ? bird.gameObject : gameObject;
            source = host.GetComponent<AudioSource>() ?? host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.55f;
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

        static string NoteLabel(float hz)
        {
            if (hz < 350f) return "E4";
            if (hz < 420f) return "G4";
            if (hz < 470f) return "A4";
            return "B4";
        }

        void SetHud(string message)
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            taskUi?.SetProgressInstruction(message);
        }
    }
}
