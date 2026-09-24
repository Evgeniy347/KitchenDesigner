using System;

namespace KitchenDesigner.Core.Construction
{
    public readonly struct RoofPlaneFootprint
    {
        public readonly float RunMm;
        public readonly float EaveEdgeMm;
        public readonly float RidgeEdgeMm;

        public RoofPlaneFootprint(float runMm, float eaveEdgeMm, float ridgeEdgeMm)
        {
            RunMm = runMm;
            EaveEdgeMm = eaveEdgeMm;
            RidgeEdgeMm = ridgeEdgeMm;
        }

        public double PlanAreaM2
        {
            get
            {
                double avgEdgeM = (Math.Max(0f, EaveEdgeMm) + Math.Max(0f, RidgeEdgeMm)) * 0.5d * 0.001d;
                double runM = Math.Max(0f, RunMm) * 0.001d;
                return avgEdgeM * runM;
            }
        }
    }
}
