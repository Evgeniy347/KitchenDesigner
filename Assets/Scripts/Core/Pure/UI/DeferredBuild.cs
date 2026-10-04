using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.UI
{
    public sealed class DeferredBuild
    {
        public const int QuietFramesBeforePrewarm = 30;

        private readonly Queue<Action> _steps = new();
        private readonly Action<Action> _around;
        private bool _running;

        public DeferredBuild(IEnumerable<Action> steps, Action<Action>? around = null)
        {
            foreach (var step in steps) _steps.Enqueue(step);
            _around = around ?? (run => run());
        }

        public bool Done => _steps.Count == 0;

        public void RunAll()
        {
            if (_running || Done) return;
            Run(() =>
            {
                while (_steps.Count > 0) _steps.Dequeue()();
            });
        }

        public bool TryPrewarmStep(int framesSinceBuild, bool inputActive)
        {
            if (Done || _running) return false;
            if (framesSinceBuild < QuietFramesBeforePrewarm || inputActive) return false;
            Run(() => _steps.Dequeue()());
            return true;
        }

        private void Run(Action work)
        {
            _running = true;
            try
            {
                _around(work);
            }
            finally
            {
                _running = false;
            }
        }
    }
}
