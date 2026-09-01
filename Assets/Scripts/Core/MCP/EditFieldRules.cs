using System;
using System.Collections.Generic;
using KitchenDesigner.Core.MCP.Contract;

namespace KitchenDesigner.Core.MCP
{
    internal static class EditFieldRules
    {
        internal static bool AcceptsOpenFlag(KitchenElement el) =>
            el is FacadeElement || el is WindowElement || el is DoorElement
            || el is OvenElement || el is DishwasherElement;

        internal static bool AcceptsHingeMode(KitchenElement el) =>
            el is FacadeElement || el is WindowElement || el is DoorElement;

        internal static bool AcceptsLegInset(KitchenElement el) =>
            el is TableElement || el is RadiusTableElement;

        internal static bool AcceptsTabletopSlots(KitchenElement el) => el is ITabletop;

        internal static bool AcceptsCornerRadius(KitchenElement el) =>
            el is RadialShelfElement || el is StoolElement || el is ChairElement;

        internal static bool AcceptsSeatHeight(KitchenElement el) => el is ChairElement;

        internal readonly struct EditTarget
        {
            public readonly EditOp op;
            public readonly KitchenElement el;
            public readonly Func<string, KitchenElement?> findByName;

            public EditTarget(EditOp op, KitchenElement el, Func<string, KitchenElement?> findByName)
            {
                this.op = op;
                this.el = el;
                this.findByName = findByName;
            }
        }

        private delegate void Check(EditTarget target, List<string> errors);

        private static readonly Check[] Checks =
        {
            Unsupported("gap_left", o => o.gap_left.HasValue, el => el.SupportsGaps),
            Unsupported("gap_right", o => o.gap_right.HasValue, el => el.SupportsGaps),
            Unsupported("gap_top", o => o.gap_top.HasValue, el => el.SupportsGaps),
            Unsupported("gap_bottom", o => o.gap_bottom.HasValue, el => el.SupportsGaps),
            Unsupported("gap_front", o => o.gap_front.HasValue, el => el.SupportsGaps),
            Unsupported("gap_back", o => o.gap_back.HasValue, el => el.SupportsGaps),

            Unsupported("fill", o => o.fill != null, el => el is FacadeElement),
            Unsupported("mode", o => o.mode != null, AcceptsHingeMode),
            Unsupported("is_open", o => o.is_open.HasValue, AcceptsOpenFlag),
            Unsupported("corner_radius", o => o.corner_radius.HasValue, AcceptsCornerRadius),
            Unsupported("seat_height", o => o.seat_height.HasValue, AcceptsSeatHeight),
            Unsupported("cutout_width", o => o.cutout_width.HasValue, el => el is CooktopElement),
            Unsupported("cutout_depth", o => o.cutout_depth.HasValue, el => el is CooktopElement),

            Unsupported("drawer_system", o => o.drawer_system != null, IsDrawer),
            Unsupported("drawer_type", o => o.drawer_type != null, IsDrawer),
            Unsupported("drawer_length", o => o.drawer_length.HasValue, IsDrawer),
            Unsupported("drawer_color", o => o.drawer_color != null, IsDrawer),
            Unsupported("internal_width", o => o.internal_width.HasValue, IsDrawer),
            Unsupported("is_double", o => o.is_double.HasValue, IsDrawer),
            Unsupported("is_upper", o => o.is_upper.HasValue, IsDrawer),
            Unsupported("paired_drawer_name", o => o.paired_drawer_name != null, IsDrawer),

            Unsupported("attached_facade_name", o => o.attached_facade_name != null, el => el is IFacadeHost),

            RejectBadAttachment,

            Unsupported("leg_inset_mm", o => o.leg_inset_mm.HasValue, AcceptsLegInset),
            Unsupported("tabletop_material", o => o.tabletop_material != null, AcceptsTabletopSlots),
            Unsupported("legs_material", o => o.legs_material != null, AcceptsTabletopSlots),

            Unsupported("mid_height_mm", o => o.mid_height_mm.HasValue, el => el is PillarElement),
            Unsupported("diameter_mm", o => o.diameter_mm.HasValue, el => el is PillarElement),
            Unsupported("tint", o => o.tint != null, el => el is WindowElement),
            Unsupported("sill_protrusion_mm", o => o.sill_protrusion_mm.HasValue, el => el is WindowElement),
            Unsupported("sash_type", o => o.sash_type != null, el => el is DoorElement),

            RejectParametricResize,
            RejectFixedApplianceEdits,
            RejectNonVerticalRotation,
            RejectUnparsableGrooves,
            RejectUnparsableTextureOverlays,
            RejectEdgeFields,
        };

