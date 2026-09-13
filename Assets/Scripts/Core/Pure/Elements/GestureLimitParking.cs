namespace KitchenDesigner.Core
{
    public struct GestureLimitParking
    {
        private bool _parked;

        public bool IsParked => _parked;

        public void Release() => _parked = false;

        public bool RunsThisFrame(bool reachedTarget, bool stillBlocked)
        {
            _parked = !reachedTarget && stillBlocked;
            return !reachedTarget && !stillBlocked;
        }
    }
}
