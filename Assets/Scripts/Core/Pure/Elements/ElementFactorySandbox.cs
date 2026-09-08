using System;

namespace KitchenDesigner.Core
{
    public static class ElementFactorySandbox
    {
        [ThreadStatic] private static int _depth;

        public static bool IsActive => _depth > 0;

        public static IDisposable Enter() => new Scope();

        private sealed class Scope : IDisposable
        {
            private bool _disposed;

            public Scope() => _depth++;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _depth--;
            }
        }
    }
}
