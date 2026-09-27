using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    internal sealed class JsonKeyIndex
    {
        private const int MinimumSlots = 16;

        private readonly List<int> _escaped = new List<int>();
        private int[] _slots = new int[MinimumSlots];
        private int _mask = MinimumSlots - 1;
        private string _source = "";
        private List<JsonSpan> _keys = new List<JsonSpan>();

        public void Build(string source, List<JsonSpan> keys)
        {
            _source = source;
            _keys = keys;
            _escaped.Clear();

            int size = MinimumSlots;
            while (size < keys.Count * 2) size <<= 1;
            if (_slots.Length != size) _slots = new int[size];
            else Array.Clear(_slots, 0, size);
            _mask = size - 1;

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (source.IndexOf('\\', key.Start, key.Length) >= 0)
                {
                    _escaped.Add(i);
                    continue;
                }
                int slot = Hash(source, key.Start, key.Length) & _mask;
                while (_slots[slot] != 0 && !SameText(keys[_slots[slot] - 1], source, key.Start, key.Length))
                    slot = (slot + 1) & _mask;
                if (_slots[slot] == 0) _slots[slot] = i + 1;
            }
        }

        public int FirstIndexOf(string key)
        {
            int found = -1;
            int slot = Hash(key, 0, key.Length) & _mask;
            while (_slots[slot] != 0)
            {
                int index = _slots[slot] - 1;
                if (SameText(_keys[index], key, 0, key.Length))
                {
                    found = index;
                    break;
                }
                slot = (slot + 1) & _mask;
            }

            foreach (int index in _escaped)
            {
                if (found >= 0 && index > found) break;
                if (JsonText.Unescape(_keys[index].Text(_source)) == key) return index;
            }
            return found;
        }

        private bool SameText(JsonSpan span, string other, int otherStart, int length) =>
            span.Length == length && string.CompareOrdinal(_source, span.Start, other, otherStart, length) == 0;

        private static int Hash(string text, int start, int length)
        {
            uint hash = 2166136261;
            for (int i = start; i < start + length; i++)
                hash = (hash ^ text[i]) * 16777619;
            return (int)(hash & 0x7fffffff);
        }
    }
}
