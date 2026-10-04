using System.Collections.Generic;
using KitchenDesigner.Core.Analysis;
using KitchenDesigner.Core.Keybinding;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class ErrorPanelUI : MonoBehaviour, IProjectWindow
    {
        public static ErrorPanelUI? Instance { get; private set; }

        public const string TableNode = "ErrTable";
        public const string FirstErrorNode = "ErrFirst";
        public const string HelpNode = "ErrHelp";

        private readonly IssueTableSelection _selection = new();
        private readonly List<AnalysisIssue> _allIssues = new();

        private WindowChrome? _chrome;
        private WindowTitleCount? _titleCount;
        private IssueFilterBar? _filters;
        private DataTable? _table;
        private Button? _firstError;
        private int _lastSceneVersion;

        public int TotalIssueCount => _allIssues.Count;
        public int VisibleIssueCount => _table != null ? _table.ShownRows.Count : 0;
        public int SelectedCount => _selection.SelectedCount;
        public bool IsSelected(AnalysisIssue iss) => _selection.IsSelected(iss);
        public int SelectedVisibleIdx => _selection.FocusVisibleIdx;
        public TMP_InputField SearchField => _filters!.Search;
        internal DataTable Table => _table!;
        internal IssueFilterBar Filters => _filters!;
        internal WindowChrome Chrome => _chrome!;

        public void Build(Transform layer)
        {
            Instance = this;
            _chrome = WindowChrome.Create(layer, "ErrorPanel", Loc.T("errors.title"), UIStyle.ErrorsSize,
                new WindowChromeOptions
                {
                    OnClose = () => SetVisible(false),
                    HasFooter = true,
                    RuledHeader = true,
                });
            ProjectWindows.Register(this);
            _titleCount = WindowTitleCount.Create(_chrome);

            float barTop = -(UIStyle.TitleBarH + UIStyle.Space3);
            _filters = new IssueFilterBar(_chrome.Panel, _chrome.BodyPad, barTop, _chrome.BodyWidth, RebuildRows);
            BuildTable(UIStyle.TitleBarH + UIStyle.Space3 + UIStyle.ChipH + UIStyle.Space3);

            _chrome.Footer!.AddLeftText(HelpNode, Loc.T("errors.hint"));
            _firstError = _chrome.Footer.AddSecondary(FirstErrorNode, Loc.T("errors.goFirst"), GoToFirst);

            _chrome.Panel.gameObject.SetActive(false);
        }

        public string WindowId => "errors";
        public RectTransform? WindowRect => _chrome?.Panel;
        public bool HeightAdjustable => false;

        public bool IsVisible => _chrome != null && _chrome.Panel.gameObject.activeSelf;

        public void Toggle() => SetVisible(_chrome != null && !_chrome.Panel.gameObject.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_chrome == null) return;
            if (visible) Analyze();
            _chrome.Panel.gameObject.SetActive(visible);
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
            _table?.RepaintSelection();
        }

        public void CopySelectedToClipboard()
        {
            var text = _selection.SelectedAsClipboardText();
            if (text.Length > 0) GUIUtility.systemCopyBuffer = text;
        }

        public bool ClaimsPageNavigation =>
            IsVisible && !CameraController.IsTypingInInputField() && !AnyCtrl() && !AnyShift()
            && _selection.VisibleCount > 0;

        private void OnDestroy()
        {
            ProjectWindows.Unregister(this);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsVisible) return;
            if (_lastSceneVersion != SceneRevision.Version) Analyze();
            HandleKeyboardNavigation();
        }

        private void BuildTable(float top)
        {
            float height = _chrome!.Panel.sizeDelta.y - top - UIStyle.FooterH;
            _table = DataTable.Create(_chrome.Panel, TableNode, new Vector2(_chrome.BodyWidth, height),
                IssueRows.Columns(), selectable: true);
            var rt = _table.Root;
            UIFactory.AnchorTopLeft(rt);
            rt.anchoredPosition = new Vector2(_chrome.BodyPad, -top);

            _table.SelectionSource = row => row.Tag is AnalysisIssue iss && _selection.IsSelected(iss);
            _table.SelectionChanged += OnRowSelected;
            _table.RowActivated += row => { if (row.Tag is AnalysisIssue iss) IssueDisplay.RevealIssue(iss); };
            _table.SetEmptyState(Loc.T("errors.empty.title"), Loc.T("errors.empty.hint"));
        }

        private void OnRowSelected(DataRow? row)
        {
            if (row == null || row.Tag is not AnalysisIssue iss) return;
            int index = IndexOfShown(row);
            if (index >= 0) HandleRowClick(iss, index, AnyCtrl(), AnyShift());
        }

        private int IndexOfShown(DataRow row)
        {
            var shown = _table!.ShownRows;
            for (int i = 0; i < shown.Count; i++)
                if (shown[i] == row) return i;
            return -1;
        }

        private void GoToFirst()
        {
            int index = FirstErrorIndex();
            if (index < 0) return;
            var iss = _selection.Visible[index];
            HandleRowClick(iss, index, false, false);
            IssueDisplay.RevealIssue(iss);
        }

        private int FirstErrorIndex()
        {
            var visible = _selection.Visible;
            for (int i = 0; i < visible.Count; i++)
                if (visible[i].Level == IssueLevel.Error) return i;
            return visible.Count > 0 ? 0 : -1;
        }

        private void Analyze()
        {
            using var _ = PerfMarkers.ErrorPanelAnalyze.Auto();

            _allIssues.Clear();
            _allIssues.AddRange(SceneAnalyzer.Analyze());
            _lastSceneVersion = SceneRevision.Version;

            _selection.Clear();

            int errors = 0, warnings = 0;
            foreach (var iss in _allIssues)
            {
                if (iss.Level == IssueLevel.Error) errors++;
                else if (iss.Level == IssueLevel.Warning) warnings++;
            }
            _filters!.SetCounts(_allIssues.Count, errors, warnings);
            _filters.SetCodes(CodesPresentIn(_allIssues));
            _filters.SetFloors(FloorIdsPresentIn(_allIssues), FloorNameOf);

            RebuildRows();
        }

        private static List<string> CodesPresentIn(List<AnalysisIssue> issues)
        {
            var codes = new List<string>();
            foreach (var iss in issues)
                if (!codes.Contains(iss.Code)) codes.Add(iss.Code);
            codes.Sort(System.StringComparer.Ordinal);
            return codes;
        }

        internal static List<string> FloorIdsPresentIn(List<AnalysisIssue> issues)
        {
            var ids = new List<string>();
            foreach (var iss in issues)
            {
                if (iss.Target == null) continue;
                string id = LevelRegistry.LevelOf(iss.Target).id;
                if (!ids.Contains(id)) ids.Add(id);
            }
            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }

        internal static string FloorNameOf(string levelId)
        {
            foreach (var level in LevelRegistry.Snapshot())
                if (level != null && level.id == levelId) return level.name;
            return levelId;
        }

        private void RebuildRows()
        {
            _selection.BeginRebuild();
            _filters!.SyncSearchHint();

            var rows = new List<DataRow>();
            foreach (var iss in _allIssues)
            {
                if (!_filters.Passes(iss, FloorIdOf(iss))) continue;
                _selection.AddVisible(iss);
                rows.Add(IssueRows.For(iss));
            }

            _table!.SetRows(rows);
            _selection.EndRebuild();
            _table.RepaintSelection();
            SyncChrome(rows.Count);
        }

        private static string? FloorIdOf(AnalysisIssue iss) =>
            iss.Target != null ? LevelRegistry.LevelOf(iss.Target).id : null;

        private void SyncChrome(int shown)
        {
            _titleCount!.Set(_allIssues.Count);
            if (_firstError != null) _firstError.interactable = shown > 0;

            var empty = _table!.Empty;
            if (empty == null) return;
            bool nothingFound = _allIssues.Count > 0;
            empty.Title.text = nothingFound ? Loc.T("errors.nothingFound.title") : Loc.T("errors.empty.title");
            empty.Hint.text = nothingFound ? Loc.T("errors.nothingFound.hint") : Loc.T("errors.empty.hint");
        }

        private void HandleKeyboardNavigation()
        {
            if (CameraController.IsTypingInInputField()) return;

            if (InputMap.Down(InputAction.ErrorPanelCopy))
            {
                CopySelectedToClipboard();
                return;
            }

            if (InputMap.Down(InputAction.ErrorPanelSelectAll))
            {
                SelectAll();
                return;
            }

            if (AnyCtrl() || AnyShift()) return;
            int count = _selection.VisibleCount;
            if (count == 0) return;

            int cur = _selection.FocusVisibleIdx;
            int next = cur;
            int step = _table!.RowsPerPage();

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
            _table!.RepaintSelection();
            _table.ScrollIntoView(_selection.FocusVisibleIdx);
        }

        private static bool AnyCtrl() =>
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static bool AnyShift() =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
