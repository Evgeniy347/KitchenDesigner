using System;
using System.Collections.Generic;
using System.Text;

namespace KitchenDesigner.Core
{
    internal sealed class NamedTally
    {
        private readonly struct Entry
        {
            public readonly string Name;
            public readonly long Ticks;
            public readonly int Times;

            public Entry(string name, long ticks, int times)
            {
                Name = name;
                Ticks = ticks;
                Times = times;
            }

            public Entry Plus(long ticks) => new Entry(Name, Ticks + ticks, Times + 1);
        }

        private readonly int _capacity;
        private readonly List<Entry> _entries;
        private int _beyondTheLimit;

        internal NamedTally(int capacity)
        {
            _capacity = capacity;
            _entries = new List<Entry>(capacity);
        }

        internal int OverflowCount => _beyondTheLimit;

        internal bool IsEmpty => _entries.Count == 0 && _beyondTheLimit == 0;

        internal void Add(string name, long ticks)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!string.Equals(_entries[i].Name, name, StringComparison.Ordinal)) continue;
                _entries[i] = _entries[i].Plus(ticks);
                return;
            }

            if (_entries.Count >= _capacity)
            {
                _beyondTheLimit++;
                return;
            }
            _entries.Add(new Entry(name, ticks, 1));
        }

        internal bool TryGet(string name, out long ticks, out int times)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!string.Equals(_entries[i].Name, name, StringComparison.Ordinal)) continue;
                ticks = _entries[i].Ticks;
                times = _entries[i].Times;
                return true;
            }
            ticks = 0;
            times = 0;
            return false;
        }

        internal void Clear()
        {
            _entries.Clear();
            _beyondTheLimit = 0;
        }

        internal string Format(Func<string, long, int, string> formatEntry)
        {
            var text = new StringBuilder();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i > 0) text.Append(", ");
                text.Append(formatEntry(_entries[i].Name, _entries[i].Ticks, _entries[i].Times));
            }
            if (_beyondTheLimit > 0)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append("и ещё ").Append(_beyondTheLimit).Append(" сверх предела");
            }
            return text.ToString();
        }
    }
}
