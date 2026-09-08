using System.Collections.Generic;
using UnityEngine;
using KitchenDesigner.Core;

namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeSeatCandidate
    {
        public readonly int PortIndex;
        public readonly int TwistSteps;
        public readonly int LinkedPortCount;

        public PipeSeatCandidate(int portIndex, int twistSteps, int linkedPortCount)
        {
            PortIndex = portIndex;
            TwistSteps = twistSteps;
            LinkedPortCount = linkedPortCount;
        }
    }

    public static class PipeFittingSeatChoice
    {
        public static PipeSeatCandidate BestForNewFitting(PipeNodeKind kind, string? frameSizeId,
            in PipeAxis mandatoryOutwardAxis, in PointMm mandatoryPositionMm,
            IReadOnlyList<PipePort> otherFreePorts)
        {
            var legs = PipeFittingSpec.Legs(kind);
            var best = new PipeSeatCandidate(0, 0, -1);

            for (int p = 0; p < legs.Count; p++)
            {
                for (int twist = 0; twist < 4; twist++)
                {
                    int linked = 1 + BonusLinks(kind, frameSizeId, p, twist,
                        mandatoryOutwardAxis, mandatoryPositionMm, otherFreePorts);
                    if (linked > best.LinkedPortCount)
                        best = new PipeSeatCandidate(p, twist, linked);
                }
            }
            return best;
        }

        private static int BonusLinks(PipeNodeKind kind, string? frameSizeId, int portIndex,
            int twistSteps, in PipeAxis mandatoryOutwardAxis, in PointMm mandatoryPositionMm,
            IReadOnlyList<PipePort> otherFreePorts)
        {
            var legs = PipeFittingSpec.Legs(kind);
            Vector3 s = AsVector(legs[portIndex]);
            Vector3 t = AsVector(mandatoryOutwardAxis.Opposite);
            var ownOffset = PipeFittingSpec.PortOffsetMm(kind, frameSizeId, portIndex);

            int bonus = 0;
            for (int i = 0; i < legs.Count; i++)
            {
                if (i == portIndex) continue;

                var otherOffset = PipeFittingSpec.PortOffsetMm(kind, frameSizeId, i);
                Vector3 deltaLocal = AsVector(otherOffset) - AsVector(ownOffset);
                Vector3 deltaWorld = PortTurn.RotateWithTwist(deltaLocal, s, t, twistSteps);
                Vector3 worldAxisVec = PortTurn.RotateWithTwist(AsVector(legs[i]), s, t, twistSteps);

                var worldPos = new PointMm(
                    mandatoryPositionMm.XMm + deltaWorld.x,
                    mandatoryPositionMm.YMm + deltaWorld.y,
                    mandatoryPositionMm.ZMm + deltaWorld.z);
                var worldAxis = new PipeAxis(worldAxisVec.x, worldAxisVec.y, worldAxisVec.z);
                var probe = new PipePort("§candidate", kind, i, worldPos, worldAxis);

                if (ConnectsAny(probe, otherFreePorts)) bonus++;
            }
            return bonus;
        }

        private static bool ConnectsAny(in PipePort probe, IReadOnlyList<PipePort> candidates)
        {
            if (candidates == null) return false;
            for (int i = 0; i < candidates.Count; i++)
                if (PipeJoint.Connects(probe, candidates[i])) return true;
            return false;
        }

        private static Vector3 AsVector(in PipeAxis a) => new Vector3(a.X, a.Y, a.Z);

        private static Vector3 AsVector(in PointMm p) => new Vector3(p.XMm, p.YMm, p.ZMm);
    }
}
