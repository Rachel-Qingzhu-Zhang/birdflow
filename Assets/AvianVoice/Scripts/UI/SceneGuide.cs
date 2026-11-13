using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    /// <summary>
    /// Scene prompt: for the study scenes the scene title, then the instruction in Japanese and in English (both
    /// always shown, the same text in every condition; see AvianUiCopy.GuideText). Same card style as the wish menu,
    /// and not tied to the debug HUD toggle.
    ///
    /// Since 2026-11-12 the card is fixed in the scene at the top of the mirror whenever the mirror is shown (practice,
    /// meet, breathing, singing, rest): a card that moved with the head was uncomfortable. It sits above the mirror
    /// frame, or, when that is too high to read (the 4 m mirror of Avian_Lab_Embodiment), on the upper part of the
    /// mirror just in front of the glass (AvianHudLayout.GuideMirror*). It faces the side the participant stands on
    /// and is placed once per scene from the rig's start pose, so it does not follow the head. It is on the UI layer,
    /// which the mirror's reflection camera does not draw. Only the scenes without the mirror (flying, calling
    /// friends: not in the study flow) still carry it on the camera.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public class SceneGuide : MonoBehaviour
    {
        public const string CanvasName = "SceneGuide";

        /// <summary>A study scene waits this long (card hidden) for the mirror before falling back to the camera.</summary>
        const float MirrorWaitSeconds = 1f;

        static readonly Color Ivory = new(0.96f, 0.93f, 0.88f, 0.96f);
        static readonly Color Card = new(0.07f, 0.08f, 0.07f, 0.82f);

        static SceneGuide instance;

        TaskManager taskManager;
        TextMeshProUGUI label;
        Canvas canvas;
        RectTransform canvasRect;
        string phaseEnglish = "";
        string phaseJapanese = "";

        MirrorBodySetup mirror;
        Transform headTarget;
        bool aboveMirror;
        bool overGlass;
        Bounds anchoredFrame;
        Bounds anchoredGlass;
        float anchoredDistance;
        float anchoredElevation;
        float nextFrameCheck;
        float nextMirrorSearch;
        float mirrorWaitStarted = -1f;
        bool fallbackWarned;

        /// <summary>True while the card is fixed above the mirror (false: carried on the camera, or hidden).</summary>
        public static bool IsAboveMirror => instance != null && instance.aboveMirror;

        public static void Ensure(Transform parent)
        {
            var target = parent;
            if (Camera.main != null)
                target = Camera.main.transform;
            if (target == null)
                return;

            SceneGuide keep = null;
            var found = Object.FindObjectsByType<SceneGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                var guide = found[i];
                if (guide == null)
                    continue;
                if (keep == null)
                {
                    keep = guide;
                    continue;
                }

                Object.Destroy(guide.gameObject);
            }

            // The host is not parented here: Place() fixes it above the mirror, or puts it on the camera (target)
            // in the scenes that have no mirror.
            if (keep == null)
                keep = new GameObject("SceneGuideHost").AddComponent<SceneGuide>();

            keep.headTarget = target;
            keep.gameObject.SetActive(true);
            instance = keep;
            keep.EnsureBuilt();
            keep.Place(false);
        }

        /// <summary>Where the card is and what it shows, for the logs (Debug/Log Scene Guide).</summary>
        public static string DescribeState()
        {
            Resolve();
            if (instance == null)
                return "no SceneGuide";

            var guide = instance;
            var t = guide.transform;
            var scale = guide.canvasRect != null ? guide.canvasRect.lossyScale.x : 0f;
            var size = AvianHudLayout.GuideCanvasSize * scale;
            var text = guide.label != null ? guide.label.text.Replace("\n", " | ") : "";
            return "placement=" + (guide.aboveMirror
                       ? guide.overGlass ? "mirror_top_over_glass" : "mirror_above_frame"
                       : t.parent != null ? "on_camera(" + t.parent.name + ")" : "unplaced")
                   + " visible=" + (guide.canvas != null && guide.canvas.enabled && guide.gameObject.activeInHierarchy)
                   + " pos=" + t.position.ToString("F3") + " euler=" + t.eulerAngles.ToString("F1")
                   + " card=" + size.x.ToString("F2") + "x" + size.y.ToString("F2") + " m"
                   + (guide.aboveMirror
                       ? " distance=" + guide.anchoredDistance.ToString("F2") + " m frameTop=" + guide.anchoredFrame.max.y.ToString("F3")
                         + " frameSize=" + guide.anchoredFrame.size.ToString("F2")
                         + " glassTop=" + guide.anchoredGlass.max.y.ToString("F3") + " glassSize=" + guide.anchoredGlass.size.ToString("F2")
                         + " elevationFromNominalEye=" + guide.anchoredElevation.ToString("F1") + " deg"
                       : "")
                   + " layer=" + guide.gameObject.layer
                   + " task=" + (guide.taskManager != null ? guide.taskManager.CurrentTaskId : "-")
                   + " text=\"" + text + "\"";
        }

        public static void SetPhase(string line)
        {
            SetPhase(line, line);
        }

        public static void SetPhase(string english, string japanese)
        {
            Resolve();
            if (instance == null)
                return;
            instance.phaseEnglish = english ?? "";
            instance.phaseJapanese = japanese ?? english ?? "";
            instance.RefreshLabel();
        }

        public static void ClearPhase()
        {
            SetPhase("");
        }

        static void Resolve()
        {
            if (instance != null)
                return;
            instance = FindAnyObjectByType<SceneGuide>();
        }

        void Awake()
        {
            instance = this;
            EnsureBuilt();
        }

        void OnEnable()
        {
            instance = this;
            AvianUiLanguage.Changed -= OnLanguageChanged;
            AvianUiLanguage.Changed += OnLanguageChanged;
            Bind();
            RefreshLabel();
        }

        void OnDisable()
        {
            AvianUiLanguage.Changed -= OnLanguageChanged;
            if (taskManager != null)
                taskManager.OnTaskChanged -= OnTaskChanged;
        }

        void Update()
        {
            AvianUiLanguage.PollToggle();
            if (taskManager == null)
            {
                Bind();
                RefreshLabel();
            }
        }

        void LateUpdate()
        {
            Place(false);
        }

        // ---- placement ------------------------------------------------------------------------------

        /// <summary>
        /// Fix the card above the mirror when the mirror is shown, otherwise carry it on the camera. Above the mirror
        /// it is placed once and then left alone: it is placed again only when the scene changes (<paramref name="force"/>)
        /// or the mirror itself has moved.
        /// </summary>
        void Place(bool force)
        {
            if (canvas == null || canvasRect == null)
                return;

            var now = Time.unscaledTime;
            if (!force && aboveMirror && now < nextFrameCheck)
                return;
            nextFrameCheck = now + 0.5f;

            if (mirror == null && now >= nextMirrorSearch)
            {
                nextMirrorSearch = now + 1f;
                mirror = FindAnyObjectByType<MirrorBodySetup>();
            }

            if (mirror != null && mirror.TryGetVisibleMirrorFrame(out var frame, out var glass, out var facing))
            {
                if (force || !aboveMirror || !SameFrame(frame, anchoredFrame))
                    PlaceAboveMirror(frame, glass, facing);
                mirrorWaitStarted = -1f;
                canvas.enabled = true;
                return;
            }

            // No mirror in view. In a study scene (and before the first scene is known) the mirror is about to be
            // shown: keep the card hidden for a moment instead of flashing it on the head.
            var taskId = taskManager != null ? taskManager.CurrentTaskId : "";
            var studyScene = !string.IsNullOrEmpty(AvianUiCopy.Title(taskId));
            if (studyScene || taskManager == null)
            {
                if (mirrorWaitStarted < 0f)
                    mirrorWaitStarted = now;
                if (now - mirrorWaitStarted < MirrorWaitSeconds)
                {
                    canvas.enabled = false;
                    return;
                }

                if (studyScene && !fallbackWarned)
                {
                    fallbackWarned = true;
                    Debug.LogWarning("[AvianVoice] SceneGuide: no visible mirror in the study scene '" + taskId
                                     + "'; the guide card is carried on the camera (head-locked) instead of above the mirror.");
                }
            }

            PlaceOnHead();
        }

        void PlaceAboveMirror(Bounds frame, Bounds glass, Vector3 facing)
        {
            // Where the participant stands in this scene: the rig origin (the floor under the start pose) at a nominal
            // eye height, not the head, so the place of the card does not depend on where the headset is right now.
            // Only its distance to the mirror and its height are used, to size, raise and tilt the card.
            var eye = StandingEyePoint(glass.center + facing * 2f);
            var toViewer = eye - glass.center;
            toViewer.y = 0f;
            if (Vector3.Dot(facing, toViewer) < 0f)
                facing = -facing;

            var distance = Mathf.Clamp(Vector3.Dot(toViewer, facing), AvianHudLayout.GuideMirrorMinDistance, AvianHudLayout.GuideMirrorMaxDistance);
            var scale = AvianHudLayout.GuideMirrorScalePerMeter * distance;
            var height = AvianHudLayout.GuideCanvasSize.y * scale;

            // Above the frame: centred on the glass, just in front of the frame, its bottom edge a gap above the top.
            var frameHalfDepth = 0.5f * (Mathf.Abs(facing.x) * frame.size.x + Mathf.Abs(facing.z) * frame.size.z);
            var frameDepth = Vector3.Dot(frame.center - glass.center, facing) + frameHalfDepth + 0.03f;
            var aboveFrameY = frame.max.y + AvianHudLayout.GuideMirrorGap + 0.5f * height;

            // A tall mirror: that would be too high to read (GuideMirrorMaxElevationDegrees). Then the card hangs on
            // the upper part of the mirror, 5 cm in front of the glass, as high as the limit allows.
            var glassDepth = 0.5f * (Mathf.Abs(facing.x) * glass.size.x + Mathf.Abs(facing.z) * glass.size.z) + 0.05f;
            var highestY = eye.y + Mathf.Tan(AvianHudLayout.GuideMirrorMaxElevationDegrees * Mathf.Deg2Rad) * Mathf.Max(0.5f, distance - glassDepth);
            overGlass = aboveFrameY > highestY;
            var depth = overGlass ? glassDepth : frameDepth;
            var position = glass.center + facing * depth;
            position.y = overGlass ? highestY : aboveFrameY;

            // Upright and parallel to the mirror, leaning toward the eyes so the text is seen square-on from below.
            var tilt = Mathf.Atan2(position.y - eye.y, Mathf.Max(0.5f, distance - depth)) * Mathf.Rad2Deg;
            anchoredElevation = tilt;
            tilt = Mathf.Clamp(tilt, 0f, AvianHudLayout.GuideMirrorMaxTiltDegrees) * Mathf.Deg2Rad;
            var forward = -facing * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt);

            if (transform.parent != null)
                transform.SetParent(null, false);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            transform.localScale = Vector3.one;
            canvasRect.localScale = Vector3.one * scale;

            aboveMirror = true;
            anchoredFrame = frame;
            anchoredGlass = glass;
            anchoredDistance = distance;
            fallbackWarned = false;
            Debug.Log("[AvianVoice] SceneGuide fixed above the mirror: " + DescribeState());
        }

        static Vector3 StandingEyePoint(Vector3 fallback)
        {
            if (AvianLabSceneLayout.TryResolveXrOrigin(out var origin, out _))
                return origin.position + Vector3.up * AvianHudLayout.GuideNominalEyeHeight;
            var cam = Camera.main;
            return cam != null ? cam.transform.position : fallback;
        }

        void PlaceOnHead()
        {
            if (headTarget == null && Camera.main != null)
                headTarget = Camera.main.transform;
            if (headTarget == null)
            {
                canvas.enabled = false;
                return;
            }

            if (transform.parent != headTarget)
                transform.SetParent(headTarget, false);
            transform.localPosition = AvianHudLayout.GuideLocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            canvasRect.localScale = Vector3.one * AvianHudLayout.GuideWorldScale;
            aboveMirror = false;
            canvas.enabled = true;
        }

        static bool SameFrame(Bounds a, Bounds b)
        {
            return (a.center - b.center).sqrMagnitude < 0.0001f && (a.size - b.size).sqrMagnitude < 0.0001f;
        }

        void EnsureBuilt()
        {
            if (label != null)
                return;

            // The host carries the pose (above the mirror, or on the camera); the canvas only carries the scale.
            var canvasGo = new GameObject(CanvasName);
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = Vector3.zero;
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * AvianHudLayout.GuideWorldScale;

            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 45;
            var rect = canvasGo.GetComponent<RectTransform>();
            canvasRect = rect;
            rect.sizeDelta = AvianHudLayout.GuideCanvasSize;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 12f;
            canvasGo.AddComponent<AvianWorldCanvasCameraBinder>();

            var cardGo = new GameObject("Card");
            cardGo.transform.SetParent(rect, false);
            var cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.anchorMin = Vector2.zero;
            cardRect.anchorMax = Vector2.one;
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;
            var card = cardGo.AddComponent<Image>();
            card.color = Card;
            card.raycastTarget = false;

            var textGo = new GameObject("Line");
            textGo.transform.SetParent(rect, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(AvianHudLayout.GuideCanvasSize.x - 30f, AvianHudLayout.GuideCanvasSize.y - 14f);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 17;
            label.richText = true;
            label.color = Ivory;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            AvianUiFont.ApplyBilingual(label);

            SetLayerRecursive(gameObject, BirdSceneRoles.UiLayer);
            Bind();
            RefreshLabel();
        }

        void Bind()
        {
            if (taskManager != null)
                return;
            taskManager = FindAnyObjectByType<TaskManager>();
            if (taskManager == null)
                return;
            taskManager.OnTaskChanged += OnTaskChanged;
        }

        void OnTaskChanged(string taskName, string instruction)
        {
            phaseEnglish = "";
            phaseJapanese = "";
            RefreshLabel();
            // The mirror and the rig's start pose of the new scene are set by now: place the card for this scene.
            mirrorWaitStarted = -1f;
            Place(true);
        }

        void OnLanguageChanged()
        {
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (label == null)
                return;
            var taskId = taskManager != null ? taskManager.CurrentTaskId : "";
            label.text = AvianUiCopy.GuideText(taskId, phaseEnglish, phaseJapanese);
            AvianUiFont.ApplyBilingual(label);
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
