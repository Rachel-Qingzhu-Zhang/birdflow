using AvianVoice.Embodiment;
using AvianVoice.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace AvianVoice.UI
{
    /// <summary>
    /// Replay chip for a guide-note task. Keyboard R / B and the controller B button
    /// are handled by the task via AvianInput. The chip click replays too.
    /// </summary>
    public class SingingReplayButton : MonoBehaviour
    {
        static SingingReplayButton instance;

        System.Action replay;
        TextMeshProUGUI label;

        public static void Show(System.Action replayAction)
        {
            if (replayAction == null)
                return;

            var parent = Camera.main != null ? Camera.main.transform : null;
            if (instance == null)
                instance = FindAnyObjectByType<SingingReplayButton>(FindObjectsInactive.Include);

            if (instance == null)
            {
                var go = new GameObject("SingingReplay");
                if (parent != null)
                    go.transform.SetParent(parent, false);
                instance = go.AddComponent<SingingReplayButton>();
            }

            instance.replay = replayAction;
            instance.gameObject.SetActive(true);
            if (parent != null)
                instance.Follow(parent);
            instance.RefreshLabel();
        }

        public static void Show(SingingBranchTask singing)
        {
            if (singing == null)
                return;
            Show(singing.ReplayMelody);
        }

        public static void Hide()
        {
            if (instance != null)
                instance.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            AvianUiLanguage.Changed -= RefreshLabel;
            AvianUiLanguage.Changed += RefreshLabel;
            EnsureBuilt();
            RefreshLabel();
        }

        void OnDisable()
        {
            AvianUiLanguage.Changed -= RefreshLabel;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam != null)
                Follow(cam.transform);
        }

        void Follow(Transform parent)
        {
            if (parent == null || transform.parent == parent)
                return;

            transform.SetParent(parent, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        void EnsureBuilt()
        {
            if (label != null)
                return;

            var canvasGo = new GameObject("ReplayChip");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = AvianHudLayout.ReplayChipLocalPosition;
            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one * AvianHudLayout.ReplayChipWorldScale;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 46;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = AvianHudLayout.ReplayChipCanvasSize;
            canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 12f;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
            canvasGo.AddComponent<AvianWorldCanvasCameraBinder>();

            var card = canvasGo.AddComponent<Image>();
            card.color = new Color(0.07f, 0.08f, 0.07f, 0.82f);
            var button = canvasGo.AddComponent<Button>();
            button.targetGraphic = card;
            button.onClick.AddListener(() => replay?.Invoke());

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(rect, false);
            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            label = textGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.96f, 0.93f, 0.88f, 0.96f);
            label.raycastTarget = false;

            SetLayerRecursive(gameObject, BirdSceneRoles.UiLayer);
        }

        void RefreshLabel()
        {
            EnsureBuilt();
            if (label == null)
                return;
            label.text = AvianUiCopy.Replay;
            AvianUiFont.Apply(label);
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
