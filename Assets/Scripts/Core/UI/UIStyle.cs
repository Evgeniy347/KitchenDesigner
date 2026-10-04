using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class UIStyle
    {
        public static readonly Color Panel = Hex(0x1E1F24);
        public static readonly Color NavBg = Hex(0x191A1F);
        public static readonly Color Surface = Hex(0x33363F);
        public static readonly Color SurfaceHover = Hex(0x3D404B);
        public static readonly Color SurfaceActive = Hex(0x484C59);
        public static readonly Color SurfaceInactive = Hex(0x2A2C33);
        public static readonly Color Field = Hex(0x131418);
        public static readonly Color FieldStroke = Hex(0x6B6F7C);
        public static readonly Color FieldStrokeHover = Hex(0x858A97);
        public static readonly Color FocusRing = Hex(0x5B93DB);
        public static readonly Color Accent = Hex(0x3D72B8);
        public static readonly Color AccentHover = Hex(0x3568AA);
        public static readonly Color AccentText = Hex(0x8AB4EA);
        public static readonly Color AccentSubtle = Hex(0x25344A);
        public static readonly Color Danger = Hex(0xA83232);
        public static readonly Color DangerText = Hex(0xF07A7A);
        public static readonly Color TextOnAccent = Hex(0xFFFFFF);
        public static readonly Color Text = Hex(0xEBEBEB);
        public static readonly Color TextSecondary = Hex(0xA3A7B0);
        public static readonly Color TextDisabled = Hex(0x8C8C8C);
        public static readonly Color TextError = Hex(0xF06A6A);
        public static readonly Color TextWarning = Hex(0xF0A04B);
        public static readonly Color TextSuccess = Hex(0x7AD67A);
        public static readonly Color RowHover = Hex(0x262830);
        public static readonly Color RowSelected = Hex(0x4A4322);
        public static readonly Color SelectionBar = Hex(0xE0B83A);
        public static readonly Color RowError = Hex(0x52292A);
        public static readonly Color Divider = Hex(0x2C2E35);
        public static readonly Color Separator = Hex(0x3A3D47);
        public static readonly Color ModalBackdrop = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color WindowShadow = new Color(0f, 0f, 0f, 0.45f);
        public static readonly Color ScrollTrack = new Color(0.10f, 0.10f, 0.13f, 0.6f);
        public static readonly Color ScrollHandle = Hex(0x4A4E5A);
        public static readonly Color RaycastOnly = new Color(0f, 0f, 0f, 0.01f);
        public static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        public static readonly Color TintHidden = new Color(1f, 1f, 1f, 0f);
        public static readonly Color TintPressed = new Color(0.85f, 0.85f, 0.85f, 1f);

        public static readonly Color DisabledTint = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        public static readonly Color NoTint = Color.white;
        public static readonly Color HighlightChanged = new Color(1f, 0.84f, 0.0f, 1f);
        public static readonly Color HighlightError = new Color(0.90f, 0.25f, 0.25f, 1f);
        public static readonly Color HighlightWarning = new Color(1f, 0.55f, 0.1f, 1f);
        public static readonly Color HighlightOk = new Color(0.45f, 0.85f, 0.45f, 1f);

        public static readonly Color EdgePresent = new Color(0.30f, 0.75f, 0.35f, 1f);
        public static readonly Color EdgeAbsent = new Color(0.72f, 0.74f, 0.78f, 1f);
        public static readonly Color EdgeBoard = new Color(0.18f, 0.19f, 0.22f, 1f);
        public static readonly Color EdgeForcedSide = new Color(0.95f, 0.80f, 0.25f, 1f);
        public static readonly Color EdgeSuppressedSide = new Color(0.90f, 0.31f, 0.28f, 1f);
        public static readonly Color EdgeHighlight3D = new Color(1f, 0.15f, 0.1f, 0.8f);
        public static readonly Color PreviewGhost = new Color(0.25f, 0.9f, 0.35f, 0.45f);
        public static readonly Color HoverHighlight3D = new Color(0.25f, 0.9f, 0.35f, 1f);

        public static readonly Color HintIcon = new Color(0.52f, 0.58f, 0.70f, 1f);
        public static readonly Color HintIconHover = AccentText;
        public static readonly Color HintCloud = NavBg;
        public static readonly Color HintCloudBorder = Divider;

        public static readonly Color MeasureHint = new Color(1f, 0.35f, 0.75f, 1f);
        public static readonly Color MeasureLine = new Color(0.95f, 0.15f, 0.15f, 1f);
        public static readonly Color MeasureHover = new Color(1f, 0.95f, 0.55f, 1f);
        public static readonly Color MeasureSelected = new Color(1f, 0.85f, 0.1f, 0.25f);

        public const float Space1 = 4f;
        public const float Space2 = 8f;
        public const float Space3 = 12f;
        public const float Space4 = 16f;
        public const float Space5 = 24f;
        public const float Space6 = 32f;

        public const float GapInner = Space2;
        public const float GapSection = Space4;
        public const float WindowPad = Space3;
        public const float ToolPanelPad = Space3;
        public const float DialogPad = Space4;
        public const float ModalPad = Space5;

        public const float HitTarget = 32f;
        public const float ControlH = 32f;
        public const float ControlHCompact = 28f;
        public const float RowStep = 32f;
        public const float TitleBarH = 48f;
        public const float FooterH = 56f;
        public const float FooterButtonH = ControlH;
        public const float CloseBtnSize = 32f;
        public const float CloseBtnInset = 8f;
        public const float WindowTitleInset = Space4;
        public const float WindowTitleCenterFromTop = CloseBtnInset + CloseBtnSize * 0.5f;
        public const float DragStripHeight = 40f;
        public const float DividerPx = 1f;
        public const float SelectionBarW = 3f;
        public const float FocusRingPx = 1f;
        public const float RadiusControl = 4f;
        public const float RadiusWindow = 6f;
        public const float WindowShadowPx = 8f;

        public const float DialogW = 440f;
        public const float InspectorW = 360f;
        public const float InspectorLabelW = 136f;
        public const float InspectorColumnGap = Space3;
        public const float InspectorValueW = 188f;
        public const float SubRowIndent = Space4;
        public const float SettingsLabelW = 300f;
        public const float SettingsColumnGap = Space4;
        public const float SettingsControlW = 240f;
        public const float NumberFieldW = 120f;
        public const float NavW = 200f;
        public const float NavItemH = 32f;
        public const float HierarchyW = 300f;
        public const float LevelsW = 440f;
        public static readonly Vector2 SettingsSize = new Vector2(920f, 640f);
        public static readonly Vector2 ErrorsSize = new Vector2(940f, 560f);
        public static readonly Vector2 SpecificationSize = new Vector2(960f, 640f);
        public static readonly Vector2 LoadProjectSize = new Vector2(760f, 500f);

        public const float TableHeaderH = 32f;
        public const float TableRowH = 28f;
        public const float TableRowInteractiveH = 32f;
        public const float TableGroupRowH = 36f;
        public const float TableCellPadX = Space2;
        public const float SectionHeaderH = 20f;
        public const float SectionGapBefore = Space4;
        public const float SectionGapAfter = Space2;
        public const float ChevronW = 12f;
        public const float SwitchW = 40f;
        public const float SwitchH = 20f;
        public const float SwitchKnob = 12f;
        public const float SliderThumb = 16f;
        public const float SliderTrackH = 4f;
        public const float SliderValueW = 64f;
        public const float SegmentPad = 2f;
        public const float IconSize = 20f;
        public const float IconSizeSmall = 16f;
        public const float IconButton = 40f;
        public const float ToolbarH = 48f;
        public const float StatusBarH = 28f;
        public const float EmptyStateIcon = 28f;

        public const float UIScaleFloor = UiScale.Floor;
        public const int UIScaleUserMinPercent = UiScale.MinPercent;
        public const int UIScaleUserMaxPercent = UiScale.MaxPercent;

        public const float DropdownItemMinH = 24f;
        public const int DropdownVisibleItems = 7;
        public const float DropdownListMaxH = 320f;
        public const float DropdownScrollBarW = 8f;
        public const float HintBadgeSize = 24f;
        public const float HintBubbleMaxWidth = 320f;
        public const float HintBubblePadX = Space3;
        public const float HintBubblePadY = Space2;
        public const float HintCloudBorderPx = 1f;

        public const int FontWindowTitle = 20;
        public const int FontBody = 16;
        public const int FontSection = 14;
        public const int FontSmall = 14;
        public const int FontCaption = 13;
        public const int FontMono = 13;
        public const int FontTab = 14;
        public const int FontMin = FontCaption;

        public const string GlyphClose = "×";
        public const string GlyphConfirm = "?!";
        public const string GlyphCollapsed = "►";
        public const string GlyphExpanded = "▼";
        public const string GlyphDropdown = "▼";
        public const string GlyphAngle = "∟";
        public const string GlyphHint = "i";
        public const string GlyphDash = "—";

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
