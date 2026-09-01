using System;

namespace KitchenDesigner.Core
{
    public static class HighlightBatch
    {
        private static int _depth;
        private static bool _refreshOwed;

        public static bool Suspended => _depth > 0;

        public static IDisposable Open() => new Scope();

        internal static void Defer() => _refreshOwed = true;

        internal static void Reset()
        {
            _depth = 0;
            _refreshOwed = false;
        }

        private sealed class Scope : IDisposable
        {
            private bool _closed;

            public Scope() => _depth++;

            public void Dispose()
            {
                if (_closed) return;
                _closed = true;
                if (--_depth > 0) return;
                if (!_refreshOwed) return;
                _refreshOwed = false;
                if (ElementHighlighter.Instance != null)
                    ElementHighlighter.Instance.RefreshHighlights();
            }
        }
    }
}
