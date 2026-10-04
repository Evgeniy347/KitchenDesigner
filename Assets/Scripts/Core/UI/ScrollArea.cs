using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class ScrollArea
    {
        public const float Sensitivity = 20f;
        public const float BarInset = 2f;

        private ScrollArea(RectTransform viewport, RectTransform content, ScrollRect scroll)
        {
            Viewport = viewport;
            Content = content;
            Scroll = scroll;
        }

        public RectTransform Viewport { get; }

        public RectTransform Content { get; }

        public ScrollRect Scroll { get; }

        public float ContentHeight
        {
            get => Content.sizeDelta.y;
            set
            {
                var size = Content.sizeDelta;
                size.y = value;
                Content.sizeDelta = size;
            }
        }

        public static ScrollArea Create(string name, Transform parent, float barWidth)
        {
            var viewport = UIFactory.CreateRect(name, parent);
            var backdrop = viewport.gameObject.AddComponent<Image>();
            backdrop.color = UIStyle.RaycastOnly;
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = UIFactory.CreateRect(name + "Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = Sensitivity;

            if (barWidth > 0f) AttachBar(name, viewport, scroll, barWidth);

            return new ScrollArea(viewport, content, scroll);
        }

        public static Scrollbar AttachBar(string name, RectTransform parent, ScrollRect scroll,
            float barWidth, ScrollRect.ScrollbarVisibility visibility = ScrollRect.ScrollbarVisibility.AutoHide)
        {
            var bar = UIFactory.CreateRect(name + "Bar", parent);
            bool rtl = LayoutDirection.IsRtl;
            float side = rtl ? 0f : 1f;
            bar.anchorMin = new Vector2(side, 0f);
            bar.anchorMax = new Vector2(side, 1f);
            bar.pivot = new Vector2(side, 0.5f);
            bar.offsetMin = new Vector2(rtl ? BarInset : -(barWidth + BarInset), 0f);
            bar.offsetMax = new Vector2(rtl ? barWidth + BarInset : -BarInset, 0f);

            var track = bar.gameObject.AddComponent<Image>();
            track.color = UIStyle.ScrollTrack;

            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var handle = UIFactory.CreateRect("Handle", bar);
            handle.sizeDelta = Vector2.zero;
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = UIStyle.ScrollHandle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = visibility;
            scroll.verticalScrollbarSpacing = BarInset;
            return scrollbar;
        }
    }
}
