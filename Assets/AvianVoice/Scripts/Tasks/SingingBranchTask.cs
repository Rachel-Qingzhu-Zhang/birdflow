using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Physiology;
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
        BreathSimulator breath;

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
        float deepBreathThreshold = 0.7f;

        [SerializeField]
        bool enableDebugCheat = true;

        ParrotMatchVisuals friendVisuals;
        TaskFlowUI taskUi;
        int stepIndex;
        float matchHold;
        float score;
        float stablePitch;
        bool imitateClip;
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
            stablePitch = 0f;
            imitateClip = BirdExperienceAudio.Ensure()?.HasPitchClip == true;
            PlayStepReference();
            SetHud($"Orange bird sings {NoteLabel(melodyHz[0])} first — imitate the note and keep breath (Space).");
        }

        void OnDisable()
        {
            friendVisuals?.ApplyMatch(0f);
            BirdExperienceAudio.Ensure()?.StopAll();
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

            if (analyzer == null || breath == null || stepIndex >= melodyHz.Length)
                return;

            var v = analyzer.Current;
            var target = melodyHz[stepIndex];
            var pitchMatch = 0f;
            if (v.voiced > 0.5f && v.pitchHz > 0f)
            {
                var error = Mathf.Abs(v.pitchHz - target);
                pitchMatch = Mathf.Clamp01(1f - error / toleranceHz);
            }

            var breathOk = breath.Current.depth > 0.25f;
            var richBreath = breath.Current.depth >= deepBreathThreshold ? 1f : 0.6f;
            var stable = false;
            if (v.voiced > 0.5f && v.pitchHz > 90f && v.pitchHz < 950f)
            {
                if (stablePitch <= 0f)
                    stablePitch = v.pitchHz;
                stable = Mathf.Abs(v.pitchHz - stablePitch) <= 50f;
                if (!stable)
                    stablePitch = v.pitchHz;
            }
            else
            {
                stablePitch = 0f;
            }

            var combined = pitchMatch * (breathOk ? richBreath : 0.3f);
            if (imitateClip && stable)
                combined = Mathf.Max(combined, breathOk ? richBreath : 0.3f);
            score = Mathf.Lerp(score, combined, Time.deltaTime * 2f);
            taskManager?.ReportScore(score);
            SessionScoreCache.RecordVoice(combined);
            friendVisuals?.ApplyMatch(combined);

            var targetHit = pitchMatch > 0.75f;
            var imitateHit = imitateClip && stable;
            if ((targetHit || imitateHit) && breathOk)
                matchHold += Time.deltaTime;
            else
                matchHold = 0f;

            if (matchHold >= matchHoldSeconds)
                AdvanceStep(combined);
            else
                SetHud($"Hold {NoteLabel(target)} — pitch {v.pitchHz:0}/{target:0} Hz, breath {breath.Current.depth:0.00}");
        }

        void AdvanceStep(float stepScore)
        {
            matchHold = 0f;
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
            var rootHz = melodyHz[0] > 1f ? melodyHz[0] : target;
            if (audio != null && audio.HasPitchClip)
            {
                audio.PlayPitchStep(stepIndex, melodyHz.Length, target / rootHz);
                friendVisuals?.PulsePerfect();
                return;
            }

            library?.PlayNearest(source, target, 1f, 0f);
            friendVisuals?.PulsePerfect();
        }

        void EnsureSource()
        {
            if (source != null)
                return;
            var friend = BirdSceneRoles.Friend;
            var host = friend != null ? friend.gameObject : gameObject;
            source = host.GetComponent<AudioSource>() ?? host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.55f;
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
