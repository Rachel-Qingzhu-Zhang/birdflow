using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.Embodiment.Avatars;
using AvianVoice.Tasks;
using AvianVoice.UI;
using UnityEngine;

namespace AvianVoice.Study
{
    public enum TrialOrder
    {
        /// <summary>All trials of pitch 1, then pitch 2, then pitch 3.</summary>
        Blocked,
        /// <summary>1, 2, 3, 1, 2, 3, ...</summary>
        Interleaved,
        /// <summary>Shuffled, each pitch still exactly trialsPerPitch times.</summary>
        Random
    }

    public enum TrialPhase
    {
        /// <summary>Waiting for the experimenter to start the next trial.</summary>
        Idle,
        /// <summary>The teacher bird is singing the target note.</summary>
        Guide,
        /// <summary>The participant may sing; voice onset / offset are being detected.</summary>
        Response,
        /// <summary>All trials done.</summary>
        Finished
    }

    /// <summary>One singing trial: timestamps of every phase plus summary measures. Written to trials.csv.</summary>
    [Serializable]
    public sealed class TrialRecord
    {
        public int trial;
        public int attempt;
        public int block;
        public int pitchIndex;
        public float targetHz;
        public string note = "";
        public string order = "";
        public string source = "";
        public string avatar = "";
        public string avatarModel = "";
        public string voiceFeedback = "";
        /// <summary>Condition the trial started in (StudyRecorder.ConditionId), also its folder under conditions/.</summary>
        public string condition = "";
        /// <summary>Human body shown ("neutral" / "male" / "female" ...), "" with the bird avatar.</summary>
        public string humanAvatar = "";
        /// <summary>
        /// Pass through the trials of this condition: 1, then 2, 3 ... when the trials were started over
        /// (StudyRecorder.BeginTrialRun). The highest run of a condition is the one to analyse.
        /// </summary>
        public int run = 1;
        public StudyTime start;
        public StudyTime guideStart;
        public StudyTime guideEnd;
        public StudyTime responseStart;
        public StudyTime voiceOnset;
        public StudyTime voiceOffset;
        public StudyTime end;
        public string endReason = "";
        public int voiceSegments;
        public float onsetLatencySeconds = float.NaN;
        public float voicedSeconds;
        public float meanPitchHz = float.NaN;
        public float medianPitchHz = float.NaN;
        public float pitchMatch;
        public float maxLoudness;
        public string segmentFile = "";

        public bool HadVoice => voiceOnset.IsSet;

        public const string CsvHeader =
            "trial,attempt,block,pitch_index,target_hz,note,order,source,avatar,avatar_model,voice_feedback,condition,human_avatar,run,"
            + "t_start_utc,t_start_local,t_start_unity,t_start_dsp,mic_sample_start,out_frame_start,"
            + "t_guide_start_unity,mic_sample_guide_start,t_guide_end_unity,mic_sample_guide_end,"
            + "t_response_start_unity,mic_sample_response_start,"
            + "t_voice_onset_utc,t_voice_onset_unity,mic_sample_voice_onset,"
            + "t_voice_offset_utc,t_voice_offset_unity,mic_sample_voice_offset,"
            + "t_end_utc,t_end_unity,mic_sample_end,out_frame_end,"
            + "end_reason,voice_segments,onset_latency_s,voiced_s,mean_pitch_hz,median_pitch_hz,pitch_match,max_loudness,segment_file";

        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(trial).Append(',').Append(attempt).Append(',').Append(block).Append(',').Append(pitchIndex).Append(',')
              .Append(targetHz.ToString("0.###", c)).Append(',').Append(Csv(note)).Append(',').Append(Csv(order)).Append(',')
              .Append(Csv(source)).Append(',').Append(Csv(avatar)).Append(',').Append(Csv(avatarModel)).Append(',').Append(Csv(voiceFeedback)).Append(',')
              .Append(Csv(condition)).Append(',').Append(Csv(humanAvatar)).Append(',').Append(run).Append(',')
              .Append(Csv(start.utc)).Append(',').Append(Csv(start.local)).Append(',').Append(Num(start.unity)).Append(',').Append(Num(start.dsp)).Append(',')
              .Append(start.micSample).Append(',').Append(start.outFrame).Append(',')
              .Append(Num(guideStart.unity)).Append(',').Append(guideStart.micSample).Append(',')
              .Append(Num(guideEnd.unity)).Append(',').Append(guideEnd.micSample).Append(',')
              .Append(Num(responseStart.unity)).Append(',').Append(responseStart.micSample).Append(',')
              .Append(Csv(voiceOnset.utc)).Append(',').Append(voiceOnset.IsSet ? Num(voiceOnset.unity) : "").Append(',').Append(voiceOnset.IsSet ? voiceOnset.micSample.ToString(c) : "").Append(',')
              .Append(Csv(voiceOffset.utc)).Append(',').Append(voiceOffset.IsSet ? Num(voiceOffset.unity) : "").Append(',').Append(voiceOffset.IsSet ? voiceOffset.micSample.ToString(c) : "").Append(',')
              .Append(Csv(end.utc)).Append(',').Append(Num(end.unity)).Append(',').Append(end.micSample).Append(',').Append(end.outFrame).Append(',')
              .Append(Csv(endReason)).Append(',').Append(voiceSegments).Append(',')
              .Append(Num(onsetLatencySeconds)).Append(',').Append(voicedSeconds.ToString("0.###", c)).Append(',')
              .Append(Num(meanPitchHz)).Append(',').Append(Num(medianPitchHz)).Append(',')
              .Append(pitchMatch.ToString("0.###", c)).Append(',').Append(maxLoudness.ToString("0.###", c)).Append(',')
              .Append(Csv(segmentFile));
            return sb.ToString();
        }

