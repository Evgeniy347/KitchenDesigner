using System;
using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public sealed class IssueFilterBar
    {
        public const string ChipAllNode = "FltChipAll";
        public const string ChipErrorsNode = "FltChipErrors";
        public const string ChipWarningsNode = "FltChipWarnings";
        public const string CodeNode = "FltCode";
        public const string FloorNode = "FltFloor";
        public const string SearchNode = "FltSearch";
        public const string SearchHintNode = "FltSearchHint";

        private const float CodeW = 150f;
        private const float FloorW = 130f;

        private readonly RectTransform _parent;
        private readonly float _left;
        private readonly float _top;
        private readonly float _width;
        private readonly Action _changed;
        private readonly FilterChip _all;
        private readonly FilterChip _errors;
        private readonly FilterChip _warnings;
        private readonly MultiSelectDropdown _code;
        private readonly MultiSelectDropdown _floor;
        private readonly TMP_InputField _search;
        private readonly TMP_Text _searchHint;

        public IssueFilterBar(RectTransform parent, float left, float top, float width, Action changed)
        {
            _parent = parent;
            _left = left;
            _top = top;
            _width = width;
            _changed = changed;

            _all = Chip(ChipAllNode, Loc.T("errors.filter.all"), null, IssueLevelFilter.All);
            _errors = Chip(ChipErrorsNode, Loc.T("errors.chip.errors"), IssueDisplay.LevelGlyph(IssueLevel.Error),
                IssueLevelFilter.Errors, IssueDisplay.LevelColor(IssueLevel.Error));
            _warnings = Chip(ChipWarningsNode, Loc.T("errors.chip.warnings"),
                IssueDisplay.LevelGlyph(IssueLevel.Warning), IssueLevelFilter.Warnings,
                IssueDisplay.LevelColor(IssueLevel.Warning));

            var field = new Vector2(CodeW, UIStyle.ControlHCompact);
            _code = MultiSelectDropdown.Create(CodeNode, parent, Loc.T("errors.filter.allCodes"), Vector2.zero,
                field, OnChanged);
            _floor = MultiSelectDropdown.Create(FloorNode, parent, Loc.T("errors.filter.allFloors"), Vector2.zero,
                new Vector2(FloorW, UIStyle.ControlHCompact), OnChanged);

            _search = UIFactory.CreateInputField(SearchNode, parent, "", Vector2.zero,
                new Vector2(UIStyle.ControlHCompact, UIStyle.ControlHCompact));
            UIFactory.AnchorTopLeft((RectTransform)_search.transform);
            _search.onValueChanged.AddListener(_ => OnChanged());
            _searchHint = SearchHint();

            SetSelected(IssueLevelFilter.All);
            Relayout();
        }

        public IssueLevelFilter Level { get; private set; }

        public TMP_InputField Search => _search;

        public string SearchText => _search.text.Trim();

        public MultiSelectDropdown Code => _code;

        public MultiSelectDropdown Floor => _floor;

        public FilterChip ChipFor(IssueLevelFilter level) =>
            level == IssueLevelFilter.Errors ? _errors : level == IssueLevelFilter.Warnings ? _warnings : _all;

        public void SetCounts(int all, int errors, int warnings)
        {
            _all.SetCount(all);
            _errors.SetCount(errors);
            _warnings.SetCount(warnings);
            Relayout();
        }

        public void SetCodes(IEnumerable<string> codes) => _code.SetOptions(codes);

        public void SetFloors(IEnumerable<string> ids, Func<string, string> nameOf) =>
            _floor.SetOptionsWithLabels(ids, nameOf);

        public void SyncSearchHint() => _searchHint.gameObject.SetActive(SearchText.Length == 0);

        public bool Passes(AnalysisIssue issue, string? floorId)
        {
            if (!LevelAllows(issue.Level)) return false;
            if (!_code.IsAllowed(issue.Code)) return false;
            if (floorId != null && !_floor.IsAllowed(floorId)) return false;

            string search = SearchText;
            if (search.Length == 0) return true;
            return Contains(issue.Code, search) || Contains(issue.Detail, search) || Contains(issue.Message, search);
        }

        private bool LevelAllows(IssueLevel level) => Level switch
        {
            IssueLevelFilter.Errors => level == IssueLevel.Error,
            IssueLevelFilter.Warnings => level == IssueLevel.Warning,
            _ => true,
        };

        private static bool Contains(string? haystack, string needle) =>
            haystack != null && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private FilterChip Chip(string node, string caption, string? glyph, IssueLevelFilter level,
            Color glyphColor = default)
        {
            var chip = FilterChip.Create(_parent, node, caption, glyph, glyphColor, () => Choose(level));
            UIFactory.AnchorTopLeft(chip.Rect);
            return chip;
        }

        private void Choose(IssueLevelFilter level)
        {
            SetSelected(level);
            OnChanged();
        }

        private void SetSelected(IssueLevelFilter level)
        {
            Level = level;
            _all.SetSelected(level == IssueLevelFilter.All);
            _errors.SetSelected(level == IssueLevelFilter.Errors);
            _warnings.SetSelected(level == IssueLevelFilter.Warnings);
            Relayout();
        }

        private void OnChanged()
        {
            SyncSearchHint();
            _changed();
        }

        private void Relayout()
        {
            float x = _left;
            foreach (var chip in new[] { _all, _errors, _warnings })
            {
                chip.Rect.anchoredPosition = new Vector2(x, _top);
                x += chip.Width + UIStyle.Space2;
            }

            x += UIStyle.Space2;
            Place((RectTransform)_code.transform, x, CodeW);
            x += CodeW + UIStyle.Space2;
            Place((RectTransform)_floor.transform, x, FloorW);
            x += FloorW + UIStyle.Space2;

            float searchW = Mathf.Max(UIStyle.ControlHCompact, _left + _width - x);
            Place((RectTransform)_search.transform, x, searchW);
        }

        private void Place(RectTransform rt, float x, float width)
        {
            UIFactory.AnchorTopLeft(rt);
            rt.sizeDelta = new Vector2(width, UIStyle.ControlHCompact);
            rt.anchoredPosition = new Vector2(x, _top);
        }

        private TMP_Text SearchHint()
        {
            var hint = UIFactory.CreateLabel(SearchHintNode, _search.transform, Loc.T("errors.filter.searchHint"),
                UIStyle.FontSmall, Vector2.zero, new Vector2(UIStyle.ControlH, UIStyle.ControlHCompact),
                TextAnchor.MiddleLeft);
            hint.color = UIStyle.TextSecondary;
            hint.raycastTarget = false;
            hint.enableWordWrapping = false;
            hint.overflowMode = TextOverflowModes.Ellipsis;
            var rt = hint.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(UIStyle.Space2, 0f);
            rt.offsetMax = new Vector2(-UIStyle.Space2, 0f);
            return hint;
        }
    }
}
