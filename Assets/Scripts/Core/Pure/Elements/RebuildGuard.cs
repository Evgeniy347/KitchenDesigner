using System;

namespace KitchenDesigner.Core
{
    internal sealed class RebuildGuard
    {
        private bool _running;

        public void Run(Action rebuild)
        {
            if (rebuild == null) throw new ArgumentNullException(nameof(rebuild));
            if (_running) return;
            _running = true;
            try
            {
                rebuild();
            }
            finally
            {
                _running = false;
            }
        }
    }
}
