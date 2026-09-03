using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public readonly struct ContentSpan
    {
        private readonly string? _name;

        public ContentSpan(string name, float top, float bottom)
        {
            _name = name;
            Top = top;
            Bottom = bottom;
        }

        public string Name => _name ?? "";

        public float Top { get; }

        public float Bottom { get; }
    }

    public readonly struct ContentFit
    {
        public ContentFit(float contentHeight, float visibleHeight, ContentSpan lowest)
        {
            ContentHeight = contentHeight;
            VisibleHeight = visibleHeight;
            Lowest = lowest;
        }

        public float ContentHeight { get; }

        public float VisibleHeight { get; }

        public ContentSpan Lowest { get; }

        public float Overflow => Mathf.Max(0f, ContentHeight - VisibleHeight);

        public bool Overflows => ContentHeight - VisibleHeight > ContentExtent.SlackPx;

        public override string ToString()
        {
            string head = $"содержимое {ContentHeight:0.#} px при видимой высоте {VisibleHeight:0.#} px";
            if (!Overflows) return head;
            return head + $"; ниже края на {Overflow:0.#} px, самый нижний узел «{Lowest.Name}» "
                + $"занимает {Lowest.Top:0.#}..{Lowest.Bottom:0.#} px от верха области";
        }
    }

    public static class ContentExtent
    {
        public const float SlackPx = 0.5f;

        public static ContentFit Measure(IReadOnlyList<ContentSpan> spans, float bottomPad,
            float visibleHeight)
        {
            var lowest = new ContentSpan("", 0f, 0f);
            for (int i = 0; i < spans.Count; i++)
                if (spans[i].Bottom > lowest.Bottom)
                    lowest = spans[i];

            float contentHeight = lowest.Bottom <= 0f ? 0f : lowest.Bottom + bottomPad;
            return new ContentFit(contentHeight, visibleHeight, lowest);
        }
    }
}
