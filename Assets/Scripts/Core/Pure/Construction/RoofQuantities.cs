using System;

namespace KitchenDesigner.Core.Construction
{
    public static class RoofQuantities
    {
        public const double MinCosPitch = 1e-6d;

        public static double PlanAreaM2(RoofFrame frame)
        {
            if (frame.Planes == null) return 0d;
            double total = 0d;
            foreach (var plane in frame.Planes) total += plane.PlanAreaM2;
            return total;
        }

        public static double PitchAreaM2(RoofFrame frame, float pitchDeg)
        {
            var cos = CosPitch(pitchDeg);
            if (cos <= MinCosPitch) return 0d;
            return PlanAreaM2(frame) / cos;
        }

        public static double CoveringAreaM2(double pitchAreaM2, float wastePercent) =>
            Math.Max(0d, pitchAreaM2) * (1d + Math.Max(0f, wastePercent) * 0.01d);

        public static double RidgeLengthM(RoofFrame frame) => Math.Max(0f, frame.RidgeLengthMm) * 0.001d;

        public static double EaveLengthM(RoofFrame frame) => Math.Max(0f, frame.EaveLengthMm) * 0.001d;

        public static double GutterLengthM(RoofFrame frame) => Math.Max(0f, frame.GutterLengthMm) * 0.001d;

        public static int RafterCountPerPlane(float eaveEdgeMm, float stepMm)
        {
            if (eaveEdgeMm <= 0f || stepMm <= 0f) return 0;
            return (int)Math.Floor(eaveEdgeMm / stepMm) + 1;
        }

        public static int RafterCount(RoofFrame frame, float stepMm)
        {
            if (frame.Planes == null) return 0;
            int total = 0;
            foreach (var plane in frame.Planes)
                total += RafterCountPerPlane(plane.EaveEdgeMm, stepMm);
            return total;
        }

        public static double RafterLengthM(float runMm, float pitchDeg)
        {
            var cos = CosPitch(pitchDeg);
            if (cos <= MinCosPitch) return 0d;
            return (Math.Max(0f, runMm) * 0.001d) / cos;
        }

        private static double CosPitch(float pitchDeg) => Math.Cos(pitchDeg * Math.PI / 180d);

        public static double RafterVolumeM3(RoofFrame frame, float stepMm, float pitchDeg)
        {
            if (frame.Planes == null) return 0d;

            double total = 0d;
            foreach (var plane in frame.Planes)
            {
                var count = RafterCountPerPlane(plane.EaveEdgeMm, stepMm);
                if (count <= 0) continue;

                var section = RafterSectionTable.ForSpan(plane.RunMm);
                var lengthM = RafterLengthM(plane.RunMm, pitchDeg);
                total += count * lengthM * (section.WidthMm * 0.001d) * (section.HeightMm * 0.001d);
            }

            return total;
        }
    }
}
