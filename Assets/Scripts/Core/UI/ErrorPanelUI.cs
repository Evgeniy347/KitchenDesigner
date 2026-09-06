using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class ErrorPanelUI : MonoBehaviour, IProjectWindow
    {
        private const float PanelW = 940f;
        private const float PanelH = 640f;
        private const float Pad = 16f;
        private const float FilterLabelY = -44f;
        private const float FilterFieldY = -64f;
        private const float FilterFieldH = 28f;
        private const float SearchX = Pad + 352f;

        private readonly IssueTableView _table = new();
        private readonly IssueTableSelection _selection = new();
        private readonly List<AnalysisIssue> _allIssues = new();

        private GameObject? _root;
        private MultiSelectDropdown? _levelFilter;
        private MultiSelectDropdown? _codeFilter;
        private TMP_InputField? _searchField;
        private TMP_Text? _searchHint;
        private TMP_Text? _countLabel;
        private int _lastSceneVersion;

        public int TotalIssueCount => _allIssues.Count;
        public int VisibleIssueCount => _table.RowCount;
        public int SelectedCount => _selection.SelectedCount;
        public bool IsSelected(AnalysisIssue iss) => _selection.IsSelected(iss);
        public int SelectedVisibleIdx => _selection.FocusVisibleIdx;
        public TMP_InputField SearchField => _searchField!;

        public void Build(Transform layer)
        {
            var panel = UIFactory.CreatePanel("ErrorPanel", layer, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorCenter(panel.rectTransform);
            panel.rectTransform.anchoredPosition = Vector2.zero;
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, UIStyle.DragStripHeight);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("ErrTitle", panel.transform, "Ошибки", UIStyle.FontTitle,
                new Vector2(Pad, -10), new Vector2(200, 28), TextAnchor.MiddleLeft)
                .rectTransform.SetAnchor(new Vector2(0, 1), new Vector2(Pad, -10));

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            BuildFilters(panel.transform);
            _table.Build(panel.transform, PanelW, Pad);

            _countLabel = UIFactory.CreateLabel("ErrCount", panel.transform, "", UIStyle.FontSmall,
                Vector2.zero, new Vector2(PanelW - Pad * 2f, 22), TextAnchor.MiddleLeft);
            _countLabel.color = UIStyle.TextSecondary;
            _countLabel.rectTransform.SetAnchor(new Vector2(0, 1), new Vector2(Pad, -(PanelH - 28f)));

            _root.SetActive(false);
        }

        public string WindowId => "errors";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => false;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (visible) Analyze();
            _root.SetActive(visible);
        }

        public void HandleRowClick(AnalysisIssue iss, int index, bool ctrl, bool shift)
        {
            if (_selection.Click(iss, index, ctrl, shift))
                IssueDisplay.SelectInScene(iss);
            RepaintAndScroll();
        }

        public void SelectAll()
        {
            _selection.SelectAll();
            _table.RepaintHighlights(_selection.IsSelected);
        }

        public void CopySelectedToClipboard()
        {
            var text = _selection.SelectedAsClipboardText();
            if (text.Length > 0) GUIUtility.systemCopyBuffer = text;
        }

        private void OnDestroy() => ProjectWindows.Unregister(this);

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            if (_lastSceneVersion != SceneRevision.Version) Analyze();
            HandleKeyboardNavigation();
        }

        private void BuildFilters(Transform parent)
        {
            Caption(parent, "FltLevelLbl", "Уровень", new Vector2(Pad, FilterLabelY));
            _levelFilter = MultiSelectDropdown.Create("FltLevel", parent, "Все",
                new Vector2(Pad, FilterFieldY), new Vector2(160, FilterFieldH), RebuildRows);

            Caption(parent, "FltCodeLbl", "Код ошибки", new Vector2(Pad + 176, FilterLabelY));
            _codeFilter = MultiSelectDropdown.Create("FltCode", parent, "Все",
                new Vector2(Pad + 176, FilterFieldY), new Vector2(160, FilterFieldH), RebuildRows);

            Caption(parent, "FltSearchLbl", "Поиск по тексту", new Vector2(SearchX, FilterLabelY));
            _searchField = UIFactory.CreateInputField("FltSearch", parent, "",
                new Vector2(SearchX, FilterFieldY), new Vector2(300, FilterFieldH));
            UIFactory.AnchorTopLeft(_searchField.GetComponent<RectTransform>());
            _searchField.GetComponent<RectTransform>().anchoredPosition = new Vector2(SearchX, FilterFieldY);
            _searchField.onValueChanged.AddListener(_ => RebuildRows());

            var hint = UIFactory.CreateLabel("FltSearchHint", _searchField.transform, "Код, деталь или текст…",
                UIStyle.FontSmall, Vector2.zero, new Vector2(280, FilterFieldH), TextAnchor.MiddleLeft);
            hint.color = UIStyle.TextSecondary;
            hint.raycastTarget = false;
            var hRt = hint.rectTransform;
            hRt.anchorMin = Vector2.zero; hRt.anchorMax = Vector2.one;
            hRt.offsetMin = new Vector2(8, 0); hRt.offsetMax = Vector2.zero;
            _searchHint = hint;

            var refresh = UIFactory.CreateButton("ErrRefresh", parent, "Обновить",
                new Vector2(PanelW - Pad - 120f, FilterFieldY), new Vector2(120, FilterFieldH), Analyze);
            UIFactory.AnchorTopLeft(refresh.GetComponent<RectTransform>());
            refresh.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(PanelW - Pad - 120f, FilterFieldY);
        }

        private static void Caption(Transform parent, string name, string text, Vector2 pos)
        {
            var lbl = UIFactory.CreateLabel(name, parent, text, UIStyle.FontSection,
                Vector2.zero, new Vector2(170, 18), TextAnchor.MiddleLeft);
            lbl.color = UIStyle.TextSecondary;
            lbl.rectTransform.SetAnchor(new Vector2(0, 1), pos);
        }

        private void Analyze()
        {
            _allIssues.Clear();
            _allIssues.AddRange(SceneAnalyzer.Analyze());
            _lastSceneVersion = SceneRevision.Version;

            _selection.Clear();

            _levelFilter?.SetOptions(LevelsPresentIn(_allIssues));
            _codeFilter?.SetOptions(CodesPresentIn(_allIssues));

            RebuildRows();
        }

        private static List<string> LevelsPresentIn(List<AnalysisIssue> issues)
        {
            var levels = new List<string>();
            foreach (IssueLevel lv in System.Enum.GetValues(typeof(IssueLevel)))
            {
                string name = IssueDisplay.LevelName(lv);
                foreach (var iss in issues)
                    if (iss.Level == lv) { levels.Add(name); break; }
            }
            return levels;
        }

        private static List<string> CodesPresentIn(List<AnalysisIssue> issues)
        {
            var codes = new List<string>();
            foreach (var iss in issues)
                if (!codes.Contains(iss.Code)) codes.Add(iss.Code);
            codes.Sort(System.StringComparer.Ordinal);
            return codes;
        }

        private void RebuildRows()
        {
            _table.ClearRows(Destroy);
            _selection.BeginRebuild();

            string search = _searchField != null ? _searchField.text.Trim() : "";
            if (_searchHint != null) _searchHint.gameObject.SetActive(search.Length == 0);

            float y = 0f;
            int shown = 0;
            foreach (var iss in _allIssues)
            {
                if (!PassesFilters(iss, search)) continue;
                _selection.AddVisible(iss);
                var captured = iss;
                int index = shown;
                _table.AddRow(captured, y, index,
                    () => HandleRowClick(captured, index, AnyCtrl(), AnyShift()),
                    () => IssueDisplay.RevealIssue(captured),
                    Destroy);
                y -= IssueTableView.RowStep;
                shown++;
            }

            _table.SetContentHeight(-y + 4f);
            _selection.EndRebuild();
            _table.RepaintHighlights(_selection.IsSelected);
            UpdateCountLabel(shown);
        }

        private void UpdateCountLabel(int shown)
        {
            if (_countLabel == null) return;
            if (_allIssues.Count == 0)
                _countLabel.text = "Ошибок не найдено";
            else if (shown == _allIssues.Count)
                _countLabel.text = $"Всего: {_allIssues.Count}";
            else
                _countLabel.text = $"Показано: {shown} из {_allIssues.Count}";
        }

        private bool PassesFilters(AnalysisIssue iss, string search)
        {
            if (_levelFilter != null && !_levelFilter.IsAllowed(IssueDisplay.LevelName(iss.Level))) return false;
            if (_codeFilter != null && !_codeFilter.IsAllowed(iss.Code)) return false;
            if (search.Length == 0) return true;

            return Contains(iss.Code, search) || Contains(iss.Detail, search) || Contains(iss.Message, search);
        }

        private static bool Contains(string haystack, string needle) =>
            haystack != null && haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private void HandleKeyboardNavigation()
        {
            if (CameraController.IsTypingInInputField()) return;

            if (Input.GetKeyDown(KeyCode.C) && AnyCtrl())
            {
                CopySelectedToClipboard();
                return;
            }

            if (Input.GetKeyDown(KeyCode.A) && AnyCtrl())
            {
                SelectAll();
                return;
            }

            if (AnyCtrl() || AnyShift()) return;
            int count = _selection.VisibleCount;
            if (count == 0) return;

            int cur = _selection.FocusVisibleIdx;
            int next = cur;
            int step = _table.RowsPerPage();

            if      (Input.GetKeyDown(KeyCode.UpArrow))   next = cur - 1;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) next = cur + 1;
            else if (Input.GetKeyDown(KeyCode.PageUp))    next = cur - step;
            else if (Input.GetKeyDown(KeyCode.PageDown))  next = cur + step;
            else if (Input.GetKeyDown(KeyCode.Home))      next = 0;
            else if (Input.GetKeyDown(KeyCode.End))       next = count - 1;

            if (next == cur) return;
            if (_selection.MoveFocusTo(Mathf.Clamp(next, 0, count - 1)))
                RepaintAndScroll();
        }

        private void RepaintAndScroll()
        {
            _table.RepaintHighlights(_selection.IsSelected);
            _table.ScrollRowIntoView(_selection.FocusVisibleIdx, _selection.VisibleCount);
        }

        private static bool AnyCtrl() =>
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static bool AnyShift() =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
