using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class WallLayerQuantities
    {
        public static double GrossAreaM2(float lengthMm, float heightMm) =>
            Math.Max(0d, lengthMm) * 0.001d * (Math.Max(0d, heightMm) * 0.001d);

        public static double OpeningsAreaM2(IReadOnlyList<WallOpening>? openings)
        {
            if (openings == null) return 0d;
            var total = 0d;
            foreach (var opening in openings)
                total += Math.Max(0d, opening.WidthMm) * 0.001d
                         * (Math.Max(0d, opening.HeightMm) * 0.001d);
            return total;
        }

        public static double NetAreaM2(float lengthMm, float heightMm,
            IReadOnlyList<WallOpening>? openings)
        {
            var gross = GrossAreaM2(lengthMm, heightMm);
            var cut = OpeningsAreaM2(openings);
            return cut >= gross ? 0d : gross - cut;
        }

        public static double InsulationAreaM2(float lengthMm, float heightMm,
            IReadOnlyList<WallOpening>? openings) =>
            NetAreaM2(lengthMm, heightMm, openings);

        public static double InsulationVolumeM3(float lengthMm, float heightMm,
            float thicknessMm, IReadOnlyList<WallOpening>? openings) =>
            NetAreaM2(lengthMm, heightMm, openings) * (Math.Max(0f, thicknessMm) * 0.001d);

        public static double CladdingAreaM2(float lengthMm, float heightMm,
            IReadOnlyList<WallOpening>? openings) =>
            NetAreaM2(lengthMm, heightMm, openings);

        public static int VentGapBattenCount(float lengthMm, float stepMm)
        {
            if (lengthMm <= 0f || stepMm <= 0f) return 0;
            return (int)Math.Floor(lengthMm / stepMm) + 1;
        }

        public static double VentGapBattenRunningMetres(float lengthMm, float heightMm,
            float stepMm)
        {
            var count = VentGapBattenCount(lengthMm, stepMm);
            return count * (Math.Max(0f, heightMm) * 0.001d);
        }
    }
}