        public JsonLine ToJson()
        {
            return new JsonLine()
                .Add("trial", trial).Add("attempt", attempt).Add("block", block).Add("pitch_index", pitchIndex)
                .Add("target_hz", targetHz).Add("note", note).Add("order", order).Add("source", source).Add("avatar", avatar).Add("avatar_model", avatarModel).Add("voice_feedback", voiceFeedback)
                .Add("condition", condition).Add("human_avatar", humanAvatar).Add("run", run)
                .Add("start", start).Add("guide_start", guideStart).Add("guide_end", guideEnd)
                .Add("response_start", responseStart).Add("voice_onset", voiceOnset).Add("voice_offset", voiceOffset).Add("end", end)
                .Add("end_reason", endReason).Add("voice_segments", voiceSegments)
                .Add("onset_latency_s", onsetLatencySeconds).Add("voiced_s", voicedSeconds)
                .Add("mean_pitch_hz", meanPitchHz).Add("median_pitch_hz", medianPitchHz)
                .Add("pitch_match", pitchMatch).Add("max_loudness", maxLoudness).Add("segment_file", segmentFile);
        }

        static string Num(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "" : v.ToString("R", CultureInfo.InvariantCulture);

        static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public string Summary()
        {
            var c = CultureInfo.InvariantCulture;
            var s = "#" + trial + (attempt > 1 ? " (retry " + attempt + ")" : "") + " " + note + " " + targetHz.ToString("0", c) + " Hz: ";
            if (!HadVoice)
                return s + "no voice (" + endReason + ")";
            s += "voice";
            if (!float.IsNaN(onsetLatencySeconds))
                s += " onset " + onsetLatencySeconds.ToString("0.00", c) + " s";
            s += " voiced " + voicedSeconds.ToString("0.00", c) + " s";
            if (!float.IsNaN(medianPitchHz))
                s += " median " + medianPitchHz.ToString("0", c) + " Hz";
            s += " match " + pitchMatch.ToString("0.00", c) + " (" + endReason + ")";
            return s;
        }
    }

    /// <summary>Voice of the teacher's guide note.</summary>
    public enum TeacherVoice
    {
        /// <summary>Always the bird chirp (the teacher bird is the visible singer in every condition).</summary>
        Bird,
        /// <summary>The human vowel while the human avatar is shown, the bird chirp otherwise.</summary>
        FollowAvatar
    }

    /// <summary>
    /// Singing scene for the study: the experimenter starts each trial, the teacher bird sings one of
    /// three target pitches, the participant sings it back. 3 pitches x trialsPerPitch (10) = 30 trials.
    /// Every phase and the participant's voice onset / offset are timestamped through
    /// <see cref="StudyRecorder"/>; after the last trial the session moves to the rest scene.
    /// Replaces <see cref="SingingBranchTask"/> for the "singing" task id (see <see cref="StudyFlowController"/>).
    ///
    /// A second instance with <see cref="IsPractice"/> runs the "practice" scene: the same task with no
    /// schedule, no end and a pitch the experimenter picks freely (<see cref="PracticePitchHz"/>), used at the
    /// start of the session to explain the task and show the conditions. Practice trials are logged in
    /// events.jsonl (task "practice", practice: true) but written to neither trials.csv nor segments/.
    /// </summary>
    public class SingingTrialTask : MonoBehaviour
    {
        public const string TaskId = "singing";
        public const string PracticeTaskId = "practice";

        [Header("Protocol")]
        [Tooltip("Target pitches in human Hz. The teacher bird sings them transposed into the bird register, and the participant answers in any octave. " +
                 "Default B3 / D4 / F#4 (2026-11-07: E4 / G4 / B4 was too high for men, A3 / C4 / E4 a little low; same three intervals " +
                 "throughout): singable by men, the last one near the top of an untrained male voice; women can answer an octave up.")]
        [SerializeField]
        float[] pitchesHz = (float[])DefaultPitchesHz.Clone();

        /// <summary>B3, D4, F#4 (equal temperament).</summary>
        public static readonly float[] DefaultPitchesHz = { 246.94f, 293.66f, 369.99f };

        [SerializeField, Min(1)]
        int trialsPerPitch = 10;

        [SerializeField]
        TrialOrder order = TrialOrder.Blocked;

