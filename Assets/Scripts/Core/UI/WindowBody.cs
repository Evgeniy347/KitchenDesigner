using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class WindowBody
    {
        public const float BarW = 10f;
        public const float BottomPadPx = UIStyle.WindowPad;

        private readonly ScrollArea _scroll;
        private readonly List<ContentSpan> _spans = new();
        private readonly RectTransform _panel;
        private readonly float _panelHeight;
        private readonly float _topInset;
        private readonly float _bottomInset;

        private WindowBody(ScrollArea scroll, RectTransform panel, float topInset, float bottomInset)
        {
            _scroll = scroll;
            _panel = panel;
            _panelHeight = panel.sizeDelta.y;
            _topInset = topInset;
            _bottomInset = bottomInset;
        }

        public RectTransform Viewport => _scroll.Viewport;

        public RectTransform Content => _scroll.Content;

        public float VisibleHeight => _panel.sizeDelta.y - _topInset - _bottomInset;

        public float PanelOriginY => _topInset - _panelHeight * 0.5f;

        public static WindowBody Create(RectTransform panel, float topInset, float bottomInset,
            float sidePad)
        {
            var scroll = ScrollArea.Create(panel.name + "Body", panel, BarW);
            var viewport = scroll.Viewport;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.offsetMin = new Vector2(sidePad, bottomInset);
            viewport.offsetMax = new Vector2(-sidePad, -topInset);
            return new WindowBody(scroll, panel, topInset, bottomInset);
        }

        public ContentFit Fit()
        {
            var content = Content;
            if (content == null) return new ContentFit(0f, VisibleHeight, default);

            RectSpans.Collect(content, _spans);
            var fit = ContentExtent.Measure(_spans, BottomPadPx, VisibleHeight);
            _scroll.ContentHeight = fit.ContentHeight;
            return fit;
        }
    }
}
