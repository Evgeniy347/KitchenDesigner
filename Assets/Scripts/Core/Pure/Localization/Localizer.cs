using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KitchenDesigner.Core
{
    public sealed class Localizer
    {
        public const string SourceLanguage = "ru";
        public const string FallbackLanguage = "en";

        private readonly Dictionary<string, StringTable> _tables;
        private readonly StringTable[] _chain;

        public string Language { get; }

        public Localizer(IEnumerable<StringTable> tables, string language)
        {
            _tables = new Dictionary<string, StringTable>(StringComparer.Ordinal);
            foreach (var table in tables) _tables[table.Language] = table;
            Language = _tables.ContainsKey(language) ? language : SourceLanguage;
            _chain = new[] { Language, FallbackLanguage, SourceLanguage }
                .Distinct()
                .Where(_tables.ContainsKey)
                .Select(code => _tables[code])
                .ToArray();
        }

        public static Localizer Empty { get; } = new Localizer(Array.Empty<StringTable>(), SourceLanguage);

        public Localizer WithLanguage(string language) => new Localizer(_tables.Values, language);

        public IReadOnlyList<LanguageInfo> Languages =>
            _tables.Values
                .OrderBy(t => t.Language == SourceLanguage ? 0 : t.Language == FallbackLanguage ? 1 : 2)
                .ThenBy(t => t.Language, StringComparer.Ordinal)
                .Select(t => new LanguageInfo(t.Language, t.NativeName, t.IsRightToLeft))
                .ToList();

        public bool IsRightToLeft => _tables.TryGetValue(Language, out var t) && t.IsRightToLeft;

        public StringTable? Table(string language) =>
            _tables.TryGetValue(language, out var table) ? table : null;

        public bool TryGet(string key, out string text)
        {
            foreach (var table in _chain)
                if (table.TryGet(key, out text)) return true;
            text = "";
            return false;
        }

        public IEnumerable<string> Variants(string key)
        {
            foreach (var table in _tables.Values)
                if (table.TryGet(key, out var text)) yield return text;
        }

        public string Get(string key)
        {
            foreach (var table in _chain)
                if (table.TryGet(key, out var text)) return text;
            return key;
        }

        public string Format(string key, params object?[] args)
        {
            foreach (var table in _chain)
            {
                if (!table.TryGet(key, out var pattern)) continue;
                if (TryFormat(pattern, args, out var formatted)) return formatted;
            }
            return key;
        }

        public string Plural(string key, long count, params object?[] args)
        {
            var withCount = new object?[args.Length + 1];
            withCount[0] = count;
            Array.Copy(args, 0, withCount, 1, args.Length);

            foreach (var table in _chain)
            {
                if (!table.TryGetPlural(key, count, out var pattern)) continue;
                if (TryFormat(pattern, withCount, out var formatted)) return formatted;
            }
            return key;
        }

        private static bool TryFormat(string pattern, object?[] args, out string formatted)
        {
            try
            {
                formatted = string.Format(CultureInfo.CurrentCulture, pattern, args);
                return true;
            }
            catch (FormatException)
            {
                formatted = pattern;
                return false;
            }
        }
    }
}
