using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    /// <summary>
    /// Окно «Сцена»: дерево всех объектов (Кухня → группы/модули → элементы →
    /// прикреплённые фасады) с двусторонней синхронизацией выделения.
    ///
    /// - клик по строке — выделение в сцене (Ctrl — добавить/убрать);
    /// - клик по группе — выделение всех её членов;
    /// - выделение в сцене подсвечивает строки (и разворачивает группу);
    /// - «+ Группа» создаёт пустую группу, выпадающий список «Переместить в…»
    ///   переносит ТЕКУЩЕЕ выделение между группами;
    /// - «x» на строке группы распускает её (элементы остаются в сцене).
    ///
    /// Данные — SceneTree.Build (чистая модель), операции — GroupManager (IGroupService).
    /// Обновление: события GroupManager.Changed / OnSelectionChanged + дешёвый
    /// fingerprint-поллинг (создание/удаление/переименование из MCP или undo).
    /// </summary>
    public class HierarchyPanelUI : MonoBehaviour, IProjectWindow
    {
        public static HierarchyPanelUI? Instance { get; private set; }

        public string WindowId => "hierarchy";
        public RectTransform? WindowRect => _root != null ? (RectTransform)_root.transform : null;
        // Высоту панели пользователь тянет за нижний край (AttachResizeBottom).
        public bool HeightAdjustable => true;

        private const float PanelW = 300f;
        private const float PanelH = 660f;
        private const float MinPanelH = 160f;  // шапка + дропдаун + пара строк
        private const float TopOffset = 60f;   // под тулбаром
        private const float RowH = 24f;
        private const float RowStep = 26f;
        private const float ContentW = PanelW - 28f; // минус скроллбар и поля
        private const float IndentPx = 16f;
        private const float PollInterval = 0.5f;

        private static readonly Color RowElementColor = new Color(0.16f, 0.17f, 0.21f, 1f);
        private static readonly Color RowGroupColor = new Color(0.22f, 0.24f, 0.30f, 1f);
        private static readonly Color RowSelectedColor = new Color(0.45f, 0.40f, 0.15f, 1f);
        private static readonly Color RowRootColor = new Color(0.13f, 0.14f, 0.17f, 1f);

        private GameObject? _root;
        private RectTransform? _content;
        private TMP_Dropdown? _moveDropdown;
        private TMP_InputField? _searchField;
        private TMP_Text? _searchHint;
        private readonly HashSet<int> _collapsed = new HashSet<int>();
        private readonly List<LinkGroup> _dropdownGroups = new List<LinkGroup>();
        private float _nextPoll;
        private int _fingerprint;
        private bool _suppressDropdown; // защита от onValueChanged при программном сбросе

        private void Awake() => Instance = this;

        public void Build(Transform canvas)
        {
            var panel = UIFactory.CreatePanel("HierarchyPanel", canvas, Vector2.zero, new Vector2(PanelW, PanelH));
            UIFactory.AnchorTopRight(panel.rectTransform);
            panel.rectTransform.anchoredPosition = new Vector2(0, -TopOffset);
            _root = panel.gameObject;
            WindowDrag.Attach(panel.rectTransform, 40f);
            ProjectWindows.Register(this);

            UIFactory.CreateLabel("HierTitle", panel.transform, "Сцена", 20,
                new Vector2(14, -6), new Vector2(120, 28), TextAnchor.MiddleLeft)
                .rectTransform.SetAnchor(new Vector2(0, 1), new Vector2(14, -6));

            // «+ Группа» отодвинута от кнопки закрытия: создание и закрытие
            // не должны соседствовать (правило 3 UI-GUIDELINES о misclick).
            var addBtn = UIFactory.CreateButton("HierAddGroup", panel.transform, "+ Группа",
                Vector2.zero, new Vector2(92, 26), CreateEmptyGroup);
            SetTopRight(addBtn.GetComponent<RectTransform>(), new Vector2(-64, -6));

            UIFactory.CreateCloseButton(panel.transform, () => SetVisible(false));

            // «Переместить в…» — применяется к текущему выделению.
            _moveDropdown = UIFactory.CreateDropdown("HierMoveTo", panel.transform,
                new List<string> { "Переместить в…" },
                Vector2.zero, new Vector2(PanelW - 20, 26), OnMoveDropdown);
            var ddRt = _moveDropdown.GetComponent<RectTransform>();
            ddRt.anchorMin = ddRt.anchorMax = new Vector2(0.5f, 1);
            ddRt.pivot = new Vector2(0.5f, 1);
            ddRt.anchoredPosition = new Vector2(0, -38);

            // Поиск по имени: без фильтра список из десятков полок неуправляем.
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

            // После скролл-зоны — хэндл последним, чтобы нижние 10px панели
            // ловили ресайз, а не клики по строкам списка.
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
            viewport.offsetMin = new Vector2(6, 8);          // отступ снизу
            viewport.offsetMax = new Vector2(-16, -100);     // шапка, дропдаун, поиск
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

            // Узкий (8px) тёмный скроллбар; прячется, когда весь список помещается.
            var sbRect = UIFactory.CreateRect("HierScrollbar", parent);
            sbRect.anchorMin = new Vector2(1, 0);
            sbRect.anchorMax = new Vector2(1, 1);
            sbRect.pivot = new Vector2(1, 0.5f);
            sbRect.offsetMin = new Vector2(-12, 8);
            sbRect.offsetMax = new Vector2(-4, -100);
            var sbImg = sbRect.gameObject.AddComponent<Image>();
            sbImg.color = new Color(0.10f, 0.10f, 0.13f, 0.6f);
            var scrollbar = sbRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = UIFactory.CreateRect("Handle", sbRect);
            handle.sizeDelta = new Vector2(8, 100);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.38f, 0.40f, 0.46f, 1f);
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

        // ── Обновление ───────────────────────────────────────────────────

        private void Update()
        {
            using var _ = PerfMarkers.HierarchyPanelUpdate.Auto();
            // События покрывают группы и выделение; поллинг ловит создание/удаление/
            // переименование элементов (MCP, undo, загрузка) без событийной обвязки.
            if (_root == null || !_root.activeSelf) return;
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollInterval;
            int fp = ComputeFingerprint();
            if (fp != _fingerprint) Refresh();
        }

        private static int ComputeFingerprint()
        {
            unchecked
            {
                int h = 17;
                foreach (var e in PartRegistry.GetAll())
                {
                    if (e == null) continue;
                    h = h * 31 + e.PartName.GetHashCode();
                    h = h * 31 + e.GroupId;
                    if (e is DrawerElement d && !string.IsNullOrEmpty(d.AttachedFacadeName))
                        h = h * 31 + d.AttachedFacadeName.GetHashCode();
                }
                foreach (var g in GroupManager.AllGroups())
                {
                    h = h * 31 + g.id;
                    h = h * 31 + g.name.GetHashCode();
                }
                return h;
            }
        }

        private void OnGroupsChanged() => Refresh();

        private void OnSceneSelectionChanged(KitchenElement? selected)
        {
            // Автораскрытие группы выделенного элемента — строка должна быть видна.
            var sel = SelectionManager.Instance;
            if (sel != null)
                foreach (var e in sel.SelectedElements)
                    if (e != null && e.GroupId != 0)
                        _collapsed.Remove(e.GroupId);
            Refresh();
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        public void Toggle() => SetVisible(_root != null && !_root.activeSelf);

        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            if (visible) Refresh();
            _root.SetActive(visible);
        }

        // ── Построение строк ─────────────────────────────────────────────

        private void Refresh()
        {
            if (_content == null) return;
            _fingerprint = ComputeFingerprint();

            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            string filter = _searchField != null ? _searchField.text.Trim() : "";
            if (_searchHint != null) _searchHint.gameObject.SetActive(filter.Length == 0);

            // При активном фильтре сворачивание игнорируется: совпадение внутри
            // свёрнутой группы обязано быть видно.
            var collapsed = filter.Length == 0 ? _collapsed : new HashSet<int>();
            var nodes = SceneTree.Build(PartRegistry.GetAll(), GroupManager.AllGroups(), collapsed);
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

        private static bool MatchesFilter(SceneTree.Node node, string filter)
        {
            if (node.isRoot) return true;
            string name = node.group != null ? node.group.name : node.element!.PartName;
            if (name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            // Группа остаётся видимой, если совпал кто-то из её членов.
            if (node.group != null)
                foreach (var m in GroupManager.MembersOf(node.group))
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

            // Стрелка сворачивания у группы.
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
            bool highlighted = false;

            if (node.isRoot)
            {
                label = "Кухня";
                rowColor = RowRootColor;
            }
            else if (node.group != null)
            {
                int count = GroupManager.MembersOf(node.group).Count;
                label = $"{node.group.name} ({count})";
                rowColor = RowGroupColor;
                highlighted = sel != null && count > 0 && AllSelected(sel, node.group);
            }
            else
            {
                var el = node.element!;
                label = el.PartName;
                rowColor = RowElementColor;
                highlighted = sel != null && sel.IsSelected(el);
            }

            var mainBtn = UIFactory.CreateButton("Main", row, "", Vector2.zero, new Vector2(mainW, RowH),
                () => OnRowClick(node));
            var mRt = mainBtn.GetComponent<RectTransform>();
            mRt.anchorMin = mRt.anchorMax = mRt.pivot = new Vector2(0, 0.5f);
            mRt.anchoredPosition = new Vector2(mainX, 0);
            mainBtn.GetComponent<Image>().color = highlighted ? RowSelectedColor : rowColor;

            var text = mainBtn.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = label;
                text.fontSize = node.group != null || node.isRoot ? 15 : 14;
                text.alignment = TextAlignmentOptions.Left;
                text.margin = new Vector4(6, 0, 0, 0);
            }

            // Меню группы «…»: имя, закрепление, редактирование, роспуск —
            // операции с полными подписями вместо безымянного «x»
            // (роспуск в один клик без подтверждения — прямой путь к потере
            // структуры сцены).
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

        private static bool AllSelected(SelectionManager sel, LinkGroup g)
        {
            foreach (var m in GroupManager.MembersOf(g))
                if (!sel.IsSelected(m)) return false;
            return true;
        }

        /// <summary>Открыть меню группы (то же, что по ПКМ на её элементе):
        /// роспуск и прочие операции — только через явные подписанные кнопки.</summary>
        private void OpenGroupMenu(LinkGroup g)
        {
            var members = GroupManager.MembersOf(g);
            if (members.Count == 0)
            {
                // Пустая группа: терять нечего, распускаем сразу.
                GroupManager.Unlink(g);
                return;
            }
            var sel = SelectionManager.Instance;
            if (sel != null) sel.SelectOnly(members);
            if (GroupMenuUI.Instance != null) GroupMenuUI.Instance.Open(members[0]);
        }

        private void ToggleCollapse(int groupId)
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

        // ── Группы: создание и перемещение выделения ─────────────────────

        private void CreateEmptyGroup()
        {
            int n = 1;
            foreach (var g in GroupManager.AllGroups()) n++;
            GroupManager.Create($"Группа {n}");
            // Refresh придёт по событию GroupManager.Changed.
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

            _suppressDropdown = true;
            _moveDropdown.ClearOptions();
            _moveDropdown.AddOptions(options);
            _moveDropdown.SetValueWithoutNotify(0);
            _suppressDropdown = false;
        }

        private void OnMoveDropdown(int index)
        {
            if (_suppressDropdown || _moveDropdown == null || index == 0) return;

            var sel = SelectionManager.Instance;
            var selected = sel != null ? new List<KitchenElement>(sel.SelectedElements) : new List<KitchenElement>();

            LinkGroup? target = null;               // index 1 = «вне групп»
            int lastIndex = _dropdownGroups.Count + 2; // 0 плейсхолдер, 1 вне групп, потом группы
            if (index == lastIndex)
                target = GroupManager.Create($"Группа {_dropdownGroups.Count + 1}");
            else if (index >= 2)
                target = _dropdownGroups[index - 2];

            foreach (var e in selected)
                if (e != null) GroupManager.MoveTo(e, target);

            // Сбросить на плейсхолдер (Refresh придёт по событию Changed; если
            // выделение пустое и события не было — обновим сами).
            _suppressDropdown = true;
            _moveDropdown.SetValueWithoutNotify(0);
            _suppressDropdown = false;
            if (selected.Count == 0) Refresh();
        }
    }

    internal static class HierarchyRectExtensions
    {
        /// <summary>Якорь + позиция одной строкой (для читаемости Build).</summary>
        public static void SetAnchor(this RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }
    }
}
