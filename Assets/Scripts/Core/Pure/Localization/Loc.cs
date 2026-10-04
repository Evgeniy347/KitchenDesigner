using System;
using System.Collections.Generic;
using System.Threading;

namespace KitchenDesigner.Core
{
    public static class Loc
    {
        private static readonly object Gate = new object();
        private static Localizer? _current;
        private static int _revision;

        [ThreadStatic]
        private static Localizer? _scoped;

        public static event Action? LanguageChanged;

        public static Localizer Current
        {
            get
            {
                var scoped = _scoped;
                if (scoped != null) return scoped;
                var current = Volatile.Read(ref _current);
                if (current != null) return current;
                lock (Gate)
                {
                    _current ??= LocalizationFiles.Load(Localizer.SourceLanguage);
                    return _current;
                }
            }
        }

        public static int Revision => Volatile.Read(ref _revision);

        public static string Language => Current.Language;

        public static bool IsRightToLeft => Current.IsRightToLeft;

        public static IReadOnlyList<LanguageInfo> Languages => Current.Languages;

        public static string T(string key) => Current.Get(key);

        public static string F(string key, params object?[] args) => Current.Format(key, args);

        public static string Plural(string key, long count, params object?[] args) =>
            Current.Plural(key, count, args);

        public static void SetLanguage(string language)
        {
            var before = Current;
            if (before.Language == language) return;
            Use(before.WithLanguage(language));
        }

        public static void Use(Localizer localizer)
        {
            string? previous;
            lock (Gate)
            {
                previous = _current?.Language;
                Volatile.Write(ref _current, localizer);
                Interlocked.Increment(ref _revision);
            }
            if (previous != localizer.Language) LanguageChanged?.Invoke();
        }

        public static IDisposable Scope(Localizer localizer)
        {
            var outer = _scoped;
            _scoped = localizer;
            return new ScopeEnd(outer);
        }

        public static void Reload()
        {
            string language = Current.Language;
            Use(LocalizationFiles.Load(language));
        }

        private sealed class ScopeEnd : IDisposable
        {
            private readonly Localizer? _outer;

            public ScopeEnd(Localizer? outer) => _outer = outer;

            public void Dispose() => _scoped = _outer;
        }
    }
}
