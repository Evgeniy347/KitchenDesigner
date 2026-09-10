using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SidebarUI : MonoBehaviour
    {
        public const float ScrollGutterW = 16f;
        public const float ExpandedW = SidebarLayout.DockW;
        public const float CollapsedW = 52f;
        public const float TopOffsetUnderToolbar = 52f;
        public const float BottomMargin = 42f;
        public const float TopStripH = SidebarLayout.TopStripH;
        public const float ScrollBarW = 8f;

        public const float TileCaptionH = SidebarLayout.TileH - SidebarLayout.TileImageH;
        public const int TileFont = UIStyle.FontSmall;

        private const string RoomModeHint = "Доступно в режиме «Помещение»";
        private static readonly Color ActiveToggleColor = new Color(0.30f, 0.55f, 0.34f, 1f);

        private RectTransform? _panel;
        private ScrollArea? _full;
        private ScrollArea? _mini;
        private GameObject? _fullRoot;
        private GameObject? _miniRoot;
        private TMP_Text? _collapseLabel;
        private Image? _pinBg;
        private Image? _dockModeBg;
        private Image? _dockModeIcon;
        private TMP_InputField? _searchField;
        private TMP_Text? _searchHint;

        private bool _expanded = true;
        private bool _pinned = true;
        private bool _focusSearchNextFrame;
        private SidebarDockChoice _dockChoice = SidebarDockChoice.Unset;

        private readonly List<RenderTexture> _ownedTextures = new List<RenderTexture>();
        private readonly List<TileUI> _pendingThumbnails = new List<TileUI>();

        internal class TileUI
        {
            public RectTransform rect = null!;
            public Button button = null!;
            public TMP_Text caption = null!;
            public RawImage thumb = null!;
            public Image stub = null!;
            public RectTransform presetRow = null!;
            public List<SidebarCatalog.Item> presets = null!;
            public int selected;
            public string title = "";
            public string groupTitle = "";
            public bool thumbnailReady;
        }

        private class GroupUI
        {
            public string title = "";
            public RectTransform? header;
            public TMP_Text? headerLabel;
            public readonly List<TileUI> tiles = new List<TileUI>();
            public bool open;
        }

        private readonly List<GroupUI> _groups = new List<GroupUI>();
        private readonly List<SidebarTileGroupMetrics> _metrics = new List<SidebarTileGroupMetrics>();
        private readonly List<SidebarTileRow> _rows = new List<SidebarTileRow>();
        private readonly List<List<TileUI>> _visibleTilesByGroup = new List<List<TileUI>>();

        private class ModeStyledTile
        {
            public Button button = null!;
            public EditModeManager.Category cat;
        }
        private readonly List<ModeStyledTile> _modeStyledTiles = new List<ModeStyledTile>();

        public static void ResetLastUsedGroupForTests() => SidebarLastGroupPreference.ClearForTests();

        public void SetExpandedForTests(bool expanded) => SetExpanded(expanded);

        internal void SetDockChoiceForTests(SidebarDockChoice choice)
        {
            _dockChoice = choice;
            ApplyState();
        }

        internal void CollapseAfterSpawnForTests() => CollapseAfterSpawnIfDockModeSaysSo();

        internal void SimulateSlashShortcutForTests()
        {
            SetExpanded(true);
            _focusSearchNextFrame = true;
        }

        internal void ShowAllPresetRowsForTests()
        {
            foreach (var gu in _groups)
                foreach (var tile in gu.tiles)
                    if (tile.presets.Count > 1) tile.presetRow.gameObject.SetActive(true);
        }

        internal void ExpandAllGroupsForTests()
        {
            SetExpanded(true);
            foreach (var gu in _groups)
            {
                gu.open = true;
                SetGroupHeaderText(gu, gu.title);
            }
            RelayoutFull();
        }

        internal int PendingThumbnailCountForTests => _pendingThumbnails.Count;

        internal SidebarCatalog.Item ItemToSpawnForTests(string groupTitle, string tileTitle)
        {
            var gu = _groups.Find(g => g.title == groupTitle);
            var tile = gu.tiles.Find(t => t.title == tileTitle);
            return tile.presets[SpawnPresetIndex(tile)];
        }

        public void Build(Transform canvas)
        {
            _dockChoice = SidebarDockPreference.Load();
            _expanded = !SidebarDockBudget.CollapsesAfterSpawn(_dockChoice, Screen.height);

            var panel = UIFactory.CreatePanel("Sidebar", canvas, Vector2.zero,
                new Vector2(ExpandedW, 0f));
            _panel = panel.rectTransform;
            StretchUnderToolbar(_panel);

            var collapseBtn = UIFactory.CreateButton("SbCollapse", _panel, "«",
                new Vector2(4, -4), new Vector2(36, 28), () => SetExpanded(!_expanded));
            UIFactory.AnchorTopLeft(collapseBtn.GetComponent<RectTransform>());
            collapseBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(4, -4);
            _collapseLabel = collapseBtn.GetComponentInChildren<TMP_Text>();

            var pinBtn = UIFactory.CreateIconButton("SbPin", _panel, IconFactory.Pin,
                new Vector2(46, -4), new Vector2(28, 28), TogglePin);
            UIFactory.AnchorTopLeft(pinBtn.GetComponent<RectTransform>());
            pinBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(46, -4);
            _pinBg = pinBtn.GetComponent<Image>();

            var dockModeBtn = UIFactory.CreateIconButton("SbDockMode", _panel, IconFactory.DockExpanded,
                new Vector2(78, -4), new Vector2(28, 28), ToggleDockMode);
            UIFactory.AnchorTopLeft(dockModeBtn.GetComponent<RectTransform>());
            dockModeBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(78, -4);
            _dockModeBg = dockModeBtn.GetComponent<Image>();
            var dockModeIconRect = dockModeBtn.transform.Find("SbDockMode_Icon");
            _dockModeIcon = dockModeIconRect != null ? dockModeIconRect.GetComponent<Image>() : null;
            TooltipUI.Attach(dockModeBtn.gameObject,
                "Раскрытый док остаётся открытым после установки детали.\n"
                + "Рейка иконок сворачивается после установки.\n"
                + "Клик переключает и запоминается для следующего запуска.");

            BuildSearchField(_panel);

            _full = ScrollArea.Create("SbFull", _panel, ScrollBarW);
            FillBelowSearch(_full.Viewport);
            _fullRoot = _full.Viewport.gameObject;
            BuildFull();

            _mini = ScrollArea.Create("SbMini", _panel, 0f);
            FillBelowStrip(_mini.Viewport);
            _miniRoot = _mini.Viewport.gameObject;
            BuildMini();

            ApplyState();

            EditModeManager.Changed += ApplyModeStyling;
            ApplyModeStyling();
        }

        private void OnDestroy()
        {
            EditModeManager.Changed -= ApplyModeStyling;
            foreach (var tex in _ownedTextures)
                if (tex != null) tex.Release();
        }

        private static void StretchUnderToolbar(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(0f, BottomMargin);
            rt.offsetMax = new Vector2(ExpandedW, -TopOffsetUnderToolbar);
        }

        private static void FillBelowStrip(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -TopStripH);
        }

        private static void FillBelowSearch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -(TopStripH + SidebarLayout.SearchBandH));
        }

        private void BuildSearchField(RectTransform parent)
        {
            _searchField = UIFactory.CreateInputField("SbSearch", parent, "",
                Vector2.zero, new Vector2(ExpandedW - 2f * SidebarLayout.Pad - ScrollBarW,
                    SidebarLayout.SearchBandH - 6f));
            var rt = _searchField.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(SidebarLayout.Pad, -TopStripH - 2f);
            _searchField.onValueChanged.AddListener(_ => RelayoutFull());

            var hint = UIFactory.CreateLabel("SbSearchHint", _searchField.transform, "Поиск…",
                UIStyle.FontSmall, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft);
            hint.color = UIStyle.TextSecondary;
            hint.raycastTarget = false;
            var hRt = hint.rectTransform;
            hRt.anchorMin = Vector2.zero; hRt.anchorMax = Vector2.one;
            hRt.offsetMin = new Vector2(8f, 0f); hRt.offsetMax = Vector2.zero;
            _searchHint = hint;
        }

        private void BuildFull()
        {
            var catalog = SidebarCatalog.Build();
            string? openTitle = SidebarLastGroupPreference.Load();
            bool anyOpen = openTitle != null && catalog.Any(g => g.title == openTitle);
            if (!anyOpen) openTitle = catalog.Count > 0 ? catalog[0].title : null;

            foreach (var g in catalog)
            {
                var gu = new GroupUI { title = g.title, open = g.title == openTitle };
                var header = UIFactory.CreateButton("SbGrp_" + g.title, _full!.Content, g.title,
                    Vector2.zero, new Vector2(SidebarLayout.DockW - 2f * SidebarLayout.Pad - ScrollBarW,
                        SidebarLayout.HeaderH), () => ToggleGroup(gu));
                UIFactory.AnchorTopLeft(header.GetComponent<RectTransform>());
                gu.header = header.GetComponent<RectTransform>();

                var headerLabel = header.GetComponentInChildren<TMP_Text>();
                if (headerLabel != null)
                {
                    headerLabel.alignment = TextAlignmentOptions.Left;
                    headerLabel.margin = new Vector4(SidebarLayout.Pad, 2f, SidebarLayout.Pad, 2f);
                    headerLabel.enableWordWrapping = false;
                    headerLabel.overflowMode = TextOverflowModes.Ellipsis;
                    gu.headerLabel = headerLabel;
                    SetGroupHeaderText(gu, g.title);
                }

                foreach (var tile in SidebarTileBuilder.BuildTiles(g.items))
                    gu.tiles.Add(BuildTile(g.title, tile));

                _groups.Add(gu);
            }
            RelayoutFull();
        }

        private TileUI BuildTile(string groupTitle, SidebarTileBuilder.Tile tile)
        {
            var btn = UIFactory.CreateButton("SbTile_" + groupTitle + "_" + tile.title,
                _full!.Content, "", Vector2.zero, new Vector2(SidebarLayout.TileW, SidebarLayout.TileH),
                null);
            UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());

            var tileUi = new TileUI
            {
                rect = btn.GetComponent<RectTransform>(),
                button = btn,
                presets = tile.presets,
                title = tile.title,
                groupTitle = groupTitle,
                selected = InitialPresetIndex(tile),
            };
            btn.onClick.AddListener(() => SpawnSelected(tileUi));

            var thumbGo = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage));
            thumbGo.transform.SetParent(btn.transform, false);
            var thumbRect = (RectTransform)thumbGo.transform;
            thumbRect.anchorMin = new Vector2(0f, 1f);
            thumbRect.anchorMax = new Vector2(1f, 1f);
            thumbRect.pivot = new Vector2(0.5f, 1f);
            thumbRect.offsetMin = new Vector2(0f, -SidebarLayout.TileImageH);
            thumbRect.offsetMax = Vector2.zero;
            var raw = thumbGo.GetComponent<RawImage>();
            raw.raycastTarget = false;
            tileUi.thumb = raw;

            var stubGo = new GameObject("Stub", typeof(RectTransform), typeof(Image));
            stubGo.transform.SetParent(btn.transform, false);
            var stubRect = (RectTransform)stubGo.transform;
            stubRect.anchorMin = thumbRect.anchorMin;
            stubRect.anchorMax = thumbRect.anchorMax;
            stubRect.pivot = thumbRect.pivot;
            stubRect.offsetMin = thumbRect.offsetMin;
            stubRect.offsetMax = thumbRect.offsetMax;
            var stub = stubGo.GetComponent<Image>();
            stub.sprite = IconFactory.TileStub;
            stub.color = UIStyle.TextSecondary;
            stub.preserveAspect = true;
            stub.raycastTarget = false;
            tileUi.stub = stub;

            var caption = btn.GetComponentInChildren<TMP_Text>();
            caption.fontSize = TileFont;
            caption.alignment = TextAlignmentOptions.Top;
            caption.enableWordWrapping = true;
            caption.overflowMode = TextOverflowModes.Truncate;
            caption.margin = new Vector4(4f, 2f, 4f, 2f);
            var cRt = caption.rectTransform;
            cRt.anchorMin = new Vector2(0f, 0f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.offsetMin = Vector2.zero;
            cRt.offsetMax = new Vector2(0f, -SidebarLayout.TileImageH);
            tileUi.caption = caption;
            caption.text = tile.presets.Count > 1 ? tile.title : tile.presets[0].DisplayName;

            var presetRowGo = new GameObject("Presets", typeof(RectTransform));
            presetRowGo.transform.SetParent(btn.transform, false);
            var presetRowRect = (RectTransform)presetRowGo.transform;
            presetRowRect.anchorMin = new Vector2(0f, 1f);
            presetRowRect.anchorMax = new Vector2(1f, 1f);
            presetRowRect.pivot = new Vector2(0f, 1f);
            float presetRowsH = SidebarLayout.PresetRowsHeight(tile.presets.Count);
            presetRowRect.offsetMin = new Vector2(SidebarLayout.PresetRowInset, -(2f + presetRowsH));
            presetRowRect.offsetMax = new Vector2(-SidebarLayout.PresetRowInset, -2f);
            presetRowGo.SetActive(false);
            tileUi.presetRow = presetRowRect;

            if (tile.presets.Count > 1)
                BuildPresetDots(tileUi);

            var cat = ItemCategory(tile.presets[0]);
            _modeStyledTiles.Add(new ModeStyledTile { button = btn, cat = cat });

            AddHoverEntries(btn.gameObject,
                () => tileUi.presetRow.gameObject.SetActive(tile.presets.Count > 1),
                () => tileUi.presetRow.gameObject.SetActive(false));

            TooltipUI.Attach(btn.gameObject, () => TileTooltipText(tileUi));
            return tileUi;
        }

        private static int InitialPresetIndex(SidebarTileBuilder.Tile tile)
        {
            string? lastUsed = SidebarPresetPreference.Load(tile.title);
            if (lastUsed == null) return 0;
            int idx = tile.presets.FindIndex(p => p.name == lastUsed);
            return idx >= 0 ? idx : 0;
        }

        private void BuildPresetDots(TileUI tileUi)
        {
            for (int i = 0; i < tileUi.presets.Count; i++)
            {
                int presetIndex = i;
                var dotGo = new GameObject("PresetDot_" + i, typeof(RectTransform), typeof(Image));
                dotGo.transform.SetParent(tileUi.presetRow, false);
                var dotRect = (RectTransform)dotGo.transform;
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 1f);
                dotRect.pivot = new Vector2(0f, 1f);
                dotRect.sizeDelta = new Vector2(SidebarLayout.PresetDotSize, SidebarLayout.PresetDotSize);
                dotRect.anchoredPosition = SidebarLayout.PresetDotPosition(i);
                var dotImg = dotGo.GetComponent<Image>();
                dotImg.color = presetIndex == tileUi.selected ? UIStyle.SurfaceActive : UIStyle.SurfaceInactive;

                var dotBtn = dotGo.AddComponent<UnityEngine.UI.Button>();
                dotBtn.targetGraphic = dotImg;
                dotBtn.onClick.AddListener(() => SelectPreset(tileUi, presetIndex));

                var dotLabel = UIFactory.CreateLabel("PresetDotLabel", dotGo.transform,
                    (presetIndex + 1).ToString(), UIStyle.FontSmall, Vector2.zero, Vector2.zero,
                    TextAnchor.MiddleCenter);
                dotLabel.raycastTarget = false;
                var dotLabelRect = dotLabel.rectTransform;
                dotLabelRect.anchorMin = Vector2.zero;
                dotLabelRect.anchorMax = Vector2.one;
                dotLabelRect.offsetMin = Vector2.zero;
                dotLabelRect.offsetMax = Vector2.zero;

                TooltipUI.Attach(dotGo, tileUi.presets[presetIndex].name);
            }
        }

        private void SelectPreset(TileUI tileUi, int presetIndex)
        {
            tileUi.selected = presetIndex;
            for (int i = 0; i < tileUi.presetRow.childCount; i++)
            {
                var img = tileUi.presetRow.GetChild(i).GetComponent<Image>();
                if (img != null) img.color = i == presetIndex ? UIStyle.SurfaceActive : UIStyle.SurfaceInactive;
            }
            SidebarPresetPreference.Save(tileUi.title, tileUi.presets[presetIndex].name);
            TooltipUI.Hide();

            tileUi.thumbnailReady = false;
            tileUi.stub.gameObject.SetActive(true);
            RequestThumbnail(tileUi);
        }

        private static void AddHoverEntries(GameObject go, System.Action onEnter, System.Action onExit)
        {
            var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            AddEntry(trigger, EventTriggerType.PointerEnter, onEnter);
            AddEntry(trigger, EventTriggerType.PointerExit, onExit);
        }

        private static void AddEntry(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static void SetGroupHeaderText(GroupUI gu, string title)
        {
            if (gu.headerLabel == null) return;
            string glyph = gu.open ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed;
            gu.headerLabel.text = $"{glyph}  {title}";
        }

        internal static string TileTooltipText(TileUI tile)
        {
            var item = tile.presets[tile.selected];
            return ItemTooltipText(item, ItemCategory(item));
        }

        internal static string ItemTooltipText(SidebarCatalog.Item it, EditModeManager.Category cat)
        {
            string text = $"{it.name}\n{FormatDims(it.dims)}";
            return cat == EditModeManager.Category.Room ? $"{text}\n{RoomModeHint}" : text;
        }

        public static string FormatDims(Vector3Int dims) => $"{dims.x} × {dims.y} × {dims.z}";

        internal static EditModeManager.Category ItemCategory(SidebarCatalog.Item it)
        {
            if (it.name == EditModeManager.KorobName) return EditModeManager.Category.Always;
            return it.Category;
        }

        private void ApplyModeStyling()
        {
            foreach (var s in _modeStyledTiles)
                s.button.interactable = EditModeManager.IsCategoryActive(s.cat);
        }

        private bool MatchesSearch(TileUI tile, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (tile.title.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            foreach (var preset in tile.presets)
                if (preset.name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static int? FindMatchingPresetIndex(TileUI tile, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return null;
            for (int i = 0; i < tile.presets.Count; i++)
                if (tile.presets[i].name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            return null;
        }

        private int SpawnPresetIndex(TileUI tile)
        {
            string filter = _searchField != null ? _searchField.text.Trim() : "";
            if (string.IsNullOrEmpty(filter)) return tile.selected;
            if (tile.title.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return tile.selected;
            return FindMatchingPresetIndex(tile, filter) ?? tile.selected;
        }

        private void RelayoutFull()
        {
            string filter = _searchField != null ? _searchField.text.Trim() : "";
            if (_searchHint != null) _searchHint.gameObject.SetActive(filter.Length == 0);
            bool searching = filter.Length > 0;

            _metrics.Clear();
            _visibleTilesByGroup.Clear();

            foreach (var gu in _groups)
                foreach (var t in gu.tiles)
                    t.rect.gameObject.SetActive(false);

            foreach (var gu in _groups)
            {
                var visible = gu.tiles.Where(t => MatchesSearch(t, filter)).ToList();
                _visibleTilesByGroup.Add(visible);
                bool open = searching ? visible.Count > 0 : gu.open;
                _metrics.Add(new SidebarTileGroupMetrics(open, visible.Count));
            }

            float height = SidebarLayout.PlaceTiles(_metrics, _rows);

            foreach (var row in _rows)
            {
                var gu = _groups[row.Group];
                if (row.IsHeader)
                {
                    gu.header!.anchoredPosition = row.Position;
                    gu.header.gameObject.SetActive(!searching || _visibleTilesByGroup[row.Group].Count > 0);
                    continue;
                }

                var visible = _visibleTilesByGroup[row.Group];
                if (row.Tile >= visible.Count) continue;
                var tile = visible[row.Tile];
                tile.rect.gameObject.SetActive(row.Visible);
                if (row.Visible)
                {
                    tile.rect.anchoredPosition = row.Position;
                    RequestThumbnail(tile);
                }
            }

            _full!.ContentHeight = height;
        }

        private void RequestThumbnail(TileUI tile)
        {
            if (!_expanded) return;
            if (tile.thumbnailReady) return;
            if (_pendingThumbnails.Contains(tile)) return;
            _pendingThumbnails.Add(tile);
        }

        private void ReleaseThumbnail(TileUI tile)
        {
            if (tile.thumb.texture is RenderTexture oldTexture)
            {
                _ownedTextures.Remove(oldTexture);
                oldTexture.Release();
            }
        }

        private void Update()
        {
            using var _ = PerfMarkers.SidebarUpdate.Auto();
            CollapseIfClickedOutsideWhileUnpinned();
            ProcessThumbnailQueue();
            HandleKeyboardShortcuts();
        }

        private const int ThumbnailsPerFrame = 2;

        private void ProcessThumbnailQueue()
        {
            int budget = ThumbnailsPerFrame;
            while (budget > 0 && _pendingThumbnails.Count > 0)
            {
                var tile = _pendingThumbnails[0];
                _pendingThumbnails.RemoveAt(0);
                budget--;
                if (tile.thumbnailReady) continue;

                var spawn = SidebarThumbnailSpawns.For(tile.presets[tile.selected]);
                if (spawn == null) continue;

                ReleaseThumbnail(tile);

                var texture = ThumbnailRenderer.Render(spawn, ThumbnailRenderer.DefaultSize);
                _ownedTextures.Add(texture);
                tile.thumb.texture = texture;
                tile.stub.gameObject.SetActive(false);
                tile.thumbnailReady = true;
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (_focusSearchNextFrame)
            {
                _focusSearchNextFrame = false;
                FocusSearchField();
                return;
            }

            if (IsTypingElsewhere()) return;
            if (!Input.GetKeyDown(KeyCode.Slash)) return;

            SetExpanded(true);
            _focusSearchNextFrame = true;
        }

        private void FocusSearchField()
        {
            if (_searchField == null) return;
            _searchField.Select();
            _searchField.ActivateInputField();
        }

        private bool IsTypingElsewhere()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var field = selected.GetComponent<TMP_InputField>();
            return field != null && field.isFocused;
        }

        private void BuildMini()
        {
            var groups = SidebarCatalog.Build();
            for (int i = 0; i < groups.Count; i++)
            {
                int index = i;
                var btn = UIFactory.CreateIconButton("SbMini_" + groups[i].title, _mini!.Content,
                    groups[i].icon, Vector2.zero,
                    new Vector2(CollapsedW - 2f * SidebarLayout.MiniPad, SidebarLayout.MiniButtonH),
                    () => OpenGroup(index));
                var rt = btn.GetComponent<RectTransform>();
                UIFactory.AnchorTopLeft(rt);
                rt.anchoredPosition = new Vector2(SidebarLayout.MiniPad, SidebarLayout.MiniItemY(i));
                TooltipUI.Attach(btn.gameObject, groups[i].title);
            }
            _mini!.ContentHeight = SidebarLayout.MiniContentHeight(groups.Count);
        }

        private void ToggleGroup(GroupUI gu)
        {
            gu.open = !gu.open;
            if (gu.open) SidebarLastGroupPreference.Save(gu.title);
            SetGroupHeaderText(gu, gu.title);
            RelayoutFull();
        }

        private void OpenGroup(int index)
        {
            SetExpanded(true);
            if (index < 0 || index >= _groups.Count) return;

            var gu = _groups[index];
            gu.open = true;
            SidebarLastGroupPreference.Save(gu.title);
            SetGroupHeaderText(gu, gu.title);
            RelayoutFull();
        }

        private void SpawnSelected(TileUI tile)
        {
            if (UIManager.Instance == null) return;
            int presetIndex = SpawnPresetIndex(tile);
            var item = tile.presets[presetIndex];
            if (presetIndex != tile.selected) SelectPreset(tile, presetIndex);
            SidebarSpawnRouter.Route(item, UIManager.Instance.Spawner);
            SidebarLastGroupPreference.Save(tile.groupTitle);

            CollapseAfterSpawnIfDockModeSaysSo();
        }

        private void CollapseAfterSpawnIfDockModeSaysSo()
        {
            if (SidebarDockBudget.CollapsesAfterSpawn(_dockChoice, Screen.height)) SetExpanded(false);
        }

        private void SetExpanded(bool expanded)
        {
            bool wasExpanded = _expanded;
            _expanded = expanded;
            ApplyState();
            if (expanded && !wasExpanded) RelayoutFull();
        }

        private void TogglePin()
        {
            _pinned = !_pinned;
            ApplyState();
        }

        private void ToggleDockMode()
        {
            bool collapsesAfterSpawn = SidebarDockBudget.CollapsesAfterSpawn(_dockChoice, Screen.height);
            _dockChoice = collapsesAfterSpawn ? SidebarDockChoice.Docked : SidebarDockChoice.Rail;
            SidebarDockPreference.Save(_dockChoice);
            SetExpanded(_dockChoice == SidebarDockChoice.Docked);
        }

        private void ApplyState()
        {
            var right = _panel!.offsetMax;
            right.x = _expanded ? ExpandedW : CollapsedW;
            _panel.offsetMax = right;

            _fullRoot!.SetActive(_expanded);
            _miniRoot!.SetActive(!_expanded);
            if (_searchField != null) _searchField.gameObject.SetActive(_expanded);
            if (_collapseLabel != null) _collapseLabel.text = _expanded ? "«" : "»";
            if (_pinBg != null)
            {
                _pinBg.gameObject.SetActive(_expanded);
                _pinBg.color = _pinned ? ActiveToggleColor : UIFactory.ButtonColor;
            }
            if (_dockModeBg != null)
            {
                _dockModeBg.gameObject.SetActive(_expanded);
                _dockModeBg.color = _dockChoice != SidebarDockChoice.Unset
                    ? ActiveToggleColor : UIFactory.ButtonColor;
            }
            if (_dockModeIcon != null)
            {
                bool collapsesAfterSpawn = SidebarDockBudget.CollapsesAfterSpawn(_dockChoice, Screen.height);
                _dockModeIcon.sprite = collapsesAfterSpawn ? IconFactory.DockRail : IconFactory.DockExpanded;
            }
        }

        private void CollapseIfClickedOutsideWhileUnpinned()
        {
            if (_pinned || !_expanded) return;
            if (Input.GetMouseButtonDown(0) &&
                !RectTransformUtility.RectangleContainsScreenPoint(_panel!, Input.mousePosition, null))
                SetExpanded(false);
        }
    }
}
