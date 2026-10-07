using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public class HierarchyPanelUI : MonoBehaviour, IProjectWindow
    {
        public static HierarchyPanelUI? Instance { get; private set; }

        public string WindowId => "hierarchy";
        public RectTransform? WindowRect => _chrome != null ? _chrome.Panel : null;
        public bool HeightAdjustable => true;

        private const float PollInterval = 0.5f;
        private static readonly HashSet<int> NoCollapsedGroups = new HashSet<int>();
        private static readonly HashSet<string> NoCollapsedLevels = new HashSet<string>();

        private WindowChrome? _chrome;
        private RectTransform? _content;
        private TMP_InputField? _searchField;
        private TMP_Text? _searchHint;
        private TMP_Text? _selectedLabel;
        private SceneMoveToDropdown? _moveTo;
        private EmptyState? _emptyScene;
        private EmptyState? _noMatches;

        private readonly HashSet<int> _collapsed = new HashSet<int>();
        private readonly HashSet<string> _collapsedLevels = new HashSet<string>();
        private float _nextPoll;
        private int _fingerprint;

        private readonly HierarchyRowHighlights _highlights = new HierarchyRowHighlights();

        private static int _rebuilds;
        private static int _rowsBuilt;

        internal static int TakeRebuilds()
        {
            int n = _rebuilds;
            _rebuilds = 0;
            return n;
        }

        internal static int TakeRowsBuilt()
        {
            int n = _rowsBuilt;
            _rowsBuilt = 0;
            return n;
        }

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            _chrome = WindowChrome.Create(canvas, "HierarchyPanel", Loc.T("hierarchy.title"),
                new Vector2(UIStyle.HierarchyW, SceneTreeMetrics.PanelH), new WindowChromeOptions
                {
                    Kind = WindowKind.Tool,
                    OnClose = () => SetVisible(false),
                    HasFooter = true,
                    RuledHeader = true,
                });
            var panel = _chrome.Panel;
            UIFactory.AnchorTopRight(panel);
            panel.anchoredPosition = new Vector2(-UIStyle.Space3, -(UIStyle.ToolbarH + UIStyle.Space3));
            ProjectWindows.Register(this);

            _chrome.AddHeaderAction("HierAddGroup", SceneTreeIcons.Plus, Loc.T("hierarchy.addGroup"),
                CreateEmptyGroup);

            BuildTreeArea();
            BuildSearch(panel);
            BuildEmptyStates(panel);
            BuildFooter(_chrome.Footer!);

            WindowDrag.AttachResizeBottom(panel, SceneTreeMetrics.MinPanelH);

            GroupManager.Changed += OnGroupsChanged;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSceneSelectionChanged;

            Refresh();
        }

        private void BuildTreeArea()
        {
            var body = _chrome!.CreateBody();
            var viewport = body.Viewport;
            viewport.offsetMax = new Vector2(viewport.offsetMax.x, -SceneTreeMetrics.TreeTop);
            _content = body.Content;
        }

        private void BuildSearch(RectTransform panel)
        {
            _searchField = UIFactory.CreateInputField("HierSearch", panel, "", Vector2.zero,
                new Vector2(0f, SceneTreeMetrics.SearchH));
            var rect = (RectTransform)_searchField.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(_chrome!.BodyPad, -(SceneTreeMetrics.SearchTop + SceneTreeMetrics.SearchH));
            rect.offsetMax = new Vector2(-_chrome.BodyPad, -SceneTreeMetrics.SearchTop);
            _searchField.onValueChanged.AddListener(_ => Refresh());

            var hint = UIFactory.CreateLabel("HierSearchHint", _searchField.transform, Loc.T("hierarchy.search"),
                UIStyle.FontBody, Vector2.zero, new Vector2(0f, SceneTreeMetrics.SearchH), TextAnchor.MiddleLeft);
            hint.color = UIStyle.TextSecondary;
            hint.raycastTarget = false;
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = Vector2.one;
            hintRect.offsetMin = new Vector2(UIStyle.Space2, 0f);
            hintRect.offsetMax = new Vector2(-UIStyle.Space2, 0f);
            _searchHint = hint;
        }

        private void BuildEmptyStates(RectTransform panel)
        {
            _emptyScene = Empty(panel, "HierEmpty", Loc.T("hierarchy.empty.title"), Loc.T("hierarchy.empty.hint"));
            _noMatches = Empty(panel, "HierNoMatches", Loc.T("hierarchy.noMatches.title"),
                Loc.T("hierarchy.noMatches.hint"));
        }

        private EmptyState Empty(RectTransform panel, string name, string title, string hint)
        {
            var state = EmptyState.Create(panel, name, title, hint, width: UIStyle.HierarchyW - 2f * _chrome!.BodyPad);
            var rect = state.Root;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(_chrome.BodyPad, _chrome.BodyBottom);
            rect.offsetMax = new Vector2(-_chrome.BodyPad, -SceneTreeMetrics.TreeTop);
            rect.gameObject.SetActive(false);
            return state;
        }

        private void BuildFooter(WindowFooter footer)
        {
            float labelW = UIStyle.HierarchyW - 2f * UIStyle.Space4 - SceneTreeMetrics.MoveToW - UIStyle.Space2;
            _selectedLabel = footer.AddLeftText("HierSelected", Loc.F("common.selectedCount", 0));
            _selectedLabel.rectTransform.sizeDelta = new Vector2(labelW, UIStyle.ControlH);

            _moveTo = SceneMoveToDropdown.Create(footer.Root, new Vector2(SceneTreeMetrics.MoveToW, UIStyle.ControlH),
                Refresh);
            var rect = (RectTransform)_moveTo.Dropdown.transform;
            bool rtl = LayoutDirection.IsRtl;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(rtl ? 0f : 1f, 0.5f);
            rect.anchoredPosition = new Vector2(rtl ? UIStyle.Space4 : -UIStyle.Space4, 0f);
        }

        private void OnDestroy()
        {
            ProjectWindows.Unregister(this);
            GroupManager.Changed -= OnGroupsChanged;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged -= OnSceneSelectionChanged;
        }

        private void Update()
        {
            using var _ = PerfMarkers.HierarchyPanelUpdate.Auto();
            RefreshWhenSceneChangedWithoutAnEvent();
        }

        private void RefreshWhenSceneChangedWithoutAnEvent()
        {
            if (!IsVisible) return;
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollInterval;
            PollSceneForChanges();
        }

        internal void PollSceneForChanges()
        {
            int fingerprint = ComputeFingerprint();
            if (fingerprint != _fingerprint) RebuildRows(fingerprint);
        }

        private static int ComputeFingerprint()
        {
            unchecked
            {
                int h = 17;
                foreach (var e in PartRegistry.All)
                {
                    if (e == null) continue;
                    h = h * 31 + e.PartName.GetHashCode();
                    h = h * 31 + e.GroupId;
                    h = h * 31 + e.LevelId.GetHashCode();
                    if (e is IFacadeHost host && !string.IsNullOrEmpty(host.AttachedFacadeName))
                        h = h * 31 + host.AttachedFacadeName.GetHashCode();
                }
                foreach (var g in GroupManager.AllGroups())
                {
                    h = h * 31 + g.id;
                    h = h * 31 + g.name.GetHashCode();
                }
                foreach (var lvl in LevelRegistry.Items)
                {
                    h = h * 31 + lvl.id.GetHashCode();
                    h = h * 31 + lvl.name.GetHashCode();
                    h = h * 31 + lvl.floorElevationMm;
                }
                return h;
            }
        }

        private void OnGroupsChanged() => Refresh();

        private void OnSceneSelectionChanged(KitchenElement? selected)
        {
            bool expanded = ExpandGroupsOfSelectedElements();
            if (!IsVisible) return;
            if (expanded) { Refresh(); return; }

            int fingerprint = ComputeFingerprint();
            if (fingerprint != _fingerprint) RebuildRows(fingerprint);
            else
            {
                _highlights.Repaint(SelectionManager.Instance);
                RefreshSelectionFooter();
            }
        }

        private bool ExpandGroupsOfSelectedElements()
        {
            var sel = SelectionManager.Instance;
            if (sel == null) return false;
            bool expanded = false;
            foreach (var e in sel.SelectedElements)
                if (e != null && e.GroupId != 0 && _collapsed.Remove(e.GroupId))
                    expanded = true;
            return expanded;
        }

        public bool IsVisible => _chrome != null && _chrome.Panel.gameObject.activeSelf;

        public void Toggle() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            if (_chrome == null) return;
            if (visible) Refresh();
            _chrome.Panel.gameObject.SetActive(visible);
        }

        private void Refresh() => RebuildRows(ComputeFingerprint());

        private void RebuildRows(int fingerprint)
        {
            if (_content == null) return;
            _fingerprint = fingerprint;
            _rebuilds++;
            _highlights.Forget();

            for (int i = _content.childCount - 1; i >= 0; i--)
                DestroyNow.The(_content.GetChild(i).gameObject);

            string filter = _searchField != null ? _searchField.text.Trim() : "";
            if (_searchHint != null) _searchHint.gameObject.SetActive(filter.Length == 0);

            bool filtering = filter.Length > 0;
            bool sceneEmpty = !filtering && !AnyListedElement() && !AnyGroups();
            var nodes = sceneEmpty ? new List<SceneTree.Node>()
                : SceneTreeByLevel.Build(filtering ? NoCollapsedGroups : _collapsed,
                    filtering ? NoCollapsedLevels : _collapsedLevels);
            var actions = new SceneTreeRowActions(OnRowClick, OnFold, OpenGroupMenu);
            var sel = SelectionManager.Instance;

            float y = 0f;
            int shown = 0;
            foreach (var node in nodes)
            {
                if (filtering && !MatchesFilter(node, filter)) continue;
                var view = SceneTreeRowFactory.Build(_content, node, y, HierarchyRowHighlights.Highlighted(node, sel),
                    actions);
                _highlights.Remember(view);
                _rowsBuilt++;
                y -= UIStyle.TreeRowH;
                if (!node.isRoot) shown++;
            }
            _content.sizeDelta = new Vector2(0f, -y);

            ShowEmptyStates(sceneEmpty, filtering, shown);
            _moveTo?.Rebuild();
            RefreshSelectionFooter();
        }

        private void ShowEmptyStates(bool sceneEmpty, bool filtering, int shownRows)
        {
            if (_emptyScene != null) _emptyScene.Root.gameObject.SetActive(sceneEmpty);
            if (_noMatches != null) _noMatches.Root.gameObject.SetActive(filtering && shownRows == 0);
        }

        private static bool AnyListedElement()
        {
            foreach (var e in PartRegistry.All)
                if (e != null) return true;
            return false;
        }

        private static bool AnyGroups()
        {
            foreach (var _ in GroupManager.AllGroups()) return true;
            return false;
        }

        private void RefreshSelectionFooter()
        {
            var sel = SelectionManager.Instance;
            int count = sel != null ? sel.SelectedElements.Count : 0;
            if (_selectedLabel != null) _selectedLabel.text = Loc.F("common.selectedCount", NumberFormat.Integer(count));
            _moveTo?.SetEnabled(count > 0);
        }

        private static bool MatchesFilter(SceneTree.Node node, string filter)
        {
            if (node.isRoot) return true;
            string name = node.group != null ? node.group.name : node.element!.PartName;
            if (name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return node.group != null && AnyMemberMatches(node.group, filter);
        }

        private static bool AnyMemberMatches(LinkGroup group, string filter)
        {
            foreach (var m in GroupManager.MembersOf(group))
                if (m != null && m.PartName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private void OpenGroupMenu(LinkGroup g)
        {
            var members = GroupManager.MembersOf(g);
            if (members.Count == 0)
            {
                GroupManager.Unlink(g);
                return;
            }
            var sel = SelectionManager.Instance;
            if (sel != null) sel.SelectOnly(members);
            if (GroupMenuUI.Instance != null) GroupMenuUI.Instance.Open(members[0]);
        }

        internal void ToggleCollapse(int groupId)
        {
            if (!_collapsed.Remove(groupId))
                _collapsed.Add(groupId);
            Refresh();
        }

        internal void ToggleLevelCollapse(string levelKey)
        {
            if (!_collapsedLevels.Remove(levelKey))
                _collapsedLevels.Add(levelKey);
            Refresh();
        }

        private void OnFold(SceneTree.Node node)
        {
            if (node.isRoot) ToggleLevelCollapse(node.rootKey);
            else if (node.group != null) ToggleCollapse(node.group.id);
        }

        private void OnRowClick(SceneTree.Node node)
        {
            var sel = SelectionManager.Instance;

            if (node.isRoot)
            {
                if (node.hasChildren) ToggleLevelCollapse(node.rootKey);
                return;
            }
            if (sel == null) return;

            if (node.group != null)
            {
                var members = GroupManager.MembersOf(node.group);
                if (members.Count > 0) sel.SelectOnly(members);
                return;
            }

            var el = node.element!;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl) sel.ToggleInSelection(el);
            else sel.Select(el);
        }

        private void CreateEmptyGroup()
        {
            int n = 1;
            foreach (var g in GroupManager.AllGroups()) n++;
            GroupManager.Create(Loc.F("group.defaultName", n));
        }
    }
}
