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
        public readonly bool ReachedTheScene;

        public McpCallTimingBreakdown(long acceptedTicks, long queuedTicks, long startedTicks,
            long executedTicks, long respondedTicks)
        {
            ReachedTheScene = queuedTicks != 0 && startedTicks != 0 && executedTicks != 0;
            AcceptedToQueuedMs = ReachedTheScene ? MsBetween(acceptedTicks, queuedTicks) : 0.0;
            QueuedToStartedMs = ReachedTheScene ? MsBetween(queuedTicks, startedTicks) : 0.0;
            StartedToExecutedMs = ReachedTheScene ? MsBetween(startedTicks, executedTicks) : 0.0;
            ExecutedToRespondedMs = ReachedTheScene ? MsBetween(executedTicks, respondedTicks) : 0.0;
            TotalMs = MsBetween(acceptedTicks, respondedTicks);
        }

        private static double MsBetween(long fromTicks, long toTicks) =>
            (toTicks - fromTicks) * 1000.0 / Stopwatch.Frequency;

        public string Format(string method, int validationRecomputes,
            string? stages = null, long sceneScans = 0, string? sceneScanners = null)
        {
            var head = $"[MCP][Timing] method={method} total={TotalMs:F2}ms ";
            if (!ReachedTheScene)
                return head + "answeredWithoutTheScene";

            var text = head
                + $"accepted->queued={AcceptedToQueuedMs:F2}ms queued->started={QueuedToStartedMs:F2}ms "
                + $"started->executed={StartedToExecutedMs:F2}ms executed->responded={ExecutedToRespondedMs:F2}ms "
                + $"validateRecomputes={validationRecomputes} sceneScans={sceneScans}";
            if (!string.IsNullOrEmpty(sceneScanners))
                text += $" [{sceneScanners}]";
            if (!string.IsNullOrEmpty(stages))
                text += $" stages=[{stages}]";
            return text;
        }
    }
}
