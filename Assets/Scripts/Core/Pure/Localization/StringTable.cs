using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class StringTable
    {
        public const string NativeNameKey = "@name";
        public const string RightToLeftKey = "@rtl";
        public const string DecimalSeparatorKey = "@decimal";
        public const string DefaultDecimalSeparator = ".";
        public const char PluralSeparator = '#';

        private readonly Dictionary<string, string> _strings;

        public string Language { get; }
        public string NativeName { get; }
        public bool IsRightToLeft { get; }
        public string DecimalSeparator { get; }

        public StringTable(string language, IReadOnlyDictionary<string, string> entries)
        {
            Language = language;
            _strings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in entries)
                if (!IsMeta(kv.Key)) _strings[kv.Key] = kv.Value;
            NativeName = entries.TryGetValue(NativeNameKey, out var name) && name.Length > 0 ? name : language;
            IsRightToLeft = entries.TryGetValue(RightToLeftKey, out var rtl)
                && string.Equals(rtl, "true", StringComparison.OrdinalIgnoreCase);
            DecimalSeparator = entries.TryGetValue(DecimalSeparatorKey, out var separator) && separator.Length > 0
                ? separator
                : DefaultDecimalSeparator;
        }

        public IEnumerable<string> Keys => _strings.Keys;

        public int Count => _strings.Count;

        public bool TryGet(string key, out string text)
        {
            if (_strings.TryGetValue(key, out var found))
            {
                text = found;
                return true;
            }
            text = "";
            return false;
        }

        public bool TryGetPlural(string key, long count, out string text)
        {
            var category = PluralRules.For(Language, count);
            return TryGet(PluralKey(key, category), out text)
                || TryGet(PluralKey(key, PluralCategory.Other), out text)
                || TryGet(key, out text);
        }

        public static string PluralKey(string key, PluralCategory category) =>
            key + PluralSeparator + PluralRules.Suffix(category);

        public static string BaseKey(string key)
        {
            int cut = key.IndexOf(PluralSeparator);
            return cut < 0 ? key : key.Substring(0, cut);
        }

        public static bool IsMeta(string key) => key.Length > 0 && key[0] == '@';

        public static StringTable Parse(string language, string json)
        {
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            var root = JsonText.RootObject(json);
            if (!root.Found)
                throw new FormatException("Localization/" + language + ".json: root is not a JSON object");

            foreach (var member in JsonText.Members(json, root))
            {
                string raw = member.Value.Text(json);
                entries[member.Key] = raw.Length >= 2 && raw[0] == '"'
                    ? JsonText.Unescape(raw.Substring(1, raw.Length - 2))
                    : raw;
            }
            return new StringTable(language, entries);
        }
    }
}
