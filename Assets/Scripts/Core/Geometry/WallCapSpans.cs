using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public static class WallCapSpans
    {
        public const float MinCellNorm = 0.001f;

        public static float TopNorm => -DoorOpeningLayout.WallBaseNorm;

        public static bool ReachesBase(Span vertical) =>
            vertical.Min <= DoorOpeningLayout.WallBaseNorm + MinCellNorm;

        public static bool ReachesTop(Span vertical) =>
            vertical.Max >= TopNorm - MinCellNorm;

        public static bool Reaches(Span vertical, bool top) =>
            top ? ReachesTop(vertical) : ReachesBase(vertical);

        public static List<Span> Solid(IReadOnlyList<Span> holes, float from, float to)
        {
            var spans = new List<Span> { new Span(from, to) };

            foreach (var hole in holes)
            {
                var kept = new List<Span>();
                foreach (var span in spans)
                {
                    if (hole.Max <= span.Min || hole.Min >= span.Max) { kept.Add(span); continue; }
                    if (hole.Min - span.Min > MinCellNorm) kept.Add(new Span(span.Min, hole.Min));
                    if (span.Max - hole.Max > MinCellNorm) kept.Add(new Span(hole.Max, span.Max));
                }
                spans = kept;
            }

            return spans;
        }
    }
}
