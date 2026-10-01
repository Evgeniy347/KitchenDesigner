using System;
using System.Collections.Generic;
using System.Linq;

namespace KitchenDesigner.Core
{
    public static class LanguageChoice
    {
        public static string Decide(string? stored, IEnumerable<string?> systemLanguages,
            IReadOnlyCollection<string> available, bool testRun)
        {
            if (testRun) return Localizer.SourceLanguage;

            var storedCode = Normalize(stored);
            if (storedCode != null && available.Contains(storedCode)) return storedCode;

            foreach (var system in systemLanguages)
            {
                var code = Normalize(system);
                if (code != null && available.Contains(code)) return code;
            }

            return available.Contains(Localizer.FallbackLanguage)
                ? Localizer.FallbackLanguage
                : Localizer.SourceLanguage;
        }

        public static string? Normalize(string? language)
        {
            if (string.IsNullOrWhiteSpace(language)) return null;
            var trimmed = language!.Trim();
            int cut = trimmed.IndexOfAny(new[] { '-', '_' });
            var primary = cut < 0 ? trimmed : trimmed.Substring(0, cut);
            return primary.Length == 0 ? null : primary.ToLowerInvariant();
        }
    }
}
