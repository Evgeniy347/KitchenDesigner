using System;

namespace KitchenDesigner.Core
{
    public static class PluralRules
    {
        public static PluralCategory For(string language, long count)
        {
            long n = Math.Abs(count);
            switch (language)
            {
                case "ru":
                case "uk":
                case "be":
                    return EastSlavic(n);
                case "ar":
                    return Arabic(n);
                default:
                    return n == 1 ? PluralCategory.One : PluralCategory.Other;
            }
        }

        public static string Suffix(PluralCategory category) => category switch
        {
            PluralCategory.Zero => "zero",
            PluralCategory.One => "one",
            PluralCategory.Two => "two",
            PluralCategory.Few => "few",
            PluralCategory.Many => "many",
            _ => "other",
        };

        private static PluralCategory EastSlavic(long n)
        {
            long mod10 = n % 10;
            long mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11) return PluralCategory.One;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return PluralCategory.Few;
            return PluralCategory.Many;
        }

        private static PluralCategory Arabic(long n)
        {
            long mod100 = n % 100;
            if (n == 0) return PluralCategory.Zero;
            if (n == 1) return PluralCategory.One;
            if (n == 2) return PluralCategory.Two;
            if (mod100 >= 3 && mod100 <= 10) return PluralCategory.Few;
            if (mod100 >= 11) return PluralCategory.Many;
            return PluralCategory.Other;
        }
    }
}
