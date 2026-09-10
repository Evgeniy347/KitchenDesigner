using System;
using System.Collections.Generic;
using KitchenDesigner.Core;

namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeRunFit
    {
        public const float ReachMm = 2f;

        public readonly int LengthMm;
        public readonly PointMm CentreMm;

        private PipeRunFit(int lengthMm, in PointMm centreMm)
        {
            LengthMm = lengthMm;
            CentreMm = centreMm;
        }

        public static PipeRunFit Between(in PointMm mouthA, in PointMm mouthB)
        {
            float spanMm = mouthA.DistanceMmTo(mouthB);
            int length = PipeElementSpec.ClampLengthMM(
                (int)Math.Round(spanMm, MidpointRounding.AwayFromZero));
            var centre = new PointMm(
                (mouthA.XMm + mouthB.XMm) * 0.5f,
                (mouthA.YMm + mouthB.YMm) * 0.5f,
                (mouthA.ZMm + mouthB.ZMm) * 0.5f);
            return new PipeRunFit(length, centre);
        }

        public static PipeRunFit? ForRun(in PipePort endA, in PipePort endB,
            IReadOnlyList<PipePort> mouths, float reachMm = ReachMm)
        {
            var a = FacingMouth(endA, mouths, reachMm);
            var b = FacingMouth(endB, mouths, reachMm);
            if (!a.HasValue || !b.HasValue) return null;
            if (string.Equals(a.Value.ElementId, b.Value.ElementId, StringComparison.Ordinal)
                && a.Value.PortIndex == b.Value.PortIndex) return null;
            return Between(a.Value.PositionMm, b.Value.PositionMm);
        }

        public float ResidueMm(in PointMm mouthA, in PointMm mouthB) =>
            Math.Abs(LengthMm - mouthA.DistanceMmTo(mouthB)) * 0.5f;

        public bool Holds(in PointMm mouthA, in PointMm mouthB) =>
            ResidueMm(mouthA, mouthB) <= PipeJoint.JoinToleranceMm;

        private static PipePort? FacingMouth(in PipePort end, IReadOnlyList<PipePort> mouths,
            float reachMm)
        {
            if (mouths == null) return null;

            PipePort? best = null;
            float bestGap = reachMm;
            for (int i = 0; i < mouths.Count; i++)
            {
                var mouth = mouths[i];
                if (string.Equals(mouth.ElementId, end.ElementId, StringComparison.Ordinal))
                    continue;
                if (!PipeConnectionRule.CanConnect(end.OwnerKind, mouth.OwnerKind)) continue;
                if (!PipeAxis.AreOpposite(end.OutwardAxis, mouth.OutwardAxis)) continue;

                float gap = end.PositionMm.DistanceMmTo(mouth.PositionMm);
                if (gap > bestGap) continue;
                bestGap = gap;
                best = mouth;
            }
            return best;
        }
    }
}
