using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core.Plumbing;

namespace KitchenDesigner.Core
{
    public static class PipePartHighlight
    {
        public static HighlightSleeve PipeEnd(int lengthMM, float outerDiameterMm, PartEnd end)
        {
            float depth = PartHighlightBands.DepthMM(lengthMM);
            if (depth <= 0f || outerDiameterMm <= 0f) return default;

            float sign = end == PartEnd.Start ? -1f : 1f;
            float half = lengthMM * 0.5f;
            var tip = new Vector3(0f, half * sign, 0f);
            var root = new Vector3(0f, (half - depth) * sign, 0f);
            return new HighlightSleeve(root, tip, outerDiameterMm * 0.5f);
        }

        public static HighlightSleeve FittingMouth(PipeNodeKind kind, string? frameSizeId,
            IReadOnlyList<string?>? boreSizeIds, int portIndex)
        {
            if (portIndex < 0 || portIndex >= PipeFittingSpec.PortCount(kind)) return default;

            var port = PipeFittingSpec.PortOffsetMm(kind, frameSizeId, portIndex);
            var mouth = new Vector3(port.XMm, port.YMm, port.ZMm);
            var hub = PipeFittingLayout.HubMM(kind, frameSizeId);

            var leg = mouth - hub;
            float legLength = leg.magnitude;
            float depth = PartHighlightBands.MouthDepthMM(legLength);
            if (depth <= 0f) return default;

            return new HighlightSleeve(
                mouth - leg / legLength * depth,
                mouth,
                PipeFittingSpec.BodyDiameterMm(BoreAt(boreSizeIds, portIndex, frameSizeId)) * 0.5f);
        }

        private static string BoreAt(IReadOnlyList<string?>? boreSizeIds, int portIndex,
            string? frameSizeId)
        {
            if (boreSizeIds != null && portIndex < boreSizeIds.Count
                && boreSizeIds[portIndex] != null)
                return boreSizeIds[portIndex]!;

            return boreSizeIds == null || boreSizeIds.Count == 0
                ? PipeSpec.NormalizeSize(frameSizeId)
                : PipeSizes.Widest(boreSizeIds);
        }
    }
}