        [Tooltip("Seed for Random order (0 = pick from the clock and log it).")]
        [SerializeField]
        int randomSeed;

        [Header("Timing (seconds)")]
        [SerializeField]
        float guideSeconds = 0.9f;

        [SerializeField]
        float gapAfterGuideSeconds = 0.3f;

        [Tooltip("A trial lasts from one experimenter press to the next: no response window, no auto end after the voice " +
                 "stops, and the next press is accepted at any time (also while the guide note is still playing). " +
                 "Off = the trial ends by itself after Response Window Seconds / End After Voice Offset Seconds.")]
        [SerializeField]
        bool trialUntilNextPress = true;

        [Tooltip("Only when Trial Until Next Press is off: maximum time the participant has to answer after the guide note.")]
        [SerializeField]
        float responseWindowSeconds = 5f;

        [Tooltip("Only when Trial Until Next Press is off: the trial ends this long after the participant's voice stops.")]
        [SerializeField]
        float endAfterVoiceOffsetSeconds = 1.0f;

        [SerializeField]
        float onsetHoldSeconds = 0.06f;

        [SerializeField]
        float offsetHoldSeconds = 0.25f;

        [Header("Scoring")]
        [Tooltip("Match window in Hz at the target (any octave). 30 Hz on B3 / D4 / F#4 gives the same window in semitones per note " +
                 "as the original 40 Hz on E4 / G4 / B4 (1.98 / 1.68 / 1.35 st), so pitch_match stays comparable.")]
        [SerializeField]
        float toleranceHz = 30f;

        [Header("Behaviour")]
        [Tooltip("During the response the participant hears their own voice through the condition's feedback: a bird voice with the " +
                 "bird avatar, their own microphone as it is with the human avatar, or nothing (StudyFlowController / Experimenter Panel).")]
        [SerializeField]
        bool participantVoiceFeedback = true;

        [Tooltip("Voice of the teacher's guide note: always the bird, or the human voice while the human avatar is shown.")]
        [SerializeField]
        TeacherVoice teacherVoice = TeacherVoice.Bird;

        [Tooltip("The teacher bird glows cyan while the participant's pitch matches the target (visual feedback on the pitch). " +
                 "Off since 2026-11-12: the participant gets no visual feedback on how well the pitch matches.")]
        [SerializeField]
        bool pitchMatchGlow;

        [Tooltip("Space / R on the PC keyboard also starts the next trial (same key as the old replay).")]
        [SerializeField]
        bool acceptKeyboardStart = true;

        [SerializeField]
        bool autoAdvanceToRest = true;

        [SerializeField]
        float restDelaySeconds = 3f;

        [SerializeField]
        string restTaskId = "reflection";

        [Header("Practice")]
        [Tooltip("This instance runs the practice scene: free pitch, no schedule, no end, nothing written to trials.csv.")]
        [SerializeField]
        bool practiceMode;

        [Tooltip("Pitch of the teacher's note in the practice scene (human Hz), chosen from the experimenter panel.")]
        [SerializeField]
        float practicePitchHz = 246.94f;

        TaskManager taskManager;
        VoiceAnalyzer analyzer;
        StudyRecorder recorder;
        AvatarEmbodimentController avatarController;
        ParrotMatchVisuals friendVisuals;
        FriendBreathVisual friendPose;
        ParrotBreathEmbodiment playerPose;
        BirdFriendDemonstrator demonstratorPause;
        TaskFlowUI taskUi;

        int[] schedule;
        int usedSeed;
        int nextSlot;
        int attemptOfSlot;
        bool protocolStarted;
        bool protocolLogged;
        TrialPhase phase = TrialPhase.Idle;
        TrialRecord current;
        TrialRecord last;

        float guideEndsAt;
        bool guideEndLogged;
        float responseEndsAt;
        float trialStartedAt;
        bool voiceActive;
        float voicedRunStart = -1f;
        float unvoicedRunStart = -1f;
        float lastOffsetAt;
        readonly List<float> pitchSamples = new();
        float matchAccum;
        int matchCount;
        float maxLoudness;
        float voicedSeconds;
        Coroutine restCoroutine;

        public event Action<TrialRecord> TrialCompleted;

