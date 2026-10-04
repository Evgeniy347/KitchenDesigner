using System;

namespace KitchenDesigner.Core.UI
{
    public static class ClockTime
    {
        public const float HoursPerDay = 24f;
        private const int MinutesPerHour = 60;

        public static string Format(float hours)
        {
            int total = (int)Math.Round(Math.Clamp(hours, 0f, HoursPerDay) * MinutesPerHour);
            return $"{total / MinutesPerHour:00}:{total % MinutesPerHour:00}";
        }

        public static bool TryParse(string? text, out float hours)
        {
            hours = 0f;
            var parts = (text ?? "").Trim().Split(':', '.', ',');
            if (parts.Length > 2) return false;
            if (!TryDigits(parts[0], out int h)) return false;
            int m = 0;
            if (parts.Length == 2 && (parts[1].Length == 0 || !TryDigits(parts[1], out m))) return false;
            if (m >= MinutesPerHour || h > (int)HoursPerDay || (h == (int)HoursPerDay && m != 0)) return false;
            hours = h + m / (float)MinutesPerHour;
            return true;
        }

        private static bool TryDigits(string part, out int value)
        {
            value = 0;
            if (part.Length == 0 || part.Length > 2) return false;
            foreach (char c in part)
                if (c < '0' || c > '9') return false;
            value = int.Parse(part);
            return true;
        }
    }
}
