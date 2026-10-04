using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class LoadProjectWindowUI : MonoBehaviour, IProjectWindow
    {
        public const string SearchNode = "LoadSearch";
        public const string SearchHintNode = "LoadSearchHint";
        public const string TableNode = "LoadTable";
        public const string NewProjectNode = "LoadNewProject";
        public const string BrowseNode = "LoadOpenFile";
        public const string OpenSelectedNode = "LoadOpenSelected";

        private readonly ProjectFileActions _actions = new();

        private WindowChrome? _chrome;
        private DataTable? _table;
        private TMP_InputField? _search;
        private TMP_Text? _searchHint;
        private Button? _openSelected;
        private Canvas? _canvas;

        public string WindowId => "loadProject";
        public RectTransform? WindowRect => _chrome?.Panel;
        public bool HeightAdjustable => false;
        public bool IsVisible => _chrome != null && _chrome.Panel.gameObject.activeSelf;
        internal DataTable Table => _table!;
        internal TMP_InputField Search => _search!;

        public void Build(Transform canvas)
        {
            _canvas = canvas.GetComponentInParent<Canvas>();
            _chrome = WindowChrome.Create(canvas, "LoadProjectWindow", Loc.T("window.load.title"),
                UIStyle.LoadProjectSize, new WindowChromeOptions
                {
                    OnClose = () => SetVisible(false),
                    HasFooter = true,
                    RuledHeader = true,
                });

            BuildSearch();
            BuildTable();
            BuildFooter();

            _chrome.Panel.gameObject.SetActive(false);
        }

        private void BuildSearch()
        {
            float top = UIStyle.TitleBarH + UIStyle.Space3;
            _search = UIFactory.CreateInputField(SearchNode, _chrome!.Panel, "", Vector2.zero,
                new Vector2(_chrome.BodyWidth, UIStyle.ControlH));
            var rt = (RectTransform)_search.transform;
            UIFactory.AnchorTopLeft(rt);
            rt.anchoredPosition = new Vector2(_chrome.BodyPad, -top);
            _search.onValueChanged.AddListener(_ => RebuildRows());

            _searchHint = UIFactory.CreateLabel(SearchHintNode, _search.transform, Loc.T("window.load.search"),
                UIStyle.FontBody, Vector2.zero, new Vector2(UIStyle.ControlH, UIStyle.ControlH), TextAnchor.MiddleLeft);
            _searchHint.color = UIStyle.TextSecondary;
            _searchHint.raycastTarget = false;
            _searchHint.enableWordWrapping = false;
            var hint = _searchHint.rectTransform;
            hint.anchorMin = Vector2.zero;
            hint.anchorMax = Vector2.one;
            hint.offsetMin = new Vector2(UIStyle.Space2, 0f);
            hint.offsetMax = new Vector2(-UIStyle.Space2, 0f);
        }

        private void BuildTable()
        {
            float top = UIStyle.TitleBarH + UIStyle.Space3 + UIStyle.ControlH + UIStyle.Space2;
            _table = DataTable.Create(_chrome!.Panel, TableNode,
                new Vector2(_chrome.BodyWidth, UIStyle.LoadProjectSize.y - top - UIStyle.FooterH),
                LoadProjectRows.Columns(), selectable: true);
            var rt = _table.Root;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(_chrome.BodyPad, UIStyle.FooterH);
            rt.offsetMax = new Vector2(-_chrome.BodyPad, -top);

            _table.RowDecorator = new LoadProjectRowDecor(_table, IsCurrent, Forget).Decorate;
            _table.SortBy(LoadProjectRows.ModifiedColumn, descending: true);
            _table.SelectionChanged += _ => SyncOpenButton();
            _table.RowActivated += row => { if (row.Tag is RecentProjectRow project) OpenRecent(project.Path); };
            _table.SetEmptyState(Loc.T("window.load.empty.title"), Loc.T("window.load.empty.hint"),
                Loc.T("window.load.newProject"), OnNewProject);
        }

        private void BuildFooter()
        {
            var footer = _chrome!.Footer!;
            footer.AddLeft(NewProjectNode, Loc.T("window.load.newProject"), OnNewProject);
            footer.AddLeft(BrowseNode, Loc.T("window.load.browse"), OnLoadFile, ButtonRole.Link);
            _openSelected = footer.AddPrimary(OpenSelectedNode, Loc.T("window.load.open"), OpenSelected);
        }

        private static bool IsCurrent(string path) =>
            SaveLoadManager.HasLastPath
            && string.Equals(SaveLoadManager.LastPath, path, System.StringComparison.OrdinalIgnoreCase);

        private void Forget(string path)
        {
            RecentProjects.Forget(path);
            RebuildRows();
        }

        private void OnNewProject() => _actions.NewProjectDialog(ok => { if (ok) SetVisible(false); });

        private void OnLoadFile() => _actions.LoadDialog(ok => { if (ok) SetVisible(false); });

        private void OpenRecent(string path) =>
            _actions.OpenExisting(path, ok => { if (ok) SetVisible(false); });

        private void OpenSelected()
        {
            if (_table!.Selected?.Tag is RecentProjectRow project) OpenRecent(project.Path);
        }

        private void SyncOpenButton()
        {
            if (_openSelected != null) _openSelected.interactable = _table!.Selected != null;
        }

        public void Toggle() => SetVisible(_chrome != null && !_chrome.Panel.gameObject.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_chrome == null) return;
            if (visible) Refresh();
            _chrome.Panel.gameObject.SetActive(visible);
        }

        private void Refresh()
        {
            float screenHeight = _canvas != null
                ? ((RectTransform)_canvas.transform).rect.height
                : UIStyle.LoadProjectSize.y * 2f;
            float height = LoadWindowLayout.HeightFor(UIStyle.LoadProjectSize.y, screenHeight);
            _chrome!.Panel.sizeDelta = new Vector2(UIStyle.LoadProjectSize.x, height);
            RebuildRows();
        }

        private void RebuildRows()
        {
            string search = _search!.text.Trim();
            _searchHint!.gameObject.SetActive(search.Length == 0);

            var paths = RecentProjects.Paths();
            var rows = new List<DataRow>();
            foreach (var path in paths)
            {
                var project = RecentProjectRowSource.For(path);
                if (LoadProjectRows.MatchesSearch(project, search)) rows.Add(LoadProjectRows.For(project));
            }

            _table!.SetRows(rows);
            SyncEmptyState(paths.Length > 0);
            _table.Select(DefaultSelection(rows));
            SyncOpenButton();
        }

        private static DataRow? DefaultSelection(List<DataRow> rows)
        {
            DataRow? first = null;
            foreach (var row in rows)
            {
                if (row.Tag is not RecentProjectRow project || !project.FileExists) continue;
                if (IsCurrent(project.Path)) return row;
                first ??= row;
            }
            return first;
        }

        private void SyncEmptyState(bool anyProjects)
        {
            var empty = _table!.Empty;
            if (empty == null) return;
            empty.Title.text = anyProjects ? Loc.T("window.load.noMatch.title") : Loc.T("window.load.empty.title");
            empty.Hint.text = anyProjects ? Loc.T("window.load.noMatch.hint") : Loc.T("window.load.empty.hint");
            if (empty.ActionButton != null) empty.ActionButton.gameObject.SetActive(!anyProjects);
        }
    }
}