        public TrialPhase Phase => phase;
        public bool HasStarted => protocolStarted;
        /// <summary>True for the practice-scene instance (see the class summary).</summary>
        public bool IsPractice => practiceMode;
        /// <summary>Task id this instance answers to: "singing", or "practice" for the practice instance.</summary>
        public string ActiveTaskId => practiceMode ? PracticeTaskId : TaskId;
        /// <summary>Teacher pitch of the practice scene (human Hz); the next practice trial uses it.</summary>
        public float PracticePitchHz
        {
            get => practicePitchHz;
            set => practicePitchHz = Mathf.Clamp(value, 60f, 2000f);
        }
        public bool IsFinished => !practiceMode && schedule != null && nextSlot >= schedule.Length && phase != TrialPhase.Guide && phase != TrialPhase.Response;
        public int TotalTrials => Mathf.Max(0, pitchesHz.Length) * Mathf.Max(1, trialsPerPitch);
        /// <summary>Trials finished so far (a repeated trial counts once).</summary>
        public int CompletedTrials => practiceMode || phase == TrialPhase.Guide || phase == TrialPhase.Response ? nextSlot : Mathf.Min(nextSlot, TotalTrials);
        public int NextTrialNumber => Mathf.Min(nextSlot + 1, TotalTrials);
        public TrialRecord Current => current;
        public TrialRecord Last => last;
        public float[] PitchesHz => pitchesHz;
        public int TrialsPerPitch => trialsPerPitch;
        public TrialOrder Order => order;
        public int[] Schedule => schedule;
        public bool IsVoiceActive => voiceActive;
        /// <summary>True when trials run from one press to the next (no automatic end).</summary>
        public bool TrialUntilNextPress => trialUntilNextPress;
        /// <summary>Seconds left in the response window; 0 when the trial waits for the next press.</summary>
        public float ResponseSecondsLeft => phase == TrialPhase.Response && !trialUntilNextPress ? Mathf.Max(0f, responseEndsAt - Time.time) : 0f;
        /// <summary>Seconds since the running trial started (the last press), 0 when idle.</summary>
        public float CurrentTrialSeconds => current != null && (phase == TrialPhase.Guide || phase == TrialPhase.Response) ? Mathf.Max(0f, Time.time - trialStartedAt) : 0f;
        public float CurrentTargetHz => current != null ? current.targetHz : practiceMode ? practicePitchHz : (schedule != null && nextSlot < schedule.Length ? pitchesHz[schedule[nextSlot]] : 0f);
        public string CurrentNoteName => CurrentTargetHz > 0f ? BirdSongPhrase.NoteName(CurrentTargetHz) : "";
        /// <summary>Target of the trial the next press starts (the slot after the running one), 0 after the last.</summary>
        public float NextTargetHz
        {
            get
            {
                if (practiceMode)
                    return practicePitchHz;
                var slot = phase == TrialPhase.Guide || phase == TrialPhase.Response ? nextSlot + 1 : nextSlot;
                return schedule != null && slot < schedule.Length ? pitchesHz[schedule[slot]] : 0f;
            }
        }
        public string NextNoteName => NextTargetHz > 0f ? BirdSongPhrase.NoteName(NextTargetHz) : "";
        public string RestTaskId => restTaskId;

        /// <summary>Make this instance the practice-scene task (call before it is enabled).</summary>
        public void ConfigurePractice(bool practice)
        {
            practiceMode = practice;
        }

        /// <summary>Change the protocol before the first trial (from the experimenter panel).</summary>
        public bool Configure(float[] pitches, int perPitch, TrialOrder trialOrder, int seed)
        {
            if (protocolStarted)
                return false;

            if (pitches != null && pitches.Length > 0)
                pitchesHz = (float[])pitches.Clone();
            trialsPerPitch = Mathf.Max(1, perPitch);
            order = trialOrder;
            randomSeed = seed;
            schedule = null;
            EnsureSchedule();
            return true;
        }

        /// <summary>Abort any running trial and start the protocol over from trial 1.</summary>
        public void ResetProtocol(string source = "experimenter")
        {
            if (phase == TrialPhase.Guide || phase == TrialPhase.Response)
                EndCurrentTrial("reset");
            if (restCoroutine != null)
            {
                StopCoroutine(restCoroutine);
                restCoroutine = null;
            }

            nextSlot = 0;
            attemptOfSlot = 0;
            protocolStarted = false;
            protocolLogged = false;
            last = null;
            schedule = null;
            EnsureSchedule();
            phase = TrialPhase.Idle;
            recorder?.LogEvent("protocol_reset", j => j.Add("source", source));
            if (isActiveAndEnabled && TaskActivity.IsActive(ActiveTaskId))
                ShowCue("wait", 0f);
            UpdateProgressUi();
        }

