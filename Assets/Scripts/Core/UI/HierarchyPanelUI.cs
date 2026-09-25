using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class HierarchyPanelUI : MonoBehaviour, IProjectWindow
    {
        public static HierarchyPanelUI? Instance { get; private set; }

        public string WindowId => "hierarchy";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        public bool HeightAdjustable => true;

        private const float PanelW = 300f;
        private const float PanelH = 660f;
        private const float MinPanelH = 160f;
        private const float TopOffsetUnderToolbar = 60f;
        private const float RowH = 24f;
        private const float RowStep = 26f;
        private const float ScrollbarAndPaddingW = 28f;
        private const float ContentW = PanelW - ScrollbarAndPaddingW;
        private const float IndentPx = 16f;
        private const float HeaderStripH = 100f;
        private const int MovePlaceholderIndex = 0;
        private const int MoveUngroupedIndex = 1;
        private const int MoveFirstGroupIndex = 2;
        private const float PollInterval = 0.5f;

        private static readonly Color RowElementColor = new Color(0.16f, 0.17f, 0.21f, 1f);
        private static readonly Color RowGroupColor = new Color(0.22f, 0.24f, 0.30f, 1f);
        private static readonly Color RowRootColor = new Color(0.13f, 0.14f, 0.17f, 1f);

        private GameObject? _root;
        private RectTransform? _content;
        private TMP_Dropdown? _moveDropdown;
        private TMP_InputField? _searchField;
        private TMP_Text? _searchHint;
        private static readonly HashSet<int> NoCollapsedGroups = new HashSet<int>();

        private readonly HashSet<int> _collapsed = new HashSet<int>();
        private readonly List<LinkGroup> _dropdownGroups = new List<LinkGroup>();
        private float _nextPoll;
        private int _fingerprint;
        private bool _ignoreDropdownCallback;

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
            var panel = UIFactory.CreatePanel("HierarchyPanel", canvas, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(0, -TopOffsetUnderToolbar);
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, UIStyle.DragStripHeight);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("HierTitle", panel.transform, "Сцена", UIStyle.FontWindowTitle,
                new Vector2(14, -6), new Vector2(120, 28), TextAnchor.MiddleLeft)
                .rectTransform.SetAnchor(new Vector2(0, 1), new Vector2(14, -6));

            var addBtn = UIFactory.CreateButton("HierAddGroup", panel.transform, "+ Группа",
                Vector2.zero, new Vector2(92, 26), CreateEmptyGroup);
            SetTopRight(addBtn.GetComponent<RectTransform>(), new Vector2(-64, -6));

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            _moveDropdown = UIFactory.CreateDropdown("HierMoveTo", panel.transform,
                new List<string> { "Переместить в…" },
                Vector2.zero, new Vector2(PanelW - 20, 26), OnMoveDropdown);
            var ddRt = _moveDropdown.GetComponent<RectTransform>();
            ddRt.anchorMin = ddRt.anchorMax = new Vector2(0.5f, 1);
            ddRt.pivot = new Vector2(0.5f, 1);
            ddRt.anchoredPosition = new Vector2(0, -38);

            _searchField = UIFactory.CreateInputField("HierSearch", panel.transform, "",
                Vector2.zero, new Vector2(PanelW - 20, 26));
            var sfRt = _searchField.GetComponent<RectTransform>();
            sfRt.anchorMin = sfRt.anchorMax = new Vector2(0.5f, 1);
            sfRt.pivot = new Vector2(0.5f, 1);
            sfRt.anchoredPosition = new Vector2(0, -68);
            _searchField.onValueChanged.AddListener(_ => Refresh());
            var searchHint = UIFactory.CreateLabel("HierSearchHint", _searchField.transform, "Поиск…", 14,
                Vector2.zero, new Vector2(PanelW - 36, 26), TextAnchor.MiddleLeft);
            searchHint.color = UIStyle.TextSecondary;
            searchHint.raycastTarget = false;
            var shRt = searchHint.rectTransform;
            shRt.anchorMin = Vector2.zero; shRt.anchorMax = Vector2.one;
            shRt.offsetMin = new Vector2(8, 0); shRt.offsetMax = Vector2.zero;
            _searchHint = searchHint;

            BuildScrollArea(panel.transform);

            WindowDrag.AttachResizeBottom(panel.rectTransform, MinPanelH);

            GroupManager.Changed += OnGroupsChanged;
            if (SelectionManager.Instance != null)
                SelectionManager.Instance.OnSelectionChanged += OnSceneSelectionChanged;

            Refresh();
        }

        private static void SetTopRight(RectTransform rt, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = pos;
        }

        private void BuildScrollArea(Transform parent)
        {
            var viewport = UIFactory.CreateRect("HierViewport", parent);
            viewport.anchorMin = new Vector2(0, 0);
            viewport.anchorMax = new Vector2(1, 1);
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.offsetMin = new Vector2(6, 8);
            viewport.offsetMax = new Vector2(-16, -HeaderStripH);
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _content = UIFactory.CreateRect("HierContent", viewport);
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = new Vector2(0, 0);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;

            var sbRect = UIFactory.CreateRect("HierScrollbar", parent);
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot = new Vector2(1, 0.5f);
            sbRect.offsetMin = new Vector2(-12, 8);
            sbRect.offsetMax = new Vector2(-4, -HeaderStripH);
            var sbImg = sbRect.gameObject.AddComponent<Image>();
            sbImg.color = new Color(0.10f, 0.10f, 0.13f, 0.6f);
            var scrollbar = sbRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = UIFactory.CreateRect("Handle", sbRect);
            handle.sizeDelta = new Vector2(8, 100);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = UIStyle.ScrollHandle;
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
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
            if (_root == null || !_root.activeSelf) return;
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
                    h = h * 31 + lvl.floorElevationMm;
                }
                return h;
            }
        }

        private void OnGroupsChanged() => Refresh();

        private void OnSceneSelectionChanged(KitchenElement? selected)
        {
            bool expanded = ExpandGroupsOfSelectedElements();
            if (_root == null || !_root.activeSelf) return;
            if (expanded) { Refresh(); return; }

            int fingerprint = ComputeFingerprint();
            if (fingerprint != _fingerprint) RebuildRows(fingerprint);
            else _highlights.Repaint(SelectionManager.Instance);
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

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (visible) Refresh();
            _root.SetActive(visible);
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

            var collapsed = filter.Length == 0 ? _collapsed : NoCollapsedGroups;
            var nodes = BuildNodesGroupedByLevel(collapsed);
            var sel = SelectionManager.Instance;

            float y = 0f;
            foreach (var node in nodes)
            {
                if (filter.Length > 0 && !MatchesFilter(node, filter)) continue;
                BuildRow(node, y, sel);
                y -= RowStep;
            }
            _content.sizeDelta = new Vector2(0, -y + 4);

            RefreshMoveDropdown();
        }

        private static List<SceneTree.Node> BuildNodesGroupedByLevel(ISet<int>? collapsed)
        {
            var levels = new List<Level>(LevelRegistry.Items);
            if (levels.Count <= 1)
                return SceneTree.Build(PartRegistry.All, GroupManager.AllGroups(), collapsed);

            levels.Sort((a, b) => b.floorElevationMm.CompareTo(a.floorElevationMm));

            var nodes = new List<SceneTree.Node>();
            foreach (var level in levels)
            {
                var elementsOnLevel = ElementsOnLevel(level);
                var groupsOnLevel = GroupsOnLevel(level, elementsOnLevel);
                var levelNodes = SceneTree.Build(elementsOnLevel, groupsOnLevel, collapsed);
                levelNodes[0].rootLabel = level.name;
                nodes.AddRange(levelNodes);
            }
            return nodes;
        }

        private static List<KitchenElement> ElementsOnLevel(Level level)
        {
            var result = new List<KitchenElement>();
            foreach (var e in PartRegistry.GetAll())
                if (e != null && LevelRegistry.LevelOf(e).id == level.id) result.Add(e);
            return result;
        }

        private static List<LinkGroup> GroupsOnLevel(Level level, List<KitchenElement> elementsOnLevel)
        {
            var result = new List<LinkGroup>();
            foreach (var g in GroupManager.AllGroups())
            {
                bool hasMemberHere = false;
                foreach (var e in elementsOnLevel)
                    if (e.GroupId == g.id) { hasMemberHere = true; break; }
                if (hasMemberHere) result.Add(g);
            }
            return result;
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

        private void BuildRow(SceneTree.Node node, float y, SelectionManager? sel)
        {
            float indent = node.depth * IndentPx;

            var row = UIFactory.CreateRect("Row", _content!);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0, y);
            row.sizeDelta = new Vector2(0, RowH);

            if (node.group != null && node.hasChildren)
            {
                int gid = node.group.id;
                var arrow = UIFactory.CreateButton("Fold", row,
                    node.collapsed ? UIStyle.GlyphCollapsed : UIStyle.GlyphExpanded,
                    Vector2.zero, new Vector2(20, RowH), () => ToggleCollapse(gid));
                var aRt = arrow.GetComponent<RectTransform>();
                aRt.anchorMin = aRt.anchorMax = aRt.pivot = new Vector2(0, 0.5f);
                aRt.anchoredPosition = new Vector2(indent, 0);
                arrow.GetComponent<Image>().color = new Color(0, 0, 0, 0f);
                var aLbl = arrow.GetComponentInChildren<TMP_Text>();
                if (aLbl != null) { aLbl.fontSize = 10; aLbl.color = UIStyle.TextSecondary; }
            }

            float mainX = indent + (node.group != null ? 22f : 4f);
            float mainW = ContentW - mainX - (node.group != null ? 24f : 0f);

            string label;
            Color rowColor;

            if (node.isRoot)
            {
                label = node.rootLabel ?? "Кухня";
                rowColor = RowRootColor;
            }
            else if (node.group != null)
            {
                label = $"{node.group.name} ({GroupManager.MembersOf(node.group).Count})";
                rowColor = RowGroupColor;
            }
            else
            {
                label = node.element!.PartName;
                rowColor = RowElementColor;
            }

            var mainBtn = UIFactory.CreateButton("Main", row, "", Vector2.zero, new Vector2(mainW, RowH),
                () => OnRowClick(node));
            var mRt = mainBtn.GetComponent<RectTransform>();
            mRt.anchorMin = mRt.anchorMax = mRt.pivot = new Vector2(0, 0.5f);
            mRt.anchoredPosition = new Vector2(mainX, 0);
            var tint = mainBtn.GetComponent<Image>();
            tint.color = HierarchyRowHighlights.Highlighted(node, sel) ? UIStyle.RowSelected : rowColor;
            _highlights.Remember(tint, node, rowColor);
            _rowsBuilt++;

            var text = mainBtn.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = label;
                text.fontSize = node.group != null || node.isRoot ? 15 : 14;
                text.alignment = TextAlignmentOptions.Left;
                text.margin = new Vector4(6, 0, 0, 0);
            }

            if (node.group != null)
            {
                var g = node.group;
                var menuBtn = UIFactory.CreateButton("GroupMenu", row, "…",
                    Vector2.zero, new Vector2(24, RowH - 4), () => OpenGroupMenu(g));
                var dRt = menuBtn.GetComponent<RectTransform>();
                dRt.anchorMin = dRt.anchorMax = dRt.pivot = new Vector2(1, 0.5f);
                dRt.anchoredPosition = new Vector2(-2, 0);
            }
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

        private void OnRowClick(SceneTree.Node node)
        {
            var sel = SelectionManager.Instance;
            if (sel == null) return;

            if (node.isRoot) return;

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
            GroupManager.Create($"Группа {n}");
        }

        private void RefreshMoveDropdown()
        {
            if (_moveDropdown == null) return;
            _dropdownGroups.Clear();
            var options = new List<string> { "Переместить в…", "— вне групп —" };
            foreach (var g in GroupManager.AllGroups())
            {
                _dropdownGroups.Add(g);
                options.Add(g.name);
            }
            options.Add("+ Новая группа");

            _ignoreDropdownCallback = true;
            _moveDropdown.ClearOptions();
            _moveDropdown.AddOptions(options);
            _moveDropdown.SetValueWithoutNotify(MovePlaceholderIndex);
            _ignoreDropdownCallback = false;
        }

        private void ResetMoveDropdownToPlaceholder()
        {
            if (_moveDropdown == null) return;
            _ignoreDropdownCallback = true;
            _moveDropdown.SetValueWithoutNotify(MovePlaceholderIndex);
            _ignoreDropdownCallback = false;
        }

        private void OnMoveDropdown(int index)
        {
            if (_ignoreDropdownCallback || _moveDropdown == null || index == MovePlaceholderIndex) return;

            var sel = SelectionManager.Instance;
            var selected = sel != null ? new List<KitchenElement>(sel.SelectedElements) : new List<KitchenElement>();

            LinkGroup? target = null;
            int newGroupIndex = _dropdownGroups.Count + MoveFirstGroupIndex;
            if (index == newGroupIndex)
                target = GroupManager.Create($"Группа {_dropdownGroups.Count + 1}");
            else if (index >= MoveFirstGroupIndex)
                target = _dropdownGroups[index - MoveFirstGroupIndex];
            else if (index == MoveUngroupedIndex)
                target = null;

            foreach (var e in selected)
                if (e != null) GroupManager.MoveTo(e, target);

            ResetMoveDropdownToPlaceholder();
            if (selected.Count == 0) Refresh();
        }
    }

    internal static class HierarchyRectExtensions
    {
        public static void SetAnchor(this RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }
    }
}
