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
    /// condition: avatar (bird / human) x voice feedback (the participant's own voice heard as the avatar's
    /// voice, or only their natural voice) = 4 conditions.
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
        [Tooltip("Mirror the listener output to the PC speakers when Unity's own output goes to the headset (PcAudioMonitor on the AudioListener).")]
        [SerializeField]
        bool pcAudioMonitor = true;

        [Header("Condition")]
        [Tooltip("On: the participant hears their own voice converted (bird chirps with the bird avatar, a human voice with the human avatar). " +
                 "Off: only their natural voice. The avatar is the other factor (AvatarEmbodimentController / Experimenter Panel).")]
        [SerializeField]
        bool voiceFeedback = true;

        AvatarEmbodimentController avatar;
        string lastAvatarLabel = "";
        BirdVoiceMapper mapper;
        VoiceFeedback lastFeedback = (VoiceFeedback)(-1);
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

        /// <summary>Whether the participant hears their own voice converted (the second factor of the condition).</summary>
        public bool VoiceFeedbackOn => voiceFeedback;

        /// <summary>The feedback voice that follows from avatar x voice feedback.</summary>
        public VoiceFeedback EffectiveFeedback =>
            !voiceFeedback ? VoiceFeedback.None
            : avatar != null && avatar.Current == AvatarKind.Human ? VoiceFeedback.Human
            : VoiceFeedback.Bird;

        /// <summary>"bird" / "human" / "none" for logs.</summary>
        public string EffectiveFeedbackName => FeedbackName(EffectiveFeedback);

        /// <summary>Condition label for logs and the panel, e.g. "Bird+bird", "Bird+own", "Human:neutral+human", "Human:neutral+own".</summary>
        public string ConditionLabel => (avatar != null ? avatar.CurrentLabel : "?") + "+" + (voiceFeedback ? EffectiveFeedbackName : "own");

        public static string FeedbackName(VoiceFeedback feedback)
        {
            return feedback switch
            {
                VoiceFeedback.Bird => "bird",
                VoiceFeedback.Human => "human",
                _ => "none"
            };
        }

        /// <summary>Set one of the four conditions: avatar (bird / human) and whether the own voice is converted.</summary>
        public void SetCondition(AvatarKind kind, bool convertedVoice)
        {
            voiceFeedback = convertedVoice;
            if (avatar != null && avatar.Current != kind)
                avatar.SetAvatar(kind); // raises AvatarChanged -> ApplyVoiceFeedback
            ApplyVoiceFeedback();
        }

        public void SetVoiceFeedback(bool convertedVoice)
        {
            voiceFeedback = convertedVoice;
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
                    Recorder.CurrentAvatar = avatar.CurrentLabel;
            }

            if (lastFeedback == effective)
                return;

            lastFeedback = effective;
            Recorder?.LogEvent("voice_feedback", j => j
                .Add("feedback", FeedbackName(effective))
                .Add("converted", voiceFeedback)
                .Add("condition", ConditionLabel)
                .Add("bird_feedback", mapper != null ? mapper.BirdFeedbackModeName : ""));
            Debug.Log("[AvianVoice] Condition: " + ConditionLabel);
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
            Recorder.BeginSession(ParticipantId, SceneManager.GetActiveScene().name);

            if (avatar != null)
            {
                lastAvatarLabel = avatar.CurrentLabel;
                Recorder.CurrentAvatar = lastAvatarLabel;
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
                    .Add("condition", ConditionLabel));
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

        void Update()
        {
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
                Recorder.LogEvent("avatar_changed", j => j.Add("kind", avatar.Current.ToString()).Add("human_id", avatar.HumanAvatarId).Add("condition", ConditionLabel));
            }
        }
    }
}
