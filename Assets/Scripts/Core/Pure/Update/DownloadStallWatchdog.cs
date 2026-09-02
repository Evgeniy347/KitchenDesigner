using System;

namespace KitchenDesigner.Core.Update
{
    public sealed class DownloadStallWatchdog
    {
        public const float DefaultIdleSeconds = 30f;

        private readonly float _idleSeconds;
        private long _bytesAtLastGrowth;
        private float _timeOfLastGrowth;
        private bool _watching;

        public DownloadStallWatchdog(float idleSeconds)
        {
            if (idleSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(idleSeconds),
                    "детектор простоя без окна ожидания обрывал бы любую загрузку");
            _idleSeconds = idleSeconds;
        }

        public float IdleSeconds => _idleSeconds;

        public void Observe(long downloadedBytes, float nowSeconds)
        {
            if (_watching && downloadedBytes <= _bytesAtLastGrowth) return;
            _watching = true;
            _bytesAtLastGrowth = downloadedBytes;
            _timeOfLastGrowth = nowSeconds;
        }

        public float SecondsWithoutGrowth(float nowSeconds) =>
            _watching ? Math.Max(0f, nowSeconds - _timeOfLastGrowth) : 0f;

        public bool IsStalled(float nowSeconds) =>
            _watching && SecondsWithoutGrowth(nowSeconds) >= _idleSeconds;
    }
}
