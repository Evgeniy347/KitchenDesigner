using System;

namespace KitchenDesigner.Core.Construction
{
    public static class SlabQuantities
    {
        public static double ConcreteM3(double polygonAreaM2, float thicknessMm) =>
            Math.Max(0d, polygonAreaM2) * (Math.Max(0f, thicknessMm) * 0.001d);

        public static int MeshBarCountAcross(float spanMm, float stepMm)
        {
            if (spanMm <= 0f || stepMm <= 0f) return 0;
            return (int)Math.Floor(spanMm / stepMm) + 1;
        }

        public static double MeshRebarLengthM(float lengthMm, float widthMm, float stepMm)
        {
            int barsAlongLength = MeshBarCountAcross(widthMm, stepMm);
            int barsAlongWidth = MeshBarCountAcross(lengthMm, stepMm);
            if (barsAlongLength <= 0 || barsAlongWidth <= 0) return 0d;

            double lengthM = Math.Max(0f, lengthMm) * 0.001d;
            double widthM = Math.Max(0f, widthMm) * 0.001d;
            return barsAlongLength * lengthM + barsAlongWidth * widthM;
        }

        public static double RebarKg(float lengthMm, float widthMm, float stepMm, float diameterMm)
        {
            double totalLengthM = MeshRebarLengthM(lengthMm, widthMm, stepMm);
            if (totalLengthM <= 0d) return 0d;
            return totalLengthM * FoundationQuantities.RebarMassPerMetreKg(diameterMm);
        }
    }
}
