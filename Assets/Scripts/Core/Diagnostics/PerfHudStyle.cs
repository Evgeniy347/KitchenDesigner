using UnityEngine;

namespace KitchenDesigner.Core
{
    public sealed class PerfHudStyle
    {
        public PerfHudStyle(Color panel, Color text, Color secondary, Color warning, int fontSize,
            int padX, int padY)
        {
            Panel = panel;
            Text = text;
            Secondary = secondary;
            Warning = warning;
            FontSize = fontSize;
            PadX = padX;
            PadY = padY;
        }

        public Color Panel { get; }

        public Color Text { get; }

        public Color Secondary { get; }

        public Color Warning { get; }

        public int FontSize { get; }

        public int PadX { get; }

        public int PadY { get; }

        public Color ColorOf(PerfHudLineKind kind) => kind switch
        {
            PerfHudLineKind.Secondary => Secondary,
            PerfHudLineKind.Warning => Warning,
            _ => Text,
        };

        public static PerfHudStyle Fallback { get; } = new PerfHudStyle(new Color(0f, 0f, 0f, 0.88f),
            Color.white, Color.gray, Color.yellow, 13, 12, 8);
    }
}