        /// <summary>
        /// Experimenter button / Space: the teacher bird sings the next target and the participant answers.
        /// With <see cref="TrialUntilNextPress"/> (default) every press closes the running trial, whatever its
        /// phase, and opens the next one, so each trial spans exactly the interval between two presses; the press
        /// after the last trial closes it and finishes the protocol. Otherwise a press is ignored while the guide
        /// plays and ends a running response.
        /// </summary>
        public bool StartNextTrial(string source)
        {
            if (!isActiveAndEnabled || !TaskActivity.IsActive(ActiveTaskId))
                return false;

            Resolve();
            EnsureSchedule();
            if (phase == TrialPhase.Guide && !trialUntilNextPress)
                return false;
            if (phase == TrialPhase.Guide || phase == TrialPhase.Response)
                EndCurrentTrial("experimenter_next");
            if (!practiceMode && nextSlot >= schedule.Length)
            {
                FinishProtocol();
                return false;
            }

            var mapper = BirdVoiceMapper.Find();
            if (mapper == null)
            {
                Debug.LogWarning("[AvianVoice] SingingTrialTask: no BirdVoiceMapper, cannot play the guide note.");
                return false;
            }

            if (!protocolLogged && !practiceMode)
            {
                protocolLogged = true;
                recorder?.LogEvent("protocol_start", j => j
                    .AddArray("pitches_hz", pitchesHz)
                    .Add("trials_per_pitch", trialsPerPitch)
                    .Add("order", order.ToString())
                    .Add("seed", usedSeed)
                    .AddArray("schedule_pitch_index", schedule)
                    .Add("guide_s", guideSeconds)
                    .Add("gap_s", gapAfterGuideSeconds)
                    .Add("trial_until_next_press", trialUntilNextPress)
                    .Add("response_window_s", trialUntilNextPress ? -1.0 : responseWindowSeconds)
                    .Add("end_after_offset_s", trialUntilNextPress ? -1.0 : endAfterVoiceOffsetSeconds)
                    .Add("tolerance_hz", toleranceHz));
            }

            protocolStarted = true;
            var slot = nextSlot;
            var pitchIndex = practiceMode ? PracticePitchIndex() : schedule[slot];
            var hz = practiceMode ? practicePitchHz : pitchesHz[pitchIndex];
            current = new TrialRecord
            {
                trial = slot + 1,
                attempt = attemptOfSlot + 1,
                block = practiceMode ? 0 : BlockOf(slot),
                pitchIndex = pitchIndex,
                targetHz = hz,
                note = BirdSongPhrase.NoteName(hz),
                order = order.ToString(),
                source = source ?? "",
                avatar = recorder != null ? recorder.CurrentAvatar : "",
                avatarModel = recorder != null ? recorder.CurrentAvatarModel : "",
                voiceFeedback = recorder != null ? recorder.CurrentVoiceFeedback : "",
                condition = recorder != null ? recorder.CurrentCondition : "",
                humanAvatar = recorder != null ? recorder.CurrentHumanAvatar : "",
                run = recorder != null && !practiceMode ? recorder.BeginTrialRun(slot + 1, attemptOfSlot + 1) : 1
            };

            ResetResponseState();
            current.start = Now();
            trialStartedAt = Time.time;
            mapper.SetFollowVoice(false);
            var guideVoice = GuideVoice();
            mapper.PlayGuideNote(hz, guideSeconds, guideVoice);
            current.guideStart = Now();
            guideEndsAt = Time.time + guideSeconds;
            guideEndLogged = false;
            phase = TrialPhase.Guide;
            ShowCue("listen", hz);
            recorder?.LogEvent("trial_start", j => j
                .Add("trial", current.trial).Add("attempt", current.attempt).Add("block", current.block)
                .Add("pitch_index", pitchIndex).Add("target_hz", hz).Add("note", current.note).Add("source", current.source)
                .Add("practice", practiceMode));
            recorder?.LogEvent("guide_start", j => j.Add("trial", current.trial).Add("target_hz", hz).Add("guide_s", guideSeconds).Add("teacher_voice", guideVoice.ToString()));
            UpdateProgressUi();
            return true;
        }

        /// <summary>Run the previous trial slot again (same target). Only while idle.</summary>
        public bool RepeatLastTrial(string source)
        {
            if (phase != TrialPhase.Idle || nextSlot <= 0 || !isActiveAndEnabled || !TaskActivity.IsActive(ActiveTaskId))
                return false;

            nextSlot--;
            attemptOfSlot = last != null && last.trial == nextSlot + 1 ? last.attempt : 0;
            recorder?.LogEvent("trial_repeat", j => j.Add("trial", nextSlot + 1).Add("source", source ?? ""));
            return StartNextTrial((source ?? "") + "_repeat");
        }

        /// <summary>End the running trial now (experimenter judged the answer complete or void).</summary>
        public bool EndTrialNow(string source)
        {
            if (phase != TrialPhase.Guide && phase != TrialPhase.Response)
                return false;

            EndCurrentTrial("experimenter_end:" + (source ?? ""));
            return true;
        }

        public string DescribeState()
        {
            var c = CultureInfo.InvariantCulture;
            return "phase=" + phase + " next=" + NextTrialNumber + "/" + TotalTrials + " done=" + CompletedTrials
                   + " untilNextPress=" + trialUntilNextPress + " order=" + order + " pitches=" + string.Join("/", Array.ConvertAll(pitchesHz, p => p.ToString("0", c)))
                   + " voice=" + voiceActive + " last=" + (last != null ? last.Summary() : "-");
        }

        // ---- lifecycle ----------------------------------------------------------------------------

        void Start()
        {
            Resolve();
            EnsureSchedule();
        }

