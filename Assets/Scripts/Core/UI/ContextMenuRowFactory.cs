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
        private const float PairFieldW = 45f;
        private const float PairGap = 4f;
        private const float PairFirstX = FieldX - 27f;
        private const float PairLabelW = 140f;
        private const float PairLabelH = 20f;
        private const int PairLabelFont = 13;
        private const float PairFieldH = 22f;

        private readonly Transform _parent;
        private readonly ContextMenuLayout _layout;
        private readonly UIRowRegistry _registered = new();

        public ContextMenuRowFactory(Transform parent, ContextMenuLayout layout)
        {
            _parent = parent;
            _layout = layout;
        }

        public Transform Parent => _parent;

        public void SyncEnabledState() => _registered.Sync();

        public IEnumerable<(TMP_Text? label, Selectable? control)> LabelledRows =>
            _registered.Rows;

        public TMP_InputField NumberField(string label, RowVisibility visibility, string unit = "мм",
            string? nodeSuffix = null, string? hint = null) =>
            LabelledNumberField(label, visibility, unit, nodeSuffix, hint).field;

        public (TextMeshProUGUI label, TMP_InputField field) LabelledNumberField(string label,
            RowVisibility visibility, string unit = "мм", string? nodeSuffix = null,
            string? hint = null)
        {
            var node = nodeSuffix ?? label;
            var lbl = UIFactory.CreateLabel("L_" + node, _parent, label, 15,
                new Vector2(LabelX, 0), new Vector2(LabelW, LabelH));
            var field = UIFactory.CreateNumberField("F_" + node, _parent, "",
                new Vector2(FieldX, 0), new Vector2(FieldW, FieldH), unit);
            _registered.Add(lbl, field);
            visibility.Register(_layout, RowH, RowGap,
                lbl.rectTransform, field.GetComponent<RectTransform>());
            if (hint != null) HintBadge.AttachAfterLabel(lbl, hint);
            return (lbl, field);
        }

        public (TMP_InputField first, TMP_InputField second) PairField(string label,
            string firstNode, string secondNode, string initial, RowVisibility visibility)
        {
            var lbl = UIFactory.CreateLabel("L_" + firstNode + "_" + secondNode, _parent, label,
                PairLabelFont, new Vector2(LabelX, 0), new Vector2(PairLabelW, PairLabelH),
                TextAnchor.MiddleLeft);
            var first = PairInput(firstNode, initial, PairFirstX);
            var second = PairInput(secondNode, initial, PairFirstX + PairFieldW + PairGap);
            _registered.Add(lbl, first);
            _registered.Add(lbl, second);
            visibility.Register(_layout, FieldH, PairGap, lbl.rectTransform,
                first.GetComponent<RectTransform>(), second.GetComponent<RectTransform>());
            return (first, second);
        }

        private TMP_InputField PairInput(string node, string initial, float x)
        {
            var field = UIFactory.CreateInputField("F_" + node, _parent, initial,
                new Vector2(x, 0), new Vector2(PairFieldW, PairFieldH));
            field.contentType = TMP_InputField.ContentType.Custom;
            field.onValidateInput = DimensionFieldValidation.Char();
            return field;
        }

        public TMP_InputField NameField()
        {
            var lbl = UIFactory.CreateLabel("L_Название", _parent, "Название", 15,
                new Vector2(NameLabelX, 0), new Vector2(NameLabelW, LabelH));
            var field = UIFactory.CreateInputField("F_Название", _parent, "",
                new Vector2(NameFieldX, 0), new Vector2(NameFieldW, FieldH));
            _registered.Add(lbl, field);
            _layout.Add(RowH, RowGap, lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        public TMP_InputField TriField(string label, float columnX)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, _parent, label, 12,
                new Vector2(columnX, 0), new Vector2(TriLabelW, TriLabelH), TextAnchor.MiddleCenter);
            var field = UIFactory.CreateInputField("F_" + label, _parent, "",
                new Vector2(columnX, 0), new Vector2(TriFieldW, FieldH));
            _registered.Add(lbl, field);
            _layout.AddTriColumn(lbl.rectTransform, field.GetComponent<RectTransform>());
            return field;
        }

        public TMP_Dropdown Dropdown(string label, List<string> options, Action<int> onChanged,
            RowVisibility visibility, string? nodeName = null, string? hint = null)
        {
            var lbl = UIFactory.CreateLabel("L_" + label, _parent, label, 15,
                new Vector2(DropdownLabelX, 0), new Vector2(DropdownLabelW, LabelH));
            var dd = UIFactory.CreateDropdown(nodeName ?? ("Dd_" + label), _parent, options,
                new Vector2(DropdownX, 0), new Vector2(DropdownW, DropdownH), onChanged);
            _registered.Add(lbl, dd);
            visibility.Register(_layout, DropdownH, RowGap,
                lbl.rectTransform, dd.GetComponent<RectTransform>());
            if (hint != null) HintBadge.AttachAfterLabel(lbl, hint);
            return dd;
        }

        public (TMP_Text label, TMP_Dropdown dropdown) NamedDropdown(string nodeName, string label,
            List<string> options, Action<int> onChanged, RowVisibility visibility,
            string? labelNodeName = null)
        {
            var lbl = UIFactory.CreateLabel(labelNodeName ?? ("L_" + label), _parent, label, 15,
                new Vector2(DropdownLabelX, 0), new Vector2(DropdownLabelW, LabelH));
            var dd = UIFactory.CreateDropdown(nodeName, _parent, options,
                new Vector2(DropdownX, 0), new Vector2(DropdownW, DropdownH), onChanged);
            _registered.Add(lbl, dd);
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
            RowVisibility visibility, float gapAfter, string? hint = null)
        {
            float lane = hint == null ? 0f : HintBadge.LaneWidth;
            var toggle = UIFactory.CreateToggle(nodeName, _parent, caption, value,
                Vector2.zero, new Vector2(RowWidth, ToggleH), onChanged, lane);
            visibility.Register(_layout, ToggleH, gapAfter, toggle.GetComponent<RectTransform>());
            if (hint != null)
                HintBadge.Attach(toggle.transform,
                    new Vector2(RowWidth * 0.5f - UIStyle.HintBadgeSize * 0.5f, 0f), hint);
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
