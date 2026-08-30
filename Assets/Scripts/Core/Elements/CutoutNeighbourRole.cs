using System;

namespace KitchenDesigner.Core
{
    [Flags]
    public enum CutoutNeighbourRole
    {
        None = 0,
        BlocksCutout = 1,
        AlignsCutout = 2,
        Carcass = BlocksCutout | AlignsCutout,
    }
}