        void OnEnable()
        {
            if (!TaskActivity.IsActive(ActiveTaskId))
                return;

            Resolve();
            EnsureSchedule();
            BirdSceneRoles.SetFriendVisible(true);
            BirdSceneRoles.EnsurePlayerBirdVisible();
            demonstratorPause = FindAnyObjectByType<BirdFriendDemonstrator>();
            demonstratorPause?.SetTeachingActive(false);
            BindPoses();
            EnsureFriendVisuals();
            BirdVoiceMapper.Find()?.SetFollowVoice(false);
            phase = !practiceMode && nextSlot >= schedule.Length ? TrialPhase.Finished : TrialPhase.Idle;
            ShowCue(phase == TrialPhase.Finished ? "finished" : "wait", 0f);
            // Where the teacher stands (world metres "x,y,z"; the same spot as in practice), so it can be checked afterwards.
            var teacherSpot = BirdSceneRoles.TryGetFriendSpot(out var teacherPosition, out _)
                ? teacherPosition.x.ToString("F3", CultureInfo.InvariantCulture) + "," + teacherPosition.y.ToString("F3", CultureInfo.InvariantCulture)
                  + "," + teacherPosition.z.ToString("F3", CultureInfo.InvariantCulture)
                : "";
            recorder?.LogEvent("singing_task_enabled", j => j.Add("next_trial", NextTrialNumber).Add("total", TotalTrials).Add("done", CompletedTrials).Add("practice", practiceMode)
                .Add("pitch_match_glow", pitchMatchGlow).Add("teacher_position", teacherSpot));
            UpdateProgressUi();
        }

        void OnDisable()
        {
            if (phase == TrialPhase.Guide || phase == TrialPhase.Response)
                EndCurrentTrial("task_disabled");
            if (restCoroutine != null)
            {
                StopCoroutine(restCoroutine);
                restCoroutine = null;
            }

            friendVisuals?.ApplyMatch(0f);
            friendPose?.ClearPerformancePose();
            playerPose?.ClearPerformancePose();
            BirdSceneRoles.SetFriendVisible(false);
            demonstratorPause?.SetTeachingActive(true);
            BirdVoiceMapper.Find()?.SetFollowVoice(false);
            SceneGuide.ClearPhase();
            recorder?.LogEvent("singing_task_disabled", j => j.Add("done", CompletedTrials).Add("total", TotalTrials));
        }

        void Update()
        {
            if (!enabled || !TaskActivity.IsActive(ActiveTaskId))
                return;

            if (acceptKeyboardStart && AvianInput.WasReplayPressedThisFrame())
                StartNextTrial("keyboard");

            switch (phase)
            {
                case TrialPhase.Guide:
                    if (!guideEndLogged && Time.time >= guideEndsAt)
                    {
                        guideEndLogged = true;
                        current.guideEnd = Now().Shift(Time.time - guideEndsAt, MicRate, OutRate);
                        recorder?.LogEvent("guide_end", j => j.Add("trial", current.trial));
                    }

                    if (Time.time >= guideEndsAt + gapAfterGuideSeconds)
                        BeginResponse();
                    break;
                case TrialPhase.Response:
                    UpdateResponse();
                    break;
            }
        }

        void LateUpdate()
        {
            if (!enabled || !TaskActivity.IsActive(ActiveTaskId))
                return;

            BindPoses();
            var friendSinging = phase == TrialPhase.Guide && Time.time < guideEndsAt;
            friendPose?.SetPerformancePose(friendSinging ? 0.92f : 0.08f, false);
            playerPose?.SetPerformancePose(voiceActive ? 0.5f : 0.08f, false);
        }

        // ---- trial phases -------------------------------------------------------------------------

        void BeginResponse()
        {
            phase = TrialPhase.Response;
            current.responseStart = Now();
            responseEndsAt = trialUntilNextPress ? float.PositiveInfinity : Time.time + responseWindowSeconds;
            if (participantVoiceFeedback)
                BirdVoiceMapper.Find()?.SetFollowVoice(true);
            ShowCue("sing", current.targetHz);
            recorder?.LogEvent("response_start", j => j.Add("trial", current.trial).Add("window_s", trialUntilNextPress ? -1.0 : responseWindowSeconds));
        }

