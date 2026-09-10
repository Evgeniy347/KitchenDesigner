using System.Diagnostics;

namespace KitchenDesigner.Core.MCP
{
    public readonly struct McpCallTimingBreakdown
    {
        public readonly double AcceptedToQueuedMs;
        public readonly double QueuedToStartedMs;
        public readonly double StartedToExecutedMs;
        public readonly double ExecutedToRespondedMs;
        public readonly double TotalMs;

        public McpCallTimingBreakdown(long acceptedTicks, long queuedTicks, long startedTicks,
            long executedTicks, long respondedTicks)
        {
            AcceptedToQueuedMs = MsBetween(acceptedTicks, queuedTicks);
            QueuedToStartedMs = MsBetween(queuedTicks, startedTicks);
            StartedToExecutedMs = MsBetween(startedTicks, executedTicks);
            ExecutedToRespondedMs = MsBetween(executedTicks, respondedTicks);
            TotalMs = MsBetween(acceptedTicks, respondedTicks);
        }

        private static double MsBetween(long fromTicks, long toTicks) =>
            (toTicks - fromTicks) * 1000.0 / Stopwatch.Frequency;

        public string Format(string method, int validationRecomputes) =>
            $"[MCP][Timing] method={method} total={TotalMs:F2}ms " +
            $"accepted->queued={AcceptedToQueuedMs:F2}ms queued->started={QueuedToStartedMs:F2}ms " +
            $"started->executed={StartedToExecutedMs:F2}ms executed->responded={ExecutedToRespondedMs:F2}ms " +
            $"validateRecomputes={validationRecomputes}";
    }
}
