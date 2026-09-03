using UnityEngine;

namespace KitchenDesigner.Core.UI
{
    public static class UIStyle
    {
        public static readonly Color Panel = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        public static readonly Color Surface = new Color(0.22f, 0.24f, 0.30f, 1f);
        public static readonly Color Field = new Color(0.08f, 0.08f, 0.10f, 1f);
        public static readonly Color Accent = new Color(0.30f, 0.50f, 0.75f, 1f);
        public static readonly Color Danger = new Color(0.62f, 0.20f, 0.20f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.92f, 0.92f, 1f);
        public static readonly Color TextSecondary = new Color(0.60f, 0.62f, 0.66f, 1f);
        public static readonly Color TextDisabled = new Color(0.55f, 0.55f, 0.55f, 1f);
        public static readonly Color HighlightChanged = new Color(1f, 0.84f, 0.0f, 1f);
        public static readonly Color HighlightError = new Color(0.90f, 0.25f, 0.25f, 1f);
        public static readonly Color HighlightWarning = new Color(1f, 0.55f, 0.1f, 1f);
        public static readonly Color HighlightOk = new Color(0.45f, 0.85f, 0.45f, 1f);
        public static readonly Color SurfaceActive = new Color(0.28f, 0.33f, 0.42f, 1f);
        public static readonly Color SurfaceInactive = new Color(0.15f, 0.16f, 0.20f, 1f);
        public static readonly Color RowSelected = new Color(0.45f, 0.40f, 0.15f, 1f);
        public static readonly Color Separator = new Color(0.35f, 0.37f, 0.42f, 1f);
        public static readonly Color ModalBackdrop = new Color(0f, 0f, 0f, 0.55f);
        public static readonly Color ScrollTrack = new Color(0.10f, 0.10f, 0.13f, 0.6f);
        public static readonly Color ScrollHandle = new Color(0.38f, 0.40f, 0.46f, 1f);
        public static readonly Color RaycastOnly = new Color(0f, 0f, 0f, 0.01f);

        public static readonly Color EdgePresent = new Color(0.30f, 0.75f, 0.35f, 1f);
        public static readonly Color EdgeAbsent = new Color(0.72f, 0.74f, 0.78f, 1f);
        public static readonly Color EdgeBoard = new Color(0.18f, 0.19f, 0.22f, 1f);
        public static readonly Color EdgeManualSide = new Color(0.95f, 0.80f, 0.25f, 1f);
        public static readonly Color EdgeHighlight3D = new Color(1f, 0.15f, 0.1f, 0.8f);

        public static readonly Color MeasureHint = new Color(1f, 0.35f, 0.75f, 1f);
        public static readonly Color MeasureLine = new Color(0.95f, 0.15f, 0.15f, 1f);
        public static readonly Color MeasureHover = new Color(1f, 0.95f, 0.55f, 1f);
        public static readonly Color MeasureSelected = new Color(1f, 0.85f, 0.1f, 0.25f);

        public const float HitTarget = 32f;
        public const float GapInner = 8f;
        public const float GapSection = 16f;
        public const float WindowPad = 12f;
        public const float CloseBtnSize = 32f;
        public const float CloseBtnInset = 8f;
        public const float DropdownItemMinH = 24f;

        public const int FontTitle = 20;
        public const int FontWindowTitle = 24;
        public const int FontSection = 15;
        public const int FontBody = 16;
        public const int FontSmall = 14;

        public const string GlyphClose = "×";
        public const string GlyphConfirm = "?!";
        public const string GlyphCollapsed = "►";
        public const string GlyphExpanded = "▼";
        public const string GlyphDropdown = "▼";
        public const string GlyphAngle = "∟";
    }
}