        void UpdateResponse()
        {
            var now = Time.time;
            var dt = Time.deltaTime;
            var v = analyzer != null ? analyzer.Current : default;
            var voicedNow = v.voiced > 0.5f;

            if (voicedNow)
            {
                if (voicedRunStart < 0f)
                    voicedRunStart = now;
                unvoicedRunStart = -1f;

                if (!voiceActive && now - voicedRunStart >= onsetHoldSeconds)
                {
                    voiceActive = true;
                    current.voiceSegments++;
                    var onset = Now().Shift(now - voicedRunStart, MicRate, OutRate);
                    if (!current.voiceOnset.IsSet)
                    {
                        current.voiceOnset = onset;
                        current.onsetLatencySeconds = current.guideEnd.IsSet ? (float)(onset.unity - current.guideEnd.unity) : float.NaN;
                    }

                    recorder?.LogEvent("voice_onset", j => j.Add("trial", current.trial).Add("segment", current.voiceSegments)
                        .Add("t_onset_unity", onset.unity).Add("mic_sample_onset", onset.micSample));
                }

                if (voiceActive)
                {
                    voicedSeconds += dt;
                    maxLoudness = Mathf.Max(maxLoudness, v.loudness);
                    if (v.pitchHz > 0f)
                    {
                        pitchSamples.Add(v.pitchHz);
                        var match = BirdSongPhrase.Match(v.pitchHz, current.targetHz, toleranceHz);
                        matchAccum += match;
                        matchCount++;
                        if (pitchMatchGlow)
                            friendVisuals?.ApplyMatch(match);
                    }
                }
            }
            else
            {
                voicedRunStart = -1f;
                if (unvoicedRunStart < 0f)
                    unvoicedRunStart = now;

                if (voiceActive && now - unvoicedRunStart >= offsetHoldSeconds)
                {
                    voiceActive = false;
                    lastOffsetAt = unvoicedRunStart;
                    var offset = Now().Shift(now - unvoicedRunStart, MicRate, OutRate);
                    current.voiceOffset = offset;
                    friendVisuals?.ApplyMatch(0f);
                    recorder?.LogEvent("voice_offset", j => j.Add("trial", current.trial).Add("segment", current.voiceSegments)
                        .Add("t_offset_unity", offset.unity).Add("mic_sample_offset", offset.micSample));
                }
            }

            if (trialUntilNextPress)
                return; // the trial ends with the next experimenter press (or EndTrialNow)

            if (!voiceActive && current.voiceOffset.IsSet && now - lastOffsetAt >= endAfterVoiceOffsetSeconds)
            {
                EndCurrentTrial("voice_done");
                return;
            }

            if (now >= responseEndsAt)
                EndCurrentTrial(!current.HadVoice ? "no_voice" : voiceActive ? "window_end_voicing" : "window_end");
        }

        void EndCurrentTrial(string reason)
        {
            if (current == null || (phase != TrialPhase.Guide && phase != TrialPhase.Response))
                return;

            var record = current;
            if (voiceActive)
            {
                voiceActive = false;
                record.voiceOffset = Now();
                recorder?.LogEvent("voice_offset", j => j.Add("trial", record.trial).Add("segment", record.voiceSegments).Add("forced", true));
            }

            record.end = Now();
            record.endReason = reason ?? "";
            record.voicedSeconds = voicedSeconds;
            record.maxLoudness = maxLoudness;
            record.pitchMatch = matchCount > 0 ? matchAccum / matchCount : 0f;
            if (pitchSamples.Count > 0)
            {
                var sum = 0f;
                foreach (var p in pitchSamples)
                    sum += p;
                record.meanPitchHz = sum / pitchSamples.Count;
                pitchSamples.Sort();
                var mid = pitchSamples.Count / 2;
                record.medianPitchHz = pitchSamples.Count % 2 == 1
                    ? pitchSamples[mid]
                    : 0.5f * (pitchSamples[mid - 1] + pitchSamples[mid]);
            }

            BirdVoiceMapper.Find()?.SetFollowVoice(false);
            friendVisuals?.ApplyMatch(0f);

            if (recorder != null)
            {
                // Practice trials stay out of trials.csv and segments/ (events.jsonl keeps them, task "practice").
                if (!practiceMode)
                {
                    // conditions/<condition>/segments/trial_NNN_aN_<note>[_<human body>][_run<N>].wav
                    var safeNote = record.note.Replace('#', 's');
                    record.segmentFile = recorder.WriteSegment(record.start.micSample, record.end.micSample,
                        "trial_" + record.trial.ToString("000", CultureInfo.InvariantCulture) + "_a" + record.attempt + "_" + safeNote
                        + (string.IsNullOrEmpty(record.humanAvatar) ? "" : "_" + record.humanAvatar)
                        + (record.run > 1 ? "_run" + record.run.ToString(CultureInfo.InvariantCulture) : "") + ".wav", record.condition) ?? "";
                    recorder.WriteTrial(record);
                }

                recorder.LogEvent("trial_end", j =>
                {
                    j.Add("trial", record.trial).Add("attempt", record.attempt).Add("end_reason", record.endReason)
                     .Add("had_voice", record.HadVoice).Add("onset_latency_s", record.onsetLatencySeconds)
                     .Add("voiced_s", record.voicedSeconds).Add("mean_pitch_hz", record.meanPitchHz)
                     .Add("median_pitch_hz", record.medianPitchHz).Add("pitch_match", record.pitchMatch)
                     .Add("max_loudness", record.maxLoudness).Add("segment_file", record.segmentFile)
                     .Add("practice", practiceMode);
                });
            }

            last = record;
            current = null;
            taskManager?.ReportScore(record.pitchMatch);
            if (!practiceMode)
                Data.SessionScoreCache.RecordVoice(record.pitchMatch);

            if (reason != "reset")
            {
                nextSlot++;
                attemptOfSlot = 0;
            }

            ResetResponseState();
            TrialCompleted?.Invoke(record);

            if (!practiceMode && schedule != null && nextSlot >= schedule.Length)
            {
                FinishProtocol();
            }
            else
            {
                phase = TrialPhase.Idle;
                if (isActiveAndEnabled && TaskActivity.IsActive(ActiveTaskId))
                    ShowCue("wait", 0f);
            }

            UpdateProgressUi();
        }

