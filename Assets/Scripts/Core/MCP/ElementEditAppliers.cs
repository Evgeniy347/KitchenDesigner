using System;
using System.Collections.Generic;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    internal static class ElementEditAppliers
    {
        private delegate void Applier(EditOp op, KitchenElement el, List<IUndoCommand> undo);

        private static readonly Applier[] All =
        {
            For<FacadeElement>((op, facade) =>
            {
                if (op.mode != null && McpWireEnums.TryParseDoorMode(op.mode, out var m)) facade.Mode = m;
            }),
            For<AssembledFacadeElement>((op, asm) =>
            {
                if (op.fill != null) asm.Fill = McpWireEnums.ParseFill(op.fill);
            }),
            For<IHasCornerRadius>((op, rounded) =>
            {
                if (op.corner_radius.HasValue) rounded.CornerRadiusMM = op.corner_radius.Value;
            }),
            For<IHasSeatHeight>((op, seated) =>
            {
                if (op.seat_height.HasValue) seated.SeatHeightMM = op.seat_height.Value;
            }),
            For<IHasEdgeRadius>((op, edged) =>
            {
                if (op.edge_radius.HasValue) edged.EdgeRadiusMM = op.edge_radius.Value;
            }),
            For<PouffeElement>((op, pouffe) =>
            {
                if (op.pouffe_seat_thickness.HasValue)
                    pouffe.SeatThicknessMM = op.pouffe_seat_thickness.Value;
            }),
            For<WallHungToiletElement>((op, toilet) =>
            {
                if (op.flush_plate_height.HasValue)
                    toilet.FlushPlateHeightMM = op.flush_plate_height.Value;
            }),
            For<BathtubElement>((op, tub) =>
            {
                if (op.rim_width.HasValue) tub.RimWidthMM = op.rim_width.Value;
                if (op.bowl_depth.HasValue) tub.BowlDepthMM = op.bowl_depth.Value;
                if (op.bowl_radius.HasValue) tub.BowlRadiusMM = op.bowl_radius.Value;
                if (op.bowl_fillet.HasValue) tub.BowlFilletMM = op.bowl_fillet.Value;
            }),
            For<BathMixerElement>((op, mixer) =>
            {
                if (op.mixer_body_diameter.HasValue)
                    mixer.BodyDiameterMM = op.mixer_body_diameter.Value;
                if (op.mixer_body_length.HasValue)
                    mixer.BodyLengthMM = op.mixer_body_length.Value;
                if (op.mixer_centres.HasValue) mixer.CentresMM = op.mixer_centres.Value;
                if (op.mixer_escutcheon_reach.HasValue)
                    mixer.EscutcheonReachMM = op.mixer_escutcheon_reach.Value;
                if (op.mixer_spout_length.HasValue)
                    mixer.SpoutLengthMM = op.mixer_spout_length.Value;
                if (op.mixer_outlet_diameter.HasValue)
                    mixer.OutletDiameterMM = op.mixer_outlet_diameter.Value;
            }),
            For<ShowerColumnElement>((op, column) =>
            {
                if (op.shower_riser_diameter.HasValue)
                    column.RiserDiameterMM = op.shower_riser_diameter.Value;
                if (op.shower_wall_offset.HasValue)
                    column.WallOffsetMM = op.shower_wall_offset.Value;
                if (op.shower_column_height.HasValue)
                    column.ColumnHeightMM = op.shower_column_height.Value;
                if (op.shower_arm_reach.HasValue)
                    column.ArmReachMM = op.shower_arm_reach.Value;
                if (op.shower_head_diameter.HasValue)
                    column.HeadDiameterMM = op.shower_head_diameter.Value;
                if (op.shower_head_thickness.HasValue)
                    column.HeadThicknessMM = op.shower_head_thickness.Value;
                if (op.shower_hand_diameter.HasValue)
                    column.HandShowerDiameterMM = op.shower_hand_diameter.Value;
                if (op.shower_hose_length.HasValue)
                    column.HoseLengthMM = op.shower_hose_length.Value;
            }),
            For<IWallDevice>((op, device) =>
            {
                if (op.wall_device_plate_width.HasValue)
                    device.PlateWidthMM = op.wall_device_plate_width.Value;
                if (op.wall_device_plate_height.HasValue)
                    device.PlateHeightMM = op.wall_device_plate_height.Value;
                if (op.wall_device_protrusion.HasValue)
                    device.ProtrusionMM = op.wall_device_protrusion.Value;
                if (op.wall_device_posts.HasValue)
                    device.PostCount = op.wall_device_posts.Value;
            }),
            For<ILightSwitch>((op, source) =>
            {
                if (op.switch_on.HasValue) source.IsOn = op.switch_on.Value;
                if (op.switch_lights != null)
                    source.SetLightNames(SwitchLightLinks.Sanitized(op.switch_lights,
                        LightSwitchNetwork.LiveLightNames()));
            }),
            For<BedElement>((op, bed) =>
            {
                if (op.bed_double.HasValue) bed.IsDouble = op.bed_double.Value;
                if (op.bed_headboard.HasValue) bed.HasHeadboard = op.bed_headboard.Value;
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
            For<IHasTwoDecorSlots>(ApplyDecorSlotMaterials),
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
            For<PipeElement>((op, pipe) =>
            {
                if (op.pipe_size != null) pipe.SizeId = op.pipe_size;
            }),
            For<ScrewLegElement>((op, leg) =>
            {
                if (op.screw_thread != null) leg.Thread = op.screw_thread;
                if (op.screw_base_diameter_mm.HasValue) leg.BaseDiameterMM = op.screw_base_diameter_mm.Value;
                if (op.screw_base_height_mm.HasValue) leg.BaseHeightMM = op.screw_base_height_mm.Value;
                if (op.screw_thread_length_mm.HasValue) leg.ThreadLengthMM = op.screw_thread_length_mm.Value;
                if (op.screw_left_mm.HasValue) leg.LeftInHostMM = op.screw_left_mm.Value;
                if (op.screw_right_mm.HasValue) leg.RightInHostMM = op.screw_right_mm.Value;
                if (op.screw_top_mm.HasValue) leg.TopInHostMM = op.screw_top_mm.Value;
                if (op.screw_bottom_mm.HasValue) leg.BottomInHostMM = op.screw_bottom_mm.Value;
            }),
            For<WindowElement>((op, window) =>
            {
                if (op.tint != null) window.Tint = McpWireEnums.ParseGlassTint(op.tint);
                if (op.sill_protrusion_mm.HasValue) window.SillProtrusionMM = op.sill_protrusion_mm.Value;
                if (op.mode != null && McpWireEnums.TryParseDoorMode(op.mode, out var m)) window.Mode = m;
            }),
            For<DoorElement>((op, door) =>
            {
                if (op.sash_type != null) door.SashType = McpWireEnums.ParseDoorSashType(op.sash_type);
                if (op.mode != null && McpWireEnums.TryParseDoorMode(op.mode, out var m)) door.Mode = m;
            }),
            For<LaundryMachineElement>((op, machine) =>
            {
                if (op.laundry_kind != null)
                    machine.Kind = McpWireEnums.ParseLaundryKind(op.laundry_kind);
            }),
            For<IOpenable>((op, openable) =>
            {
                if (op.is_open.HasValue) openable.SetOpen(op.is_open.Value);
            }),
            For<FoundationElement>((op, foundation) =>
            {
                if (op.foundation_soil != null)
                    foundation.SoilKind = McpWireEnums.ParseSoilKind(op.foundation_soil);
                if (op.foundation_sand_mm.HasValue) foundation.SandMm = op.foundation_sand_mm.Value;
                if (op.foundation_gravel_mm.HasValue) foundation.GravelMm = op.foundation_gravel_mm.Value;
                if (op.foundation_compacted.HasValue) foundation.Compacted = op.foundation_compacted.Value;
                if (op.foundation_concrete != null)
                    foundation.ConcreteGrade = McpWireEnums.ParseConcreteGrade(op.foundation_concrete);
                if (op.foundation_rebar_diameter_mm.HasValue)
                    foundation.RebarDiameterMm = op.foundation_rebar_diameter_mm.Value;
                if (op.foundation_rebar_step_mm.HasValue)
                    foundation.RebarStepMm = op.foundation_rebar_step_mm.Value;
                if (op.foundation_cover_mm.HasValue) foundation.CoverMm = op.foundation_cover_mm.Value;
            }),
            For<RoofElement>((op, roof) =>
            {
                if (op.roof_type != null) roof.Type = McpWireEnums.ParseRoofType(op.roof_type);
                if (op.roof_ridge_axis != null)
                    roof.RidgeAxis = McpWireEnums.ParseRoofRidgeAxis(op.roof_ridge_axis);
                if (op.roof_pitch_deg.HasValue) roof.PitchDeg = op.roof_pitch_deg.Value;
                if (op.roof_overhang_mm.HasValue) roof.OverhangMm = op.roof_overhang_mm.Value;
                if (op.roof_rafter_step_mm.HasValue) roof.RafterStepMm = op.roof_rafter_step_mm.Value;
            }),
            For<FloorSlabElement>((op, slab) =>
            {
                if (op.floor_slab_technology != null)
                    slab.Technology = McpWireEnums.ParseSlabTechnology(op.floor_slab_technology);
                if (op.floor_slab_concrete != null)
                    slab.ConcreteGrade = McpWireEnums.ParseConcreteGrade(op.floor_slab_concrete);
                if (op.floor_slab_rebar_diameter_mm.HasValue)
                    slab.RebarDiameterMm = op.floor_slab_rebar_diameter_mm.Value;
                if (op.floor_slab_rebar_step_mm.HasValue)
                    slab.RebarStepMm = op.floor_slab_rebar_step_mm.Value;
            }),
            For<FenceElement>((op, fence) =>
            {
                if (op.fence_post_section_mm.HasValue) fence.PostSectionMm = op.fence_post_section_mm.Value;
                if (op.fence_post_step_mm.HasValue) fence.PostStepMm = op.fence_post_step_mm.Value;
                if (op.fence_pit_depth_mm.HasValue) fence.PitDepthMm = op.fence_pit_depth_mm.Value;
                if (op.fence_sheet_mark != null)
                    fence.SheetMark = McpWireEnums.ParseFenceSheetMark(op.fence_sheet_mark);
            }),
            For<DuctElement>((op, duct) =>
            {
                if (op.duct_profile_kind != null)
                    duct.ProfileKind = McpWireEnums.ParseDuctProfileKind(op.duct_profile_kind);
                if (op.duct_diameter_mm.HasValue) duct.DiameterMm = op.duct_diameter_mm.Value;
                if (op.duct_width_mm.HasValue) duct.RectWidthMm = op.duct_width_mm.Value;
                if (op.duct_height_mm.HasValue) duct.RectHeightMm = op.duct_height_mm.Value;
                if (op.duct_airflow_m3_per_hour.HasValue)
                    duct.AirflowM3PerHour = op.duct_airflow_m3_per_hour.Value;
            }),
            For<GrilleElement>((op, grille) =>
            {
                if (op.grille_airflow_m3_per_hour.HasValue)
                    grille.AirflowM3PerHour = op.grille_airflow_m3_per_hour.Value;
            }),
            ForRecording<WallLayerElement>((op, layer, undo) =>
            {
                if (op.wall_layer_host_wall_name != null) Rehost(layer, op.wall_layer_host_wall_name, undo);
                if (op.wall_layer_thickness_mm.HasValue) layer.ThicknessMm = op.wall_layer_thickness_mm.Value;
            }),
            For<VentGapElement>((op, ventGap) =>
            {
                if (op.wall_layer_batten_step_mm.HasValue) ventGap.BattenStepMm = op.wall_layer_batten_step_mm.Value;
            }),
            (op, el, undo) =>
            {
                var wall = el.GetComponent<Wall>();
                if (wall == null) return;
                if (op.masonry != null) wall.Masonry = McpWireEnums.ParseMasonry(op.masonry);
                if (op.masonry_joint_mm.HasValue) wall.JointMm = op.masonry_joint_mm.Value;
                if (op.masonry_waste_pct.HasValue) wall.WastePct = op.masonry_waste_pct.Value;
            },
            (op, el, undo) =>
            {
                if (!op.load_bearing.HasValue) return;
                var wall = el.GetComponent<Wall>();
                if (wall != null) wall.LoadBearing = op.load_bearing.Value;
            },
        };

        public static void ApplyTypeSpecific(EditOp op, KitchenElement el, List<IUndoCommand> undo)
        {
            foreach (var applier in All) applier(op, el, undo);
        }

        private static Applier For<T>(Action<EditOp, T> apply) where T : class
            => (op, el, undo) => { if (el is T typed) apply(op, typed); };

        private static Applier ForRecording<T>(Action<EditOp, T, List<IUndoCommand>> apply) where T : class
            => (op, el, undo) => { if (el is T typed) apply(op, typed, undo); };

        private static void Rehost(WallLayerElement layer, string newHostWallName, List<IUndoCommand> undo)
        {
            string hostBefore = layer.HostWallName;
            var posBefore = layer.transform.position;
            var rotBefore = layer.transform.rotation;
            var dimsBefore = layer.DimensionsMM;

            if (!layer.SnapToNamedWall(newHostWallName)) return;
            if (layer.HostWallName == hostBefore) return;

            var command = new WallLayerRehostCommand(layer, hostBefore, layer.HostWallName,
                posBefore, layer.transform.position, rotBefore, layer.transform.rotation,
                dimsBefore, layer.DimensionsMM);
            undo.Add(command);
        }

        private static void ApplyDecorSlotMaterials(EditOp op, IHasTwoDecorSlots slots)
        {
            if (op.tabletop_material != null)
            {
                var def = MaterialCatalog.Find(op.tabletop_material);
                if (def != null) MaterialManager.ApplyPrimarySlot(slots, def);
            }
            if (op.legs_material != null)
            {
                var def = MaterialCatalog.Find(op.legs_material);
                if (def != null) MaterialManager.ApplySecondarySlot(slots, def);
            }
        }
    }
}
