using System;

namespace KitchenDesigner.Core.MCP
{
    public sealed class McpRevisionCache<T> where T : class
    {
        private const int NoRevisionSeen = int.MinValue;

        private int _cachedRevision = NoRevisionSeen;
        private T? _cached;
        private int _recomputeCount;

        public T Get(int currentRevision, Func<T> compute)
        {
            if (_cached != null && currentRevision == _cachedRevision)
                return _cached;

            _cached = compute();
            _cachedRevision = currentRevision;
            _recomputeCount++;
            return _cached;
        }

        public int TakeRecomputeCount()
        {
            int n = _recomputeCount;
            _recomputeCount = 0;
            return n;
        }

        public void Reset()
        {
            _cached = null;
            _cachedRevision = NoRevisionSeen;
            _recomputeCount = 0;
        }
    }
}
