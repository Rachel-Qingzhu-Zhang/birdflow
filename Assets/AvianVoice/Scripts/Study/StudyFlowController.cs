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
        [Tooltip("Put a practice scene in front of the flow and start the session there: the singing task with a free pitch, " +
                 "no schedule and nothing written to trials.csv, so the experimenter can explain the task and show the conditions.")]
        [SerializeField]
        bool includePractice = true;

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

        void Start()
        {
            TaskManager = FindAnyObjectByType<TaskManager>();
            ParticipantId = ResolveParticipantId(defaultParticipantId);

            if (recordSession)
            {
                Recorder = StudyRecorder.Ensure();
                Recorder.BeginSession(ParticipantId, SceneManager.GetActiveScene().name);
            }

            if (pcAudioMonitor)
            {
                var monitor = PcAudioMonitor.Ensure();
                Recorder?.LogEvent("pc_audio_monitor", j => j.Add("state", monitor != null ? monitor.Describe() : "no AudioListener"));
            }

            avatar = FindAnyObjectByType<AvatarEmbodimentController>();
            if (avatar != null && Recorder != null)
            {
                lastAvatarLabel = avatar.CurrentLabel;
                Recorder.CurrentAvatar = lastAvatarLabel;
            }

            if (avatar != null)
                avatar.AvatarChanged += _ => ApplyVoiceFeedback();
            ApplyVoiceFeedback();

            if (TaskManager == null)
            {
                Debug.LogWarning("[AvianVoice] StudyFlowController: no TaskManager in the scene.");
                return;
            }

            if (useFourSceneFlow)
                TaskManager.SetFlow(FlowWithPractice());

            if (useSingingTrials)
            {
                var host = GameObject.Find("TaskRoot") ?? gameObject;
                Trials = FindTrialTask(host, false) ?? host.AddComponent<SingingTrialTask>();
                TaskManager.BindTaskBehaviour(SingingTrialTask.TaskId, Trials);

                if (includePractice)
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

            if (includePractice && useFourSceneFlow && TaskManager.CurrentTaskId != SingingTrialTask.PracticeTaskId)
                TaskManager.LoadTaskById(SingingTrialTask.PracticeTaskId);

            Recorder?.LogEvent("study_flow", j => j
                .AddArray("flow", TaskManager.TaskIds)
                .Add("singing_trials", useSingingTrials)
                .Add("total_trials", Trials != null ? Trials.TotalTrials : 0)
                .Add("condition", ConditionLabel));

            Debug.Log("[AvianVoice] Study flow: " + string.Join(" > ", TaskManager.TaskIds)
                      + (Trials != null ? " | singing trials " + Trials.TotalTrials : "")
                      + (Recorder != null ? " | recording " + Recorder.SessionDirectory : " | not recording"));
        }

        /// <summary>The configured flow, with the practice scene in front when it is enabled.</summary>
        string[] FlowWithPractice()
        {
            if (!includePractice || flow == null || System.Array.IndexOf(flow, SingingTrialTask.PracticeTaskId) >= 0)
                return flow;

            var withPractice = new string[flow.Length + 1];
            withPractice[0] = SingingTrialTask.PracticeTaskId;
            flow.CopyTo(withPractice, 1);
            return withPractice;
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
