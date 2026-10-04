using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public sealed class SettingsPage
    {
        public const string RootPrefix = "Page_";
        public const string TitleNode = "PageTitle";
        public const string HintNode = "PageHint";
        public const string SectionPrefix = "Sec_";

        private sealed class Entry
        {
            public Entry(FormRow? row, string text)
            {
                Row = row;
                Text = text;
            }

            public FormRow? Row { get; }

            public string Text { get; set; }
        }

        private sealed class SectionGroup
        {
            public SectionGroup(string title) => Title = title;

            public string Title { get; }

            public List<Entry> Entries { get; } = new();
        }

        private readonly SettingsForm _form;
        private readonly Dictionary<string, Entry> _byKey = new();
        private readonly List<Entry> _entries = new();
        private readonly Dictionary<TMP_InputField, string> _cleanValues = new();
        private SectionGroup? _section;
        private RectTransform? _tail;
        private Entry? _tailEntry;
        private string _query = "";

        public SettingsPage(SettingsForm form, Transform content, string id, string title, string subtitle)
        {
            _form = form;
            Id = id;
            Title = title;
            Root = UIFactory.CreateRect(RootPrefix + id, content);
            Anchor(Root);
            Root.sizeDelta = new Vector2(ContentW, 0f);
            Rows = new FormRows(Root, RowDensity.Regular);
            BuildHeader(title, subtitle);
        }

        public static float ContentW =>
            UIStyle.SettingsSize.x - UIStyle.NavW - 2f * UIStyle.DialogPad - WindowBody.BarW;

        public string Id { get; }

        public string Title { get; }

        public RectTransform Root { get; }

        public FormRows Rows { get; }

        public SettingsForm Form => _form;

        public Action? OnReset { get; set; }

        public Func<bool>? CanReset { get; set; }

        public int MatchCount
        {
            get
            {
                int count = 0;
                foreach (var entry in _entries)
                    if (Matches(entry)) count++;
                return count;
            }
        }

        public bool Matches(string text) =>
            _query.Length == 0 || text.ToLowerInvariant().Contains(_query);

        public void SetQuery(string query)
        {
            _query = query.Trim().ToLowerInvariant();
            Relayout();
        }

        public float Relayout()
        {
            float height = Rows.Relayout();
            if (_tail != null && _tailEntry != null)
            {
                bool shown = Matches(_tailEntry);
                _tail.gameObject.SetActive(shown);
                if (shown)
                {
                    float y = height + UIStyle.SectionGapAfter;
                    _tail.anchoredPosition = new Vector2(0f, -y);
                    height = y + _tail.sizeDelta.y;
                }
            }
            Root.sizeDelta = new Vector2(ContentW, height);
            return height;
        }

        public RectTransform Section(string title)
        {
            var rect = UIFactory.CreateSectionHeader(SectionPrefix + title, Root, title, ContentW);
            var header = Rows.Custom(rect, UIStyle.SectionHeaderH);
            rect.sizeDelta = new Vector2(ContentW, UIStyle.SectionHeaderH);
            header.IsSectionHeader = true;
            header.GapAfter = UIStyle.SectionGapAfter;

            var section = new SectionGroup(title);
            header.VisibleWhen = () => section.Entries.Exists(Matches);
            _section = section;
            return rect;
        }

        public Button SectionAction(RectTransform header, string caption, Action onClick)
        {
            var button = UIFactory.CreateButton(header.name + "_Action", header, caption, Vector2.zero,
                new Vector2(WindowFooter.MinButtonW, UIStyle.SectionHeaderH), onClick);
            ButtonRoles.Paint(button, ButtonRole.Link);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = UIStyle.FontSmall;
            float width = Mathf.Ceil(label.GetPreferredValues(caption).x) + 2f * UIStyle.Space2;

            bool rtl = LayoutDirection.IsRtl;
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(rtl ? 0f : 1f, 0.5f);
            rect.sizeDelta = new Vector2(width, UIStyle.SectionHeaderH);
            rect.anchoredPosition = Vector2.zero;

            var line = header.Find(header.name + "_Line") as RectTransform;
            if (line == null) return button;
            float cut = width + UIStyle.Space2;
            if (rtl) line.offsetMin = new Vector2(cut, line.offsetMin.y);
            else line.offsetMax = new Vector2(-cut, line.offsetMax.y);
            return button;
        }

        public Toggle AddSwitch(string label, bool value, Action<bool> onChanged, string? id = null,
            int indent = 0, Func<bool>? read = null)
        {
            string key = id ?? label;
            var (lbl, toggle) = Rows.Switch(key, label, value, onChanged, indent);
            Track(key, label, lbl);
            if (read != null)
                _form.OnReadBack(() =>
                {
                    if (toggle != null) SwitchControl.SetWithoutNotify(toggle, read());
                });
            return toggle;
        }

        public TMP_Dropdown AddDropdown(string label, List<string> options, int value, Action<int> onChanged,
            string? id = null, Func<int>? read = null, IReadOnlyList<string?>? optionLanguages = null,
            int indent = 0)
        {
            string key = id ?? label;
            var (lbl, dropdown) = Rows.Dropdown(key, label, options, value, onChanged, indent);
            if (optionLanguages != null) UIFactory.SetDropdownOptionLanguages(dropdown, optionLanguages);
            Track(key, label, lbl);
            if (read != null)
                _form.OnReadBack(() =>
                {
                    if (dropdown == null) return;
                    dropdown.SetValueWithoutNotify(
                        Mathf.Clamp(read(), 0, Mathf.Max(0, dropdown.options.Count - 1)));
                    dropdown.RefreshShownValue();
                });
            return dropdown;
        }

        public SegmentedControl AddSegmented(string label, IReadOnlyList<string> options, int value,
            Action<int> onChanged, string? id = null, Func<int>? read = null, float width = 0f)
        {
            string key = id ?? label;
            var (lbl, segmented) = Rows.Segmented(key, label, options, value, onChanged);
            if (width > Rows.Metrics.ValueW) Widen(segmented, width);
            Track(key, label, lbl);
            if (read != null)
                _form.OnReadBack(() =>
                {
                    if (segmented != null) segmented.SetValueWithoutNotify(read());
                });
            return segmented;
        }

        public TMP_InputField AddNumber(string label, string initial, bool allowDecimal,
            Action<TMP_InputField> onEndEdit, string unit, Func<string> read, string? id = null,
            int indent = 0)
        {
            string key = id ?? label;
            var (lbl, field) = Rows.Number(key, label, unit, indent, null, allowDecimal);
            field.SetTextWithoutNotify(initial);
            _cleanValues[field] = initial;
            field.onValueChanged.AddListener(_ => UpdateFieldHighlight(field));
            field.onEndEdit.AddListener(_ =>
            {
                onEndEdit(field);
                UpdateFieldHighlight(field);
            });
            Track(key, label, lbl);
            _form.OnReadBack(() =>
            {
                if (field == null) return;
                string text = read();
                field.SetTextWithoutNotify(text);
                _cleanValues[field] = text;
                UpdateFieldHighlight(field);
            });
            return field;
        }

        public Slider AddSlider(string label, float min, float max, float value, Func<float, string> format,
            Action<float> onChanged, bool wholeNumbers, Func<float>? read = null, string? id = null)
        {
            string key = id ?? label;
            var (lbl, slider, readout) = Rows.Slider(key, label, min, max, value, format, onChanged,
                wholeNumbers);
            Track(key, label, lbl);
            var undo = SettingsSliderUndo.Attach(slider, label, onChanged, _form.ReadBackFromSettings);
            if (read != null)
                _form.OnReadBack(() =>
                {
                    if (slider == null) return;
                    float v = read();
                    slider.SetValueWithoutNotify(v);
                    undo.Sync();
                    if (readout != null) readout.text = format(v);
                });
            return slider;
        }

        public Slider AddIntSlider(string label, int min, int max, int value, Func<int, string> format,
            Action<int> onChanged, Func<int>? read = null, string? id = null) =>
            AddSlider(label, min, max, value, v => format(Mathf.RoundToInt(v)),
                v => onChanged(Mathf.RoundToInt(v)), true,
                read == null ? null : () => read(), id);

        public TextMeshProUGUI AddReadOnly(string label, string value, Func<string>? read = null, string? id = null)
        {
            string key = id ?? label;
            var (lbl, text) = Rows.ReadOnly(key, label, value);
            Track(key, label, lbl);
            if (read != null)
                _form.OnReadBack(() =>
                {
                    if (text != null) text.text = read();
                });
            return text;
        }

        public TextMeshProUGUI Note(string node, string text)
        {
            var note = Rows.Note(node, text);
            AddEntry(Rows.Rows[Rows.Rows.Count - 1], text);
            return note;
        }

        public FormRow Block(RectTransform rect, float height, string searchText)
        {
            var row = Rows.Custom(rect, height);
            AddEntry(row, searchText);
            return row;
        }

        public void SetTail(RectTransform tail, string searchText)
        {
            _tail = tail;
            tail.SetParent(Root, false);
            Anchor(tail);
            _tailEntry = AddEntry(null, searchText);
        }

        public HintBadge? Hint(string key, string hint)
        {
            if (_byKey.TryGetValue(key, out var entry))
                entry.Text += " " + HintText.Of(hint).ToLowerInvariant();
            return HintBadge.AttachAfterLabel(_form.RowLabel(key), hint);
        }

        public FormRow? RowOf(string key) => _byKey.TryGetValue(key, out var entry) ? entry.Row : null;

        public float SegmentedWidthFor(IReadOnlyList<string> options)
        {
            var probe = UIFactory.CreateLabel("SegmentProbe", Root, "", UIStyle.FontBody, Vector2.zero, Vector2.zero);
            float widest = 0f;
            foreach (var option in options) widest = Mathf.Max(widest, probe.GetPreferredValues(option).x);
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            float pad = UIStyle.SegmentPad;
            float segment = Mathf.Ceil(widest) + 2f * UIStyle.Space3;
            return Mathf.Max(Rows.Metrics.ValueW, options.Count * segment + (options.Count + 1) * pad);
        }

        private static void Widen(SegmentedControl segmented, float width)
        {
            var root = (RectTransform)segmented.transform;
            root.sizeDelta = new Vector2(width, root.sizeDelta.y);
            float pad = UIStyle.SegmentPad;
            int count = segmented.Segments.Count;
            float segmentW = (width - pad * 2f - pad * (count - 1)) / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                var rect = (RectTransform)segmented.Segments[i].transform;
                rect.sizeDelta = new Vector2(segmentW, rect.sizeDelta.y);
                rect.anchoredPosition = new Vector2(pad + i * (segmentW + pad), 0f);
            }
        }

        private void BuildHeader(string title, string subtitle)
        {
            var label = UIFactory.CreateLabel(TitleNode, Root, title, UIStyle.FontWindowTitle, Vector2.zero,
                new Vector2(ContentW, 0f), TextAnchor.MiddleLeft);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            float height = Mathf.Ceil(label.GetPreferredValues(title).y);
            Rows.Custom(label.rectTransform, height);
            label.rectTransform.sizeDelta = new Vector2(ContentW, height);
            Rows.Gap(UIStyle.Space1);
            Rows.Note(HintNode, subtitle);
        }

        private void Track(string key, string label, TextMeshProUGUI lbl)
        {
            var entry = AddEntry(Rows.Rows[Rows.Rows.Count - 1], label);
            _byKey[key] = entry;
            _form.Register(key, lbl);
        }

        private Entry AddEntry(FormRow? row, string text)
        {
            var entry = new Entry(row, (text + " " + (_section?.Title ?? "")).ToLowerInvariant());
            if (row != null) row.VisibleWhen = () => Matches(entry);
            _entries.Add(entry);
            _section?.Entries.Add(entry);
            return entry;
        }

        private bool Matches(Entry entry) => _query.Length == 0 || entry.Text.Contains(_query);

        private void UpdateFieldHighlight(TMP_InputField field)
        {
            if (field == null) return;
            string clean = _cleanValues.TryGetValue(field, out var v) ? v : field.text;
            UIFactory.SetHighlight(field, field.text != clean);
        }

        private static void Anchor(RectTransform rect)
        {
            float x = LayoutDirection.IsRtl ? 1f : 0f;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(x, 1f);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
