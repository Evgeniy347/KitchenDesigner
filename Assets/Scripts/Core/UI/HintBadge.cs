using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class HintBadge : MonoBehaviour, IPointerClickHandler
    {
        public const float LaneWidth = UIStyle.HintBadgeSize + UIStyle.GapInner;

        private string _text = string.Empty;
        private TextMeshProUGUI? _glyph;

        public string Key { get; private set; } = string.Empty;

        public static HintBadge Attach(Transform parent, Vector2 anchoredPos, string hint)
        {
            string text = HintText.Of(hint);

            var rect = UIFactory.CreateRect("Hint_" + hint, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(UIStyle.HintBadgeSize, UIStyle.HintBadgeSize);
            rect.anchoredPosition = anchoredPos;

            var hit = rect.gameObject.AddComponent<Image>();
            hit.color = UIStyle.RaycastOnly;
            hit.raycastTarget = true;

            var glyph = UIFactory.CreateLabel("HintGlyph", rect, UIStyle.GlyphHint,
                UIStyle.FontSmall, Vector2.zero,
                new Vector2(UIStyle.HintBadgeSize, UIStyle.HintBadgeSize), TextAnchor.MiddleCenter);
            glyph.color = UIStyle.HintIcon;
            glyph.raycastTarget = false;

            var self = rect.gameObject.AddComponent<HintBadge>();
            self._text = text;
            self._glyph = glyph;
            self.Key = hint;

            PointerHover.Attach(rect.gameObject, self.OnEnter, self.OnExit);
            return self;
        }

        public static HintBadge? AttachAfterLabel(TextMeshProUGUI? label, string hint)
        {
            if (label == null)
            {
                Debug.LogError("Подсказка «" + hint + "» потеряна: подписи, к которой ведёт ключ, "
                    + "в реестре строк панели нет, и значок «i» не приклеился ни к чему. "
                    + "Проверьте ключ строки в Hint(...) и то, что строка регистрирует свою подпись.");
                return null;
            }

            var labelRect = label.rectTransform;
            float textWidth = label.GetPreferredValues(label.text).x;
            float laneWidth = NarrowToTextAndLane(labelRect, textWidth);
            float x = HintBadgeLane.AfterLabel(0f, laneWidth,
                textWidth, UIStyle.HintBadgeSize, UIStyle.GapInner);

            return Attach(labelRect, new Vector2(x, 0f), hint);
        }

        private static float NarrowToTextAndLane(RectTransform labelRect, float textWidth)
        {
            float width = labelRect.rect.width;
            if (labelRect.anchorMin != labelRect.anchorMax) return width;

            var narrowed = HintBadgeLane.NarrowedToTextAndLane(
                new HintBadgeLane.LabelRect(labelRect.anchoredPosition.x, labelRect.pivot.x, width),
                textWidth, UIStyle.HintBadgeSize, UIStyle.GapInner);

            labelRect.pivot = new Vector2(narrowed.PivotX, labelRect.pivot.y);
            labelRect.anchoredPosition = new Vector2(narrowed.AnchoredX, labelRect.anchoredPosition.y);
            labelRect.sizeDelta = new Vector2(narrowed.Width, labelRect.sizeDelta.y);
            return narrowed.Width;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (HintBubbleUI.IsPinnedBy(rect))
            {
                HintBubbleUI.Hide();
                return;
            }

            HintBubbleUI.Pin(rect, _text);
        }

        private void OnEnter()
        {
            if (_glyph != null) _glyph.color = UIStyle.HintIconHover;
            HintBubbleUI.Show((RectTransform)transform, _text);
        }

        private void OnExit()
        {
            if (_glyph != null) _glyph.color = UIStyle.HintIcon;
            HintBubbleUI.HideUnpinned((RectTransform)transform);
        }
    }
}
