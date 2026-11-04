#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// study from here: scene (task) transitions, the singing trials (3 pitches x 20), bird / human avatar,
    /// participant input policy, recording status. Menu: Avian Voice / Experimenter Panel.
    /// </summary>
    public class AvianExperimenterWindow : EditorWindow
    {
        const string EmbodimentScenePath = AvianEmbodimentSceneSetup.ScenePath;
        const string ParticipantIdEditorKey = "AvianVoice.Experimenter.ParticipantId";

        static readonly Dictionary<string, string> TaskLabelsJa = new()
        {
            { "meet", "鳥に会う" },
            { "breath_sync", "呼吸" },
            { "singing", "歌う" },
            { "flying", "飛ぶ" },
            { "call_friends", "仲間を呼ぶ" },
            { "reflection", "休む" }
        };

        static readonly string[] OrderLabels = { "ブロック (A×20 → B×20 → C×20)", "交互 (A B C A B C …)", "ランダム (各 20 回)" };

        double nextRepaint;
        Vector2 scroll;
        float outputLevel;
        float outputPeakHold;
        string participantId;
        GUIStyle bigButton;

        [MenuItem("Avian Voice/Experimenter Panel (実験者パネル)")]
        public static void Open()
        {
            var window = GetWindow<AvianExperimenterWindow>();
            window.titleContent = new GUIContent("Avian Experimenter");
            window.minSize = new Vector2(380f, 520f);
            window.Show();
        }

        void OnEnable()
        {
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
            var trials = playing ? FindAnyObjectByType<SingingTrialTask>(FindObjectsInactive.Include) : null;
            var recorder = playing ? StudyRecorder.Instance : null;

            DrawStatusSection(playing, taskManager, controller);
            EditorGUILayout.Space(8f);
            DrawTaskSection(playing, taskManager, trials);
            EditorGUILayout.Space(8f);
            DrawTrialSection(playing, taskManager, trials);
            EditorGUILayout.Space(8f);
            DrawRecordingSection(playing, recorder);
            EditorGUILayout.Space(8f);
            DrawAvatarSection(playing, controller);
            EditorGUILayout.Space(8f);
            DrawParticipantSection(playing, controller);
            EditorGUILayout.EndScrollView();
        }

        void DrawSceneSection()
        {
            EditorGUILayout.LabelField("シーン / Scene", EditorStyles.boldLabel);
            var active = SceneManager.GetActiveScene();
            var isEmbodiment = active.path == EmbodimentScenePath;
            EditorGUILayout.LabelField("開いているシーン", active.name + (isEmbodiment ? "" : "  ← 体トラッキングは Avian_Lab_Embodiment だけ"));

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                var id = EditorGUILayout.TextField("被験者 ID (Play 前に設定)", participantId);
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
                    if (GUILayout.Button("Avian_Lab_Embodiment を開く"))
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
                else if (GUILayout.Button("■ Stop (記録を閉じる)"))
                {
                    EditorApplication.isPlaying = false;
                }
            }
        }

        void DrawStatusSection(bool playing, TaskManager taskManager, AvatarEmbodimentController controller)
        {
            EditorGUILayout.LabelField("状態 / Status", EditorStyles.boldLabel);
            if (!playing)
            {
                EditorGUILayout.HelpBox("Play 中にここから場面・試行・アバターを操作できます。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("場面", taskManager != null
                ? (taskManager.CurrentIndex + 1) + " / " + taskManager.TaskCount + "  " + LabelFor(taskManager.CurrentTaskId) + "  (" + taskManager.CurrentTaskId + ")"
                : "TaskManager なし");

            if (controller == null)
            {
                EditorGUILayout.HelpBox("AvatarEmbodimentController がありません。Avian_Lab_Embodiment を Play してください。", MessageType.Warning);
                return;
            }

            var tracking = controller.BodyTrackingValid
                ? (controller.Frame.IsFullBody ? "全身" : "上半身") + "  (信頼度 " + controller.Frame.Confidence.ToString("0.00") + ")"
                : controller.Backend == null ? "なし (バックエンド無し)"
                : !controller.Backend.IsRunning ? "なし (トラッカー未起動)"
                : "なし (データ待ち: ヘッドセット装着?)";
            EditorGUILayout.LabelField("アバター", controller.Current == AvatarKind.Bird ? "鳥 (Bird)" : "人間 (Human)");
            EditorGUILayout.LabelField("体トラッキング", tracking);
            EditorGUILayout.LabelField("被験者コントローラ", ParticipantInputPolicy.ControllersEnabled ? "有効" : "無効 (手・頭・声のみ)");

            var meterRect = EditorGUILayout.GetControlRect(false, 16f);
            var labelRect = new Rect(meterRect.x, meterRect.y, EditorGUIUtility.labelWidth, meterRect.height);
            var barRect = new Rect(meterRect.x + EditorGUIUtility.labelWidth, meterRect.y, meterRect.width - EditorGUIUtility.labelWidth, meterRect.height);
            EditorGUI.LabelField(labelRect, "音声出力 (PC 側)");
            var normalized = Mathf.Clamp01(Mathf.Log10(1f + outputLevel * 30f));
            EditorGUI.ProgressBar(barRect, normalized, outputPeakHold > 0.001f ? "音が出ています" : "無音");

            if (GUILayout.Button("詳細をコンソールに出す (Log Avatar State)"))
                Debug.Log("[AvianVoice] Avatar state: " + controller.DescribeState());
        }

        void DrawTaskSection(bool playing, TaskManager taskManager, SingingTrialTask trials)
        {
            EditorGUILayout.LabelField("場面の遷移 / Scene flow", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing || taskManager == null))
            {
                var ids = taskManager != null ? taskManager.TaskIds : StudyFlowController.FourSceneFlow;
                for (var i = 0; i < ids.Length; i++)
                {
                    var current = taskManager != null && taskManager.CurrentTaskId == ids[i];
                    var label = (i + 1) + " " + LabelFor(ids[i]) + "   (" + ids[i] + ")";
                    if (ids[i] == SingingTrialTask.TaskId && trials != null)
                        label += "  " + trials.TotalTrials + " 回";
                    using (new EditorGUI.DisabledScope(current))
                    {
                        if (GUILayout.Button((current ? "● " : "   ") + label, GUILayout.Height(26f)))
                            taskManager.LoadTaskById(ids[i]);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("◀ 前の場面"))
                        taskManager.StepTask(-1);
                    if (GUILayout.Button("次の場面 ▶"))
                        taskManager.StepTask(1);
                }
            }
        }

        void DrawTrialSection(bool playing, TaskManager taskManager, SingingTrialTask trials)
        {
            EditorGUILayout.LabelField("歌う場面の試行 / Singing trials", EditorStyles.boldLabel);
            if (!playing)
            {
                EditorGUILayout.HelpBox("Play 中、歌う場面で「試行を開始」を押すと教師の鳥が歌い、被験者の発声を待ちます。3 ピッチ × 20 回 = 60 回で休む場面へ移ります。", MessageType.None);
                return;
            }

            if (trials == null)
            {
                EditorGUILayout.HelpBox("SingingTrialTask がありません（Avian Embodiment の StudyFlowController / Use Singing Trials を確認）。", MessageType.Warning);
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
                    EditorGUILayout.PrefixLabel("ピッチ (Hz)");
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

                var perPitch = EditorGUILayout.IntField("各ピッチの回数", trials.TrialsPerPitch);
                if (perPitch != trials.TrialsPerPitch && perPitch > 0)
                    changed = true;

                var order = (TrialOrder)EditorGUILayout.Popup("順序", (int)trials.Order, OrderLabels);
                if (order != trials.Order)
                    changed = true;

                if (changed)
                    trials.Configure(pitches, perPitch, order, 0);
            }

            // Progress.
            var total = trials.TotalTrials;
            var done = trials.CompletedTrials;
            var progressRect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.ProgressBar(progressRect, total > 0 ? done / (float)total : 0f, "試行 " + done + " / " + total + " 完了");

            if (!singingActive)
            {
                EditorGUILayout.HelpBox(trials.IsFinished
                    ? "全 " + total + " 回が完了しています。「最初からやり直す」で再開できます。"
                    : "歌う場面 (3) に切り替えると試行を開始できます。", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!singingActive))
            {
                var c = CultureInfo.InvariantCulture;
                switch (trials.Phase)
                {
                    case TrialPhase.Idle:
                    {
                        var hz = trials.CurrentTargetHz;
                        var label = "▶ 試行 " + trials.NextTrialNumber + " / " + total + " を開始 — 教師の鳥が "
                                    + trials.CurrentNoteName + " (" + hz.ToString("0", c) + " Hz) を歌う   [Space]";
                        if (GUILayout.Button(label, bigButton, GUILayout.Height(44f)))
                            trials.StartNextTrial("experimenter_panel");
                        break;
                    }
                    case TrialPhase.Guide:
                        using (new EditorGUI.DisabledScope(true))
                            GUILayout.Button("♪ 教師の鳥が " + trials.CurrentNoteName + " を歌っています…", bigButton, GUILayout.Height(44f));
                        break;
                    case TrialPhase.Response:
                    {
                        var voice = trials.IsVoiceActive ? "発声中" : trials.Current != null && trials.Current.HadVoice ? "発声終了、待機中" : "被験者の発声待ち";
                        EditorGUILayout.LabelField("試行 " + (trials.Current != null ? trials.Current.trial : 0) + ": " + voice
                                                   + "   残り " + trials.ResponseSecondsLeft.ToString("0.0", c) + " s");
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("この試行を終了して次へ ▶", bigButton, GUILayout.Height(36f)))
                                trials.StartNextTrial("experimenter_panel_next");
                            if (GUILayout.Button("この試行を終了 (待機)", GUILayout.Height(36f), GUILayout.Width(150f)))
                                trials.EndTrialNow("experimenter_panel");
                        }

                        break;
                    }
                    case TrialPhase.Finished:
                        EditorGUILayout.LabelField("全 " + total + " 回完了。" + (singingActive ? "休む場面へ移ります。" : ""), EditorStyles.boldLabel);
                        break;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(trials.Phase != TrialPhase.Idle || done == 0))
                    {
                        if (GUILayout.Button("直前の試行をやり直す"))
                            trials.RepeatLastTrial("experimenter_panel");
                    }

                    using (new EditorGUI.DisabledScope(!trials.HasStarted))
                    {
                        if (GUILayout.Button("最初からやり直す (リセット)")
                            && EditorUtility.DisplayDialog("試行をリセット", "試行を 1 から始め直します。記録済みのデータはファイルに残ります。", "リセット", "キャンセル"))
                            trials.ResetProtocol("experimenter_panel");
                    }
                }
            }

            if (trials.Last != null)
                EditorGUILayout.LabelField("前の試行", trials.Last.Summary(), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("キーボード: Space / R で次の試行 (Game ビューにフォーカスがある時)", EditorStyles.miniLabel);
        }

        void DrawRecordingSection(bool playing, StudyRecorder recorder)
        {
            EditorGUILayout.LabelField("記録 / Recording", EditorStyles.boldLabel);
            var root = Path.Combine(Application.persistentDataPath, StudyRecorder.RootFolderName);
            if (playing && recorder != null && recorder.SessionOpen)
            {
                var c = CultureInfo.InvariantCulture;
                EditorGUILayout.LabelField("セッション", recorder.SessionId);
                EditorGUILayout.LabelField("マイク", recorder.MicrophoneRecording
                    ? recorder.MicSecondsWritten.ToString("0", c) + " s  @" + recorder.MicSampleRate + " Hz  " + recorder.MicDeviceName
                    : "待機中 (マイク未開始)");
                EditorGUILayout.LabelField("出力音声", recorder.OutputRecording
                    ? recorder.OutputSecondsWritten.ToString("0", c) + " s  @" + recorder.OutputSampleRate + " Hz"
                    : "待機中");
                EditorGUILayout.LabelField("試行の行数", recorder.TrialRowsWritten.ToString(c));
                EditorGUILayout.LabelField("保存先", recorder.SessionDirectory, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("このセッションのフォルダを開く"))
                    EditorUtility.RevealInFinder(Path.Combine(recorder.SessionDirectory, "session.json"));
            }
            else
            {
                EditorGUILayout.LabelField("保存先", root, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.HelpBox("Play すると events.jsonl / trials.csv / mic.wav / output.wav / segments/ をセッションごとのフォルダに書きます。", MessageType.None);
                if (GUILayout.Button("記録フォルダを開く"))
                {
                    Directory.CreateDirectory(root);
                    EditorUtility.RevealInFinder(root);
                }
            }
        }

        void DrawAvatarSection(bool playing, AvatarEmbodimentController controller)
        {
            EditorGUILayout.LabelField("アバター / Avatar", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing || controller == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var isBird = controller != null && controller.Current == AvatarKind.Bird;
                    using (new EditorGUI.DisabledScope(isBird))
                    {
                        if (GUILayout.Button((isBird ? "● " : "") + "鳥 (Bird)", GUILayout.Height(30f)))
                            controller.SetAvatar(AvatarKind.Bird);
                    }

                    var options = controller != null ? controller.HumanAvatars : null;
                    if (options != null)
                    {
                        foreach (var option in options)
                        {
                            if (option == null)
                                continue;

                            var available = controller.HasHumanAvatar(option.id);
                            var active = controller.Current == AvatarKind.Human && controller.HumanAvatarId == option.id;
                            var label = (active ? "● " : "") + "人間 (" + option.labelJa + ")" + (available ? "" : " 未配置");
                            using (new EditorGUI.DisabledScope(active || !available))
                            {
                                if (GUILayout.Button(label, GUILayout.Height(30f)))
                                {
                                    controller.SetHumanAvatar(option.id);
                                    controller.SetAvatar(AvatarKind.Human);
                                }
                            }
                        }
                    }
                }
            }

            EditorGUILayout.LabelField("キーボード: V で鳥 ⇄ 人間 (選択中の人間モデル)。「未配置」はモデルが Resources に無いもの。", EditorStyles.miniLabel);
        }

        void DrawParticipantSection(bool playing, AvatarEmbodimentController controller)
        {
            EditorGUILayout.LabelField("被験者側の入力 / Participant input", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing))
            {
                var controllers = EditorGUILayout.ToggleLeft(
                    "XR コントローラを有効にする (スティック旋回・メニュー・X/B ボタン)",
                    ParticipantInputPolicy.ControllersEnabled);
                if (controllers != ParticipantInputPolicy.ControllersEnabled)
                    ParticipantInputPolicy.ControllersEnabled = controllers;

                var hands = EditorGUILayout.ToggleLeft(
                    "手のメッシュ・レイを表示する (トラッキングは常に有効)",
                    ParticipantInputPolicy.HandsVisible);
                if (hands != ParticipantInputPolicy.HandsVisible)
                    ParticipantInputPolicy.HandsVisible = hands;

                if (controller != null)
                {
                    var hint = EditorGUILayout.ToggleLeft("視界下の状態カードを表示する", controller.HintVisible);
                    if (hint != controller.HintVisible)
                        controller.HintVisible = hint;
                }

                if (GUILayout.Button("音声の状態をコンソールに出す"))
                    AvianEmbodimentSceneSetup.LogAudioState();
            }

            EditorGUILayout.HelpBox(
                "既定ではコントローラ入力は無視されます（Avian Embodiment の Participant Controllers Enabled）。" +
                "被験者は頭・手・体・声だけで参加し、場面・試行・アバターはこのパネルか PC のキーボード (1-4, N, Space, V) で操作します。",
                MessageType.None);
        }

        static string LabelFor(string taskId)
        {
            return taskId != null && TaskLabelsJa.TryGetValue(taskId, out var label) ? label : taskId ?? "";
        }
    }
}
#endif
