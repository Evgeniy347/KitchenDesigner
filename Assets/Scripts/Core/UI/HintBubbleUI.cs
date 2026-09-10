using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class HintBubbleUI : MonoBehaviour
    {
        private static HintBubbleUI? _instance;

        private RectTransform _canvasRect = null!;
        private Image? _border;
        private Image? _cloud;
        private TextMeshProUGUI? _label;
        private RectTransform? _owner;
        private bool _pinned;
        private int _pinnedFrame = -1;

        public static bool IsOpen => _instance != null && _instance._owner != null;

        public static bool IsPinnedBy(RectTransform badge) =>
            _instance != null && _instance._pinned && _instance._owner == badge;

        public static void Show(RectTransform badge, string text)
        {
            var self = EnsureOnCanvasOf(badge);
            if (self == null) return;
            self.Open(badge, text);
        }

        public static void Pin(RectTransform badge, string text)
        {
            var self = EnsureOnCanvasOf(badge);
            if (self == null) return;
            self.Open(badge, text);
            self._pinned = true;
            self._pinnedFrame = Time.frameCount;
        }

        public static void HideUnpinned(RectTransform badge)
        {
            if (_instance == null || _instance._pinned) return;
            if (_instance._owner != badge) return;
            _instance.Close();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.Close();
        }

        private void Open(RectTransform badge, string text)
        {
            Build();
            if (_border == null || _cloud == null || _label == null) return;

            _owner = badge;
            _label.text = text;

            float maxTextW = UIStyle.HintBubbleMaxWidth - UIStyle.HintBubblePadX * 2f;
            var preferred = _label.GetPreferredValues(text, maxTextW, 0f);
            float textW = Mathf.Min(preferred.x, maxTextW);
            _label.rectTransform.sizeDelta = new Vector2(textW, preferred.y);

            var size = new Vector2(textW + UIStyle.HintBubblePadX * 2f,
                preferred.y + UIStyle.HintBubblePadY * 2f);
            _cloud.rectTransform.sizeDelta = size;
            _border.rectTransform.sizeDelta = size + Vector2.one * (UIStyle.HintCloudBorderPx * 2f);

            _border.rectTransform.anchoredPosition = Place(badge, size);
            _border.gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Close()
        {
            _owner = null;
            _pinned = false;
            if (_border != null) _border.gameObject.SetActive(false);
        }

        private Vector2 Place(RectTransform badge, Vector2 size)
        {
            var corners = new Vector3[4];
            badge.GetWorldCorners(corners);
            Vector2 screenCenter = (corners[0] + corners[2]) * 0.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenCenter, null, out Vector2 local);

            return HintBubbleLayout.Beside(local, badge.rect.width, size, _canvasRect.rect.size);
        }

        private static HintBubbleUI? EnsureOnCanvasOf(RectTransform badge)
        {
            if (_instance != null) return _instance;

            var canvas = badge.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var canvasRect = (RectTransform)canvas.transform;
            var root = UIFactory.CreateRect("HintBubble", canvasRect);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var self = root.gameObject.AddComponent<HintBubbleUI>();
            self._canvasRect = canvasRect;
            _instance = self;
            return self;
        }

        private void Build()
        {
            if (_border != null) return;

            _border = UIFactory.CreatePanel("HintCloudBorder", transform, Vector2.zero,
                Vector2.zero, UIStyle.HintCloudBorder);
            UIFactory.AnchorCenter(_border.rectTransform);
            _border.raycastTarget = false;

            _cloud = UIFactory.CreatePanel("HintCloud", _border.transform, Vector2.zero,
                Vector2.zero, UIStyle.HintCloud);
            UIFactory.AnchorCenter(_cloud.rectTransform);
            _cloud.raycastTarget = false;

            _label = UIFactory.CreateLabel("HintText", _cloud.transform, string.Empty,
                UIStyle.FontSmall, Vector2.zero, Vector2.zero, TextAnchor.UpperLeft);
            UIFactory.AnchorCenter(_label.rectTransform);
            _label.enableWordWrapping = true;
            _label.raycastTarget = false;

            _border.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_owner == null) return;

            if (!_owner.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) && OwnsEscape())
            {
                Close();
                return;
            }

            if (!_pinned) return;
            bool clickedAway = (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                && Time.frameCount != _pinnedFrame;
            if (clickedAway) Close();
        }

        private static bool OwnsEscape() =>
            EscapeOwnership.Resolve(new EscapeClaims
            {
                HintOpen = true,
                Dragging = ElementMover.IsDragging,
                LightPicking = Lighting.LightPickMode.Active,
                Measuring = Measure.MeasureMode.Active,
                Eyedropping = Tools.EyedropperMode.Active,
            }) == EscapeOwner.HintBubble;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
