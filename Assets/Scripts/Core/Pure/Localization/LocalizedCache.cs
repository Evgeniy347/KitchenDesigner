using System;

namespace KitchenDesigner.Core
{
    public sealed class LocalizedCache<T> where T : class
    {
        private readonly Func<T> _build;
        private readonly Func<int> _revision;
        private Entry? _entry;

        public LocalizedCache(Func<T> build) : this(build, () => Loc.Revision) { }

        public LocalizedCache(Func<T> build, Func<int> revision)
        {
            _build = build;
            _revision = revision;
        }

        public T Value
        {
            get
            {
                int revision = _revision();
                var entry = _entry;
                if (entry != null && entry.Revision == revision) return entry.Value;
                var built = new Entry(revision, _build());
                _entry = built;
                return built.Value;
            }
        }

        private sealed class Entry
        {
            public readonly int Revision;
            public readonly T Value;

            public Entry(int revision, T value)
            {
                Revision = revision;
                Value = value;
            }
        }
    }
}
