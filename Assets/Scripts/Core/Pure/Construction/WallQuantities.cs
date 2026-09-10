using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class WallQuantities
    {
        public const float FrameStudStepMm = 600f;
        public const int FramePlateRuns = 2;

        public static double NetVolumeM3(float lengthMm, float heightMm, float thicknessMm,
            IReadOnlyList<WallOpening>? openings)
        {
            var gross = GrossVolumeM3(lengthMm, heightMm, thicknessMm);
            var cut = OpeningsVolumeM3(thicknessMm, openings);
            return cut >= gross ? 0d : gross - cut;
        }

        public static double GrossVolumeM3(float lengthMm, float heightMm, float thicknessMm) =>
            Math.Max(0d, lengthMm) * 0.001d
            * (Math.Max(0d, heightMm) * 0.001d)
            * (Math.Max(0d, thicknessMm) * 0.001d);

        public static double OpeningsVolumeM3(float thicknessMm,
            IReadOnlyList<WallOpening>? openings)
        {
            if (openings == null) return 0d;
            var total = 0d;
            foreach (var opening in openings)
                total += Math.Max(0d, opening.WidthMm) * 0.001d
                         * (Math.Max(0d, opening.HeightMm) * 0.001d)
                         * (Math.Max(0d, thicknessMm) * 0.001d);
            return total;
        }

        public static int StudCount(float lengthMm)
        {
            if (lengthMm <= 0f) return 0;
            return (int)Math.Floor(lengthMm / FrameStudStepMm) + 1;
        }

        public static WallQuantitiesResult Of(MasonryTechnology technology,
            float lengthMm, float heightMm, float thicknessMm,
            IReadOnlyList<WallOpening>? openings, float jointMm, float sparePercent)
        {
            var unit = MasonryUnit.Of(technology);
            var gross = GrossVolumeM3(lengthMm, heightMm, thicknessMm);
            var cut = OpeningsVolumeM3(thicknessMm, openings);
            var net = cut >= gross ? 0d : gross - cut;

            switch (unit.Counting)
            {
                case MasonryCounting.Pieces:
                    return Pieced(unit, gross, cut, net, jointMm, sparePercent);
                case MasonryCounting.Volume:
                    return new WallQuantitiesResult(technology, unit.Counting,
                        gross, cut, net, 0, 0, 0d, net, 0, 0d, 0d);
                case MasonryCounting.Studs:
                    return Framed(technology, unit, gross, cut, net, lengthMm, heightMm);
                default:
                    throw new ArgumentOutOfRangeException(nameof(technology),
                        technology, "неизвестный способ счёта кладки");
            }
        }

        private static WallQuantitiesResult Pieced(in MasonryUnit unit,
            double gross, double cut, double net, float jointMm, float sparePercent)
        {
            var jointed = unit.JointedVolumeM3(Math.Max(0d, jointMm));
            var laid = jointed <= 0d ? 0 : (int)Math.Ceiling(net / jointed);
            var spare = Math.Max(0d, sparePercent) * 0.01d;
            var bought = (int)Math.Ceiling(laid * (1d + spare));
            var mortar = net - laid * unit.BareVolumeM3;
            return new WallQuantitiesResult(unit.Technology, unit.Counting,
                gross, cut, net, laid, bought, Math.Max(0d, mortar), 0d, 0, 0d, 0d);
        }

        private static WallQuantitiesResult Framed(MasonryTechnology technology,
            in MasonryUnit unit, double gross, double cut, double net,
            float lengthMm, float heightMm)
        {
            var studs = StudCount(lengthMm);
            var studMetres = studs * Math.Max(0d, heightMm) * 0.001d;
            var plateMetres = FramePlateRuns * Math.Max(0d, lengthMm) * 0.001d;
            return new WallQuantitiesResult(technology, unit.Counting,
                gross, cut, net, 0, 0, 0d, 0d, studs, studMetres, plateMetres);
        }
    }
}
