using System;

namespace KitchenDesigner.Core.MCP
{
    public static class PlanLayers
    {
        private static readonly string[] Floors = { "floor", "floor_slab", "foundation" };

        private static readonly string[] Appliances =
        {
            "sink", "cooktop", "oven", "dishwasher",
            LaundryMachineBody.TypeId(LaundryMachineKind.Washer),
            LaundryMachineBody.TypeId(LaundryMachineKind.Dryer),
        };

        public static PlanLayer Of(DigestEntry entry)
        {
            if (entry.IsModule) return PlanLayer.Module;
            if (Array.IndexOf(Floors, entry.Kind) >= 0) return PlanLayer.Floor;
            if (Array.IndexOf(Appliances, entry.Kind) >= 0) return PlanLayer.Appliance;
            switch (entry.Kind)
            {
                case "wall": return PlanLayer.Wall;
                case "door": return PlanLayer.Door;
                case "window": return PlanLayer.Window;
                default: return PlanLayer.Part;
            }
        }

        public static bool IsThin(PlanLayer layer) =>
            layer == PlanLayer.Wall || layer == PlanLayer.Door || layer == PlanLayer.Window;

        public static bool IsSolid(PlanLayer layer) =>
            layer == PlanLayer.Module || layer == PlanLayer.Appliance || layer == PlanLayer.Part;
    }
}
