namespace KitchenDesigner.Core
{
    public sealed class DoubleClickTracker
    {
        public const float WindowSeconds = 0.35f;

        private float _time = float.NegativeInfinity;
        private int _groupId;
        private int _elementId;

        public void Remember(int groupId, int elementId, float now)
        {
            _groupId = groupId;
            _elementId = elementId;
            _time = now;
        }

        public void Forget()
        {
            _time = float.NegativeInfinity;
            _groupId = 0;
            _elementId = 0;
        }

        public bool RepeatsGroup(int groupId, float now)
            => groupId != 0 && groupId == _groupId && InWindow(now);

        public bool RepeatsElement(int elementId, float now)
            => _groupId == 0 && elementId != 0 && elementId == _elementId && InWindow(now);

        private bool InWindow(float now) => now - _time <= WindowSeconds;
    }
}
