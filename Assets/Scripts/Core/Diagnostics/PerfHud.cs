using System;
using UnityEngine;

namespace KitchenDesigner.Core
{
    public class PerfHud : MonoBehaviour
    {
        private const int Width = 460;
        private const int Margin = 8;
        internal const string MonospaceFontName = "Consolas";

        internal static float ToolbarBottomY = DefaultToolbarBottomY;
        internal const float DefaultToolbarBottomY = 48f;

        internal static PerfHudStyle? Style;
        internal static Func<float>? RightLimit;

        private GUIStyle? _style;
        private PerfHudStyle? _builtFor;
        private Texture2D? _background;

        internal static bool ShouldPaint(EventType currentEvent, bool monitorEnabled, string hudText) =>
            monitorEnabled
            && currentEvent == EventType.Repaint
            && !string.IsNullOrEmpty(hudText);

        internal static Rect ComputeRect(float screenWidth, float toolbarBottomY, float height,
            float rightLimit = float.MaxValue)
        {
            float right = Mathf.Min(screenWidth, rightLimit) - Margin;
            float width = Mathf.Min(Width, Mathf.Max(0f, right - Margin));
            float x = Mathf.Max(Margin, right - width);
            float y = toolbarBottomY + Margin;
            return new Rect(x, y, width, height);
        }

        private void OnGUI()
        {
            var monitor = PerfMonitor.Instance;
            if (monitor == null) return;
            if (!ShouldPaint(Event.current.type, PerfMonitor.Enabled, monitor.HudText)) return;

            Paint(monitor, Screen.width);
        }

        internal void Paint(PerfMonitor monitor, float screenWidth)
        {
            var style = EnsureStyle();
            var theme = _builtFor!;
            var lines = monitor.HudLines;
            float lineHeight = style.lineHeight;
            float limit = RightLimit != null ? RightLimit() : float.MaxValue;
            var rect = ComputeRect(screenWidth, ToolbarBottomY, lines.Count * lineHeight + 2 * theme.PadY, limit);

            GUI.DrawTexture(rect, _background!);
            for (int i = 0; i < lines.Count; i++)
            {
                style.normal.textColor = theme.ColorOf(lines[i].Kind);
                var row = new Rect(rect.x + theme.PadX, rect.y + theme.PadY + i * lineHeight,
                    rect.width - 2 * theme.PadX, lineHeight);
                GUI.Label(row, lines[i].Text, style);
            }
        }

        internal GUIStyle EnsureStyle()
        {
            var theme = Style ?? PerfHudStyle.Fallback;
            if (_style != null && ReferenceEquals(_builtFor, theme)) return _style;

            DestroyNow.The(_background);
            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, theme.Panel);
            _background.Apply();

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = theme.FontSize,
                richText = false,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(0, 0, 0, 0),
                wordWrap = false,
            };
            style.normal.textColor = theme.Text;

            var monospace = Font.CreateDynamicFontFromOSFont(MonospaceFontName, theme.FontSize);
            if (monospace != null) style.font = monospace;

            _style = style;
            _builtFor = theme;
            return style;
        }

        private void OnDestroy()
        {
            DestroyNow.The(_background);
        }
    }
}
