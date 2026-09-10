using System.Collections.Generic;

namespace KitchenDesigner.Core.Plumbing
{
    public readonly struct PipeFittingPortSlot
    {
        public readonly int PortIndex;
        public readonly float DirX;
        public readonly float DirY;

        public PipeFittingPortSlot(int portIndex, float dirX, float dirY)
        {
            PortIndex = portIndex;
            DirX = dirX;
            DirY = dirY;
        }
    }

    public static class PipeFittingDiagramLayout
    {
        public static IReadOnlyList<PipeFittingPortSlot> SlotsFor(PipeNodeKind kind)
        {
            var legs = PipeFittingSpec.Legs(kind);
            var slots = new PipeFittingPortSlot[legs.Count];
            for (int i = 0; i < legs.Count; i++)
                slots[i] = new PipeFittingPortSlot(i, legs[i].X, legs[i].Y);
            return slots;
        }

        public static int MaxPortCount()
        {
            int max = 0;
            foreach (var kind in PipeFittingNames.Kinds)
            {
                int count = PipeFittingSpec.PortCount(kind);
                if (count > max) max = count;
            }
            return max;
        }
    }
}
