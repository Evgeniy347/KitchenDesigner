namespace KitchenDesigner.Core.UI
{
    internal sealed class FrameThrottle
    {
        private readonly int _period;
        private int _countdown;

        public FrameThrottle(int period)
        {
            _period = period;
            _countdown = 0;
        }

        public bool Due()
        {
            if (--_countdown > 0) return false;
            Reset();
            return true;
        }

        public void Reset() => _countdown = _period;
    }
}
