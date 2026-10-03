using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public static class PresetCurrentMark
    {
        public const string MarkName = "CurrentMark";
        public const float MarkSize = 16f;
        public const float MarkGap = 4f;
        public const float MarkLane = MarkSize + MarkGap;

        public static void Attach(Button button)
        {
            var rect = UIFactory.CreateRect(MarkName, button.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(MarkSize, MarkSize);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = IconFactory.Star;
            image.color = UIStyle.Text;
            image.raycastTarget = false;
            image.preserveAspect = true;
            rect.gameObject.SetActive(false);
        }

        public static void Set(Button button, bool current)
        {
            var mark = button.transform.Find(MarkName);
            if (mark != null) mark.gameObject.SetActive(current);

            var caption = button.GetComponentInChildren<TMPro.TMP_Text>();
            if (caption == null) return;
            caption.rectTransform.offsetMin = new Vector2(current ? MarkLane : 0f, 0f);
            if (!current || mark == null) return;

            float buttonWidth = ((RectTransform)button.transform).rect.width;
            float groupWidth = MarkLane + caption.GetPreferredValues(caption.text).x;
            float left = Mathf.Max(UIStyle.GapInner, (buttonWidth - groupWidth) * 0.5f);
            ((RectTransform)mark).anchoredPosition = new Vector2(left, 0f);
        }
    }
}
