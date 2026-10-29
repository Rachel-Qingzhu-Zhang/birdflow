using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AvianVoice.UI
{
    /// <summary>
    /// Short scene prompt above the view. Same card style as the wish menu,
    /// and not tied to the debug HUD toggle.
    /// </summary>
    [DefaultExecutionOrder(210)]
    public class SceneGuide : MonoBehaviour
    {
        public const string CanvasName = "SceneGuide";

        static readonly Color Ivory = new(0.96f, 0.93f, 0.88f, 0.96f);
        static readonly Color Card = new(0.07f, 0.08f, 0.07f, 0.82f);

        static SceneGuide instance;

        TaskManager taskManager;
        TextMeshProUGUI label;
        string taskLine = "";
        string phaseLine = "";

        public static void Ensure(Transform parent)
        {
            if (parent == null)
                return;

            var existing = parent.GetComponentInChildren<SceneGuide>(true);
            if (existing == null)
            {
                var go = new GameObject("SceneGuideHost");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                existing = go.AddComponent<SceneGuide>();
            }

            existing.EnsureBuilt();
        }

        public static void SetPhase(string line)
        {
            Resolve();
            if (instance == null)
                return;
            instance.phaseLine = line ?? "";
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
            Bind();
            RefreshLabel();
        }

        void OnDisable()
        {
            if (taskManager != null)
                taskManager.OnTaskChanged -= OnTaskChanged;
        }

        void Update()
        {
            if (taskManager == null)
            {
                Bind();
                RefreshLabel();
            }
        }

        void EnsureBuilt()
        {
            if (label != null)
                return;

            var canvasGo = new GameObject(CanvasName);
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = AvianHudLayout.GuideLocalPosition;
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * AvianHudLayout.GuideWorldScale;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 45;
            var rect = canvasGo.GetComponent<RectTransform>();
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
            textRect.sizeDelta = new Vector2(430f, 78f);
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18;
            label.color = Ivory;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                label.font = font;

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
            taskLine = taskManager.CurrentInstruction;
        }

        void OnTaskChanged(string taskName, string instruction)
        {
            taskLine = instruction ?? "";
            phaseLine = "";
            RefreshLabel();
        }

        void RefreshLabel()
        {
            if (label == null)
                return;
            var line = string.IsNullOrEmpty(phaseLine) ? taskLine : phaseLine;
            label.text = line ?? "";
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
