using AvianVoice.Core;
using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AvianVoice.UI
{
    /// <summary>
    /// Head-locked wish card for choosing a scene. Copy is invitational, not numbered.
    /// Independent of the debug HUD (showHudPanels). Toggle with M, XR menu, or the chip.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class SceneWishMenu : MonoBehaviour
    {
        public const string PanelName = "WishPanel";
        public const string ChipName = "WishChip";

        static readonly Color Ivory = new(0.96f, 0.93f, 0.88f, 0.96f);
        static readonly Color IvoryMuted = new(0.84f, 0.79f, 0.72f, 0.72f);
        static readonly Color Gold = new(0.80f, 0.66f, 0.42f, 1f);
        static readonly Color GoldSoft = new(0.80f, 0.66f, 0.42f, 0.55f);
        static readonly Color Card = new(0.07f, 0.08f, 0.07f, 0.78f);
        static readonly Color RowIdle = new(1f, 1f, 1f, 0.03f);
        static readonly Color RowHover = new(0.96f, 0.90f, 0.78f, 0.10f);
        static readonly Color RowActive = new(0.80f, 0.66f, 0.42f, 0.14f);

        [SerializeField]
        bool startOpen = true;

        TaskManager taskManager;
        GameObject panelRoot;
        GameObject chipRoot;
        WishRow[] rows;
        bool open;
        bool leftMenuHeld;
        bool rightMenuHeld;
        bool layoutReady;
        Sprite roundedSprite;

        struct Wish
        {
            public string taskId;
            public string line;
        }

        struct WishRow
        {
            public string taskId;
            public Image background;
            public TextMeshProUGUI wishText;
            public TextMeshProUGUI mark;
        }

        static readonly Wish[] Wishes =
        {
            new() { taskId = "meet", line = "I want to meet my bird self." },
            new() { taskId = "breath_sync", line = "I want to learn its breath." },
            new() { taskId = "flying", line = "I want to fly above the water." },
            new() { taskId = "singing", line = "I want to sing as a bird." },
            new() { taskId = "call_friends", line = "I want to call my bird friends." },
            new() { taskId = "reflection", line = "I want to rest by the water." }
        };

        public static SceneWishMenu Ensure(Transform head)
        {
            if (head == null)
                return null;

            var parent = ResolveFollowTarget(head);
            var existing = Object.FindAnyObjectByType<SceneWishMenu>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.transform.parent != parent)
                {
                    existing.transform.SetParent(parent, false);
                    existing.transform.localPosition = Vector3.zero;
                    existing.transform.localRotation = Quaternion.identity;
                }

                existing.gameObject.SetActive(true);
                existing.EnsureBuilt();
                existing.KeepReachable();
                SceneGuide.Ensure(parent);
                return existing;
            }

            var go = new GameObject("SceneWishMenu");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var menu = go.AddComponent<SceneWishMenu>();
            menu.EnsureBuilt();
            SceneGuide.Ensure(parent);
            return menu;
        }

        void KeepReachable()
        {
            if (chipRoot != null)
                chipRoot.SetActive(!open);
            if (panelRoot != null)
                panelRoot.SetActive(open);
        }

        static Transform ResolveFollowTarget(Transform head)
        {
            var cam = head.GetComponent<Camera>() ?? head.GetComponentInChildren<Camera>(true);
            return cam != null ? cam.transform : head;
        }

        void Awake() => EnsureBuilt();

        void OnEnable()
        {
            BindTaskManager();
            RefreshActive();
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
                BindTaskManager();
                if (taskManager != null)
                    RefreshActive();
            }

            FollowViewerCamera();
            if (AvianInput.WasWishMenuPressedThisFrame() || WasXrMenuPressed())
                SetOpen(!open);
        }

        void FollowViewerCamera()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            if (transform.parent != cam.transform)
            {
                transform.SetParent(cam.transform, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                SceneGuide.Ensure(cam.transform);
            }

            cam.cullingMask |= 1 << BirdSceneRoles.UiLayer;
            KeepReachable();
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (panelRoot != null)
                panelRoot.SetActive(open);
            if (chipRoot != null)
                chipRoot.SetActive(!open);
            if (open)
                RefreshActive();
        }

        void EnsureBuilt()
        {
            EnsureEventSystem();
            if (roundedSprite == null)
                roundedSprite = CreateRoundedSprite(64, 14);

            if (panelRoot == null)
            {
                panelRoot = transform.Find(PanelName)?.gameObject;
                if (panelRoot == null)
                    panelRoot = BuildPanel();
            }

            if (chipRoot == null)
            {
                chipRoot = transform.Find(ChipName)?.gameObject;
                if (chipRoot == null)
                    chipRoot = BuildChip();
            }

            BindTaskManager();
            SetLayerRecursive(gameObject, BirdSceneRoles.UiLayer);
            if (!layoutReady)
            {
                SetOpen(startOpen);
                layoutReady = true;
            }
        }

        void BindTaskManager()
        {
            if (taskManager != null)
                return;
            taskManager = FindAnyObjectByType<TaskManager>();
            if (taskManager != null)
                taskManager.OnTaskChanged += OnTaskChanged;
        }

        void OnTaskChanged(string taskName, string instruction) => RefreshActive();

        void RefreshActive()
        {
            if (rows == null)
                return;

            var current = taskManager != null ? taskManager.CurrentTaskId : "";
            for (var i = 0; i < rows.Length; i++)
            {
                var active = rows[i].taskId == current;
                if (rows[i].background != null)
                    rows[i].background.color = active ? RowActive : RowIdle;
                if (rows[i].wishText != null)
                    rows[i].wishText.color = active ? Gold : Ivory;
                if (rows[i].mark != null)
                    rows[i].mark.text = active ? "·" : "";
            }
        }

        GameObject BuildPanel()
        {
            var canvasGo = CreateCanvas(PanelName, AvianHudLayout.WishPanelLocalPosition, AvianHudLayout.WishPanelCanvasSize, AvianHudLayout.WishPanelWorldScale, 40);
            var root = canvasGo.GetComponent<RectTransform>();

            var card = CreateImage(root, "Card", Vector2.zero, AvianHudLayout.WishPanelCanvasSize, Card, roundedSprite, true);
            card.raycastTarget = true;

            var title = CreateTmp(root, "Title", new Vector2(0f, 158f), new Vector2(320f, 32f), "What do you wish?", 20, FontStyles.Normal, Ivory, TextAlignmentOptions.Center);
            title.characterSpacing = 2.2f;

            CreateImage(root, "Rule", new Vector2(0f, 136f), new Vector2(72f, 1.2f), GoldSoft, null, false);

            rows = new WishRow[Wishes.Length];
            var y = 96f;
            for (var i = 0; i < Wishes.Length; i++)
            {
                rows[i] = BuildRow(root, Wishes[i], new Vector2(0f, y));
                y -= 44f;
            }

            BuildStayButton(root, new Vector2(0f, -168f));
            return canvasGo;
        }

        WishRow BuildRow(RectTransform parent, Wish wish, Vector2 pos)
        {
            var go = new GameObject(wish.taskId);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(340f, 40f);

            var bg = go.AddComponent<Image>();
            bg.sprite = roundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = RowIdle;
            bg.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = bg;
            go.AddComponent<WishRowHover>().Bind(this, bg, wish.taskId);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var taskId = wish.taskId;
            button.onClick.AddListener(() => Choose(taskId));

            var mark = CreateTmp(rect, "Mark", new Vector2(-152f, 0f), new Vector2(22f, 28f), "", 20, FontStyles.Bold, Gold, TextAlignmentOptions.Center);
            var wishText = CreateTmp(rect, "Wish", new Vector2(10f, 0f), new Vector2(300f, 28f), wish.line, 16, FontStyles.Normal, Ivory, TextAlignmentOptions.MidlineLeft);

            return new WishRow
            {
                taskId = wish.taskId,
                background = bg,
                wishText = wishText,
                mark = mark
            };
        }

        void BuildStayButton(RectTransform parent, Vector2 pos)
        {
            var go = new GameObject("StayHere");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(160f, 28f);

            var label = CreateTmp(rect, "Label", Vector2.zero, new Vector2(160f, 28f), "Stay here", 13, FontStyles.Italic, IvoryMuted, TextAlignmentOptions.Center);
            var hit = go.AddComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => SetOpen(false));
            _ = label;
        }

        GameObject BuildChip()
        {
            var canvasGo = CreateCanvas(ChipName, AvianHudLayout.WishChipLocalPosition, AvianHudLayout.WishChipCanvasSize, AvianHudLayout.WishChipWorldScale, 41);
            var root = canvasGo.GetComponent<RectTransform>();
            var card = CreateImage(root, "Card", Vector2.zero, AvianHudLayout.WishChipCanvasSize, Card, roundedSprite, true);
            card.raycastTarget = true;
            var button = canvasGo.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = card;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => SetOpen(true));
            CreateTmp(root, "Label", Vector2.zero, new Vector2(150f, 28f), "I want …", 16, FontStyles.Italic, Ivory, TextAlignmentOptions.Center);
            return canvasGo;
        }

        void Choose(string taskId)
        {
            BindTaskManager();
            taskManager?.LoadTaskById(taskId);
            RefreshActive();
            SetOpen(false);
        }

        bool WasXrMenuPressed()
        {
            var left = ReadMenu(XRNode.LeftHand, ref leftMenuHeld);
            var right = ReadMenu(XRNode.RightHand, ref rightMenuHeld);
            return left || right;
        }

        static bool ReadMenu(XRNode node, ref bool held)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            var pressed = false;
            if (device.isValid)
                device.TryGetFeatureValue(CommonUsages.menuButton, out pressed);
            var edge = pressed && !held;
            held = pressed;
            return edge;
        }

        GameObject CreateCanvas(string name, Vector3 localPos, Vector2 size, float worldScale, int sortingOrder)
        {
            var canvasGo = new GameObject(name);
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = localPos;
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * worldScale;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 12f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            canvasGo.AddComponent<AvianWorldCanvasCameraBinder>();
            return canvasGo;
        }

        static Image CreateImage(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color, Sprite sprite, bool sliced)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                if (sliced)
                    image.type = Image.Type.Sliced;
            }

            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI CreateTmp(
            RectTransform parent,
            string name,
            Vector2 pos,
            Vector2 size,
            string content,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        static Sprite CreateRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            var r = radius;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = InsideRoundRect(x, y, size, r);
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var border = r;
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        static bool InsideRoundRect(int x, int y, int size, int radius)
        {
            var dx = 0;
            var dy = 0;
            if (x < radius)
                dx = radius - x;
            else if (x >= size - radius)
                dx = x - (size - radius - 1);
            if (y < radius)
                dy = radius - y;
            else if (y >= size - radius)
                dy = y - (size - radius - 1);
            if (dx == 0 || dy == 0)
                return true;
            return dx * dx + dy * dy <= radius * radius;
        }

        static void EnsureEventSystem()
        {
            var es = EventSystem.current;
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }

            if (Object.FindAnyObjectByType<XRUIInputModule>() != null)
                return;
            if (es.GetComponent<BaseInputModule>() == null)
                es.gameObject.AddComponent<XRUIInputModule>();
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        bool IsWishActive(string taskId) =>
            taskManager != null && taskManager.CurrentTaskId == taskId;

        void PaintRow(string taskId)
        {
            if (rows == null)
                return;
            var current = taskManager != null ? taskManager.CurrentTaskId : "";
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].taskId != taskId)
                    continue;
                var active = rows[i].taskId == current;
                if (rows[i].background != null)
                    rows[i].background.color = active ? RowActive : RowIdle;
                return;
            }
        }

        sealed class WishRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            SceneWishMenu host;
            Image image;
            string taskId;

            public void Bind(SceneWishMenu menu, Image background, string id)
            {
                host = menu;
                image = background;
                taskId = id;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (image == null || host == null || host.IsWishActive(taskId))
                    return;
                image.color = RowHover;
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                host?.PaintRow(taskId);
            }
        }
    }
}
