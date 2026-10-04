using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    internal sealed class InspectorRows
    {
        public const string PairRowPrefix = "Row_Pair_";

        private readonly FormRows _forms;
        private readonly RectTransform _host;
        private readonly Func<ElementFacet> _facets;
        private readonly UIRowRegistry _own = new();
        private readonly List<InspectorSection> _sections = new();
        private readonly List<ComputedNumberRow> _computed = new();
        private InspectorSection? _open;
        private int _firstMember;

        public InspectorRows(RectTransform host, Func<ElementFacet> facets)
        {
            _host = host;
            _facets = facets;
            _forms = new FormRows(host, RowDensity.Compact);
        }

        public FormRows Forms => _forms;

        public RowMetrics Metrics => _forms.Metrics;

        public Transform Parent => _host;

        public IReadOnlyList<InspectorSection> Sections => _sections;

        public IEnumerable<(TMP_Text? label, Selectable? control)> LabelledRows
        {
            get
            {
                foreach (var row in _forms.LabelledRows) yield return row;
                foreach (var row in _own.Rows) yield return row;
            }
        }

        public void SyncEnabledState()
        {
            _forms.SyncEnabledState();
            _own.Sync();
        }

        public float Relayout() => _forms.Relayout();

        public void SyncComputed()
        {
            foreach (var row in _computed) row.Sync();
        }

        public InspectorSection BeginSection(string id, string title, bool expandedByDefault,
            string? actionCaption = null, Action? onAction = null)
        {
            EndSection();
            _firstMember = _forms.Rows.Count + 1;
            var view = _forms.Section(id, title, null, actionCaption, onAction);
            _open = new InspectorSection(id, view, Last, expandedByDefault);
            _open.Header.VisibleWhen = _open.HasVisibleMember;
            _sections.Add(_open);
            return _open;
        }

        public void EndSection()
        {
            if (_open == null) return;
            var members = new List<FormRow>();
            for (int i = _firstMember; i < _forms.Rows.Count; i++) members.Add(_forms.Rows[i]);
            _open.Adopt(members);
            _forms.EndSection();
            _open = null;
        }

        public TMP_InputField NumberField(string label, RowVisibility visibility, string? unit = null,
            string? nodeSuffix = null, string? hint = null, bool allowDecimal = false) =>
            LabelledNumberField(label, visibility, unit, nodeSuffix, hint, allowDecimal).field;

        public (TextMeshProUGUI label, TMP_InputField field) LabelledNumberField(string label,
            RowVisibility visibility, string? unit = null, string? nodeSuffix = null, string? hint = null,
            bool allowDecimal = false)
        {
            var made = _forms.Number(nodeSuffix ?? label, label, unit ?? Loc.T("unit.mm"), 0, hint, allowDecimal);
            Show(Last, visibility);
            return made;
        }

        public ComputedNumberRow NumberOrComputed(string label, RowVisibility visibility,
            string? unit = null, string? nodeSuffix = null, string? hint = null)
        {
            string shownUnit = unit ?? Loc.T("unit.mm");
            string node = nodeSuffix ?? label;
            var (caption, field) = _forms.Number(node, label, shownUnit, 0, hint);
            var editable = Last;
            var (computedCaption, value) = _forms.ReadOnly("Computed_" + node, label, "", 0, hint);
            var computed = Last;

            var row = new ComputedNumberRow(caption, field, computedCaption, value, shownUnit);
            var facets = _facets;
            editable.VisibleWhen = () => visibility.IsVisibleFor(facets()) && !row.Locked;
            computed.VisibleWhen = () => visibility.IsVisibleFor(facets()) && row.Locked;
            _computed.Add(row);
            return row;
        }

        public TMP_InputField NameField()
        {
            var (_, field) = _forms.Text("Название", Loc.T("element.common.name"));
            return field;
        }

        public TMP_Text ReadOnlyField(string label, RowVisibility visibility, string? hint = null,
            string? nodeSuffix = null)
        {
            var (_, value) = _forms.ReadOnly(nodeSuffix ?? label, label, "", 0, hint);
            Show(Last, visibility);
            return value;
        }

        public TMP_Dropdown Dropdown(string label, List<string> options, Action<int> onChanged,
            RowVisibility visibility, string? nodeName = null, string? hint = null)
        {
            var (caption, dropdown) = _forms.Dropdown(nodeName ?? label, label, options, 0, onChanged, 0, hint);
            dropdown.gameObject.name = nodeName ?? "Dd_" + label;
            caption.gameObject.name = "L_" + label;
            Show(Last, visibility);
            return dropdown;
        }

        public (TMP_Text label, TMP_Dropdown dropdown) NamedDropdown(string nodeName, string label,
            List<string> options, Action<int> onChanged, RowVisibility visibility,
            string? labelNodeName = null)
        {
            var (caption, dropdown) = _forms.Dropdown(nodeName, label, options, 0, onChanged);
            dropdown.gameObject.name = nodeName;
            caption.gameObject.name = labelNodeName ?? "L_" + label;
            Show(Last, visibility);
            return (caption, dropdown);
        }

        public Toggle Switch(string nodeName, string caption, bool value, Action<bool> onChanged,
            RowVisibility visibility, string? hint = null)
        {
            var (_, toggle) = _forms.Switch(nodeName, caption, value, onChanged, 0, hint);
            toggle.gameObject.name = nodeName;
            Show(Last, visibility);
            return toggle;
        }

        public Button ValueButton(string nodeName, string caption, Action onClick, RowVisibility visibility)
        {
            var rect = UIFactory.CreateRect("Row_" + nodeName, _host);
            rect.sizeDelta = new Vector2(Metrics.Width, Metrics.ControlH);
            var button = UIFactory.CreateButton(nodeName, rect, caption, Vector2.zero,
                new Vector2(Metrics.ValueW, Metrics.ControlH), onClick);
            PlaceCell(button.GetComponent<RectTransform>(), Metrics.ValueX, Metrics.ValueW);
            Show(_forms.Custom(rect, Metrics.ControlH), visibility);
            return button;
        }

        public (TMP_InputField first, TMP_InputField second) PairField(string label, string firstNode,
            string secondNode, string initial, RowVisibility visibility)
        {
            float captionH = FormRows.CaptionH;
            float height = captionH + UIStyle.Space1 + Metrics.ControlH;
            var rect = UIFactory.CreateRect(PairRowPrefix + firstNode + "_" + secondNode, _host);
            rect.sizeDelta = new Vector2(Metrics.Width, height);

            var caption = UIFactory.CreateLabel("L_" + firstNode + "_" + secondNode, rect, label, UIStyle.FontSmall,
                Vector2.zero, new Vector2(Metrics.Width, captionH), TextAnchor.MiddleLeft);
            caption.color = UIStyle.TextSecondary;
            caption.enableWordWrapping = false;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            PlaceFromTop(caption.rectTransform, 0f, Metrics.Width, 0f);

            float fieldW = (Metrics.Width - UIStyle.Space1) * 0.5f;
            float fieldsTop = captionH + UIStyle.Space1;
            var first = PairInput(rect, firstNode, initial, 0f, fieldW, fieldsTop);
            var second = PairInput(rect, secondNode, initial, fieldW + UIStyle.Space1, fieldW, fieldsTop);
            _own.Add(caption, first);
            _own.Add(caption, second);
            Show(_forms.Custom(rect, height), visibility);
            return (first, second);
        }

        public TMP_Text Note(string nodeName, string text, RowVisibility visibility, bool centered = false)
        {
            var label = _forms.Note(nodeName, text);
            if (centered) label.alignment = TextAlignmentOptions.Top;
            Show(Last, visibility);
            return label;
        }

        public VectorField Vector(string node, string caption, IReadOnlyList<string>? rotateTooltips,
            Action<int>? onRotate90, RowVisibility visibility)
        {
            var vector = _forms.Vector(node, caption, rotateTooltips, onRotate90);
            Show(_forms.Rows[_forms.Rows.Count - 2], visibility);
            Show(Last, visibility);
            return vector;
        }

        public RectTransform NewRowRect(string name)
        {
            var rect = UIFactory.CreateRect("Row_" + name, _host);
            rect.sizeDelta = new Vector2(Metrics.Width, Metrics.ControlH);
            return rect;
        }

        public FormRow Custom(RectTransform content, float height, RowVisibility visibility)
        {
            var row = _forms.Custom(content, height);
            Show(row, visibility);
            return row;
        }

        private FormRow Last => _forms.Rows[_forms.Rows.Count - 1];

        private void Show(FormRow row, RowVisibility visibility)
        {
            if (visibility.IsAlways) return;
            var facets = _facets;
            row.VisibleWhen = () => visibility.IsVisibleFor(facets());
        }

        private TMP_InputField PairInput(RectTransform row, string node, string initial, float x, float width,
            float top)
        {
            var field = UIFactory.CreateInputField("F_" + node, row, initial, Vector2.zero,
                new Vector2(width, Metrics.ControlH));
            field.textComponent!.alignment = TextAlignmentOptions.Right;
            field.contentType = TMP_InputField.ContentType.Custom;
            field.onValidateInput = DimensionFieldValidation.Char();
            PlaceFromTop((RectTransform)field.transform, x, width, top);
            return field;
        }

        private void PlaceFromTop(RectTransform rt, float x, float width, float top)
        {
            float left = LayoutDirection.IsRtl ? Metrics.Width - x - width : x;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
        }

        public void PlaceCell(RectTransform rt, float x, float width)
        {
            float left = LayoutDirection.IsRtl ? Metrics.Width - x - width : x;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(left, 0f);
        }
    }
}
