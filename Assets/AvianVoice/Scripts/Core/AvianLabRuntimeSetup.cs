using AvianVoice.Audio;
using AvianVoice.Data;
using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using AvianVoice.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AvianVoice.Core
{
    /// <summary>
    /// Runtime scene setup when AvianAppBootstrap is not fully wired in the Editor.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class AvianLabRuntimeSetup : MonoBehaviour
    {
        [SerializeField]
        bool autoSetup = true;

        [SerializeField]
        AvianVoiceSettings settings;

        [SerializeField]
        PlatformProfile platformProfile;

        void Awake()
        {
            var xrOrigin = FindPreferredXROrigin();
            if (xrOrigin == null)
            {
                if (autoSetup)
                    Debug.LogWarning("[AvianVoice] XR Origin not found. Add XR Origin Hands (XR Rig) prefab to the scene.");
                return;
            }

            var head = ResolveHeadAnchor(xrOrigin.transform);
            EnsureRuntimeUiIfMissing(xrOrigin, head);
            EnsureRuntimeAudioIfMissing(xrOrigin);
            DesktopFlyCamera.Ensure(xrOrigin, head, platformProfile);

            if (!autoSetup)
                return;

            var hasMic = xrOrigin.GetComponent<MicrophoneCapture>() != null;
            var hasHud = head.GetComponentInChildren<TrainingHUD>(true) != null;
            if (hasMic && hasHud)
            {
                Debug.Log("[AvianVoice] Already initialized on " + xrOrigin.name);
                return;
            }

            if (hasMic && !hasHud)
            {
                EnsureRuntimeUi(xrOrigin, head);
                return;
            }

            if (settings == null)
                settings = ScriptableObject.CreateInstance<AvianVoiceSettings>();

            var body = AvianBodyPlaceholder.Create(head);
            body.transform.localPosition = new Vector3(0f, -0.2f, 0.25f);

            var particles = CreateVocalParticles(body.Beak);
            var transformSource = AvianAudioSourceUtility.FindOrCreate(xrOrigin.transform, AvianAudioSourceUtility.TransformSourceName);
            var assistiveSource = AvianAudioSourceUtility.FindOrCreate(xrOrigin.transform, AvianAudioSourceUtility.AssistiveSourceName);
            var callLibrary = xrOrigin.GetComponent<BirdCallLibrary>() ?? xrOrigin.AddComponent<BirdCallLibrary>();
            var mirror = xrOrigin.GetComponent<MirrorBodySetup>() ?? xrOrigin.AddComponent<MirrorBodySetup>();
            var hud = CreateHud(head);
            var toggle = CreateModeToggle(head);

            var bootstrap = xrOrigin.GetComponent<AvianAppBootstrap>() ?? xrOrigin.AddComponent<AvianAppBootstrap>();
            bootstrap.Configure(new AvianBootstrapConfig
            {
                settings = settings,
                platformProfile = platformProfile,
                headAnchor = head,
                leftWingBone = body.LeftWing,
                rightWingBone = body.RightWing,
                birdAnimator = body.GetComponent<Animator>(),
                beakMesh = body.BeakRenderer,
                beakBlendShapeIndex = -1,
                beakTransform = body.Beak,
                chestTransform = body.Chest,
                vocalParticles = particles,
                transformBirdSource = transformSource,
                assistiveMonitorSource = assistiveSource,
                callLibrary = callLibrary,
                mirrorSetup = mirror,
                trainingHud = hud,
                voiceModeToggle = toggle
            });

            if (platformProfile != null)
            {
                var perm = xrOrigin.GetComponent<PlatformRuntimePermissions>() ??
                           xrOrigin.AddComponent<PlatformRuntimePermissions>();
            }

            Debug.Log("[AvianVoice] Lab ready on '" + xrOrigin.name + "'. Speak into mic, hold Space=breath, T=voice mode. Look for HUD near hands.");
        }

        /// <summary>
        /// Always runs (even when autoSetup is off) so custom bird rigs still get HUD / mode UI.
        /// </summary>
        static void EnsureRuntimeUiIfMissing(GameObject xrOrigin, Transform head)
        {
            var bootstrap = xrOrigin.GetComponent<AvianAppBootstrap>();
            var hud = head.GetComponentInChildren<TrainingHUD>(true);
            if (hud == null)
            {
                EnsureRuntimeUi(xrOrigin, head);
                return;
            }

            var toggle = head.GetComponentInChildren<VoiceModeToggle>(true);
            CreateTaskModePanel(head);
            EnsureTaskKeyboardSwitcher(xrOrigin);
            if (bootstrap != null)
                bootstrap.AttachUi(hud, toggle);
        }

        static void EnsureRuntimeUi(GameObject xrOrigin, Transform head)
        {
            var hud = CreateHud(head);
            var toggle = CreateModeToggle(head);
            CreateTaskModePanel(head);
            EnsureTaskKeyboardSwitcher(xrOrigin);
            var bootstrap = xrOrigin.GetComponent<AvianAppBootstrap>();
            if (bootstrap != null)
                bootstrap.AttachUi(hud, toggle);
            Debug.Log("[AvianVoice] Created missing HUD/Mode UI on '" + head.name + "'.");
        }

        static void EnsureTaskKeyboardSwitcher(GameObject xrOrigin)
        {
            if (xrOrigin.GetComponent<TaskModeKeyboardSwitcher>() != null)
                return;
            xrOrigin.AddComponent<TaskModeKeyboardSwitcher>();
        }

        static void EnsureRuntimeAudioIfMissing(GameObject xrOrigin)
        {
            var bootstrap = xrOrigin.GetComponent<AvianAppBootstrap>();
            if (bootstrap == null)
                return;

            var transformSource = AvianAudioSourceUtility.FindOrCreate(
                xrOrigin.transform, AvianAudioSourceUtility.TransformSourceName);
            var assistiveSource = AvianAudioSourceUtility.FindOrCreate(
                xrOrigin.transform, AvianAudioSourceUtility.AssistiveSourceName);
            var library = xrOrigin.GetComponent<BirdCallLibrary>();
            if (library == null)
                library = xrOrigin.AddComponent<BirdCallLibrary>();

            bootstrap.AttachAudio(transformSource, assistiveSource, library);
        }

        static Transform ResolveHeadAnchor(Transform xrOrigin)
        {
            var cameraOffset = xrOrigin.Find("Camera Offset");
            if (cameraOffset == null)
            {
                foreach (var t in xrOrigin.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Camera Offset")
                    {
                        cameraOffset = t;
                        break;
                    }
                }
            }
            return cameraOffset != null ? cameraOffset : xrOrigin;
        }

        static GameObject FindPreferredXROrigin()
        {
            GameObject hands = null;
            GameObject fallback = null;
            var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (!t.name.Contains("XR Origin"))
                    continue;
                if (t.name.Contains("Hands"))
                    hands = t.gameObject;
                else if (fallback == null)
                    fallback = t.gameObject;
            }
            return hands != null ? hands : fallback;
        }

        static ParticleSystem CreateVocalParticles(Transform beak)
        {
            var go = new GameObject("VocalParticles");
            go.transform.SetParent(beak, false);
            go.transform.localPosition = Vector3.forward * 0.05f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.3f;
            main.startSpeed = 0.5f;
            main.startSize = 0.03f;
            main.maxParticles = 32;
            main.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static TrainingHUD CreateHud(Transform parent)
        {
            var canvasGo = CreateWorldCanvas(
                parent,
                "AvianHUD",
                AvianHudLayout.MetricsPanelLocalPosition,
                AvianHudLayout.MetricsPanelCanvasSize,
                AvianHudLayout.MetricsPanelWorldScale);
            var hud = canvasGo.AddComponent<TrainingHUD>();
            // HUD uses optional SerializeField; values update when wired in Editor.
            // Runtime sliders are created for visual feedback via a small helper component.
            canvasGo.AddComponent<TrainingHUDRuntimeBars>();
            return hud;
        }

        static void CreateTaskModePanel(Transform parent)
        {
            if (parent.GetComponentInChildren<TaskFlowUI>(true) != null)
                return;

            var canvasGo = CreateWorldCanvas(
                parent,
                "TaskModeHUD",
                AvianHudLayout.TaskPanelLocalPosition,
                AvianHudLayout.TaskPanelCanvasSize,
                AvianHudLayout.TaskPanelWorldScale);

            var title = CreateText(
                canvasGo.transform,
                "TaskTitle",
                new Vector2(0f, 28f),
                "Avian Lab",
                AvianHudLayout.TaskTitleSize,
                AvianHudLayout.TaskTitleFontSize);
            var instruction = CreateText(
                canvasGo.transform,
                "TaskInstruction",
                new Vector2(0f, -8f),
                "1-4 switch task | N next",
                AvianHudLayout.TaskInstructionSize,
                AvianHudLayout.TaskBodyFontSize);
            var score = CreateText(
                canvasGo.transform,
                "TaskScore",
                new Vector2(0f, -38f),
                "Score: 0",
                AvianHudLayout.TaskScoreSize,
                AvianHudLayout.TaskBodyFontSize);

            var flowUi = canvasGo.AddComponent<TaskFlowUI>();
            var binder = canvasGo.AddComponent<TaskFlowUIRuntimeBinder>();
            binder.Bind(flowUi, title, instruction, score);
        }

        static VoiceModeToggle CreateModeToggle(Transform parent)
        {
            var canvasGo = CreateWorldCanvas(
                parent,
                "ModeToggle",
                AvianHudLayout.ModePanelLocalPosition,
                AvianHudLayout.ModePanelCanvasSize,
                AvianHudLayout.ModePanelWorldScale);
            var label = CreateText(
                canvasGo.transform,
                "Label",
                Vector2.zero,
                "Transform (T)",
                AvianHudLayout.ModeLabelSize,
                AvianHudLayout.ModeLabelFontSize);
            var toggleGo = new GameObject("AssistiveToggle");
            toggleGo.transform.SetParent(canvasGo.transform, false);
            var toggle = toggleGo.AddComponent<Toggle>();
            var voiceToggle = canvasGo.AddComponent<VoiceModeToggle>();
            canvasGo.AddComponent<KeyboardModeSwitcher>();
            // VoiceModeToggle fields assigned via helper
            var helper = canvasGo.AddComponent<VoiceModeToggleRuntimeBinder>();
            helper.Bind(voiceToggle, toggle, label);
            return voiceToggle;
        }

        static GameObject CreateWorldCanvas(
            Transform parent,
            string name,
            Vector3 localPos,
            Vector2 canvasSize,
            float worldScale)
        {
            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = localPos;
            canvasGo.transform.localScale = Vector3.one * worldScale;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = canvasSize;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            var bg = new GameObject("Background");
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.08f, 0.12f, 0.85f);
            bg.transform.SetAsFirstSibling();
            canvasGo.AddComponent<AvianWorldCanvasCameraBinder>();
            return canvasGo;
        }

        static Text CreateText(
            Transform parent,
            string name,
            Vector2 pos,
            string content,
            Vector2 size,
            int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            return text;
        }
    }
}
