using System;
using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    public static class JsonText
    {
        public static int SkipWhitespace(string source, int index)
        {
            while (index < source.Length && IsWhiteSpace(source[index])) index++;
            return index;
        }

        private static bool IsWhiteSpace(char c) =>
            c == ' ' || (c < '!' ? char.IsWhiteSpace(c) : c > '~' && char.IsWhiteSpace(c));

        public static int EndOfValue(string source, int start)
        {
            if (start < 0 || start >= source.Length) return -1;
            char first = source[start];
            if (first == '"') return EndOfString(source, start);
            if (first == '{') return EndOfNested(source, start, '{', '}');
            if (first == '[') return EndOfNested(source, start, '[', ']');
            return EndOfScalar(source, start);
        }

        public static JsonSpan RootObject(string source)
        {
            if (source == null) return JsonSpan.None;
            int start = SkipWhitespace(source, 0);
            if (start >= source.Length || source[start] != '{') return JsonSpan.None;
            int end = EndOfValue(source, start);
            return end < 0 ? JsonSpan.None : new JsonSpan(start, end);
        }

        public static List<KeyValuePair<string, JsonSpan>> Members(string source, JsonSpan objectSpan)
        {
            var members = new List<KeyValuePair<string, JsonSpan>>();
            if (source == null || !objectSpan.Found) return members;
            if (objectSpan.Start >= source.Length || source[objectSpan.Start] != '{') return members;

            int i = SkipWhitespace(source, objectSpan.Start + 1);
            while (i < objectSpan.End && source[i] != '}')
            {
                if (source[i] != '"') break;
                int keyEnd = EndOfString(source, i);
                if (keyEnd < 0) break;
                string key = Unescape(source.Substring(i + 1, keyEnd - i - 2));

                i = SkipWhitespace(source, keyEnd);
                if (i >= objectSpan.End || source[i] != ':') break;
                i = SkipWhitespace(source, i + 1);

                int valueEnd = EndOfValue(source, i);
                if (valueEnd < 0) break;
                members.Add(new KeyValuePair<string, JsonSpan>(key, new JsonSpan(i, valueEnd)));

                i = SkipWhitespace(source, valueEnd);
                if (i < objectSpan.End && source[i] == ',') i = SkipWhitespace(source, i + 1);
            }
            return members;
        }

        public static JsonSpan MemberValue(string source, JsonSpan objectSpan, string key)
        {
            foreach (var member in Members(source, objectSpan))
                if (member.Key == key) return member.Value;
            return JsonSpan.None;
        }

        public static List<JsonSpan> ArrayItems(string source, JsonSpan arraySpan)
        {
            var items = new List<JsonSpan>();
            if (source == null || !arraySpan.Found) return items;
            if (arraySpan.Start >= source.Length || source[arraySpan.Start] != '[') return items;

            int i = SkipWhitespace(source, arraySpan.Start + 1);
            while (i < arraySpan.End && source[i] != ']')
            {
                int end = EndOfValue(source, i);
                if (end < 0) break;
                items.Add(new JsonSpan(i, end));
                i = SkipWhitespace(source, end);
                if (i < arraySpan.End && source[i] == ',') i = SkipWhitespace(source, i + 1);
            }
            return items;
        }

        public static string RewriteArrayItems(string source, JsonSpan arraySpan, Func<string, JsonSpan, string> rewriteItem)
        {
            var items = ArrayItems(source, arraySpan);
            if (items.Count == 0) return source;

            var sb = new StringBuilder(source.Length);
            int cursor = 0;
            foreach (var item in items)
            {
                sb.Append(source, cursor, item.Start - cursor);
                if (item.Start < source.Length && source[item.Start] == '{')
                {
                    string itemText = item.Text(source);
                    sb.Append(rewriteItem(itemText, RootObject(itemText)));
                }
                else
                {
                    sb.Append(source, item.Start, item.Length);
                }
                cursor = item.End;
            }
            sb.Append(source, cursor, source.Length - cursor);
            return sb.ToString();
        }

        public static string RewriteNamedObject(string source, JsonSpan objectSpan, Func<string, JsonSpan, string> rewriteItem)
        {
            if (!objectSpan.Found || objectSpan.Start >= source.Length || source[objectSpan.Start] != '{')
                return source;

            string itemText = objectSpan.Text(source);
            string rewritten = rewriteItem(itemText, RootObject(itemText));
            if (ReferenceEquals(rewritten, itemText)) return source;

            return source.Substring(0, objectSpan.Start) + rewritten + source.Substring(objectSpan.End);
        }

        [ThreadStatic] private static long _charsProcessedByRemoveMember;

        public static long TakeCharsProcessedByRemoveMember()
        {
            long n = _charsProcessedByRemoveMember;
            _charsProcessedByRemoveMember = 0;
            return n;
        }

        public static string RemoveMember(string source, JsonSpan objectSpan, string key)
        {
            _charsProcessedByRemoveMember += source.Length;
            var members = Members(source, objectSpan);
            int index = members.FindIndex(m => m.Key == key);
            if (index < 0) return source;

            int valueEnd = members[index].Value.End;

            if (index == 0)
            {
                int afterValue = SkipWhitespace(source, valueEnd);
                bool hasTrailingComma = afterValue < objectSpan.End && source[afterValue] == ',';
                int endCut = hasTrailingComma ? afterValue + 1 : valueEnd;
                return source.Substring(0, objectSpan.Start + 1) + source.Substring(endCut);
            }

            int startCut = members[index - 1].Value.End;
            return source.Substring(0, startCut) + source.Substring(valueEnd);
        }

        public static string LineIndentBefore(string source, int index)
        {
            int lineStart = index;
            while (lineStart > 0 && source[lineStart - 1] != '\n') lineStart--;
            int i = lineStart;
            while (i < index && (source[i] == ' ' || source[i] == '\t')) i++;
            return i == index ? source.Substring(lineStart, index - lineStart) : "";
        }

        private static int EndOfString(string source, int start)
        {
            int i = start + 1;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == '\\') { i += 2; continue; }
                if (c == '"') return i + 1;
                i++;
            }
            return -1;
        }

        private static int EndOfNested(string source, int start, char open, char close)
        {
            int depth = 0;
            int i = start;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == '"')
                {
                    int stringEnd = EndOfString(source, i);
                    if (stringEnd < 0) return -1;
                    i = stringEnd;
                    continue;
                }
                if (c == open) depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0) return i + 1;
                }
                i++;
            }
            return -1;
        }

        private static int EndOfScalar(string source, int start)
        {
            int i = start;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == ',' || c == '}' || c == ']' || IsWhiteSpace(c)) break;
                i++;
            }
            return i == start ? -1 : i;
        }

        internal static string Unescape(string raw)
        {
            if (raw.IndexOf('\\') < 0) return raw;
            var sb = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] != '\\' || i + 1 >= raw.Length) { sb.Append(raw[i]); continue; }
                i++;
                switch (raw[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 < raw.Length
                            && int.TryParse(raw.Substring(i + 1, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out int code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(raw[i]); break;
                }
            }
            return sb.ToString();
        }
    }
}
