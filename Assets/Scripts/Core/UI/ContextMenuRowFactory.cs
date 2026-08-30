using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static KitchenDesigner.Core.UI.ContextMenuMetrics;

namespace KitchenDesigner.Core.UI
{
    internal readonly struct RowVisibility
    {
        private readonly ElementFacet _facet;
        private readonly ElementFacet _except;
        private readonly Func<bool>? _when;

        private RowVisibility(ElementFacet facet, ElementFacet except, Func<bool>? when)
        {
            _facet = facet;
            _except = except;
            _when = when;
        }

        public static RowVisibility Always => new(ElementFacet.None, ElementFacet.None, null);

        public static RowVisibility When(Func<bool> when) =>
            new(ElementFacet.None, ElementFacet.None, when);

        public static RowVisibility For(ElementFacet facet) =>
            new(facet, ElementFacet.None, null);

        public static RowVisibility For(ElementFacet facet, Func<bool> when) =>
            new(facet, ElementFacet.None, when);

        public static RowVisibility ForExcept(ElementFacet facet, ElementFacet except) =>
            new(facet, except, null);

        public void Register(ContextMenuLayout layout, float height, float gapAfter,
            params RectTransform[] rects) =>
            layout.Register(height, gapAfter, _facet, _except, _when, rects);
    }

    internal sealed class ContextMenuRowFactory
    {
        private const float FieldW = 120f;
        private const float DropdownLabelX = -103f;
        private const float DropdownLabelW = 126f;
        private const float DropdownX = 65f;
        private const float DropdownW = 202f;
        private const float DropdownH = 28f;
        private const float SectionHeaderH = 18f;
        private const float ToggleH = 26f;

        private readonly Transform _parent;
        private readonly ContextMenuLayout _layout;

        public ContextMenuRowFactory(Transform parent, ContextMenuLayout layout)
        {
            _parent = parent;
            _layout = layout;
        }

        public TMP_InputField NumberField(string label, RowVisibility visibility, string unit = "мм",
            string? nodeSuffix = null)
        {
            var node = nodeSuffix ?? label;
            var lbl = UIFactory.CreateLabel("L_" + node, _parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateNumberField("F_" + node, _parent, "",
                new Vector2(FieldX, 0), new Vector2(FieldW, FieldH), unit);
            visibility.Register(_layout, RowH, RowGap,
                lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        public TMP_InputField NameField()
        {
            var lbl = UIFactory.CreateLabel("L_Название", _parent, "Название", 15,
                new Vector2(NameLabelX, 0), new Vector2(NameLabelW, LabelH));
            var field = UIFactory.CreateInputField("F_Название", _parent, "",
                new Vector2(NameFieldX, 0), new Vector2(NameFieldW, FieldH));
            _layout.Add(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        public TMP_InputField TriField(string label, float columnX)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, _parent, label, 12,
                new Vector2(columnX, 0), new Vector2(TriLabelW, TriLabelH), TextAnchor.MiddleCenter);
            var field = UIFactory.CreateInputField("F_" + label, _parent, "",
                new Vector2(columnX, 0), new Vector2(TriFieldW, FieldH));
            _layout.AddTriColumn(lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        public TMP_Dropdown Dropdown(string label, List<string> options, Action<int> onChanged,
            RowVisibility visibility, string? nodeName = null)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, _parent, label, 15,
                new Vector2(DropdownLabelX, 0), new Vector2(DropdownLabelW, LabelH));
            var dd = UIFactory.CreateDropdown(nodeName ?? ("Dd_" + label), _parent, options,
                new Vector2(DropdownX, 0), new Vector2(DropdownW, DropdownH), onChanged);
            visibility.Register(_layout, DropdownH, RowGap,
                lbl.rectTransform, dd.GetComponent<RectTransform>());
            return dd;
        }

        public (TMP_Text label, TMP_Dropdown dropdown) NamedDropdown(string nodeName, string label,
            List<string> options, Action<int> onChanged, RowVisibility visibility)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, _parent, label, 15,
                new Vector2(DropdownLabelX, 0), new Vector2(DropdownLabelW, LabelH));
            var dd = UIFactory.CreateDropdown(nodeName, _parent, options,
                new Vector2(DropdownX, 0), new Vector2(DropdownW, DropdownH), onChanged);
            visibility.Register(_layout, DropdownH, RowGap,
                lbl.rectTransform, dd.GetComponent<RectTransform>());
            return (lbl, dd);
        }

        public TMP_Text WideButton(string nodeName, string caption, Action onClick,
            RowVisibility visibility, float gapAfter)
        {
            var button = UIFactory.CreateButton(nodeName, _parent, caption,
                Vector2.zero, new Vector2(RowWidth, BtnH), onClick);
            visibility.Register(_layout, BtnH, gapAfter, button.GetComponent<RectTransform>());
            return button.GetComponentInChildren<TMP_Text>();
        }

        public Toggle Toggle(string nodeName, string caption, bool value, Action<bool> onChanged,
            RowVisibility visibility, float gapAfter)
        {
            var toggle = UIFactory.CreateToggle(nodeName, _parent, caption, value,
                Vector2.zero, new Vector2(RowWidth, ToggleH), onChanged);
            visibility.Register(_layout, ToggleH, gapAfter, toggle.GetComponent<RectTransform>());
            return toggle;
        }

        public void SectionHeader(string nodeName, string title)
        {
            _layout.Add(SectionHeaderH, RowGap,
                UIFactory.CreateSectionHeader(nodeName, _parent, title, RowWidth));
        }

        public TMP_Text Hint(string nodeName, string text, float height, float gapAfter,
            RowVisibility visibility, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var lbl = UIFactory.CreateLabel(nodeName, _parent, text, 12,
                Vector2.zero, new Vector2(RowWidth, height), anchor);
            lbl.color = UIStyle.TextSecondary;
            lbl.raycastTarget = false;
            visibility.Register(_layout, height, gapAfter, lbl.rectTransform);
            return lbl;
        }
    }
}