        void FinishProtocol()
        {
            if (phase == TrialPhase.Finished)
                return;

            phase = TrialPhase.Finished;
            ShowCue("finished", 0f);
            recorder?.LogEvent("protocol_complete", j => j.Add("trials", CompletedTrials).Add("auto_rest", autoAdvanceToRest));
            if (autoAdvanceToRest && isActiveAndEnabled && restCoroutine == null)
                restCoroutine = StartCoroutine(AdvanceToRest());
        }

        IEnumerator AdvanceToRest()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, restDelaySeconds));
            restCoroutine = null;
            if (taskManager == null || !TaskActivity.IsActive(ActiveTaskId))
                yield break;

            recorder?.LogEvent("auto_advance", j => j.Add("to_task", restTaskId));
            Debug.Log("[AvianVoice] Singing trials complete; moving to " + restTaskId + ".");
            taskManager.LoadTaskById(restTaskId);
        }

        // ---- helpers ------------------------------------------------------------------------------

        void ResetResponseState()
        {
            voiceActive = false;
            voicedRunStart = -1f;
            unvoicedRunStart = -1f;
            lastOffsetAt = 0f;
            pitchSamples.Clear();
            matchAccum = 0f;
            matchCount = 0;
            maxLoudness = 0f;
            voicedSeconds = 0f;
        }

        int MicRate => recorder != null ? recorder.MicSampleRate : 0;
        int OutRate => recorder != null ? recorder.OutputSampleRate : 0;

        StudyTime Now() => recorder != null ? recorder.Now() : StudyTime.Now(null);

        void Resolve()
        {
            if (taskManager == null)
                taskManager = FindAnyObjectByType<TaskManager>();
            if (analyzer == null)
                analyzer = FindAnyObjectByType<VoiceAnalyzer>();
            if (recorder == null)
                recorder = StudyRecorder.Instance != null ? StudyRecorder.Instance : FindAnyObjectByType<StudyRecorder>();
            if (avatarController == null)
                avatarController = FindAnyObjectByType<AvatarEmbodimentController>();
        }

        VoiceFeedback GuideVoice()
        {
            return teacherVoice == TeacherVoice.FollowAvatar && avatarController != null && avatarController.Current == AvatarKind.Human
                ? VoiceFeedback.Human
                : VoiceFeedback.Bird;
        }

        void EnsureSchedule()
        {
            if (pitchesHz == null || pitchesHz.Length == 0)
                pitchesHz = (float[])DefaultPitchesHz.Clone();
            trialsPerPitch = Mathf.Max(1, trialsPerPitch);
            if (schedule != null && schedule.Length == TotalTrials)
                return;

            var n = pitchesHz.Length;
            var total = n * trialsPerPitch;
            schedule = new int[total];
            for (var i = 0; i < total; i++)
            {
                schedule[i] = order == TrialOrder.Interleaved ? i % n : i / trialsPerPitch;
            }

            usedSeed = 0;
            if (order == TrialOrder.Random)
            {
                usedSeed = randomSeed != 0 ? randomSeed : Environment.TickCount;
                var rng = new System.Random(usedSeed);
                for (var i = total - 1; i > 0; i--)
                {
                    var j = rng.Next(i + 1);
                    (schedule[i], schedule[j]) = (schedule[j], schedule[i]);
                }
            }

            if (!practiceMode && nextSlot > total)
                nextSlot = total;
        }

        /// <summary>Index of the practice pitch among the protocol pitches, -1 for a free pitch.</summary>
        int PracticePitchIndex()
        {
            for (var i = 0; i < pitchesHz.Length; i++)
            {
                if (Mathf.Abs(pitchesHz[i] - practicePitchHz) < 0.5f)
                    return i;
            }

            return -1;
        }

        int BlockOf(int slot)
        {
            var n = Mathf.Max(1, pitchesHz.Length);
            return order == TrialOrder.Blocked ? slot / trialsPerPitch + 1 : slot / n + 1;
        }

        void ShowCue(string kind, float hz)
        {
            // The participant is not shown the note name or the frequency (2026-11-12): the cue says "this note".
            // The note stays in the record (TrialRecord.note, the trial_start event) and on the experimenter's tools.
            AvianUiCopy.ShowSingingCue(kind, "");
        }

        void UpdateProgressUi()
        {
            if (taskUi == null)
                taskUi = FindAnyObjectByType<TaskFlowUI>();
            if (taskUi == null)
                return;

            // The debug HUD (TaskModeHUD, off in the study) is in the headset too: the trial number, not the note name.
            var text = phase switch
            {
                TrialPhase.Finished => "Done.",
                TrialPhase.Guide or TrialPhase.Response when current != null =>
                    practiceMode ? "Practice trial " + current.trial : "Trial " + current.trial + " / " + TotalTrials,
                _ => ""
            };
            taskUi.SetProgressInstruction(text);
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
