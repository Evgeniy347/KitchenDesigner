using System.Collections.Generic;
using UnityEngine;

namespace KitchenDesigner.Core.Plumbing
{
    public static class PipeFittingLayout
    {
        public static PipeSegment[] PartsMM(PipeNodeKind kind, string? frameSizeId,
            IReadOnlyList<string?>? boreSizeIds)
        {
            var parts = RawPartsMM(kind, frameSizeId, boreSizeIds);
            var hub = HubMM(kind, frameSizeId);

            for (int i = 0; i < parts.Length; i++)
                parts[i] = new PipeSegment(parts[i].FromMM + hub, parts[i].ToMM + hub,
                    parts[i].FromRadiusMM, parts[i].ToRadiusMM);

            return parts;
        }

        public static Vector3 HubMM(PipeNodeKind kind, string? frameSizeId) =>
            -PipeBounds.Of(RawPartsMM(kind, frameSizeId, null)).center;

        public static Vector3 CoverSizeMM(PipeNodeKind kind, string? frameSizeId,
            IReadOnlyList<string?>? boreSizeIds)
        {
            var box = PipeBounds.Of(PartsMM(kind, frameSizeId, boreSizeIds));
            return new Vector3(
                PivotCentredReach(box.min.x, box.max.x),
                PivotCentredReach(box.min.y, box.max.y),
                PivotCentredReach(box.min.z, box.max.z));
        }

        public static string?[] UniformBores(string? boreSizeId) => new[] { boreSizeId };

        private static float PivotCentredReach(float min, float max) =>
            2f * Mathf.Max(Mathf.Abs(min), Mathf.Abs(max));

        private static PipeSegment[] RawPartsMM(PipeNodeKind kind, string? frameSizeId,
            IReadOnlyList<string?>? boreSizeIds)
        {
            var axes = PipeFittingSpec.Legs(kind);
            float leg = PipeFittingSpec.LegLengthMm(frameSizeId);
            string widest = WidestBore(boreSizeIds, frameSizeId);
            var parts = new List<PipeSegment>();

            for (int i = 0; i < axes.Count; i++)
                parts.Add(new PipeSegment(Vector3.zero, DirectionOf(axes[i]) * leg,
                    PipeFittingSpec.BodyDiameterMm(BoreOf(boreSizeIds, i, widest)) * 0.5f));

            if (PipeFittingSpec.HasFlange(kind))
                parts.Add(new PipeSegment(Vector3.zero,
                    Vector3.down * PipeFittingSpec.FlangeThicknessMm,
                    PipeFittingSpec.FlangeDiameterMm(widest) * 0.5f));

            if (kind == PipeNodeKind.Cap) AddBlindEnd(parts, widest);
            AddFlowArrows(parts, kind, widest, leg);

            return parts.ToArray();
        }

        private static void AddBlindEnd(List<PipeSegment> parts, string boreSizeId)
        {
            float crown = PipeFittingSpec.CapCrownRadiusMm(boreSizeId);
            float collar = PipeFittingSpec.CapCollarThicknessMm;
            float rise = crown * PipeFittingSpec.CapDomeRiseFactor;

            parts.Add(new PipeSegment(Vector3.zero, Vector3.down * collar, crown));
            parts.Add(new PipeSegment(Vector3.down * collar, Vector3.down * (collar + rise),
                crown, crown * PipeFittingSpec.CapDomeTipFactor));
        }

        private static void AddFlowArrows(List<PipeSegment> parts, PipeNodeKind kind,
            string boreSizeId, float legMm)
        {
            float flow = PipeFittingSpec.FlowDirection(kind);
            if (flow == 0f) return;

            float body = PipeFittingSpec.BodyDiameterMm(boreSizeId) * 0.5f;
            float gap = PipeFittingSpec.FlangeDiameterMm(boreSizeId) * 0.5f - body;
            if (gap <= 0f) return;

            float offset = body + gap * 0.5f;
            float radius = gap * 0.5f * PipeFittingSpec.FlowArrowGapFillFactor;
            float length = legMm * PipeFittingSpec.FlowArrowLengthFactor;
            float head = length * PipeFittingSpec.FlowArrowHeadFactor;
            float tail = legMm * 0.5f - flow * length * 0.5f;
            float neck = tail + flow * (length - head);
            float tip = tail + flow * length;

            for (int side = -1; side <= 1; side += 2)
            {
                float x = offset * side;
                parts.Add(new PipeSegment(new Vector3(x, tail, 0f), new Vector3(x, neck, 0f),
                    radius * PipeFittingSpec.FlowArrowShaftFactor));
                parts.Add(new PipeSegment(new Vector3(x, neck, 0f), new Vector3(x, tip, 0f),
                    radius, 0f));
            }
        }

        private static Vector3 DirectionOf(in PipeAxis axis) =>
            new Vector3(axis.X, axis.Y, axis.Z);

        private static string WidestBore(IReadOnlyList<string?>? boreSizeIds, string? frameSizeId)
            => boreSizeIds == null || boreSizeIds.Count == 0
                ? PipeSpec.NormalizeSize(frameSizeId)
                : PipeSizes.Widest(boreSizeIds);

        private static string BoreOf(IReadOnlyList<string?>? boreSizeIds, int index,
            string fallback)
            => boreSizeIds != null && index < boreSizeIds.Count && boreSizeIds[index] != null
                ? boreSizeIds[index]!
                : fallback;
    }
}
