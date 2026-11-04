using AvianVoice.Embodiment.Avatars;
using AvianVoice.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Study
{
    /// <summary>
    /// Study configuration for Avian_Lab_Embodiment: a four-scene flow (meet, breath, singing, rest),
    /// the trial-based singing task (3 pitches x 20) in place of the melody task, and session recording
    /// (timestamps + microphone + listener output) through <see cref="StudyRecorder"/>.
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
        [Tooltip("Use SingingTrialTask (experimenter-paced, 3 pitches x 20 trials, auto-advance to rest) instead of the melody task.")]
        [SerializeField]
        bool useSingingTrials = true;

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

        AvatarEmbodimentController avatar;
        string lastAvatarLabel = "";

        public TaskManager TaskManager { get; private set; }
        public SingingTrialTask Trials { get; private set; }
        public StudyRecorder Recorder { get; private set; }
        public string ParticipantId { get; private set; } = "";
        public bool UseFourSceneFlow => useFourSceneFlow;
        public string[] Flow => flow;

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

            if (TaskManager == null)
            {
                Debug.LogWarning("[AvianVoice] StudyFlowController: no TaskManager in the scene.");
                return;
            }

            if (useFourSceneFlow)
                TaskManager.SetFlow(flow);

            if (useSingingTrials)
            {
                var host = GameObject.Find("TaskRoot") ?? gameObject;
                Trials = host.GetComponent<SingingTrialTask>() ?? host.AddComponent<SingingTrialTask>();
                TaskManager.BindTaskBehaviour(SingingTrialTask.TaskId, Trials);
            }

            Recorder?.LogEvent("study_flow", j => j
                .AddArray("flow", TaskManager.TaskIds)
                .Add("singing_trials", useSingingTrials)
                .Add("total_trials", Trials != null ? Trials.TotalTrials : 0));

            Debug.Log("[AvianVoice] Study flow: " + string.Join(" > ", TaskManager.TaskIds)
                      + (Trials != null ? " | singing trials " + Trials.TotalTrials : "")
                      + (Recorder != null ? " | recording " + Recorder.SessionDirectory : " | not recording"));
        }

        void Update()
        {
            if (avatar == null || Recorder == null)
                return;

            var label = avatar.CurrentLabel;
            if (label != lastAvatarLabel)
            {
                lastAvatarLabel = label;
                Recorder.CurrentAvatar = label;
                Recorder.LogEvent("avatar_changed", j => j.Add("kind", avatar.Current.ToString()).Add("human_id", avatar.HumanAvatarId));
            }
        }
    }
}
