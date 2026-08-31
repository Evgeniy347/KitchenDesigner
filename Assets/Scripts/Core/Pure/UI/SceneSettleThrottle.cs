namespace KitchenDesigner.Core.UI
{
    internal sealed class SceneSettleThrottle
    {
        public const float DefaultSettleSeconds = 0.25f;

        private readonly float _settleSeconds;
        private int _seenRevision = -1;
        private int _recomputedRevision = -1;
        private float _stableAt;

        public SceneSettleThrottle(float settleSeconds = DefaultSettleSeconds) =>
            _settleSeconds = settleSeconds;

        public bool DueAfterSceneSettled(int sceneRevision, float now)
        {
            if (_seenRevision != sceneRevision)
            {
                _seenRevision = sceneRevision;
                _stableAt = now + _settleSeconds;
                return false;
            }

            if (_seenRevision == _recomputedRevision) return false;
            if (now < _stableAt) return false;

            _recomputedRevision = _seenRevision;
            return true;
        }
    }
}
