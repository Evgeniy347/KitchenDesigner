using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class TooltipUI : MonoBehaviour
    {
        private const float PadX = 10f;
        private const float PadY = 5f;

        private static TooltipUI? _instance;

        private readonly TooltipSchedule _schedule = new TooltipSchedule();

        private RectTransform _canvasRect = null!;
        private Image? _panel;
        private TextMeshProUGUI? _label;

        private RectTransform? _pendingTarget;
        private string _pendingText = string.Empty;

        public static void Attach(GameObject target, string text)
        {
            if (target == null || string.IsNullOrEmpty(text)) return;

            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            var rect = target.GetComponent<RectTransform>();
            Add(trigger, EventTriggerType.PointerEnter, () => Request(rect, text));
            Add(trigger, EventTriggerType.PointerExit, Hide);
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance._pendingTarget = null;
            _instance._schedule.Cancel();
            if (_instance._panel != null) _instance._panel.gameObject.SetActive(false);
        }

        private static void Request(RectTransform? target, string text)
        {
            if (target == null) return;
            var self = EnsureOnCanvasOf(target);
            if (self == null) return;

            self._pendingTarget = target;
            self._pendingText = text;
            self._schedule.Request(Time.unscaledTime);
            if (self._panel != null) self._panel.gameObject.SetActive(false);
        }

        private static TooltipUI? EnsureOnCanvasOf(RectTransform target)
        {
            if (_instance != null) return _instance;

            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var canvasRect = (RectTransform)canvas.transform;
            var root = UIFactory.CreateRect("Tooltip", canvasRect);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var self = root.gameObject.AddComponent<TooltipUI>();
            self._canvasRect = canvasRect;
            _instance = self;
            return self;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_pendingTarget == null) return;

            if (!_pendingTarget.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            if (!_schedule.DueAt(Time.unscaledTime)) return;

            var target = _pendingTarget;
            _pendingTarget = null;
            Show(target, _pendingText);
        }

        private void Show(RectTransform target, string text)
        {
            Build();
            if (_panel == null || _label == null) return;

            _label.text = text;
            var textSize = _label.GetPreferredValues(text);
            var size = new Vector2(textSize.x + PadX * 2f, textSize.y + PadY * 2f);
            _panel.rectTransform.sizeDelta = size;
            _panel.rectTransform.anchoredPosition = Place(target, size);
            _panel.gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private Vector2 Place(RectTransform target, Vector2 size)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 screenBottomCenter = (corners[0] + corners[3]) * 0.5f;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenBottomCenter, null, out Vector2 local);

            return TooltipPlacement.Below(local, target.rect.height, size, _canvasRect.rect.size);
        }

        private void Build()
        {
            if (_panel != null) return;

            _panel = UIFactory.CreatePanel("TooltipPanel", transform, Vector2.zero,
                Vector2.zero, UIStyle.Field);
            UIFactory.AnchorCenter(_panel.rectTransform);
            _panel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _panel.raycastTarget = false;

            _label = UIFactory.CreateLabel("TooltipText", _panel.transform, string.Empty,
                UIStyle.FontSmall, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            var lr = _label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(PadX, PadY);
            lr.offsetMax = new Vector2(-PadX, -PadY);
            _label.raycastTarget = false;

            _panel.gameObject.SetActive(false);
        }

        private static void Add(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }
    }
}
