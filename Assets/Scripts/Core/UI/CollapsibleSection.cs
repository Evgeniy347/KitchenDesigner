using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class CollapsibleSection : MonoBehaviour, IPointerClickHandler
    {
        public const string ChevronNode = "Chevron";
        public const string TitleNode = "Title";
        public const string CountNode = "Count";
        public const string LineNode = "Line";

        private static readonly Dictionary<string, bool> CollapsedThisSession = new();

        private TMP_Text? _chevron;
        private TMP_Text? _title;
        private TMP_Text? _count;
        private RectTransform? _line;
        private Button? _action;
        private string? _memoryKey;
        private float _width;

        public bool Expanded { get; private set; } = true;

        public event System.Action<bool>? Toggled;

        public TMP_Text? Title => _title;

        public TMP_Text? Count => _count;

        public Button? ActionButton => _action;

        public static CollapsibleSection Create(string name, Transform parent, string title, float width,
            string? memoryKey = null, string? actionCaption = null, System.Action? onAction = null)
        {
            var rect = UIFactory.CreateRect(name, parent);
            rect.sizeDelta = new Vector2(width, UIStyle.SectionHeaderH);
            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = UIStyle.RaycastOnly;

            var self = rect.gameObject.AddComponent<CollapsibleSection>();
            self._width = width;
            self._memoryKey = memoryKey;
            self._chevron = Label(ChevronNode, rect, UIStyle.GlyphExpanded, UIStyle.FontCaption, UIStyle.TextSecondary,
                UIStyle.ChevronW, TextAnchor.MiddleCenter);
            self._title = Label(TitleNode, rect, title, UIStyle.FontSection, UIStyle.TextSecondary, width,
                TextAnchor.MiddleLeft);
            self._title.fontStyle = FontStyles.Bold;
            self._count = Label(CountNode, rect, "", UIStyle.FontSection, UIStyle.TextDisabled, width,
                TextAnchor.MiddleLeft);

            self._line = UIFactory.CreateRect(LineNode, rect);
            var line = self._line.gameObject.AddComponent<Image>();
            line.color = UIStyle.Divider;
            line.raycastTarget = false;

            if (actionCaption != null && onAction != null)
            {
                self._action = UIFactory.CreateButton(name + "_Action", rect, actionCaption, Vector2.zero,
                    new Vector2(width, UIStyle.SectionHeaderH), onAction);
                ButtonRoles.Paint(self._action, ButtonRole.Link);
                var label = self._action.GetComponentInChildren<TMP_Text>();
                label.fontSize = UIStyle.FontSmall;
                var art = (RectTransform)self._action.transform;
                art.sizeDelta = new Vector2(Mathf.Ceil(label.GetPreferredValues(actionCaption).x) + UIStyle.Space2,
                    UIStyle.SectionHeaderH);
            }

            bool collapsed = memoryKey != null && CollapsedThisSession.TryGetValue(memoryKey, out var c) && c;
            self.SetExpanded(!collapsed, notify: false);
            return self;
        }

        public void SetCount(string? text)
        {
            if (_count == null) return;
            _count.text = text ?? "";
            Arrange();
        }

        public void Toggle() => SetExpanded(!Expanded, notify: true);

        public void SetExpanded(bool expanded, bool notify)
        {
            Expanded = expanded;
            if (_chevron != null) _chevron.text = expanded ? UIStyle.GlyphExpanded : LayoutDirection.CollapsedGlyph;
            if (_memoryKey != null) CollapsedThisSession[_memoryKey] = !expanded;
            Arrange();
            if (notify) Toggled?.Invoke(expanded);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) Toggle();
        }

        internal static void ForgetSessionState() => CollapsedThisSession.Clear();

        private void Arrange()
        {
            if (_title == null || _count == null || _line == null || _chevron == null) return;
            float x = UIStyle.ChevronW + UIStyle.Space1;
            float titleW = Mathf.Ceil(_title.GetPreferredValues(_title.text).x);
            Put(_chevron.rectTransform, 0f, UIStyle.ChevronW);
            Put(_title.rectTransform, x, titleW);
            x += titleW;
            if (_count.text.Length > 0)
            {
                x += UIStyle.Space2;
                float countW = Mathf.Ceil(_count.GetPreferredValues(_count.text).x);
                Put(_count.rectTransform, x, countW);
                x += countW;
            }
            _count.gameObject.SetActive(_count.text.Length > 0);

            float right = _width;
            if (_action != null)
            {
                var art = (RectTransform)_action.transform;
                right -= art.sizeDelta.x;
                Put(art, right, art.sizeDelta.x);
                right -= UIStyle.Space2;
            }
            x += UIStyle.Space2;
            float lineW = Mathf.Max(0f, right - x);
            _line.anchorMin = _line.anchorMax = _line.pivot = new Vector2(0f, 0.5f);
            _line.sizeDelta = new Vector2(lineW, UIStyle.DividerPx);
            _line.anchoredPosition = new Vector2(LayoutDirection.StartX(_width, x, lineW), 0f);
        }

        private void Put(RectTransform rt, float x, float w)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(w, UIStyle.SectionHeaderH);
            rt.anchoredPosition = new Vector2(LayoutDirection.StartX(_width, x, w), 0f);
        }

        private static TextMeshProUGUI Label(string name, RectTransform parent, string text, int size, Color color,
            float width, TextAnchor align)
        {
            var label = UIFactory.CreateLabel(name, parent, text, size, Vector2.zero,
                new Vector2(width, UIStyle.SectionHeaderH), align);
            label.color = color;
            label.enableWordWrapping = false;
            label.raycastTarget = false;
            return label;
        }
    }
}
