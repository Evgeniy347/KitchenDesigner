using System;
using System.Collections.Generic;
using System.Globalization;

namespace KitchenDesigner.Core.MCP
{
    public static class PlacementWording
    {
        public const string Arrow = "→";

        public const string GapWord = "gap";

        private const string BottomFace = "bottom";

        public static string Mm(float value)
        {
            float rounded = MathF.Round(value, 1);
            if (rounded == 0f) rounded = 0f;
            return rounded.ToString("0.#", CultureInfo.InvariantCulture);
        }

        public static string On(string name) => "on " + name;

        public static string Contact(PlacementContact contact) => contact.face + Arrow + contact.n;

        public static string Gap(PlacementGap gap) => gap.face + Arrow + gap.n + " " + GapWord + Mm(gap.gapMm);

        public static List<string> Relations(PlacementInfo placement)
        {
            var words = new List<string>();
            if (placement.touches != null)
                foreach (var contact in placement.touches)
                {
                    bool isTheSupport = contact.face == BottomFace && contact.n == placement.on;
                    if (!isTheSupport) words.Add(Contact(contact));
                }
            if (placement.gaps != null)
                foreach (var gap in placement.gaps) words.Add(Gap(gap));
            return words;
        }
    }
}
