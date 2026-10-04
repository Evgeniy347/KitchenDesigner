using System;
using System.Globalization;

namespace KitchenDesigner.Core
{
    public static class NumberFormat
    {
        public const char Minus = (char)0x2212;
        public const char AsciiMinus = '-';
        public const char InvariantDecimalPoint = '.';
        public const char UnitGap = (char)0x00A0;

        public static string DecimalSeparator => Loc.Current.DecimalSeparator;

        public static string Fixed(double value, int decimals) =>
            Fixed(value, decimals, DecimalSeparator);

        public static string Fixed(double value, int decimals, string decimalSeparator) =>
            Compose(value, decimals, decimalSeparator, Minus, trimZeros: false);

        public static string Compact(double value, int maxDecimals) =>
            Compact(value, maxDecimals, DecimalSeparator);

        public static string Compact(double value, int maxDecimals, string decimalSeparator) =>
            Compose(value, maxDecimals, decimalSeparator, Minus, trimZeros: true);

        public static string Integer(double value) => Fixed(value, 0, DecimalSeparator);

        public static string Input(double value, int decimals) =>
            Input(value, decimals, DecimalSeparator);

        public static string Input(double value, int decimals, string decimalSeparator) =>
            Compose(value, decimals, decimalSeparator, AsciiMinus, trimZeros: false);

        public static string WithUnit(string number, string unit) =>
            string.IsNullOrEmpty(unit) ? number : number + UnitGap + unit;

        public static string Normalize(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text!.Trim()
                .Replace(Minus, AsciiMinus)
                .Replace(',', InvariantDecimalPoint)
                .Replace(UnitGap.ToString(), "");
        }

        public static bool TryParse(string? text, out double value) =>
            double.TryParse(Normalize(text), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        public static bool TryParseInt(string? text, out int value) =>
            int.TryParse(Normalize(text), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private static string Compose(double value, int decimals, string decimalSeparator, char minus,
            bool trimZeros)
        {
            if (decimals < 0 || decimals > 15)
                throw new ArgumentOutOfRangeException(nameof(decimals), decimals, "0..15");
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value.ToString(CultureInfo.InvariantCulture);

            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            string digits = Math.Abs(rounded).ToString("F" + decimals, CultureInfo.InvariantCulture);
            if (trimZeros && digits.IndexOf(InvariantDecimalPoint) >= 0)
                digits = digits.TrimEnd('0').TrimEnd(InvariantDecimalPoint);
            if (decimalSeparator != ".")
                digits = digits.Replace(".", decimalSeparator);
            return rounded < 0 ? minus + digits : digits;
        }
    }
}
