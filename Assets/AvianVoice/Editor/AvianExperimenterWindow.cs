#if UNITY_EDITOR
using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.Embodiment.Avatars;
using AvianVoice.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvianVoice.Editor
{
    /// <summary>
    /// Experimenter panel (PC side). While the participant wears the headset, the experimenter drives the
    /// study from here: scene (task) transitions, bird / human avatar, participant controller policy,
    /// and a live status line. Menu: Avian Voice / Experimenter Panel.
    /// </summary>
    public class AvianExperimenterWindow : EditorWindow
    {
        const string EmbodimentScenePath = AvianEmbodimentSceneSetup.ScenePath;

        static readonly string[] TaskLabelsJa =
        {
            "1 鳥に会う",
            "2 呼吸",
            "3 歌う",
            "4 飛ぶ",
            "5 仲間を呼ぶ",
            "6 休む"
        };

        double nextRepaint;
        Vector2 scroll;

        [MenuItem("Avian Voice/Experimenter Panel (実験者パネル)")]
        public static void Open()
        {
            var window = GetWindow<AvianExperimenterWindow>();
            window.titleContent = new GUIContent("Avian Experimenter");
            window.minSize = new Vector2(360f, 420f);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.update += Tick;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextRepaint)
                return;

            nextRepaint = EditorApplication.timeSinceStartup + 0.25;
            Repaint();
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSceneSection();
            EditorGUILayout.Space(8f);

            var playing = EditorApplication.isPlaying && !EditorApplication.isCompiling;
            var taskManager = playing ? FindAnyObjectByType<TaskManager>() : null;
            var controller = playing ? FindAnyObjectByType<AvatarEmbodimentController>() : null;

            DrawStatusSection(playing, taskManager, controller);
            EditorGUILayout.Space(8f);
            DrawTaskSection(playing, taskManager);
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
                        EditorApplication.isPlaying = true;
                }
                else if (GUILayout.Button("■ Stop"))
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
                EditorGUILayout.HelpBox("Play 中にここから場面とアバターを操作できます。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("場面", taskManager != null
                ? (taskManager.CurrentIndex + 1) + " / " + taskManager.TaskCount + "  " + taskManager.CurrentTaskName + "  (" + taskManager.CurrentTaskId + ")"
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

            if (GUILayout.Button("詳細をコンソールに出す (Log Avatar State)"))
                Debug.Log("[AvianVoice] Avatar state: " + controller.DescribeState());
        }

        void DrawTaskSection(bool playing, TaskManager taskManager)
        {
            EditorGUILayout.LabelField("場面の遷移 / Scene flow", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing || taskManager == null))
            {
                var ids = XrRigTaskStartPoses.TaskIds;
                for (var i = 0; i < ids.Length; i++)
                {
                    var current = taskManager != null && taskManager.CurrentTaskId == ids[i];
                    var label = (i < TaskLabelsJa.Length ? TaskLabelsJa[i] : ids[i]) + "   " + XrRigTaskStartPoses.TaskLabels[i].Substring(2);
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

        void DrawAvatarSection(bool playing, AvatarEmbodimentController controller)
        {
            EditorGUILayout.LabelField("アバター / Avatar", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!playing || controller == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(controller != null && controller.Current == AvatarKind.Bird))
                {
                    if (GUILayout.Button("鳥 (Bird)", GUILayout.Height(30f)))
                        controller.SetAvatar(AvatarKind.Bird);
                }

                using (new EditorGUI.DisabledScope(controller != null && controller.Current == AvatarKind.Human))
                {
                    if (GUILayout.Button("人間 (Human)", GUILayout.Height(30f)))
                        controller.SetAvatar(AvatarKind.Human);
                }
            }

            EditorGUILayout.LabelField("キーボード: V で切替 (Game ビューにフォーカスがある時)", EditorStyles.miniLabel);
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

                if (controller != null)
                {
                    var hint = EditorGUILayout.ToggleLeft("視界下の状態カードを表示する", controller.HintVisible);
                    if (hint != controller.HintVisible)
                        controller.HintVisible = hint;
                }
            }

            EditorGUILayout.HelpBox(
                "既定ではコントローラ入力は無視されます（Avian Embodiment の Participant Controllers Enabled）。" +
                "被験者は頭・手・体・声だけで参加し、場面とアバターはこのパネルか PC のキーボード (1-6, N, V) で切り替えます。",
                MessageType.None);
        }
    }
}
#endif
