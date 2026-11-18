using System;
using System.Globalization;
using System.IO;
using System.Text;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment.Avatars;
using AvianVoice.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Study
{
    /// <summary>
    /// Study configuration for Avian_Lab_Embodiment: a four-scene flow (meet, breath, singing, rest),
    /// the trial-based singing task (3 pitches x 10) in place of the melody task, session recording
    /// (timestamps + microphone + listener output) through <see cref="StudyRecorder"/>, and the study
    /// condition: avatar (bird / human) x voice the participant hears of themselves in the headphones (bird
    /// voice / their own voice from the microphone) = 4 conditions, each voice with each avatar: BB, BH, HB, HH
    /// (avatar letter first). Until 2026-11-14 the voice followed the avatar and the second factor was
    /// "converted / nothing from the app"; the two conditions without sound were a design mistake.
    /// Lives on the "Avian Embodiment" object next to <see cref="AvatarEmbodimentController"/>.
    /// </summary>
    [DefaultExecutionOrder(30995)]
    public class StudyFlowController : MonoBehaviour
    {
        public const string ParticipantIdPrefKey = "AvianVoice.Study.ParticipantId";
        public static readonly string[] FourSceneFlow = { "meet", "breath_sync", "singing", "reflection" };

        [Header("Scene flow")]
        [Tooltip("Restrict the session to the tasks listed in Flow (in that order). Off = the original six scenes.")]
        [SerializeField]
        bool useFourSceneFlow = true;

        [SerializeField]
        string[] flow = { "meet", "breath_sync", "singing", "reflection" };

        [Header("Singing scene")]
        [Tooltip("Use SingingTrialTask (experimenter-paced, 3 pitches x 10 trials, auto-advance to rest) instead of the melody task.")]
        [SerializeField]
        bool useSingingTrials = true;

        [Header("Practice scene")]
        [Tooltip("Add a practice scene beside the flow (Experimenter Panel \"Practice\", key 0): the singing task with a free pitch, " +
                 "no schedule and nothing written to trials.csv, so the experimenter can explain the task and show the conditions once " +
                 "before the four conditions.")]
        [SerializeField]
        bool includePractice = true;

        [Tooltip("Begin in the practice scene. The session recording (events, audio) starts when the first study scene is entered, " +
                 "so the practice is not recorded.")]
        [SerializeField]
        bool startInPractice = true;

        [Header("Recording")]
        [SerializeField]
        bool recordSession = true;

        [Tooltip("Used when no participant id was set from the experimenter panel (PlayerPrefs).")]
        [SerializeField]
        string defaultParticipantId = "P000";

        [Header("Audio")]
        [Tooltip("Play the sound on a PC device (PcAudioMonitor on the AudioListener). By default that is the only output: Unity's own " +
                 "output (the headset over Link) is silenced. Mode and device are chosen in the Experimenter Panel.")]
        [SerializeField]
        bool pcAudioMonitor = true;

        [Header("Condition")]
        [Tooltip("The voice the participant hears of themselves in the headphones at the start: Bird = their voice as a bird voice, " +
                 "Human = their own microphone as it is. It does not depend on the avatar (the other factor: AvatarEmbodimentController / " +
                 "Experimenter Panel), so the four conditions are BB, BH, HB, HH. None = nothing from the app (not one of the four conditions).")]
        [SerializeField]
        VoiceFeedback voice = VoiceFeedback.Bird;

        AvatarEmbodimentController avatar;
        string lastAvatarLabel = "";
        BirdVoiceMapper mapper;
        VoiceFeedback lastFeedback = (VoiceFeedback)(-1);
        string lastConditionLabel = "";
        float nextMapperSearch;

        public TaskManager TaskManager { get; private set; }
        public SingingTrialTask Trials { get; private set; }
        /// <summary>The practice-scene instance of the singing task (null when the practice scene is off).</summary>
        public SingingTrialTask Practice { get; private set; }
        public StudyRecorder Recorder { get; private set; }
        public string ParticipantId { get; private set; } = "";
        public bool UseFourSceneFlow => useFourSceneFlow;
        public string[] Flow => flow;
        public AvatarEmbodimentController Avatar => avatar;

        /// <summary>
        /// The voice the participant hears of themselves (the second factor of the condition): bird voice, or their
        /// own microphone as it is (Human). The same with either avatar.
        /// </summary>
        public VoiceFeedback Voice => voice;

        /// <summary>The feedback voice in effect (the voice factor; it no longer depends on the avatar).</summary>
        public VoiceFeedback EffectiveFeedback => voice;

        /// <summary>"bird" / "human" / "none" for logs ("human" = the participant's own voice from the microphone).</summary>
        public string EffectiveFeedbackName => FeedbackName(EffectiveFeedback);

        /// <summary>Condition label for logs and the panel, e.g. "Bird+bird", "Bird+human", "Human:neutral+bird", "Human:neutral+human".</summary>
        public string ConditionLabel => (avatar != null ? avatar.CurrentLabel : "?") + "+" + (voice == VoiceFeedback.None ? "own" : EffectiveFeedbackName);

        /// <summary>
        /// Two-letter code of the condition, avatar first: "BB" bird avatar + bird voice, "BH" bird avatar + own voice
        /// in the headphones, "HB" human avatar + bird voice, "HH" human avatar + own voice in the headphones.
        /// "" when the avatar is unknown or nothing is played (None).
        /// </summary>
        public string ConditionCode => avatar == null || voice == VoiceFeedback.None
            ? ""
            : (avatar.Current == AvatarKind.Human ? "H" : "B") + (voice == VoiceFeedback.Human ? "H" : "B");

        public static string FeedbackName(VoiceFeedback feedback)
        {
            return feedback switch
            {
                VoiceFeedback.Bird => "bird",
                VoiceFeedback.Human => "human",
                _ => "none"
            };
        }

        /// <summary>Set one of the four conditions: avatar (bird / human) and the voice heard (Bird, or Human = own microphone).</summary>
        public void SetCondition(AvatarKind kind, VoiceFeedback heardVoice)
        {
            voice = heardVoice;
            if (avatar != null && avatar.Current != kind)
                avatar.SetAvatar(kind); // raises AvatarChanged -> ApplyVoiceFeedback
            ApplyVoiceFeedback();
        }

        /// <summary>Change the voice factor only (the avatar stays).</summary>
        public void SetVoice(VoiceFeedback heardVoice)
        {
            voice = heardVoice;
            ApplyVoiceFeedback();
        }

        void ApplyVoiceFeedback()
        {
            var effective = EffectiveFeedback;
            if (mapper == null)
                mapper = BirdVoiceMapper.Find();
            mapper?.SetFeedback(effective);
            if (Recorder != null)
            {
                Recorder.CurrentVoiceFeedback = FeedbackName(effective);
                // The avatar may have changed in this same call (SetCondition): label the events that follow
                // with the new avatar at once instead of waiting for the next Update.
                if (avatar != null)
                {
                    Recorder.CurrentAvatar = avatar.CurrentLabel;
                    Recorder.CurrentAvatarModel = avatar.CurrentModel;
                }
            }

            // The voice no longer changes with the avatar (BB -> HB keeps the bird voice), so the condition is
            // announced on its own; the voice_feedback event stays for changes of the voice.
            var label = ConditionLabel;
            if (label != lastConditionLabel)
            {
                lastConditionLabel = label;
                Debug.Log("[AvianVoice] Condition: " + (ConditionCode.Length > 0 ? ConditionCode + " " : "") + label);
            }

            if (lastFeedback == effective)
                return;

            lastFeedback = effective;
            Recorder?.LogEvent("voice_feedback", j => j
                .Add("feedback", FeedbackName(effective))
                .Add("converted", effective != VoiceFeedback.None)
                .Add("condition_label", ConditionLabel)
                .Add("condition_code", ConditionCode)
                .Add("bird_feedback", mapper != null ? mapper.BirdFeedbackModeName : "")
                .Add("bird_own_voice_volume", mapper != null ? mapper.BirdOwnVoiceVolume : -1f)
                .Add("feedback_gate_close_ratio", mapper != null ? mapper.FeedbackGateCloseRatio : -1f)
                .Add("feedback_gate_hold_s", mapper != null ? mapper.FeedbackGateHoldSeconds : -1f)
                .Add("human_feedback", mapper != null ? mapper.HumanFeedbackModeName : ""));
        }

        public static string ResolveParticipantId(string fallback)
        {
            var stored = PlayerPrefs.GetString(ParticipantIdPrefKey, "");
            return string.IsNullOrWhiteSpace(stored) ? fallback : stored.Trim();
        }

        public static void StoreParticipantId(string id)
        {
            PlayerPrefs.SetString(ParticipantIdPrefKey, id ?? "");
            PlayerPrefs.Save();
        }

        /// <summary>True while the practice scene is current (the session recording has not started if the practice came first).</summary>
        public bool InPractice => TaskManager != null && TaskManager.CurrentTaskId == SingingTrialTask.PracticeTaskId;

        /// <summary>True when recording is on but waits for the first study scene (practice is not recorded).</summary>
        public bool RecordingPending => recordSession && Recorder == null && TaskManager != null;

        void Start()
        {
            TaskManager = FindAnyObjectByType<TaskManager>();
            ParticipantId = ResolveParticipantId(defaultParticipantId);

            if (pcAudioMonitor)
                PcAudioMonitor.Ensure();

            avatar = FindAnyObjectByType<AvatarEmbodimentController>();
            if (avatar != null)
                avatar.AvatarChanged += _ => ApplyVoiceFeedback();

            if (TaskManager == null)
            {
                Debug.LogWarning("[AvianVoice] StudyFlowController: no TaskManager in the scene.");
                if (recordSession)
                    BeginRecording();
                ApplyVoiceFeedback();
                return;
            }

            var practice = includePractice && useSingingTrials;
            if (useFourSceneFlow)
                TaskManager.SetFlow(flow, practice ? new[] { SingingTrialTask.PracticeTaskId } : null);

            if (useSingingTrials)
            {
                var host = GameObject.Find("TaskRoot") ?? gameObject;
                Trials = FindTrialTask(host, false) ?? host.AddComponent<SingingTrialTask>();
                TaskManager.BindTaskBehaviour(SingingTrialTask.TaskId, Trials);

                if (practice && TaskManager.HasTask(SingingTrialTask.PracticeTaskId))
                {
                    Practice = FindTrialTask(host, true);
                    if (Practice == null)
                    {
                        Practice = host.AddComponent<SingingTrialTask>();
                        Practice.ConfigurePractice(true);
                    }

                    TaskManager.BindTaskBehaviour(SingingTrialTask.PracticeTaskId, Practice);
                }
            }

            if (Practice != null && startInPractice && !InPractice)
                TaskManager.LoadTaskById(SingingTrialTask.PracticeTaskId);

            ApplyVoiceFeedback();

            if (recordSession)
            {
                if (InPractice)
                {
                    TaskManager.OnTaskChanged += BeginRecordingWhenStudyStarts;
                    Debug.Log("[AvianVoice] Study flow: " + string.Join(" > ", TaskManager.TaskIds)
                              + " | practice first (not recorded); recording starts with the first study scene.");
                }
                else
                {
                    BeginRecording();
                }
            }
            else
            {
                Debug.Log("[AvianVoice] Study flow: " + string.Join(" > ", TaskManager.TaskIds) + " | not recording");
            }
        }

        void OnDestroy()
        {
            if (TaskManager != null)
                TaskManager.OnTaskChanged -= BeginRecordingWhenStudyStarts;
        }

        void BeginRecordingWhenStudyStarts(string taskName, string instruction)
        {
            if (InPractice || Recorder != null)
                return;

            TaskManager.OnTaskChanged -= BeginRecordingWhenStudyStarts;
            BeginRecording();
        }

        /// <summary>Open the session recording and log the state of the session (flow, condition, audio monitor).</summary>
        void BeginRecording()
        {
            if (Recorder != null)
                return;

            Recorder = StudyRecorder.Ensure();
            if (avatar != null)
            {
                Recorder.CurrentAvatar = avatar.CurrentLabel;
                Recorder.CurrentAvatarModel = avatar.CurrentModel;
            }

            Recorder.CurrentVoiceFeedback = EffectiveFeedbackName;
            Recorder.BeginSession(ParticipantId, SceneManager.GetActiveScene().name);

            if (avatar != null)
            {
                lastAvatarLabel = avatar.CurrentLabel;
                Recorder.CurrentAvatar = lastAvatarLabel;
                Recorder.CurrentAvatarModel = avatar.CurrentModel;
                LogHumanAvatarCatalog();
                LogHumanMouth();
            }

            lastFeedback = (VoiceFeedback)(-1); // log the condition into this session
            ApplyVoiceFeedback();

            var monitor = PcAudioMonitor.Instance;
            Recorder.LogEvent("pc_audio_monitor", j => j.Add("state", monitor != null ? monitor.Describe() : "no AudioListener"));

            if (TaskManager != null)
            {
                Recorder.LogEvent("study_flow", j => j
                    .AddArray("flow", TaskManager.TaskIds)
                    .Add("practice", Practice != null)
                    .Add("singing_trials", useSingingTrials)
                    .Add("total_trials", Trials != null ? Trials.TotalTrials : 0)
                    .Add("condition_label", ConditionLabel)
                    .Add("condition_code", ConditionCode));
            }

            Debug.Log("[AvianVoice] Study flow: " + (TaskManager != null ? string.Join(" > ", TaskManager.TaskIds) : "-")
                      + (Trials != null ? " | singing trials " + Trials.TotalTrials : "")
                      + " | recording " + Recorder.SessionDirectory);
        }

        static SingingTrialTask FindTrialTask(GameObject host, bool practice)
        {
            foreach (var task in host.GetComponents<SingingTrialTask>())
            {
                if (task != null && task.IsPractice == practice)
                    return task;
            }

            return null;
        }

        /// <summary>
        /// StudyData/live.json: what Unity is doing right now (participant ID, scene, practice, recording or not),
        /// rewritten every second while Play runs. The Study Monitor shows it, so a Play that is still in the
        /// practice (not recorded, no session folder yet) is not mistaken for the previous participant's session.
        /// </summary>
        public const string LiveStatusFileName = "live.json";

        float nextLiveWrite;
        bool liveWriteFailed;

        void WriteLiveStatus(bool playing)
        {
            try
            {
                var root = StudyRecorder.RootDirectory;
                Directory.CreateDirectory(root);
                var recording = Recorder != null && Recorder.SessionOpen;
                var j = new JsonLine()
                    .Add("playing", playing)
                    .Add("participant", ParticipantId ?? "")
                    .Add("task", TaskManager != null ? TaskManager.CurrentTaskId ?? "" : "")
                    .Add("in_practice", InPractice)
                    .Add("recording", recording)
                    .Add("recording_pending", RecordingPending)
                    .Add("session", recording ? Recorder.SessionId : "")
                    .Add("condition_code", ConditionCode)
                    .Add("t_utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                File.WriteAllText(Path.Combine(root, LiveStatusFileName), j.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                if (!liveWriteFailed)
                    Debug.LogWarning("[AvianVoice] Could not write " + LiveStatusFileName + " for the Study Monitor: " + e.Message);
                liveWriteFailed = true;
            }
        }

        void OnApplicationQuit() => WriteLiveStatus(false);

        void Update()
        {
            if (Time.unscaledTime >= nextLiveWrite)
            {
                nextLiveWrite = Time.unscaledTime + 1f;
                WriteLiveStatus(true);
            }

            // The voice mapper may be created after Start; keep its feedback in line with the condition.
            if (mapper == null && Time.unscaledTime >= nextMapperSearch)
            {
                nextMapperSearch = Time.unscaledTime + 1f;
                mapper = BirdVoiceMapper.Find();
                if (mapper != null)
                    ApplyVoiceFeedback();
            }
            else if (mapper != null && mapper.Feedback != EffectiveFeedback)
            {
                ApplyVoiceFeedback();
            }

            if (avatar == null || Recorder == null)
                return;

            var label = avatar.CurrentLabel;
            if (label != lastAvatarLabel)
            {
                lastAvatarLabel = label;
                Recorder.CurrentAvatar = label;
                Recorder.CurrentAvatarModel = avatar.CurrentModel;
                var option = avatar.CurrentHumanOption;
                Recorder.LogEvent("avatar_changed", j => j.Add("kind", avatar.Current.ToString()).Add("human_id", avatar.HumanAvatarId)
                    .Add("human_label", option != null ? option.label : "").Add("human_model", option != null ? option.resourcePath : "")
                    .Add("condition_label", ConditionLabel)
                    .Add("condition_code", ConditionCode));
                LogHumanMouth();
            }
        }

        /// <summary>
        /// How the human body's mouth follows the voice (HumanMouthEmbodiment): logged with the session and whenever
        /// the body changes, because the jaw angle for the same lip opening differs per body.
        /// </summary>
        void LogHumanMouth()
        {
            var root = avatar != null ? avatar.HumanRoot : null;
            var mouth = root != null ? root.GetComponent<HumanMouthEmbodiment>() : null;
            if (Recorder == null || mouth == null)
                return;

            Recorder.LogEvent("human_mouth", j => j.Add("human_id", avatar.HumanAvatarId)
                .Add("follows_voice", mouth.JawBone != null)
                .Add("jaw_bone", mouth.JawBone != null ? mouth.JawBone.name : "")
                .Add("lip_opening_m", mouth.LipOpeningMeters)
                .Add("full_jaw_deg", mouth.FullJawDegrees)
                .Add("soft_voice_share", mouth.SoftVoiceShare)
                .Add("open_s", mouth.OpenSeconds)
                .Add("close_s", mouth.CloseSeconds)
                .Add("close_hold_s", mouth.CloseHoldSeconds)
                .Add("interior_triangles_darkened", mouth.InteriorTriangleCount));
        }

        /// <summary>Which human bodies this session could use (id, panel label, model, offered in the panel).</summary>
        void LogHumanAvatarCatalog()
        {
            var options = avatar != null ? avatar.HumanAvatars : null;
            if (Recorder == null || options == null)
                return;

            var ids = new string[options.Length];
            var labels = new string[options.Length];
            var models = new string[options.Length];
            var selectable = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
            {
                ids[i] = options[i].id;
                labels[i] = options[i].label;
                models[i] = options[i].resourcePath;
                selectable[i] = options[i].selectable ? "yes" : "no";
            }

            Recorder.LogEvent("human_avatars", j => j.AddArray("ids", ids).AddArray("labels", labels).AddArray("models", models)
                .AddArray("selectable", selectable).Add("start_id", avatar.HumanAvatarId));
        }
    }
}