        private static bool IsDrawer(KitchenElement el) => el is DrawerElement;

        private static Check Unsupported(
            string field, Func<EditOp, bool> isSet, Func<KitchenElement, bool> acceptedBy)
            => (target, errors) => { if (isSet(target.op) && !acceptedBy(target.el)) errors.Add(field); };

        public static List<string> Reject(EditOp op, KitchenElement el, Func<string, KitchenElement?> findByName)
        {
            var errors = new List<string>();
            var target = new EditTarget(op, el, findByName);
            foreach (var check in Checks) check(target, errors);
            return errors;
        }

        private static void RejectBadAttachment(EditTarget target, List<string> errors)
        {
            var op = target.op;
            var el = target.el;
            if (op.attached_to_name == null) return;
            if (!AttachLinks.CanBeChild(el))
            {
                errors.Add("attached_to_name (this element type cannot be attached: it has its own kinematics or host)");
                return;
            }
            if (op.attached_to_name == "") return;

            var parent = target.findByName(op.attached_to_name);
            if (parent == null) errors.Add($"attached_to_name: element not found: {op.attached_to_name}");
            else if (!AttachLinks.CanBeParent(parent)) errors.Add("attached_to_name (parent must be a board or a facade)");
            else if (AttachLinks.WouldCycle(el, parent)) errors.Add("attached_to_name would create a cycle");
        }

        private static void RejectParametricResize(EditTarget target, List<string> errors)
        {
            var op = target.op;
            var el = target.el;
            if (el is DrawerElement && (op.width.HasValue || op.height.HasValue || op.depth.HasValue))
                errors.Add("width/height/depth not settable on drawers (size is parametric)");
        }

        private static void RejectFixedApplianceEdits(EditTarget target, List<string> errors)
        {
            var op = target.op;
            var el = target.el;
            if (!FixedSize.IsFixed(el)) return;
            if (op.width.HasValue || op.height.HasValue || op.depth.HasValue)
                errors.Add("width/height/depth not settable on a fixed appliance model (size is set by the manufacturer)");
            if (op.cutout_width.HasValue || op.cutout_depth.HasValue)
                errors.Add("cutout_width/cutout_depth not settable on a fixed appliance model");
        }

        private static void RejectNonVerticalRotation(EditTarget target, List<string> errors)
        {
            var op = target.op;
            var el = target.el;
            if (FixedSize.IsYawOnly(el) && (op.rot_x.HasValue || op.rot_z.HasValue))
                errors.Add("rot_x/rot_z not settable on a built-in appliance (only rot_y — rotation about the vertical axis)");
        }

        private static void RejectUnparsableGrooves(EditTarget target, List<string> errors)
        {
            var op = target.op;
            var el = target.el;
            if (op.grooves == null) return;
            if (!el.SupportsGrooves) errors.Add("grooves (plain boards only)");
            else if (!McpSpecCodec.TryParseGrooves(op.grooves, out _, out string grooveError))
                errors.Add($"grooves: {grooveError}");
        }

        private static void RejectUnparsableTextureOverlays(EditTarget target, List<string> errors)
        {
            var op = target.op;
            var el = target.el;
            if (op.texture_overlays == null) return;
            if (!el.SupportsTextureOverlays) errors.Add("texture_overlays (walls and floors only)");
            else if (!McpSpecCodec.TryParseTextureOverlays(op.texture_overlays, out _, out string overlayError))
                errors.Add($"texture_overlays: {overlayError}");
        }

        private static void RejectEdgeFields(EditTarget target, List<string> errors)
        {
            var op = target.op;
            var el = target.el;
            if (!el.SupportsGrooves)
            {
                if (op.edge_banding.HasValue) errors.Add("edge_banding (plain boards only)");
                if (op.edge_thickness_mm.HasValue) errors.Add("edge_thickness_mm (plain boards only)");
                if (op.edge_skip_validation.HasValue) errors.Add("edge_skip_validation (plain boards only)");
                return;
            }
            if (op.edge_thickness_mm.HasValue
                && (op.edge_thickness_mm.Value < AppConstants.EDGE_THICKNESS_MIN_MM
                    || op.edge_thickness_mm.Value > AppConstants.EDGE_THICKNESS_MAX_MM))
            {
                errors.Add($"edge_thickness_mm: out of range " +
                           $"({AppConstants.EDGE_THICKNESS_MIN_MM}..{AppConstants.EDGE_THICKNESS_MAX_MM} mm)");
            }
        }
    }
}
