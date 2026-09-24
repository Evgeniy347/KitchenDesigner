using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core.Construction
{
    public static class FenceQuantities
    {
        public static int PostsPerRun(float lengthMm, float postStepMm)
        {
            if (lengthMm <= 0f || postStepMm <= 0f) return 0;
            return (int)Math.Ceiling(lengthMm / postStepMm) + 1;
        }

        public static int SpansPerRun(float lengthMm, float postStepMm)
        {
            if (lengthMm <= 0f || postStepMm <= 0f) return 0;
            return (int)Math.Ceiling(lengthMm / postStepMm);
        }

        public static int PostCount(IReadOnlyList<FenceRun>? runs, float postStepMm)
        {
            if (runs == null) return 0;
            var total = 0;
            foreach (var run in runs)
                total += PostsPerRun(run.LengthMm, postStepMm);
            return total;
        }

        public static int SpanCount(IReadOnlyList<FenceRun>? runs, float postStepMm)
        {
            if (runs == null) return 0;
            var total = 0;
            foreach (var run in runs)
                total += SpansPerRun(run.LengthMm, postStepMm);
            return total;
        }

        public static double TotalLengthM(IReadOnlyList<FenceRun>? runs)
        {
            if (runs == null) return 0d;
            var total = 0d;
            foreach (var run in runs)
                total += Math.Max(0f, run.LengthMm) * 0.001d;
            return total;
        }

        public static double PostHoleVolumeM3(float holeDiameterMm, float holeDepthMm)
        {
            var radiusM = Math.Max(0f, holeDiameterMm) * 0.0005d;
            var depthM = Math.Max(0f, holeDepthMm) * 0.001d;
            return Math.PI * radiusM * radiusM * depthM;
        }

        public static double PostConcreteM3(int postCount, float holeDiameterMm, float holeDepthMm)
        {
            if (postCount <= 0) return 0d;
            return postCount * PostHoleVolumeM3(holeDiameterMm, holeDepthMm);
        }

        public static int SheetsPerRun(float lengthMm, float sheetWidthMm)
        {
            if (lengthMm <= 0f || sheetWidthMm <= 0f) return 0;
            return (int)Math.Ceiling(lengthMm / sheetWidthMm);
        }

        public static int SheetCount(IReadOnlyList<FenceRun>? runs, float sheetWidthMm)
        {
            if (runs == null) return 0;
            var total = 0;
            foreach (var run in runs)
                total += SheetsPerRun(run.LengthMm, sheetWidthMm);
            return total;
        }

        public static double SheetAreaM2(IReadOnlyList<FenceRun>? runs, float heightMm) =>
            TotalLengthM(runs) * (Math.Max(0f, heightMm) * 0.001d);

        public static double RailRunningMetres(IReadOnlyList<FenceRun>? runs, int railCount)
        {
            if (railCount <= 0) return 0d;
            return TotalLengthM(runs) * railCount;
        }
    }
}
