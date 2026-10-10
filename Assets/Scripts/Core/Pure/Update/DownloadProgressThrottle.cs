namespace KitchenDesigner.Core.Update
{
    public sealed class DownloadProgressThrottle
    {
        public const int PercentStep = 10;
        public const float MinSecondsBetweenReports = 2f;
        private const int Complete = 100;

        private float _lastReportAt;
        private int _lastStep;
        private bool _completeReported;

        public DownloadProgressThrottle(float startedAtSeconds)
        {
            _lastReportAt = startedAtSeconds;
        }

        public bool ShouldReport(long received, long total, float nowSeconds)
        {
            if (total <= 0) return ReportIfQuietLongEnough(nowSeconds);

            int percent = received >= total ? Complete : (int)(received * Complete / total);
            if (percent >= Complete) return ReportCompletionOnce(nowSeconds);

            int step = percent / PercentStep;
            if (step <= _lastStep) return false;
            if (nowSeconds - _lastReportAt < MinSecondsBetweenReports) return false;

            _lastStep = step;
            _lastReportAt = nowSeconds;
            return true;
        }

        private bool ReportIfQuietLongEnough(float nowSeconds)
        {
            if (nowSeconds - _lastReportAt < MinSecondsBetweenReports) return false;
            _lastReportAt = nowSeconds;
            return true;
        }

        private bool ReportCompletionOnce(float nowSeconds)
        {
            if (_completeReported) return false;
            _completeReported = true;
            _lastReportAt = nowSeconds;
            return true;
        }
    }
}
