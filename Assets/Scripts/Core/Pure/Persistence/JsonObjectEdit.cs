using System;
using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    public sealed class JsonObjectEdit
    {
        private readonly List<JsonSpan> _values = new List<JsonSpan>();
        private readonly List<bool> _removed = new List<bool>();
        private readonly Dictionary<string, int> _firstIndexByKey = new Dictionary<string, int>();
        private string _source = "";
        private int _start;
        private int _end;
        private int _removedCount;

        [ThreadStatic] private static long _charsRead;

        public int End => _end;

        public int Count => _values.Count;

        public bool AnyRemoved => _removedCount > 0;

        public static long TakeCharsRead()
        {
            long n = _charsRead;
            _charsRead = 0;
            return n;
        }

        public static string Apply(string source, JsonSpan objectSpan, Action<JsonObjectEdit> rule)
        {
            if (!objectSpan.Found) return source;
            var edit = new JsonObjectEdit();
            if (!edit.Read(source, objectSpan.Start)) return source;
            rule(edit);
            if (!edit.AnyRemoved) return source;

            var sb = new StringBuilder(source.Length);
            sb.Append(source, 0, edit._start);
            edit.WriteTo(sb);
            sb.Append(source, edit._end, source.Length - edit._end);
            return sb.ToString();
        }

        public bool Read(string source, int objectStart)
        {
            _source = source;
            _start = objectStart;
            _end = -1;
            _values.Clear();
            _removed.Clear();
            _firstIndexByKey.Clear();
            _removedCount = 0;
            if (objectStart < 0 || objectStart >= source.Length || source[objectStart] != '{') return false;

            int i = JsonText.SkipWhitespace(source, objectStart + 1);
            while (i < source.Length && source[i] != '}')
            {
                if (source[i] != '"') return false;
                int keyEnd = JsonText.EndOfValue(source, i);
                if (keyEnd < 0) return false;
                string key = JsonText.Unescape(source.Substring(i + 1, keyEnd - i - 2));

                i = JsonText.SkipWhitespace(source, keyEnd);
                if (i >= source.Length || source[i] != ':') return false;
                i = JsonText.SkipWhitespace(source, i + 1);

                int valueEnd = JsonText.EndOfValue(source, i);
                if (valueEnd < 0) return false;
                if (!_firstIndexByKey.ContainsKey(key)) _firstIndexByKey.Add(key, _values.Count);
                _values.Add(new JsonSpan(i, valueEnd));
                _removed.Add(false);

                i = JsonText.SkipWhitespace(source, valueEnd);
                if (i < source.Length && source[i] == ',') i = JsonText.SkipWhitespace(source, i + 1);
            }
            if (i >= source.Length) return false;
            _end = i + 1;
            _charsRead += _end - objectStart;
            return true;
        }

        public bool Has(string key) => _firstIndexByKey.ContainsKey(key);

        public bool ValueIs(string key, string text)
        {
            if (!_firstIndexByKey.TryGetValue(key, out int index)) return false;
            var value = _values[index];
            return value.Length == text.Length
                && string.CompareOrdinal(_source, value.Start, text, 0, text.Length) == 0;
        }

        public void Remove(string key)
        {
            if (!_firstIndexByKey.TryGetValue(key, out int index) || _removed[index]) return;
            _removed[index] = true;
            _removedCount++;
        }

        public void WriteTo(StringBuilder sb)
        {
            sb.Append('{');
            int cursor = _start + 1;
            int previousValueEnd = cursor;
            bool keptAny = false;
            for (int i = 0; i < _values.Count; i++)
            {
                int valueEnd = _values[i].End;
                if (!_removed[i])
                {
                    keptAny = true;
                }
                else if (keptAny)
                {
                    sb.Append(_source, cursor, previousValueEnd - cursor);
                    cursor = valueEnd;
                }
                else
                {
                    int afterValue = JsonText.SkipWhitespace(_source, valueEnd);
                    cursor = afterValue < _end && _source[afterValue] == ',' ? afterValue + 1 : valueEnd;
                }
                previousValueEnd = valueEnd;
            }
            sb.Append(_source, cursor, _end - cursor);
        }
    }
}
