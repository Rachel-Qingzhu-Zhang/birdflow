#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AvianVoice.Audio;
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.Embodiment.Avatars;
using AvianVoice.Study;
using AvianVoice.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Experimenter panel (PC side). While the participant wears the headset, the experimenter drives the
    /// study from here: scene (task) transitions, the singing trials (3 pitches x 10), bird / human avatar,
    /// participant input policy, recording status. Menu: Avian Voice / Experimenter Panel.
    /// The panel is in English; the participant-facing text in the headset stays bilingual (AvianUiCopy).
    /// </summary>
    public class AvianExperimenterWindow : EditorWindow
    {
        const string EmbodimentScenePath = AvianEmbodimentSceneSetup.ScenePath;
        const string ParticipantIdEditorKey = "AvianVoice.Experimenter.ParticipantId";

        static readonly Dictionary<string, string> TaskLabels = new()
        {
            { "practice", "Practice (explain the task)" },
            { "meet", "Meet the bird" },
            { "breath_sync", "Breathing" },
            { "singing", "Singing" },
            { "flying", "Flying" },
            { "call_friends", "Call friends" },
            { "reflection", "Rest" }
        };

        static readonly string[] OrderLabels = { "Blocked (A×10 → B×10 → C×10)", "Alternating (A B C A B C …)", "Random (10 each)" };

        double nextRepaint;
        Vector2 scroll;
        float practiceCustomHz = 440f;
        float outputLevel;
        float outputPeakHold;
        string participantId;
        GUIStyle bigButton;

        [SerializeField] bool shownAsUtility;

        [MenuItem("Avian Voice/Experimenter Panel")]
        public static void Open() => ShowPanel(true);

        /// <summary>
        /// Shows the panel in front of the main editor window. A new panel is a floating utility window placed
        /// over the right side of the main window: it stays above the editor when Play mode focuses the Game view,
        /// and it cannot end up on a display that is gone. A panel the experimenter docked stays where it is.
        /// </summary>
        public static AvianExperimenterWindow ShowPanel(bool focus)
        {
            var existing = Resources.FindObjectsOfTypeAll<AvianExperimenterWindow>();
            var window = existing.Length > 0 ? existing[0] : null;

            // A plain floating panel (from the old menu code or a saved layout) drops behind the main window
            // when Play starts; replace it with a utility window.
            if (window != null && !window.docked && !window.shownAsUtility)
            {
                window.Close();
                window = null;
            }

            if (window == null)
            {
                window = CreateInstance<AvianExperimenterWindow>();
                window.shownAsUtility = true;
                window.ShowUtility();
                var rect = PlaceOverMainWindow();
                window.position = rect;
                Debug.Log("[AvianVoice] Experimenter panel opened at " + rect);
            }
            else if (!window.docked && !IsOnMainWindow(window.position))
            {
                var rect = PlaceOverMainWindow();
                window.position = rect;
                Debug.Log("[AvianVoice] Experimenter panel moved back onto the main window at " + rect);
            }

            if (focus)
                window.Focus();
            return window;
        }

        static Rect PlaceOverMainWindow()
        {
            var main = EditorGUIUtility.GetMainWindowPosition();
            if (main.width < 400f || main.height < 300f)
                return new Rect(100f, 100f, 420f, 720f);

            var width = 420f;
            var height = Mathf.Clamp(main.height - 80f, 520f, 900f);
            return new Rect(main.xMax - width - 16f, main.y + 40f, width, height);
        }

        static bool IsOnMainWindow(Rect rect)
        {
            if (rect.width < 100f || rect.height < 100f)
                return false;
            var main = EditorGUIUtility.GetMainWindowPosition();
            return main.width < 400f || main.Overlaps(rect);
        }

        /// <summary>The study is driven from this panel, so it opens by itself when Play starts in the embodiment scene.</summary>
        [InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || SceneManager.GetActiveScene().path != EmbodimentScenePath)
                return;
            // After the Game view has taken focus for Play.
            EditorApplication.delayCall += () => ShowPanel(false);
        }

        void OnEnable()
        {
            titleContent = new GUIContent("Avian Experimenter");
            minSize = new Vector2(380f, 520f);
            participantId = EditorPrefs.GetString(ParticipantIdEditorKey, StudyFlowController.ResolveParticipantId("P000"));
            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;

            // Output meter: sampled every editor tick so short chirps are not missed between repaints.
            var level = AvianEmbodimentSceneSetup.MeasureListenerRms();
            outputLevel = Mathf.Max(level, outputLevel * 0.9f);
            outputPeakHold = Mathf.Max(level, outputPeakHold * 0.995f);

            if (EditorApplication.timeSinceStartup < nextRepaint)
                return;

            nextRepaint = EditorApplication.timeSinceStartup + 0.1;
            Repaint();
        }

        void OnGUI()
        {
            bigButton ??= new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = true };

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSceneSection();
            EditorGUILayout.Space(8f);

            var playing = EditorApplication.isPlaying && !EditorApplication.isCompiling;
            var taskManager = playing ? FindAnyObjectByType<TaskManager>() : null;
            var controller = playing ? FindAnyObjectByType<AvatarEmbodimentController>() : null;
            var flow = playing ? FindAnyObjectByType<StudyFlowController>() : null;
            var trials = playing ? (flow != null && flow.Trials != null ? flow.Trials : FindTrialTask(false)) : null;
            var practice = playing ? (flow != null && flow.Practice != null ? flow.Practice : FindTrialTask(true)) : null;
            var recorder = playing ? StudyRecorder.Instance : null;

            DrawStatusSection(playing, taskManager, controller);
            EditorGUILayout.Space(8f);
            DrawTaskSection(playing, taskManager, trials);
            EditorGUILayout.Space(8f);
            DrawPracticeSection(playing, taskManager, practice, trials);
            EditorGUILayout.Space(8f);
            DrawTrialSection(playing, taskManager, trials);
            EditorGUILayout.Space(8f);
            DrawRecordingSection(playing, recorder);
            EditorGUILayout.Space(8f);
            DrawConditionSection(playing, controller, flow);
            EditorGUILayout.Space(8f);
            DrawParticipantSection(playing, controller);
            EditorGUILayout.EndScrollView();
        }

        void DrawSceneSection()
        {
            EditorGUILayout.LabelField("Scene", EditorStyles.boldLabel);
            var active = SceneManager.GetActiveScene();
            var isEmbodiment = active.path == EmbodimentScenePath;
            EditorGUILayout.LabelField("Open scene", active.name + (isEmbodiment ? "" : "  ← body tracking only works in Avian_Lab_Embodiment"));

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                var id = EditorGUILayout.TextField("Participant ID (set before Play)", participantId);
                if (id != participantId)
                {
                    participantId = id;
                    EditorPrefs.SetString(ParticipantIdEditorKey, participantId);
                    StudyFlowController.StoreParticipantId(participantId);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || isEmbodiment))
                {
                    if (GUILayout.Button("Open Avian_Lab_Embodiment"))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                            EditorSceneManager.OpenScene(EmbodimentScenePath);
                    }
                }

                if (!EditorApplication.isPlaying)
                {
                    if (GUILayout.Button("▶ Play"))
                    {
                        StudyFlowController.StoreParticipantId(participantId);
                        EditorApplication.isPlaying = true;
                    }
                }
                else if (GUILayout.Button("■ Stop (closes the recording)"))
                {
                    EditorApplication.isPlaying = false;
                }
            }
        }

        void DrawStatusSection(bool playing, TaskManager taskManager, AvatarEmbodimentController controller)
        {
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            if (!playing)
            {
                EditorGUILayout.HelpBox("Enter Play mode to control the scene, the trials and the avatar from here.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Scene", taskManager == null ? "no TaskManager"
                : taskManager.CurrentIsSideTask ? LabelFor(taskManager.CurrentTaskId) + "  (" + taskManager.CurrentTaskId + ", outside the flow)"
                : (taskManager.CurrentIndex + 1) + " / " + taskManager.TaskCount + "  " + LabelFor(taskManager.CurrentTaskId) + "  (" + taskManager.CurrentTaskId + ")");

            if (controller == null)
            {
                EditorGUILayout.HelpBox("No AvatarEmbodimentController. Play Avian_Lab_Embodiment.", MessageType.Warning);
                return;
            }

            var tracking = controller.BodyTrackingValid
                ? (controller.Frame.IsFullBody ? "full body" : "upper body") + "  (confidence " + controller.Frame.Confidence.ToString("0.00") + ")"
                : controller.Backend == null ? "none (no backend)"
                : !controller.Backend.IsRunning ? "none (tracker not running)"
                : "none (waiting for data: is the headset worn?)";
            EditorGUILayout.LabelField("Avatar", controller.Current == AvatarKind.Bird ? "Bird" : "Human (" + controller.HumanAvatarId + ")");
            var flowForStatus = FindAnyObjectByType<StudyFlowController>();
            if (flowForStatus != null)
                EditorGUILayout.LabelField("Condition", flowForStatus.ConditionLabel + "   (the participant's voice is heard as: " + flowForStatus.EffectiveFeedbackName + ")");
            EditorGUILayout.LabelField("Body tracking", tracking);
            EditorGUILayout.LabelField("Participant controllers", ParticipantInputPolicy.ControllersEnabled ? "enabled" : "disabled (hands, head and voice only)");
            DrawMicrophoneStatus(playing);

            var meterRect = EditorGUILayout.GetControlRect(false, 16f);
            var labelRect = new Rect(meterRect.x, meterRect.y, EditorGUIUtility.labelWidth, meterRect.height);
            var barRect = new Rect(meterRect.x + EditorGUIUtility.labelWidth, meterRect.y, meterRect.width - EditorGUIUtility.labelWidth, meterRect.height);
            EditorGUI.LabelField(labelRect, "Audio output (PC)");
            var normalized = Mathf.Clamp01(Mathf.Log10(1f + outputLevel * 30f));
            EditorGUI.ProgressBar(barRect, normalized, outputPeakHold > 0.001f ? "sound playing" : "silent");
            DrawPcAudioSection();

            if (GUILayout.Button("Log details to the Console (Log Avatar State)"))
                AvianEmbodimentSceneSetup.LogAvatarState();
        }

        double nextOutputCheck;

        void DrawPcAudioSection()
        {
            if (EditorApplication.timeSinceStartup >= nextOutputCheck)
            {
                nextOutputCheck = EditorApplication.timeSinceStartup + 2.0;
                PcAudioMonitor.RefreshDefaultOutput();
            }

            var name = string.IsNullOrEmpty(PcAudioMonitor.DefaultOutputName) ? "(unknown" + (string.IsNullOrEmpty(PcAudioMonitor.DefaultOutputError) ? "" : ": " + PcAudioMonitor.DefaultOutputError) + ")" : PcAudioMonitor.DefaultOutputName;
            var vol = PcAudioMonitor.DefaultOutputVolume >= 0f ? "  volume " + Mathf.RoundToInt(PcAudioMonitor.DefaultOutputVolume * 100f) + "%" : "";
            EditorGUILayout.LabelField("Windows default output", name + vol + (PcAudioMonitor.DefaultOutputMuted ? "  MUTED" : ""));
            if (PcAudioMonitor.DefaultOutputMuted)
            {
                EditorGUILayout.HelpBox("The Windows default playback device is muted. Unity's audio goes there, so nothing is heard on the PC.", MessageType.Warning);
                if (GUILayout.Button("Unmute the Windows output"))
                {
                    if (!PcAudioMonitor.UnmuteDefaultOutput())
                        Debug.LogWarning("[AvianVoice] Could not unmute the Windows default output: " + PcAudioMonitor.DefaultOutputError);
                }
            }

            if (!string.IsNullOrEmpty(PcAudioMonitor.UnityOutputName))
            {
                var uvol = PcAudioMonitor.UnityOutputVolume >= 0f ? "  volume " + Mathf.RoundToInt(PcAudioMonitor.UnityOutputVolume * 100f) + "%" : "";
                EditorGUILayout.LabelField("Unity output device", PcAudioMonitor.UnityOutputName + uvol + (PcAudioMonitor.UnityOutputMuted ? "  MUTED" : ""));
                if (PcAudioMonitor.UnityOutputMuted)
                {
                    EditorGUILayout.HelpBox("Unity's audio goes to \"" + PcAudioMonitor.UnityOutputName + "\", which is muted in Windows, so nothing is heard there (the headset over Link). The PC speakers only get sound through the PC monitor output below.", MessageType.Warning);
                    if (GUILayout.Button("Unmute Unity's output device"))
                    {
                        if (!PcAudioMonitor.UnmuteUnityOutput())
                            Debug.LogWarning("[AvianVoice] Could not unmute Unity's output device: " + PcAudioMonitor.DefaultOutputError);
                    }
                }
                else if (PcAudioMonitor.UnityOutputName != PcAudioMonitor.DefaultOutputName)
                {
                    EditorGUILayout.LabelField(" ", "Unity does not follow the Windows default here (the Oculus runtime routes it to the headset device). The PC monitor output below mirrors it to the PC speakers.", EditorStyles.wordWrappedMiniLabel);
                }
            }

            if (EditorUtility.audioMasterMute)
            {
                EditorGUILayout.HelpBox("\"Mute Audio\" is enabled in the Unity Game view.", MessageType.Warning);
                if (GUILayout.Button("Disable Unity's Mute Audio"))
                    EditorUtility.audioMasterMute = false;
            }

            var monitor = PcAudioMonitor.Instance;
            if (monitor == null)
            {
                var storedMode = PlayerPrefs.GetString(PcAudioMonitor.ModePrefKey, PcAudioMonitor.MonitorMode.Auto.ToString());
                EditorGUILayout.LabelField("PC monitor output", "active in Play mode (mode: " + storedMode + ")");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var newMode = (PcAudioMonitor.MonitorMode)EditorGUILayout.EnumPopup("PC monitor output", monitor.Mode);
                if (newMode != monitor.Mode)
                    monitor.Mode = newMode;
                if (GUILayout.Button("Recheck", GUILayout.Width(60f)))
                    monitor.Refresh();
            }

            EditorGUILayout.LabelField(" ", monitor.Status + (monitor.DroppedChunks > 0 ? "  (dropped " + monitor.DroppedChunks + ")" : ""), EditorStyles.miniLabel);
            var newDevice = EditorGUILayout.TextField("  Device (part of the name)", monitor.DeviceNameContains);
            if (newDevice != monitor.DeviceNameContains)
                monitor.DeviceNameContains = newDevice;
        }

        /// <summary>The input device actually capturing (name, rate, live level) and a chooser (remembered across sessions).</summary>
        void DrawMicrophoneStatus(bool playing)
        {
            var mic = playing ? FindAnyObjectByType<MicrophoneCapture>() : null;
            var analyzer = playing ? FindAnyObjectByType<VoiceAnalyzer>() : null;
            var devices = Microphone.devices ?? new string[0];

            string inUse;
            if (mic == null)
                inUse = playing ? "no MicrophoneCapture in the scene" : "(shown in Play mode)";
            else if (mic.IsCapturing && !string.IsNullOrEmpty(mic.DeviceName))
                inUse = mic.DeviceName + "   capturing @" + mic.SampleRate + " Hz" + (mic.Paused ? "  (paused: test running)" : "");
            else
                inUse = "NOT capturing" + (string.IsNullOrEmpty(mic.DeviceName) ? "" : " (" + mic.DeviceName + ")") + " — restarting…";
            EditorGUILayout.LabelField("Microphone in use", inUse, mic != null && !mic.IsCapturing ? EditorStyles.boldLabel : EditorStyles.label);

            var preferred = MicrophoneCapture.PreferredDevice;
            var names = new string[devices.Length + 1];
            names[0] = "Auto (headset mic while XR runs, else the first physical mic)";
            var index = 0;
            for (var i = 0; i < devices.Length; i++)
            {
                names[i + 1] = devices[i];
                if (devices[i] == preferred)
                    index = i + 1;
            }

            if (!string.IsNullOrEmpty(preferred) && index == 0)
                names[0] += "   [preferred '" + preferred + "' not connected]";
            var chosen = EditorGUILayout.Popup("Input device", index, names);
            if (chosen != index)
            {
                var name = chosen == 0 ? "" : devices[chosen - 1];
                if (mic != null)
                    mic.SetDevice(name);
                else
                    MicrophoneCapture.PreferredDevice = name;
            }

            if (analyzer != null)
            {
                var v = analyzer.Current;
                var rect = EditorGUILayout.GetControlRect(false, 14f);
                EditorGUI.ProgressBar(rect, Mathf.Clamp01(v.loudness), "Input level " + (v.voiced > 0.5f ? "● voiced" : "silent")
                    + (v.pitchHz > 0f ? "  " + v.pitchHz.ToString("0") + " Hz" : ""));
            }
        }

        void DrawTaskSection(bool playing, TaskManager taskManager, SingingTrialTask trials)
        {
            EditorGUILayout.LabelField("Scene flow", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing || taskManager == null))
            {
                if (taskManager == null || taskManager.HasTask(SingingTrialTask.PracticeTaskId))
                {
                    var inPractice = taskManager != null && taskManager.CurrentTaskId == SingingTrialTask.PracticeTaskId;
                    using (new EditorGUI.DisabledScope(inPractice))
                    {
                        if (GUILayout.Button((inPractice ? "● " : "   ") + "P " + LabelFor(SingingTrialTask.PracticeTaskId) + "   (practice, not recorded)   [0]", GUILayout.Height(26f)))
                            taskManager.LoadTaskById(SingingTrialTask.PracticeTaskId);
                    }
                }

                var ids = taskManager != null ? taskManager.TaskIds : StudyFlowController.FourSceneFlow;
                for (var i = 0; i < ids.Length; i++)
                {
                    var current = taskManager != null && taskManager.CurrentTaskId == ids[i];
                    var label = (i + 1) + " " + LabelFor(ids[i]) + "   (" + ids[i] + ")";
                    if (ids[i] == SingingTrialTask.TaskId && trials != null)
                        label += "  " + trials.TotalTrials + " trials";
                    using (new EditorGUI.DisabledScope(current))
                    {
                        if (GUILayout.Button((current ? "● " : "   ") + label, GUILayout.Height(26f)))
                            taskManager.LoadTaskById(ids[i]);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("◀ Previous scene"))
                        taskManager.StepTask(-1);
                    if (GUILayout.Button("Next scene ▶"))
                        taskManager.StepTask(1);
                }
            }
        }

        /// <summary>
        /// Practice scene: the singing task with a pitch the experimenter picks freely, no schedule and no end,
        /// for explaining the task and showing the conditions (Condition section) before the study starts.
        /// </summary>
        void DrawPracticeSection(bool playing, TaskManager taskManager, SingingTrialTask practice, SingingTrialTask trials)
        {
            EditorGUILayout.LabelField("Practice (explain the task)", EditorStyles.boldLabel);
            if (!playing)
            {
                EditorGUILayout.HelpBox("In Play mode the session begins in the Practice scene (outside the 1-4 flow): pick any pitch, press \"Teacher bird sings\" as often as you like and switch avatar / voice in the Condition section while you explain the task. Nothing is recorded until you move to scene 1.", MessageType.None);
                return;
            }

            if (practice == null)
            {
                EditorGUILayout.HelpBox("No practice task (Include Practice on Avian Embodiment / StudyFlowController).", MessageType.Warning);
                return;
            }

            var practiceActive = taskManager != null && taskManager.CurrentTaskId == SingingTrialTask.PracticeTaskId && practice.isActiveAndEnabled;
            if (!practiceActive)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox("Practice is outside the numbered flow: \"Go to Practice\" (or key 0) to explain the task before the study.", MessageType.Info);
                    using (new EditorGUI.DisabledScope(taskManager == null))
                    {
                        if (GUILayout.Button("Go to Practice", GUILayout.Height(38f), GUILayout.Width(110f)))
                            taskManager.LoadTaskById(SingingTrialTask.PracticeTaskId);
                    }
                }

                return;
            }

            var c = CultureInfo.InvariantCulture;

            // Pitch: the protocol pitches as buttons, or any value.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Teacher pitch");
                var pitches = trials != null ? trials.PitchesHz : practice.PitchesHz;
                foreach (var hz in pitches)
                {
                    var selected = Mathf.Abs(practice.PracticePitchHz - hz) < 0.5f;
                    if (GUILayout.Toggle(selected, BirdSongPhrase.NoteName(hz) + " " + hz.ToString("0", c), "Button") && !selected)
                        practice.PracticePitchHz = hz;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Custom (Hz)");
                practiceCustomHz = EditorGUILayout.FloatField(practiceCustomHz, GUILayout.Width(60f));
                var customSelected = Mathf.Abs(practice.PracticePitchHz - practiceCustomHz) < 0.5f;
                using (new EditorGUI.DisabledScope(practiceCustomHz <= 0f || customSelected))
                {
                    if (GUILayout.Button(BirdSongPhrase.NoteName(Mathf.Max(1f, practiceCustomHz)) + "  use", GUILayout.Width(90f)))
                        practice.PracticePitchHz = practiceCustomHz;
                }

                GUILayout.FlexibleSpace();
            }

            // Teacher note / running practice trial.
            var running = practice.Phase == TrialPhase.Guide || practice.Phase == TrialPhase.Response;
            var note = BirdSongPhrase.NoteName(practice.PracticePitchHz) + " (" + practice.PracticePitchHz.ToString("0", c) + " Hz)";
            if (running)
            {
                var state = practice.Phase == TrialPhase.Guide ? "teacher bird singing"
                    : practice.IsVoiceActive ? "participant singing" : practice.Current != null && practice.Current.HadVoice ? "voice ended" : "waiting for the participant's voice";
                EditorGUILayout.LabelField("Practice trial " + (practice.Current != null ? practice.Current.trial : 0) + " running: " + state
                                           + "   elapsed " + practice.CurrentTrialSeconds.ToString("0.0", c) + " s");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button((running ? "▶ Again: teacher bird sings " : "▶ Teacher bird sings ") + note + "   [Space]", bigButton, GUILayout.Height(44f)))
                    practice.StartNextTrial("experimenter_panel_practice");
                using (new EditorGUI.DisabledScope(!running))
                {
                    if (GUILayout.Button("End (idle)", GUILayout.Height(44f), GUILayout.Width(90f)))
                        practice.EndTrialNow("experimenter_panel_practice");
                }
            }

            if (practice.Last != null)
                EditorGUILayout.LabelField("Last", practice.Last.Summary(), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Practice trials so far: " + practice.CompletedTrials + ". Show the conditions with the Condition buttons below; "
                                       + "then press scene 1 (" + LabelFor("meet") + ") to start the study — recording starts at that moment.",
                EditorStyles.wordWrappedMiniLabel);
        }

        static SingingTrialTask FindTrialTask(bool practice)
        {
            foreach (var task in FindObjectsByType<SingingTrialTask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (task != null && task.IsPractice == practice)
                    return task;
            }

            return null;
        }

        /// <summary>1-based position of a task in the session flow (the digit key that selects it), or "?".</summary>
        static string SceneNumber(TaskManager taskManager, string taskId)
        {
            var ids = taskManager != null ? taskManager.TaskIds : StudyFlowController.FourSceneFlow;
            var index = System.Array.IndexOf(ids, taskId);
            return index >= 0 ? (index + 1).ToString() : "?";
        }

        void DrawTrialSection(bool playing, TaskManager taskManager, SingingTrialTask trials)
        {
            EditorGUILayout.LabelField("Singing trials", EditorStyles.boldLabel);
            if (!playing)
            {
                EditorGUILayout.HelpBox("In Play mode, in the Singing scene, press \"Start trial\": the teacher bird sings and the participant sings back. One trial = from one press to the next (no waiting; the button is always available). After 3 pitches × 10 = 30 trials, one more press closes the last trial and moves on to the Rest scene.", MessageType.None);
                return;
            }

            if (trials == null)
            {
                EditorGUILayout.HelpBox("No SingingTrialTask (check StudyFlowController / Use Singing Trials on Avian Embodiment).", MessageType.Warning);
                return;
            }

            var singingActive = taskManager != null && taskManager.CurrentTaskId == SingingTrialTask.TaskId && trials.isActiveAndEnabled;

            // Protocol (locked once the first trial has started).
            using (new EditorGUI.DisabledScope(trials.HasStarted))
            {
                var pitches = (float[])trials.PitchesHz.Clone();
                var changed = false;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("Pitches (Hz)");
                    for (var i = 0; i < pitches.Length; i++)
                    {
                        var v = EditorGUILayout.FloatField(pitches[i], GUILayout.MinWidth(50f));
                        if (!Mathf.Approximately(v, pitches[i]) && v > 0f)
                        {
                            pitches[i] = v;
                            changed = true;
                        }
                    }
                }

                var noteNames = new string[pitches.Length];
                for (var i = 0; i < pitches.Length; i++)
                    noteNames[i] = BirdSongPhrase.NoteName(pitches[i]);
                EditorGUILayout.LabelField(" ", string.Join("  /  ", noteNames), EditorStyles.miniLabel);

                var perPitch = EditorGUILayout.IntField("Trials per pitch", trials.TrialsPerPitch);
                if (perPitch != trials.TrialsPerPitch && perPitch > 0)
                    changed = true;

                var order = (TrialOrder)EditorGUILayout.Popup("Order", (int)trials.Order, OrderLabels);
                if (order != trials.Order)
                    changed = true;

                if (changed)
                    trials.Configure(pitches, perPitch, order, 0);
            }

            // Progress.
            var total = trials.TotalTrials;
            var done = trials.CompletedTrials;
            var progressRect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.ProgressBar(progressRect, total > 0 ? done / (float)total : 0f, "Trials " + done + " / " + total + " done");

            if (!singingActive)
            {
                EditorGUILayout.HelpBox(trials.IsFinished
                    ? "All " + total + " trials are done. \"Restart from trial 1\" starts over."
                    : "Switch to the Singing scene (" + SceneNumber(taskManager, SingingTrialTask.TaskId) + ") to start the trials.", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!singingActive))
            {
                var c = CultureInfo.InvariantCulture;
                switch (trials.Phase)
                {
                    case TrialPhase.Idle:
                    {
                        var hz = trials.CurrentTargetHz;
                        var label = "▶ Start trial " + trials.NextTrialNumber + " / " + total + " — teacher bird sings "
                                    + trials.CurrentNoteName + " (" + hz.ToString("0", c) + " Hz)   [Space]";
                        if (GUILayout.Button(label, bigButton, GUILayout.Height(44f)))
                            trials.StartNextTrial("experimenter_panel");
                        break;
                    }
                    case TrialPhase.Guide when trials.TrialUntilNextPress:
                    case TrialPhase.Response when trials.TrialUntilNextPress:
                    {
                        // One trial = from this press to the next: the button is always available.
                        var running = trials.Current != null ? trials.Current.trial : 0;
                        var state = trials.Phase == TrialPhase.Guide ? "teacher bird singing"
                            : trials.IsVoiceActive ? "participant singing" : trials.Current != null && trials.Current.HadVoice ? "voice ended" : "waiting for the participant's voice";
                        EditorGUILayout.LabelField("Trial " + running + " / " + total + " running: " + state
                                                   + "   elapsed " + trials.CurrentTrialSeconds.ToString("0.0", c) + " s");
                        var isLast = running >= total;
                        var label = isLast
                            ? "■ End the last trial → Rest scene   [Space]"
                            : "▶ Next trial " + (running + 1) + " / " + total + " — teacher bird sings " + trials.NextNoteName + " (" + trials.NextTargetHz.ToString("0", c) + " Hz)   [Space]";
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(label, bigButton, GUILayout.Height(44f)))
                                trials.StartNextTrial("experimenter_panel");
                            if (GUILayout.Button("End (idle)", GUILayout.Height(44f), GUILayout.Width(90f)))
                                trials.EndTrialNow("experimenter_panel");
                        }

                        break;
                    }
                    case TrialPhase.Guide:
                        using (new EditorGUI.DisabledScope(true))
                            GUILayout.Button("♪ Teacher bird is singing " + trials.CurrentNoteName + "…", bigButton, GUILayout.Height(44f));
                        break;
                    case TrialPhase.Response:
                    {
                        var voice = trials.IsVoiceActive ? "singing" : trials.Current != null && trials.Current.HadVoice ? "voice ended, waiting" : "waiting for the participant's voice";
                        EditorGUILayout.LabelField("Trial " + (trials.Current != null ? trials.Current.trial : 0) + ": " + voice
                                                   + "   " + trials.ResponseSecondsLeft.ToString("0.0", c) + " s left");
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("End this trial and start the next ▶", bigButton, GUILayout.Height(36f)))
                                trials.StartNextTrial("experimenter_panel_next");
                            if (GUILayout.Button("End this trial (idle)", GUILayout.Height(36f), GUILayout.Width(150f)))
                                trials.EndTrialNow("experimenter_panel");
                        }

                        break;
                    }
                    case TrialPhase.Finished:
                        EditorGUILayout.LabelField("All " + total + " trials done." + (singingActive ? " Moving on to the Rest scene." : ""), EditorStyles.boldLabel);
                        break;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(trials.Phase != TrialPhase.Idle || done == 0))
                    {
                        if (GUILayout.Button("Repeat the last trial"))
                            trials.RepeatLastTrial("experimenter_panel");
                    }

                    using (new EditorGUI.DisabledScope(!trials.HasStarted))
                    {
                        if (GUILayout.Button("Restart from trial 1 (reset)")
                            && EditorUtility.DisplayDialog("Reset trials", "Start again from trial 1. Data already recorded stays in the files.", "Reset", "Cancel"))
                            trials.ResetProtocol("experimenter_panel");
                    }
                }
            }

            if (trials.Last != null)
                EditorGUILayout.LabelField("Last trial", trials.Last.Summary(), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("Keyboard: Space / R = next trial (when the Game view has focus)", EditorStyles.miniLabel);
        }

        void DrawRecordingSection(bool playing, StudyRecorder recorder)
        {
            EditorGUILayout.LabelField("Recording", EditorStyles.boldLabel);
            var root = StudyRecorder.RootDirectory;
            var flowForRecording = playing ? FindAnyObjectByType<StudyFlowController>() : null;
            if (playing && (recorder == null || !recorder.SessionOpen) && flowForRecording != null && flowForRecording.RecordingPending)
            {
                EditorGUILayout.HelpBox("Not recording yet: the session (events.jsonl, mic.wav, output.wav, trials.csv) starts when the first study scene (1 " + LabelFor("meet") + ") begins. The practice is not recorded.", MessageType.Info);
            }
            else if (playing && recorder != null && recorder.SessionOpen)
            {
                var c = CultureInfo.InvariantCulture;
                EditorGUILayout.LabelField("Session", recorder.SessionId);
                EditorGUILayout.LabelField("Microphone", recorder.MicrophoneRecording
                    ? recorder.MicSecondsWritten.ToString("0", c) + " s  @" + recorder.MicSampleRate + " Hz  " + recorder.MicDeviceName
                    : "idle (microphone not started)");
                EditorGUILayout.LabelField("Output audio", recorder.OutputRecording
                    ? recorder.OutputSecondsWritten.ToString("0", c) + " s  @" + recorder.OutputSampleRate + " Hz"
                    : "idle");
                EditorGUILayout.LabelField("Trial rows", recorder.TrialRowsWritten.ToString(c));
                EditorGUILayout.LabelField("Folder", recorder.SessionDirectory, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("Open this session's folder"))
                    EditorUtility.RevealInFinder(Path.Combine(recorder.SessionDirectory, "session.json"));
            }
            else
            {
                EditorGUILayout.LabelField("Folder", root, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.HelpBox("Play mode writes events.jsonl / trials.csv / mic.wav / output.wav / segments/ into a folder per session, inside the project (StudyData/, not shared through git).", MessageType.None);
                if (GUILayout.Button("Open the recordings folder"))
                {
                    Directory.CreateDirectory(root);
                    EditorUtility.RevealInFinder(root);
                }
            }
        }

        void DrawConditionSection(bool playing, AvatarEmbodimentController controller, StudyFlowController flow)
        {
            EditorGUILayout.LabelField("Condition (avatar × voice feedback)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing || controller == null))
            {
                var humanAvailable = controller != null && controller.HasHumanAvatar(controller.HumanAvatarId);
                DrawConditionRow(controller, flow, AvatarKind.Bird, "Bird + bird voice", "Bird + own voice only", true);
                DrawConditionRow(controller, flow, AvatarKind.Human, "Human + human voice", "Human + own voice only", humanAvailable);

                var options = controller != null ? controller.HumanAvatars : null;
                if (options != null && options.Length > 1)
                {
                    var names = new string[options.Length];
                    var index = 0;
                    for (var i = 0; i < options.Length; i++)
                    {
                        names[i] = options[i].label + (controller.HasHumanAvatar(options[i].id) ? "" : " (missing)");
                        if (options[i].id == controller.HumanAvatarId)
                            index = i;
                    }

                    var chosen = EditorGUILayout.Popup("Human body", index, names);
                    if (chosen != index)
                        controller.SetHumanAvatar(options[chosen].id);
                }
            }

            EditorGUILayout.LabelField(
                "Converted voice = the participant's own voice heard as the avatar's voice (bird chirps / sung human vowel). " +
                "Own voice only = nothing from the app. The teacher bird sings the guide note in every condition. " +
                "Keyboard: V = bird ⇄ human (keeps the voice setting). \"missing\" = model not found in Resources.",
                EditorStyles.wordWrappedMiniLabel);
        }

        void DrawConditionRow(AvatarEmbodimentController controller, StudyFlowController flow, AvatarKind kind,
            string labelConverted, string labelOwn, bool available)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var i = 0; i < 2; i++)
                {
                    var converted = i == 0;
                    var active = controller != null && controller.Current == kind
                                 && (flow == null ? converted : flow.VoiceFeedbackOn == converted);
                    var label = (active ? "● " : "") + (converted ? labelConverted : labelOwn) + (available ? "" : "  (model missing)");
                    using (new EditorGUI.DisabledScope(active || !available))
                    {
                        if (GUILayout.Button(label, GUILayout.Height(30f)))
                        {
                            if (flow != null)
                                flow.SetCondition(kind, converted);
                            else if (controller != null)
                                controller.SetAvatar(kind);
                        }
                    }
                }
            }
        }

        void DrawParticipantSection(bool playing, AvatarEmbodimentController controller)
        {
            EditorGUILayout.LabelField("Participant input", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing))
            {
                var controllers = EditorGUILayout.ToggleLeft(
                    "Enable XR controllers (stick turning, menu, X/B buttons)",
                    ParticipantInputPolicy.ControllersEnabled);
                if (controllers != ParticipantInputPolicy.ControllersEnabled)
                    ParticipantInputPolicy.ControllersEnabled = controllers;

                var hands = EditorGUILayout.ToggleLeft(
                    "Show hand meshes and rays (tracking is always on)",
                    ParticipantInputPolicy.HandsVisible);
                if (hands != ParticipantInputPolicy.HandsVisible)
                    ParticipantInputPolicy.HandsVisible = hands;

                if (controller != null)
                {
                    var hint = EditorGUILayout.ToggleLeft("Show the status card below the participant's view", controller.HintVisible);
                    if (hint != controller.HintVisible)
                        controller.HintVisible = hint;
                }

                if (GUILayout.Button("Log audio state to the Console"))
                    AvianEmbodimentSceneSetup.LogAudioState();
            }

            EditorGUILayout.HelpBox(
                "Controller input is ignored by default (Participant Controllers Enabled on Avian Embodiment). " +
                "The participant takes part with head, hands, body and voice only; scenes, trials and the avatar are driven from this panel or the PC keyboard (1-4 = scene, 0 = practice, N, Space, V).",
                MessageType.None);
        }

        static string LabelFor(string taskId)
        {
            return taskId != null && TaskLabels.TryGetValue(taskId, out var label) ? label : taskId ?? "";
        }
    }
}
#endif
