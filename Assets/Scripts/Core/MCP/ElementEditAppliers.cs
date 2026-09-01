using System;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    internal static class ElementEditAppliers
    {
        private delegate void Applier(EditOp op, KitchenElement el);

        private static readonly Applier[] All =
        {
            For<FacadeElement>((op, facade) =>
            {
                if (op.mode != null && McpWireEnums.TryParseDoorMode(op.mode, out var m)) facade.Mode = m;
                if (op.is_open.HasValue) facade.SetOpen(op.is_open.Value);
            }),
            For<AssembledFacadeElement>((op, asm) =>
            {
                if (op.fill != null) asm.Fill = McpWireEnums.ParseFill(op.fill);
            }),
            For<RadialShelfElement>((op, shelf) =>
            {
                if (op.corner_radius.HasValue) shelf.CornerRadius = op.corner_radius.Value;
            }),
            For<StoolElement>((op, stool) =>
            {
                if (op.corner_radius.HasValue) stool.CornerRadiusMM = op.corner_radius.Value;
            }),
            For<ChairElement>((op, chair) =>
            {
                if (op.corner_radius.HasValue) chair.CornerRadiusMM = op.corner_radius.Value;
                if (op.seat_height.HasValue) chair.SeatHeightMM = op.seat_height.Value;
            }),
            For<SofaElement>((op, sofa) =>
            {
                if (op.corner_radius.HasValue) sofa.CornerRadiusMM = op.corner_radius.Value;
                if (op.seat_height.HasValue) sofa.SeatHeightMM = op.seat_height.Value;
            }),
            For<CooktopElement>((op, cooktop) =>
            {
                if (op.cutout_width.HasValue) cooktop.CutoutWidthMM = op.cutout_width.Value;
                if (op.cutout_depth.HasValue) cooktop.CutoutDepthMM = op.cutout_depth.Value;
                if (op.cutout_width.HasValue || op.cutout_depth.HasValue) cooktop.SnapToPart();
            }),
            For<DrawerElement>((op, drawer) =>
            {
                if (op.drawer_system != null) drawer.System = McpWireEnums.ParseDrawerSystem(op.drawer_system);
                if (op.drawer_type != null) drawer.Type = McpWireEnums.ParseDrawerType(op.drawer_type);
                if (op.drawer_length.HasValue) drawer.NominalLength = op.drawer_length.Value;
                if (op.drawer_color != null) drawer.Color = McpWireEnums.ParseDrawerColor(op.drawer_color);
                if (op.internal_width.HasValue) drawer.InternalWidth = op.internal_width.Value;
                if (op.is_double.HasValue) drawer.IsDouble = op.is_double.Value;
                if (op.is_upper.HasValue) drawer.IsUpperDrawer = op.is_upper.Value;
                if (op.paired_drawer_name != null) drawer.PairedDrawerName = op.paired_drawer_name;
            }),
            For<IFacadeHost>((op, host) =>
            {
                if (op.attached_facade_name == null) return;
                var previous = host.FindAttachedFacade();
                host.AttachedFacadeName = op.attached_facade_name;
                host.OnAttachedFacadeChanged(previous, host.FindAttachedFacade());
            }),
            For<DishwasherElement>((op, dishwasher) =>
            {
                if (op.is_open.HasValue) dishwasher.SetOpen(op.is_open.Value);
            }),
            For<ITabletop>(ApplyTabletopAndLegsMaterials),
            For<TableElement>((op, table) =>
            {
                if (op.leg_inset_mm.HasValue) table.LegInsetMM = op.leg_inset_mm.Value;
            }),
            For<RadiusTableElement>((op, table) =>
            {
                if (op.leg_inset_mm.HasValue) table.LegInsetMM = op.leg_inset_mm.Value;
            }),
            For<PillarElement>((op, pillar) =>
            {
                if (op.mid_height_mm.HasValue) pillar.MidHeightMM = op.mid_height_mm.Value;
                if (op.diameter_mm.HasValue) pillar.DiameterMM = op.diameter_mm.Value;
            }),
            For<ScrewLegElement>((op, leg) =>
            {
                if (op.screw_thread != null) leg.Thread = op.screw_thread;
                if (op.screw_base_diameter_mm.HasValue) leg.BaseDiameterMM = op.screw_base_diameter_mm.Value;
                if (op.screw_base_height_mm.HasValue) leg.BaseHeightMM = op.screw_base_height_mm.Value;
                if (op.screw_thread_length_mm.HasValue) leg.ThreadLengthMM = op.screw_thread_length_mm.Value;
                if (op.screw_insertion_mm.HasValue) leg.InsertionDepthMM = op.screw_insertion_mm.Value;
            }),
            For<WindowElement>((op, window) =>
            {
                if (op.tint != null) window.Tint = McpWireEnums.ParseGlassTint(op.tint);
                if (op.sill_protrusion_mm.HasValue) window.SillProtrusionMM = op.sill_protrusion_mm.Value;
                if (op.mode != null && McpWireEnums.TryParseDoorMode(op.mode, out var m)) window.Mode = m;
                if (op.is_open.HasValue) window.SetOpen(op.is_open.Value);
            }),
            For<DoorElement>((op, door) =>
            {
                if (op.sash_type != null) door.SashType = McpWireEnums.ParseDoorSashType(op.sash_type);
                if (op.mode != null && McpWireEnums.TryParseDoorMode(op.mode, out var m)) door.Mode = m;
                if (op.is_open.HasValue) door.SetOpen(op.is_open.Value);
            }),
            For<OvenElement>((op, oven) =>
            {
                if (op.is_open.HasValue) oven.SetOpen(op.is_open.Value);
            }),
        };

        public static void ApplyTypeSpecific(EditOp op, KitchenElement el)
        {
            foreach (var applier in All) applier(op, el);
        }

        private static Applier For<T>(Action<EditOp, T> apply) where T : class
            => (op, el) => { if (el is T typed) apply(op, typed); };

        private static void ApplyTabletopAndLegsMaterials(EditOp op, ITabletop tabletop)
        {
            if (op.tabletop_material != null)
            {
                var def = MaterialCatalog.Find(op.tabletop_material);
                if (def != null) MaterialManager.ApplyTabletop(tabletop, def);
            }
            if (op.legs_material != null)
            {
                var def = MaterialCatalog.Find(op.legs_material);
                if (def != null) MaterialManager.ApplyLegs(tabletop, def);
            }
        }
    }
}
