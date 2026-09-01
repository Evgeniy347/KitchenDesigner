using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    [Flags]
    internal enum ElementFacet
    {
        None = 0,
        Facade = 1 << 0,
        Assembled = 1 << 1,
        Radial = 1 << 2,
        Drawer = 1 << 3,
        Table = 1 << 4,
        Pillar = 1 << 5,
        Window = 1 << 6,
        Door = 1 << 7,
        Part = 1 << 8,
        Light = 1 << 9,
        Oven = 1 << 10,
        Dishwasher = 1 << 11,
        Stool = 1 << 12,
        Chair = 1 << 13,
    }

    internal sealed class ContextMenuLayout
    {
        private readonly struct Row
        {
            public Row(RectTransform[] rects, float height, float gapAfter,
                ElementFacet showFor, ElementFacet hideFor, Func<bool>? visibleWhen)
            {
                Rects = rects;
                Height = height;
                GapAfter = gapAfter;
                ShowFor = showFor;
                HideFor = hideFor;
                VisibleWhen = visibleWhen;
            }

            public RectTransform[] Rects { get; }
            public float Height { get; }
            public float GapAfter { get; }
            public ElementFacet ShowFor { get; }
            public ElementFacet HideFor { get; }
            public Func<bool>? VisibleWhen { get; }

            public bool IsConditional =>
                ShowFor != ElementFacet.None || HideFor != ElementFacet.None || VisibleWhen != null;

            public bool IsVisibleFor(ElementFacet facets) =>
                (ShowFor == ElementFacet.None || (facets & ShowFor) != ElementFacet.None)
                && (facets & HideFor) == ElementFacet.None
                && (VisibleWhen == null || VisibleWhen());
        }

        private readonly List<Row> _rows = new();
        private readonly List<RectTransform> _pendingTriLabels = new();
        private readonly List<RectTransform> _pendingTriFields = new();
        private readonly List<RectTransform> _rotationXZ = new();

        public IReadOnlyList<RectTransform> PendingTriLabels => _pendingTriLabels;
        public IReadOnlyList<RectTransform> PendingTriFields => _pendingTriFields;

        public void Clear()
        {
            _rows.Clear();
            _pendingTriLabels.Clear();
            _pendingTriFields.Clear();
            _rotationXZ.Clear();
        }

        public static void AnchorTop(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        }

        public void Add(float height, float gapAfter, params RectTransform[] rects) =>
            Register(height, gapAfter, ElementFacet.None, ElementFacet.None, null, rects);

        public void AddWhen(Func<bool> visibleWhen, float height, float gapAfter, params RectTransform[] rects) =>
            Register(height, gapAfter, ElementFacet.None, ElementFacet.None, visibleWhen, rects);

        public void AddFor(ElementFacet showFor, float height, float gapAfter, params RectTransform[] rects) =>
            Register(height, gapAfter, showFor, ElementFacet.None, null, rects);

        public void AddFor(ElementFacet showFor, Func<bool> visibleWhen, float height, float gapAfter,
            params RectTransform[] rects) =>
            Register(height, gapAfter, showFor, ElementFacet.None, visibleWhen, rects);

        public void AddExcept(ElementFacet hideFor, float height, float gapAfter, params RectTransform[] rects) =>
            Register(height, gapAfter, ElementFacet.None, hideFor, null, rects);

        public void AddTriColumn(RectTransform label, RectTransform field)
        {
            _pendingTriLabels.Add(label);
            _pendingTriFields.Add(field);
        }

        public void EndTriRow(float labelHeight, float labelGap, float fieldHeight, float fieldGap,
            ElementFacet hideFor)
        {
            Register(labelHeight, labelGap, ElementFacet.None, hideFor, null, _pendingTriLabels.ToArray());
            Register(fieldHeight, fieldGap, ElementFacet.None, hideFor, null, _pendingTriFields.ToArray());
            _pendingTriLabels.Clear();
            _pendingTriFields.Clear();
        }

        public void AddRotationXZ(RectTransform rt) => _rotationXZ.Add(rt);

        public float Apply(ElementFacet facets, bool showRotationXZ, float topPadding)
        {
            float cursor = 0f;
            float contentBottom = 0f;
            foreach (var row in _rows)
            {
                bool visible = row.IsVisibleFor(facets);
                if (row.IsConditional)
                    foreach (var rt in row.Rects)
                        if (rt != null) rt.gameObject.SetActive(visible);

                if (!visible) continue;

                float topY = -(topPadding + cursor);
                foreach (var rt in row.Rects)
                    if (rt != null)
                        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, topY);

                contentBottom = cursor + row.Height;
                cursor = contentBottom + row.GapAfter;
            }

            foreach (var rt in _rotationXZ)
                if (rt != null) rt.gameObject.SetActive(showRotationXZ);

            return topPadding + contentBottom;
        }

        public void Register(float height, float gapAfter, ElementFacet showFor, ElementFacet hideFor,
            Func<bool>? visibleWhen, RectTransform[] rects)
        {
            foreach (var rt in rects)
                if (rt != null) AnchorTop(rt);
            _rows.Add(new Row(rects, height, gapAfter, showFor, hideFor, visibleWhen));
        }
    }
}
