using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KitchenDesigner.Core.UI
{
    public class SidebarUI : MonoBehaviour
    {
        public const float ScrollGutterW = 16f;
        public const float ExpandedW = 236f;
        public const float CollapsedW = 52f;
        public const float TopOffsetUnderToolbar = 52f;
        public const float BottomMargin = 42f;
        public const float TopStripH = 36f;
        public const float ScrollBarW = 8f;

        public const float ItemW = ExpandedW - 2f * SidebarLayout.Pad
            - SidebarLayout.ItemIndent - ScrollGutterW;
        public const float HeaderW = ItemW + SidebarLayout.ItemIndent;
        public const int ItemFont = UIStyle.FontSmall;

        private const float ItemPadH = 8f;
        private const int ItemMaxLines = 2;
        private const float ItemGlyphReserve = 2f;

        private RectTransform? _panel;
        private ScrollArea? _full;
        private ScrollArea? _mini;
        private GameObject? _fullRoot;
        private GameObject? _miniRoot;
        private TMP_Text? _collapseLabel;
        private Image? _pinBg;

        private bool _expanded = true;
        private bool _pinned = true;

        private class GroupUI
        {
            public string title = "";
            public RectTransform? header;
            public TMP_Text? headerLabel;
            public readonly List<RectTransform> items = new List<RectTransform>();
            public readonly List<float> itemHeights = new List<float>();
            public bool open = true;
        }
        private readonly List<GroupUI> _groups = new List<GroupUI>();
        private readonly List<SidebarGroupMetrics> _metrics = new List<SidebarGroupMetrics>();
        private readonly List<SidebarRow> _rows = new List<SidebarRow>();

        private class ModeStyledItem
        {
            public Button button = null!;
            public TMP_Text? label;
            public Color baseColor;
            public EditModeManager.Category cat;
        }
        private readonly List<ModeStyledItem> _modeStyledItems = new List<ModeStyledItem>();

        public void Build(Transform canvas)
        {
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

            _full = ScrollArea.Create("SbFull", _panel, ScrollBarW);
            FillBelowStrip(_full.Viewport);
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

        public static float ItemHeight(string name)
            => SidebarLayout.ItemHeight(ItemLines(name), ItemFont,
                DropdownItemFit.LineHeightFactor);

        public static int ItemLines(string name)
        {
            float textW = ItemW - 2f * ItemPadH - ItemGlyphReserve * ItemFont
                * DropdownItemFit.GlyphWidthFactor;
            return DropdownItemFit.LinesFor(name, textW, ItemFont, ItemMaxLines);
        }

        private void BuildFull()
        {
            foreach (var g in SidebarCatalog.Build())
            {
                var gu = new GroupUI { title = g.title };
                var header = UIFactory.CreateButton("SbGrp_" + g.title, _full!.Content, g.title,
                    Vector2.zero, new Vector2(HeaderW, SidebarLayout.HeaderH), () => ToggleGroup(gu));
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

                foreach (var it in g.items)
                {
                    float h = ItemHeight(it.name);
                    var btn = UIFactory.CreateButton("SbItem_" + g.title + "_" + it.name, _full.Content,
                        it.name, Vector2.zero, new Vector2(ItemW, h), () => Spawn(it));
                    UIFactory.AnchorTopLeft(btn.GetComponent<RectTransform>());
                    gu.items.Add(btn.GetComponent<RectTransform>());
                    gu.itemHeights.Add(h);

                    var style = new ModeStyledItem { button = btn, cat = ItemCategory(it) };
                    style.label = btn.GetComponentInChildren<TMP_Text>();
                    if (style.label != null)
                    {
                        style.label.fontSize = ItemFont;
                        style.label.alignment = TextAlignmentOptions.Left;
                        style.label.enableWordWrapping = true;
                        style.label.overflowMode = TextOverflowModes.Truncate;
                        style.label.margin = new Vector4(ItemPadH, 2f, ItemPadH, 2f);
                        style.baseColor = style.label.color;
                    }
                    TooltipUI.Attach(btn.gameObject, it.name);
                    _modeStyledItems.Add(style);
                }
                _groups.Add(gu);
            }
            RelayoutFull();
        }

        private static void SetGroupHeaderText(GroupUI gu, string title)
        {
            if (gu.headerLabel == null) return;
            string glyph = gu.open ? UIStyle.GlyphExpanded : UIStyle.GlyphCollapsed;
            gu.headerLabel.text = $"{glyph}  {title}";
        }

        internal static EditModeManager.Category ItemCategory(SidebarCatalog.Item it)
        {
            if (it.isWindow || it.isDoor) return EditModeManager.Category.Always;
            if (it.name == EditModeManager.KorobName) return EditModeManager.Category.Always;
            if (it.isWall || it.isFloor) return EditModeManager.Category.Room;
            return EditModeManager.Category.Regular;
        }

        private void ApplyModeStyling()
        {
            foreach (var s in _modeStyledItems)
            {
                bool active = EditModeManager.IsCategoryActive(s.cat);
                s.button.interactable = active;
                if (s.label != null)
                    s.label.color = active ? s.baseColor : UIStyle.TextSecondary;
            }
        }

        private void RelayoutFull()
        {
            _metrics.Clear();
            foreach (var gu in _groups)
                _metrics.Add(new SidebarGroupMetrics(gu.open, gu.itemHeights));

            float height = SidebarLayout.Place(_metrics, _rows);

            foreach (var row in _rows)
            {
                var gu = _groups[row.Group];
                if (row.IsHeader)
                {
                    gu.header!.anchoredPosition = row.Position;
                    continue;
                }

                var item = gu.items[row.Item];
                item.gameObject.SetActive(row.Visible);
                if (row.Visible) item.anchoredPosition = row.Position;
            }

            _full!.ContentHeight = height;
        }

        private void BuildMini()
        {
            var groups = SidebarCatalog.Build();
            for (int i = 0; i < groups.Count; i++)
            {
                int index = i;
                var btn = UIFactory.CreateButton("SbMini_" + groups[i].title, _mini!.Content,
                    groups[i].shortLabel, Vector2.zero,
                    new Vector2(CollapsedW - 2f * SidebarLayout.MiniPad, SidebarLayout.MiniButtonH),
                    () => OpenGroup(index));
                var rt = btn.GetComponent<RectTransform>();
                UIFactory.AnchorTopLeft(rt);
                rt.anchoredPosition = new Vector2(SidebarLayout.MiniPad, SidebarLayout.MiniItemY(i));
            }
            _mini!.ContentHeight = SidebarLayout.MiniContentHeight(groups.Count);
        }

        private void ToggleGroup(GroupUI gu)
        {
            gu.open = !gu.open;
            SetGroupHeaderText(gu, gu.title);
            RelayoutFull();
        }

        private void OpenGroup(int index)
        {
            SetExpanded(true);
            if (index >= 0 && index < _groups.Count)
            {
                var gu = _groups[index];
                gu.open = true;
                SetGroupHeaderText(gu, gu.title);
                RelayoutFull();
            }
        }

        private void Spawn(SidebarCatalog.Item item)
        {
            if (UIManager.Instance == null) return;
            SidebarSpawnRouter.Route(item, UIManager.Instance.Spawner);
        }

        private void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            ApplyState();
        }

        private void TogglePin()
        {
            _pinned = !_pinned;
            ApplyState();
        }

        private void ApplyState()
        {
            var right = _panel!.offsetMax;
            right.x = _expanded ? ExpandedW : CollapsedW;
            _panel.offsetMax = right;

            _fullRoot!.SetActive(_expanded);
            _miniRoot!.SetActive(!_expanded);
            if (_collapseLabel != null) _collapseLabel.text = _expanded ? "«" : "»";
            if (_pinBg != null)
            {
                _pinBg.gameObject.SetActive(_expanded);
                _pinBg.color = _pinned ? new Color(0.30f, 0.55f, 0.34f, 1f) : UIFactory.ButtonColor;
            }
        }

        private void Update()
        {
            using var _ = PerfMarkers.SidebarUpdate.Auto();
            CollapseIfClickedOutsideWhileUnpinned();
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
