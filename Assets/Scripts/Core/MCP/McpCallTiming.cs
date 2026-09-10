using System.Diagnostics;
using UnityEngine;

namespace KitchenDesigner.Core.MCP
{
    public sealed class McpCallTiming
    {
        private long _acceptedTicks;
        private long _queuedTicks;
        private long _startedTicks;
        private long _executedTicks;
        private string _method = "?";

        public void MarkAccepted() => _acceptedTicks = Stopwatch.GetTimestamp();

        public void MarkQueued(string method)
        {
            _method = method;
            _queuedTicks = Stopwatch.GetTimestamp();
        }

        public void MarkStarted() => _startedTicks = Stopwatch.GetTimestamp();

        public void MarkExecuted() => _executedTicks = Stopwatch.GetTimestamp();

        public void MarkRespondedAndLog()
        {
            long respondedTicks = Stopwatch.GetTimestamp();
            var breakdown = new McpCallTimingBreakdown(
                _acceptedTicks, _queuedTicks, _startedTicks, _executedTicks, respondedTicks);
            UnityEngine.Debug.Log(breakdown.Format(_method, McpValidationCache.TakeRecomputeCount()));
        }
    }
}
