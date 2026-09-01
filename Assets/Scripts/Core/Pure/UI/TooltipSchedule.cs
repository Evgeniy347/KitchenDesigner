namespace KitchenDesigner.Core.UI
{
    internal sealed class TooltipSchedule
    {
        public const float DefaultDelaySeconds = 0.4f;

        private readonly float _delaySeconds;
        private bool _pending;
        private float _showAt;

        public TooltipSchedule(float delaySeconds = DefaultDelaySeconds) =>
            _delaySeconds = delaySeconds;

        public bool Pending => _pending;

        public void Request(float now)
        {
            _pending = true;
            _showAt = now + _delaySeconds;
        }

        public void Cancel() => _pending = false;

        public bool DueAt(float now)
        {
            if (!_pending || now < _showAt) return false;
            _pending = false;
            return true;
        }
    }
}
