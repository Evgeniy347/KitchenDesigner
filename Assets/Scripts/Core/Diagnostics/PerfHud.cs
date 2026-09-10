using UnityEngine;

namespace KitchenDesigner.Core
{
    public class PerfHud : MonoBehaviour
    {
        private const int Width = 460;
        private const int Margin = 8;
        private const int FontSize = 13;
        internal const string MonospaceFontName = "Consolas";

        private GUIStyle? _style;
        private Texture2D? _background;

        internal static bool ShouldPaint(EventType currentEvent, bool monitorEnabled, string hudText) =>
            monitorEnabled
            && currentEvent == EventType.Repaint
            && !string.IsNullOrEmpty(hudText);

        private void OnGUI()
        {
            var monitor = PerfMonitor.Instance;
            if (monitor == null) return;
            if (!ShouldPaint(Event.current.type, PerfMonitor.Enabled, monitor.HudText)) return;

            var style = EnsureStyle();
            var content = new GUIContent(monitor.HudText);
            float height = style.CalcHeight(content, Width);
            GUI.Label(new Rect(Margin, Margin, Width, height), content, style);
        }

        internal GUIStyle EnsureStyle()
        {
            if (_style != null) return _style;

            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
            _background.Apply();

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = FontSize,
                richText = false,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 6, 6),
                wordWrap = false,
            };
            style.normal.textColor = Color.white;
            style.normal.background = _background;

            var monospace = Font.CreateDynamicFontFromOSFont(MonospaceFontName, FontSize);
            if (monospace != null) style.font = monospace;

            _style = style;
            return style;
        }

        private void OnDestroy()
        {
            if (_background != null) Destroy(_background);
        }
    }
}
