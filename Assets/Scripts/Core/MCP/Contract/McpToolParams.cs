using System;

namespace KitchenDesigner.Core.MCP.Contract
{
    [Serializable]
    public class ParamsGuide
    {
        [McpParam("Cheat-sheet topic. Omit for the workflow overview.",
            Enum = new[] { "workflow", "planning", "bulk", "elements", "fields", "drawers", "violations" })]
        public string? topic;
    }

    [Serializable]
    public class ParamsNames
    {
        [McpParam("Exact board names (from get_all_elements). At least 1.", Required = true, Min = 1)]
        public string[] names = Array.Empty<string>();
    }

    [Serializable]
    public class ParamsObjectPaths
    {
        [McpParam("Object names or hierarchy paths (Parent/Child). At least 1.", Required = true, Min = 1)]
        public string[] object_paths = Array.Empty<string>();
    }

    [Serializable]
    public class ParamsFindObjects
    {
        [McpParam("Full or partial object name.", Required = true)]
        public string name_filter = string.Empty;
    }

    [Serializable]
    public class SetActiveOp
    {
        [McpParam("Object name or path.", Required = true)]
        public string object_path = string.Empty;

        [McpParam("Enable (true) or disable (false).", Required = true)]
        public bool active;
    }

    [Serializable]
    public class ParamsSetActiveOps
    {
        [McpParam("Objects to enable/disable. At least 1.", Required = true, Min = 1)]
        public SetActiveOp[] ops = Array.Empty<SetActiveOp>();
    }

    [Serializable]
    public class TransformPositionOp
    {
        [McpParam("Object name or path.", Required = true)]
        public string object_path = string.Empty;

        [McpParam("Raw world X in MM. Omit to keep current.")] public float? x_mm;
        [McpParam("Raw world Y in MM. Omit to keep current.")] public float? y_mm;
        [McpParam("Raw world Z in MM. Omit to keep current.")] public float? z_mm;
    }

    [Serializable]
    public class ParamsTransformPositionOps
    {
        [McpParam("Objects to move. At least 1.", Required = true, Min = 1)]
        public TransformPositionOp[] ops = Array.Empty<TransformPositionOp>();
    }

    [Serializable]
    public class TransformRotationOp
    {
        [McpParam("Object name or path.", Required = true)]
        public string object_path = string.Empty;

        [McpParam("Euler rotation around X in DEGREES. Omit to keep current.")] public float? x_deg;
        [McpParam("Euler rotation around Y in DEGREES. Omit to keep current.")] public float? y_deg;
        [McpParam("Euler rotation around Z in DEGREES. Omit to keep current.")] public float? z_deg;
    }

    [Serializable]
    public class ParamsTransformRotationOps
    {
        [McpParam("Objects to rotate. At least 1.", Required = true, Min = 1)]
        public TransformRotationOp[] ops = Array.Empty<TransformRotationOp>();
    }

    [Serializable]
    public class TransformScaleOp
    {
        [McpParam("Object name or path.", Required = true)]
        public string object_path = string.Empty;

        [McpParam("Transform scale along X — a dimensionless multiplier, 1 = unchanged. Omit to keep current.")] public float? x;
        [McpParam("Transform scale along Y — a dimensionless multiplier, 1 = unchanged. Omit to keep current.")] public float? y;
        [McpParam("Transform scale along Z — a dimensionless multiplier, 1 = unchanged. Omit to keep current.")] public float? z;
    }

    [Serializable]
    public class ParamsTransformScaleOps
    {
        [McpParam("Objects to scale. At least 1.", Required = true, Min = 1)]
        public TransformScaleOp[] ops = Array.Empty<TransformScaleOp>();
    }

    [Serializable]
    public class ParamsResizeFloor
    {
        [McpParam("Floor width (X) in MM.", Required = true, Min = 1)] public int width;
        [McpParam("Floor length (Y) in MM.", Required = true, Min = 1)] public int height;
        [McpParam("Floor thickness (Z) in MM.", Required = true, Min = 1)] public int depth;
    }

    [Serializable]
    public class EditOp
    {
        [McpParam("Exact element name.", Required = true)] public string name = string.Empty;

        [McpParam("Rename the element to this new name. Must be unique among all elements (case-insensitive) "
            + "and match ^[A-Za-z0-9_-]+$ — latin letters, digits, '-' and '_' only; no spaces, no cyrillic. Omit to keep.")]
        public string? new_name;

        [McpParam("Target X of the MINIMUM world corner in MM — the same number get returns in anchor[0]. Omit to keep.")] public float? anchor_x_mm;
        [McpParam("Target Y of the MINIMUM world corner in MM — the bottom of the element. Omit to keep.")] public float? anchor_y_mm;
        [McpParam("Target Z of the MINIMUM world corner in MM — the same number get returns in anchor[1]. Omit to keep.")] public float? anchor_z_mm;
        [McpParam("New width (X) in MM. Omit to keep. Rejected for drawers (their size is parametric).", Min = 1)] public int? width;
        [McpIgnore] public int? dimX;
        [McpParam("New height (Y) in MM. Omit to keep. Rejected for drawers.", Min = 1)] public int? height;
        [McpIgnore] public int? dimY;
        [McpParam("New depth/thickness (Z) in MM. Omit to keep. Rejected for drawers.", Min = 1)] public int? depth;
        [McpIgnore] public int? dimZ;
        [McpParam("Rotation around X in DEGREES. Omit to keep.")] public float? rot_x;
        [McpParam("Rotation around Y in DEGREES. Omit to keep.")] public float? rot_y;
        [McpParam("Rotation around Z in DEGREES. Omit to keep.")] public float? rot_z;

        [McpParam("Lock (true) / unlock (false). Unlock ONLY with the user's explicit permission. Omit to keep.")]
        public bool? locked;
        [McpParam("Draw the element see-through (true) or solid (false). Visual only — size, position and the "
            + "specification are unaffected. Omit to keep.")]
        public bool? transparent;
        [McpParam("Material decor id or display name (see list_materials). Omit to keep.")]
        public string? material;

        [McpParam("Wall only: true = load-bearing (counts toward foundation planning), false = " +
                  "partition. New walls default to true. Omit to keep.")]
        public bool? load_bearing;

        [McpParam("Gap in MM on the left side. Omit to keep.", Min = 0)] public int? gap_left;
        [McpParam("Gap in MM on the right side. Omit to keep.", Min = 0)] public int? gap_right;
        [McpParam("Gap in MM on the top side. Omit to keep.", Min = 0)] public int? gap_top;
        [McpParam("Gap in MM on the bottom side. Omit to keep.", Min = 0)] public int? gap_bottom;
        [McpParam("Gap in MM on the front side (+Z). Omit to keep.", Min = 0)] public int? gap_front;
        [McpParam("Gap in MM on the back side (-Z). Omit to keep.", Min = 0)] public int? gap_back;
        [McpParam("Facade/window/door: opening mode. Facades accept all 18 modes; windows and doors accept front_* only. Omit to keep.",
            Enum = new[] {
                "front_left", "front_right", "front_top", "front_bottom",
                "back_left", "back_right", "back_top", "back_bottom",
                "edge_top_left", "edge_top_right", "edge_bottom_left", "edge_bottom_right",
                "drawer_out", "drawer_in", "drawer_right", "drawer_left", "drawer_up", "drawer_down" })]
        public string? mode;
        [McpParam("Assembled facade only: center fill — blind (panel), glass (vitrine), open (empty). Omit to keep.",
            Enum = new[] { "blind", "glass", "open" })]
        public string? fill;
        [McpParam("Facade/window/door/oven/dishwasher: true = open, false = close (the oven and dishwasher doors drop DOWN around their bottom edge; the dishwasher takes its attached furniture facade with it). Omit to keep. For drawers use cycle_drawer_animation.")]
        public bool? is_open;

        [McpParam("Corner rounding radius in MM. Radial shelf: clamped to 1..min(width, depth). " +
                  "Stool, chair, sofa (the seat body) and pouffe (the whole upholstered box): " +
                  "clamped to 0..min(width, depth)/2 — 0 is a square seat, " +
                  "the maximum is a fully round one (a circle when width == depth, a capsule otherwise). " +
                  "Omit to keep.", Min = 0)]
        public int? corner_radius;

        [McpParam("Chair, sofa and both toilets: height of the seat TOP above the floor in MM. " +
                  "Chair: clamped to 80..height-50 — the legs and the backrest each keep at " +
                  "least 50 mm. Sofa: it is the height of the solid base block, clamped to " +
                  "150..height-200. Compact toilet: clamped to 350..502, and what is left up " +
                  "to the fixed 790 mm becomes the cistern. Wall-hung toilet: clamped to " +
                  "350..600, and raising it PUSHES flush_plate_height up when the plate would " +
                  "land on the lid. Omit to keep.",
            Min = 0)]
        public int? seat_height;

        [McpParam("Wall-hung toilet only: height of the BOTTOM of the flush plate above the " +
                  "floor in MM (the plate itself is 240x165 mm). Clamped to " +
                  "seat_height+88..835 — at least 50 mm of tiling above the closed lid, and " +
                  "the plate top never leaves the 1000 mm envelope. Omit to keep.", Min = 0)]
        public int? flush_plate_height;

        [McpParam("Bathtub only: width of the flat rim around the bowl in MM. Clamped to " +
                  "5..(min(width,depth)-200)/2 — a bowl at least 200 mm across always stays " +
                  "inside. Raising it LOWERS the ceilings of bowl_radius and bowl_fillet. " +
                  "Omit to keep.", Min = 0)]
        public int? rim_width;

        [McpParam("Bathtub only: depth of the bowl below the rim in MM. Clamped to " +
                  "50..height-30 — 30 mm of shell always stays under the bowl, so the tub " +
                  "never stands with a hole in the floor. Lowering it LOWERS the ceiling of " +
                  "bowl_fillet. Omit to keep.", Min = 0)]
        public int? bowl_depth;

        [McpParam("Bathtub only: plan corner radius of the BOWL in MM, not of the outer " +
                  "shell — the shell radius is bowl_radius + rim_width, which is what keeps " +
                  "the rim the same width in the corner as along the straight side. " +
                  "Clamped to 0..(min(width,depth)-2*rim_width)/2: 0 is a strictly " +
                  "rectangular tub, the maximum gives semicircular ends. Omit to keep.",
            Min = 0)]
        public int? bowl_radius;

        [McpParam("Bathtub only: radius of the wall-to-floor fillet inside the bowl in MM. " +
                  "Clamped to 0..min(bowl_depth, (min(width,depth)-2*rim_width-60)/2) — the " +
                  "arc never reaches above the rim and never closes the bowl floor to a " +
                  "point. Omit to keep.", Min = 0)]
        public int? bowl_fillet;

        [McpParam("Wall mixer only: distance between the two wall inlets in MM (150 by " +
                  "default, the standard wall spacing). Clamped to 60..body_length minus " +
                  "body_diameter, so both escutcheons stay inside the body ends. Omit to " +
                  "keep.", Min = 0)]
        public int? mixer_centres;

        [McpParam("Wall mixer only: overall body length in MM across the flow handle and the " +
                  "thermostat head (270 by default). Clamped to max(150, body_diameter+60)" +
                  "..600. Raising it RAISES the ceiling of mixer_centres. Omit to keep.",
            Min = 0)]
        public int? mixer_body_length;

        [McpParam("Wall mixer only: body diameter in MM, which is also its height (70 by " +
                  "default). Clamped to 30..160. Raising it LOWERS the ceiling of " +
                  "mixer_centres and RAISES the floor of mixer_body_length. Omit to keep.",
            Min = 0)]
        public int? mixer_body_diameter;

        [McpParam("Wall mixer only: how far the round escutcheons stand off the wall in MM " +
                  "(34 by default). The body axis sits this far out PLUS its own radius, so " +
                  "the body rests on the escutcheon faces instead of cutting into the wall. " +
                  "Clamped to 10..200. Omit to keep.", Min = 0)]
        public int? mixer_escutcheon_reach;

        [McpParam("Wall mixer only: how far the spout reaches forward from the body axis in " +
                  "MM (110 by default); it also drops two fifths of that. Clamped to " +
                  "40..400. This field, not the body, drives the element depth. Omit to keep.",
            Min = 0)]
        public int? mixer_spout_length;

        [McpParam("Wall mixer only: diameter of the hose outlet under the body in MM (13 by " +
                  "default, G 1/2). Clamped to 8..40. Omit to keep.", Min = 0)]
        public int? mixer_outlet_diameter;

        [McpParam("Shower column only: height of the column in MM from the diverter to the " +
                  "top of the gooseneck (1150 by default). This is NOT the element height: " +
                  "the hose loop hangs below the diverter and the bounding box covers it. " +
                  "Clamped to max(500, 7*riser_diameter)..2200. Omit to keep.", Min = 0)]
        public int? shower_column_height;

        [McpParam("Shower column only: diameter of the vertical riser tube in MM (32 by " +
                  "default). Clamped to 16..60. It also sets the bend radius of the " +
                  "gooseneck (3.5x), so raising it RAISES the floor of shower_arm_reach. " +
                  "Omit to keep.", Min = 0)]
        public int? shower_riser_diameter;

        [McpParam("Shower column only: diameter of the round rain head in MM (250 by " +
                  "default). Clamped to 80..600. It is the widest part of the column and " +
                  "therefore the element width. Omit to keep.", Min = 0)]
        public int? shower_head_diameter;

        [McpParam("Shower column only: thickness of the flat rain head in MM (30 by " +
                  "default). Clamped to 8..120. Omit to keep.", Min = 0)]
        public int? shower_head_thickness;

        [McpParam("Shower column only: distance from the WALL to the rain head axis in MM " +
                  "(380 by default). Clamped to wall_offset + 40 + bend radius .. 800 — a " +
                  "shorter reach than the bend would fold the gooseneck back into the wall. " +
                  "Omit to keep.", Min = 0)]
        public int? shower_arm_reach;

        [McpParam("Shower column only: distance from the wall to the riser axis in MM (60 by " +
                  "default), set by the two wall brackets. Clamped to 20..200. Raising it " +
                  "RAISES the floor of shower_arm_reach. Omit to keep.", Min = 0)]
        public int? shower_wall_offset;

        [McpParam("Shower column only: diameter of the hand shower head in MM (110 by " +
                  "default). Clamped to 60..200; it also scales the handle. Omit to keep.",
            Min = 0)]
        public int? shower_hand_diameter;

        [McpParam("Shower column only: length of the flexible hose in MM (1000 by default). " +
                  "Clamped to 300..3000. The slack over the straight distance between the " +
                  "diverter and the handle hangs as a loop BELOW the diverter and grows the " +
                  "element downwards; a hose shorter than that distance is drawn straight " +
                  "and is not stretched. Omit to keep.", Min = 0)]
        public int? shower_hose_length;

        [McpParam("Socket and light switch only: width of ONE plate in MM (80 by default). " +
                  "Clamped to 40..200. The element width is this times wall_device_posts, so " +
                  "width/height/depth are COMPUTED here and setting them is refused. " +
                  "Omit to keep.", Min = 0)]
        public int? wall_device_plate_width;

        [McpParam("Socket and light switch only: plate height in MM (80 by default). Clamped " +
                  "to 40..200. Omit to keep.", Min = 0)]
        public int? wall_device_plate_height;

        [McpParam("Socket and light switch only: how far the device stands off the wall in MM " +
                  "(10 by default). Clamped to 3..60. It is the element depth, and it is split " +
                  "into frame, recess and body, so a larger value deepens the socket well " +
                  "rather than moving the plate. Omit to keep.", Min = 0)]
        public int? wall_device_protrusion;

        [McpParam("Socket and light switch only: COUNT of posts side by side, 1..3 " +
                  "(1 by default) - a double socket or a two-key switch. A plain count, not " +
                  "a length: it is NOT millimetres. Posts sit flush against each other, so " +
                  "this MULTIPLIES the element width in MM. Omit to keep.", Min = 0)]
        public int? wall_device_posts;

        [McpParam("Light switch only: whether the switch is on (true by default). A lamp " +
                  "named by at least one switch is lit when at least one of those switches is " +
                  "on; a lamp no switch names keeps following the global light. Omit to keep.")]
        public bool? switch_on;

        [McpParam("Light switch only: names of the light sources this switch controls, " +
                  "REPLACING the current list (send an empty array to unlink everything). " +
                  "Up to 12; unknown names and duplicates are dropped. One switch may control " +
                  "several lamps and one lamp may be controlled by several switches - the link " +
                  "is stored only here, on the switch. Omit to keep.")]
        public string[]? switch_lights;

        [McpParam("Cooktop only: cutout width in MM — the box that goes INTO the countertop " +
                  "(width/height/depth describe the 5 mm plate on top; height is the total). " +
                  "Clamped to 50..width-10. Omit to keep.", Min = 50)]
        public int? cutout_width;
        [McpParam("Cooktop only: cutout depth in MM. Clamped to 50..depth-10. Omit to keep.", Min = 50)]
        public int? cutout_depth;

        [McpParam("Plain board only: REPLACES the whole set of grooves. Comma-separated \"kind:side\" pairs, " +
                  "kind = through|blind, side = top|bottom|left|right (side names the edge the groove runs along, " +
                  "in the part's own frame). Example: \"through:top, blind:left\". Empty string removes all grooves. " +
                  "Groove size is fixed at 16*4*7 mm (offset*width*depth) by the CNC and cannot be changed. " +
                  "Duplicates of the same kind+side are rejected. Omit to keep.")]
        public string? grooves;

        [McpParam("Wall/floor only: REPLACES the whole set of texture overlays — local decors on a patch of " +
                  "one face. Semicolon-separated \"side:materialId\" items (semicolon, not comma: the area " +
                  "below already uses commas). side = a|b|c|d|e|f|all, where a..f are faces 0..5 of the box " +
                  "(index/2 = axis X|Y|Z, even = positive direction) and all means every face. " +
                  "Optional area \"@u,v+WxH\" in MM from the face's lower-left corner (axes: u along the face's " +
                  "right axis, v along its up axis); without it the overlay covers the whole face and keeps " +
                  "covering it after a resize. The picture is NOT scaled to the area — it keeps its physical " +
                  "tile size and repeats/crops, so the area is a window onto it. " +
                  "Example: \"a:oak; b:white@100,200+800x600\". Empty string removes all overlays. Omit to keep.")]
        public string? texture_overlays;

        [McpParam("Sheet board only: glue edge banding on the OPEN ends of the part. Which ends are open is " +
                  "computed from the scene (an end touching another part, a wall or the floor gets no banding) " +
                  "and cannot be set by hand. false clears all four CSV edge columns. Omit to keep.")]
        public bool? edge_banding;
        [McpParam("Sheet board only: edge banding tape thickness in MM (0.1..5, fractional). Omit to keep.")]
        public float? edge_thickness_mm;
        [McpParam("Sheet board only: force the banding decision on named ends, \"side:state\" items " +
                  "separated by ';' — side = L1|L2|W1|W2, state = on|off|auto (e.g. \"L1:on; W1:off\"). " +
                  "on = there IS banding on that end even though the scene covers it; off = there is NONE " +
                  "even though the end is open; auto = let the scene decide (the default). Both explicit " +
                  "states also silence EDG-01 on that end. An empty string puts every end back to auto. " +
                  "Ends not named are left alone. Omit to keep.")]
        public string? edge_sides;

        [McpParam("Drawer only: runner system — gtv (bought metal box, one spec line) or " +
            "movento (wooden box exploded into separate spec parts). Omit to keep.",
            Enum = new[] { "gtv", "movento" })]
        public string? drawer_system;
        [McpParam("Drawer only: side height type — A=86, B=120, C=168, D=200 mm. Omit to keep.",
            Enum = new[] { "A", "B", "C", "D" })]
        public string? drawer_type;
        [McpParam("Drawer only: nominal slide length in MM, one of 250/300/350/400/450/500/550/600. Omit to keep.")]
        public int? drawer_length;
        [McpParam("Drawer only: GTV color. Omit to keep.", Enum = new[] { "anthracite", "white", "black" })]
        public string? drawer_color;
        [McpParam("Drawer only: internal box width in MM (min 100). Omit to keep.", Min = 100)]
        public int? internal_width;
        [McpParam("Drawer only: mark as part of a DOUBLE drawer (two stacked boxes). Omit to keep.")]
        public bool? is_double;
        [McpParam("Double drawer only: this box is the UPPER one. Omit to keep.")]
        public bool? is_upper;
        [McpParam("Double drawer only: exact name of the paired drawer element (link both ways). Empty string detaches. Omit to keep.")]
        public string? paired_drawer_name;
        [McpParam("Drawer and dishwasher only: exact name of the facade acting as this element's front. Empty string detaches. Omit to keep.")]
        public string? attached_facade_name;
        [McpParam("Static parts only (board, panel, shelf, table, pillar): exact name of the board or facade this element is ATTACHED to. An attached board follows its parent when the parent is moved, rotated or opened; resizing is never propagated. A facade cannot be attached to anything. Empty string detaches. Omit to keep.")]
        public string? attached_to_name;

        [McpParam("Table only: inward offset of legs from corners along X and Z, in MM. Omit to keep.", Min = 0)]
        public int? leg_inset_mm;
        [McpParam("Table and stool only: material id or display name for the tabletop (the stool seat) — see list_materials. Omit to keep.")]
        public string? tabletop_material;
        [McpParam("Table and stool only: material id or display name for the legs (see list_materials). Omit to keep.")]
        public string? legs_material;

        [McpParam("Pillar only: middle cylinder height in MM (clamped 50..100). Omit to keep.", Min = 50, Max = 100)]
        public int? mid_height_mm;
        [McpParam("Pillar only: outer diameter in MM (clamped 20..200) — width and depth are always equal. Omit to keep.", Min = 20, Max = 200)]
        public int? diameter_mm;

        [McpParam("Screw leg only: thread designation. Omit to keep.",
            Enum = new[] { "M6", "M8", "M10" })]
        public string? screw_thread;
        [McpParam("Screw leg only: length of the threaded rod in MM (5..1000). The leg re-derives it from the floor when the leg is moved. Omit to keep.", Min = 5, Max = 1000)]
        public int? screw_thread_length_mm;
        [McpParam("NOT settable: how deep the thread sits in the part it is screwed to, in MM, is DERIVED from geometry and reported read-only in ElementInfo (screwLeg.insertionMM, 0 when the leg has no host). Sending it is rejected — move the leg or change screw_thread_length_mm instead.", Min = 1, Max = 1000)]
        public int? screw_insertion_mm;
        [McpParam("Screw leg only: diameter of the foot in MM (5..200) — width and depth are always equal. Omit to keep.", Min = 5, Max = 200)]
        public int? screw_base_diameter_mm;
        [McpParam("Screw leg only: height of the foot in MM (1..200). Omit to keep.", Min = 1, Max = 200)]
        public int? screw_base_height_mm;
        [McpParam("Screw leg only: distance in MM from the CENTRE of the foot to the left edge of its host, measured on the host face the thread enters, along that face's right axis (it turns with the host). Moves the leg; screw_right_mm is the rest of the host span and follows. Rejected when the leg has no host. Omit to keep.", Min = 0, Max = 10000)]
        public int? screw_left_mm;
        [McpParam("Screw leg only: distance in MM from the CENTRE of the foot to the right edge of its host, the opposite of screw_left_mm — the pair adds up to the host's span across that face. Moves the leg. Rejected when the leg has no host. Omit to keep.", Min = 0, Max = 10000)]
        public int? screw_right_mm;
        [McpParam("Screw leg only: distance in MM from the CENTRE of the foot to the top edge of its host, measured along the up axis of the host face the thread enters (it turns with the host). Moves the leg; screw_bottom_mm follows. Rejected when the leg has no host. Omit to keep.", Min = 0, Max = 10000)]
        public int? screw_top_mm;
        [McpParam("Screw leg only: distance in MM from the CENTRE of the foot to the bottom edge of its host, the opposite of screw_top_mm — the pair adds up to the host's span along that face. Moves the leg. Rejected when the leg has no host. Omit to keep.", Min = 0, Max = 10000)]
        public int? screw_bottom_mm;

        [McpParam("Pipe only: nominal bore of the GOST 3262-75 run. It is the ONLY source of "
                  + "the section: outer diameter, inner diameter and wall thickness are derived "
                  + "from it and reported read-only in ElementInfo (pipe.*), and width/depth "
                  + "follow the outer diameter — sending them is rejected. Length is the height "
                  + "of the element. Omit to keep.",
            Enum = new[] { "dn15", "dn20", "dn25", "dn32", "dn40", "dn50" })]
        public string? pipe_size;

        [McpParam("Pouffe only: thickness of the soft seat cushion in MM, clamped to "
                  + "20..height/3. What is left of the height goes to the upholstered box "
                  + "below it — a pouffe has no legs. Omit to keep.", Min = 1)]
        public int? pouffe_seat_thickness;

        [McpParam("Bed only: true = double (1800 wide, two pillows, six legs), false = single "
                  + "(900 wide, one pillow, four legs). Switching RESETS width/height/depth to "
                  + "the defaults of the new type, discarding a manual resize. Omit to keep.")]
        public bool? bed_double;
        [McpParam("Bed only: true = with a headboard at the head end (-Z), false = without. "
                  + "Switching RESETS the height to the default of the new type (900 with a "
                  + "headboard, 600 without) and leaves width and length alone. Omit to keep.")]
        public bool? bed_headboard;

        [McpParam("Window only: glass tint — clear (transparent) or tinted (slightly darkened). Omit to keep.",
            Enum = new[] { "clear", "tinted" })]
        public string? tint;
        [McpParam("Window only: windowsill outward protrusion in MM (0..200). Omit to keep.", Min = 0, Max = 200)]
        public int? sill_protrusion_mm;

        [McpParam("Door only: sash type — glass (transparent) or blind (solid panel). Omit to keep.",
            Enum = new[] { "glass", "blind" })]
        public string? sash_type;
    }

    [Serializable]
    public class ParamsEditElements
    {
        [McpParam("Operations to apply — one per element. Each op: exact name + ANY editable properties (geometry, lock, material, facade gaps/mode/fill, drawer params, table/pillar/window/door params).",
            Required = true, Min = 1)]
        public EditOp[] ops = Array.Empty<EditOp>();

        [McpParam("true = DRY-RUN: apply everything, report per-op state and violations, then revert. Use this INSTEAD of a separate simulate call. Default false.")]
        public bool dry_run;
    }

    [Serializable]
    public class CreateItem
    {
        [McpParam("Unique name for the new element (case-insensitive across the whole project). "
            + "Must match ^[A-Za-z0-9_-]+$ — latin letters, digits, '-' and '_' only; no spaces, no cyrillic.",
            Required = true)]
        public string name = string.Empty;

        [McpParam("Element type. Default board. wall = board acting as a structural anchor; floor ignores size/position. An unknown type is rejected and the whole batch with it.",
            Enum = new[] { "board", "wall", "floor", "facade", "assembled_facade", "radial_shelf", "panel", "drawer", "movento_drawer", "table", "radius_table", "stool", "chair", "sofa", "pouffe", "bed", "pillar", "screw_leg", "pipe", "pipe_elbow", "pipe_coupling", "pipe_tee", "pipe_cap", "pipe_supply", "pipe_return", "window", "door", "sink", "cooktop", "oven", "dishwasher", "toilet", "wall_hung_toilet", "bathtub", "bath_mixer", "shower_column", "socket", "light_switch" })]
        public string? type;

        [McpParam("X of the MINIMUM world corner in MM — the same number get returns in anchor[0].")] public float anchor_x_mm;
        [McpParam("Y of the MINIMUM world corner in MM — the bottom of the element.")] public float anchor_y_mm;
        [McpParam("Z of the MINIMUM world corner in MM — the same number get returns in anchor[1].")] public float anchor_z_mm;

        [McpParam("Size along X in MM. Defaults: board 800, assembled facade 450, radial shelf 600, table 2000, window 900, door 900.", Min = 1)]
        public int? width;
        [McpParam("Size along Y in MM. Defaults: board 400, assembled facade 700, table 750, window 1200, door 2000.", Min = 1)]
        public int? height;
        [McpParam("Thickness along Z in MM. Defaults: board 18, radial shelf 400 (its depth), table 1000, window/door 100.", Min = 1)]
        public int? depth;

        [McpParam("Appliance model for a built-in appliance (type cooktop). Its size and cutout come from the " +
                  "manufacturer and cannot be edited afterwards; width/height/depth are ignored. Omit for a free-size appliance. " +
                  "types oven and dishwasher have exactly one model each and need no value here.",
            Enum = new[] { "Bosch PUE611BB5E", "Bosch HBA514BB3", "Bosch SMV25EX02E" })]
        public string? model;

        [McpParam("Pipe only: nominal bore of the GOST 3262-75 run, default dn20 (3/4\"). It sets "
                  + "the whole section — width and depth follow the outer diameter and are "
                  + "ignored here; height is the length of the run.",
            Enum = new[] { "dn15", "dn20", "dn25", "dn32", "dn40", "dn50" })]
        public string? pipe_size;
    }

    [Serializable]
    public class ParamsCreateElements
    {
        [McpParam("Elements to create. At least 1. Whole batch is ONE undo step.", Required = true, Min = 1)]
        public CreateItem[] items = Array.Empty<CreateItem>();
    }

    [Serializable]
    public class PlanPointMm
    {
        [McpParam("X in MM from the declaration origin.", Required = true)] public int x;
        [McpParam("Z in MM from the declaration origin.", Required = true)] public int z;
    }

    [Serializable]
    public class WallSegmentMm
    {
        [McpParam("Stable wall id/name.", Required = true)] public string name = "";
        [McpParam("Start X in MM from origin.", Required = true)] public int from_x;
        [McpParam("Start Z in MM from origin.", Required = true)] public int from_z;
        [McpParam("End X in MM from origin.", Required = true)] public int to_x;
        [McpParam("End Z in MM from origin.", Required = true)] public int to_z;
        [McpParam("Wall kind; thickness/material come from project instructions.", Required = true,
            Enum = new[] { "bearing", "partition" })] public string kind = "";
        [McpParam("Wall height in MM.", Required = true, Min = 1)] public int height;
        [McpParam("Thickness in MM, overriding the kind's project instruction. Use for walls that " +
            "do not match the project default (e.g. a 150 mm facade in a 250/125 project). " +
            "When given, the '<kind>_wall_thickness_mm' instruction is not required.", Min = 1)]
        public int? thickness_mm;
    }

    [Serializable]
    public class ParamsCreateWalls
    {
        [McpParam("World X of declaration origin in MM.")] public int origin_x_mm;
        [McpParam("World Z of declaration origin in MM.")] public int origin_z_mm;
        [McpParam("Floor/base Y in MM. Walls extend upward from it.")] public int base_y_mm;
        [McpParam("Wall segments. Existing walls with the same names are updated (idempotent).",
            Required = true, Min = 1)] public WallSegmentMm[] segments = Array.Empty<WallSegmentMm>();
    }

    [Serializable]
    public class ParamsCreateFloorV2
    {
        [McpParam("Stable floor id/name.", Required = true)] public string name = "";
        [McpParam("World X of declaration origin in MM.")] public int origin_x_mm;
        [McpParam("World Z of declaration origin in MM.")] public int origin_z_mm;
        [McpParam("Top surface Y in MM.")] public int top_y_mm;
        [McpParam("Floor thickness in MM. If omitted, floor_thickness_mm is required in project instructions.", Min = 1)]
        public int? thickness_mm;
        [McpParam("Simple polygon vertices in MM from origin (clockwise or counter-clockwise).",
            Required = true, Min = 3)] public PlanPointMm[] poly = Array.Empty<PlanPointMm>();
    }

    [Serializable]
    public class ParamsAddOpening
    {
        [McpParam("Stable opening id/name.", Required = true)] public string name = "";
        [McpParam("Exact wall name.", Required = true)] public string wall = "";
        [McpParam("Opening kind.", Required = true, Enum = new[] { "window", "door" })]
        public string kind = "";
        [McpParam("Distance from wall start endpoint to opening left edge in MM.", Required = true, Min = 0)]
        public int offset_mm;
        [McpParam("Opening width in MM.", Required = true, Min = 1)] public int width;
        [McpParam("Opening height in MM.", Required = true, Min = 1)] public int height;
        [McpParam("Height from wall base to opening bottom in MM. Must be 0 for a door: a door opening starts at the floor.", Min = 0)]
        public int sill_mm;
    }

    [Serializable]
    public class ConvertOp
    {
        [McpParam("Exact element name to convert.", Required = true)] public string name = string.Empty;
        [McpParam("Target type: part (plain board), facade (door/front), assembled_facade (framed facade), radial_shelf (corner shelf).",
            Required = true, Enum = new[] { "part", "facade", "assembled_facade", "radial_shelf" })]
        public string target = string.Empty;
        [McpParam("When target=assembled_facade: center fill — blind (panel), glass, open (empty). Default keeps/blind.",
            Enum = new[] { "blind", "glass", "open" })]
        public string? fill;
    }

    [Serializable]
    public class ParamsConvertElements
    {
        [McpParam("Conversions to apply. At least 1.", Required = true, Min = 1)]
        public ConvertOp[] ops = Array.Empty<ConvertOp>();
    }

    [Serializable]
    public class CloneOp
    {
        [McpParam("Exact board name to clone.", Required = true)] public string name = string.Empty;
        [McpParam("How many copies (default 1, max 50).", Min = 1, Max = 50)] public int count = 1;
        [McpParam("X shift between copies in MM (default 0).")] public float offset_x_mm;
        [McpParam("Y shift between copies in MM (default 0).")] public float offset_y_mm;
        [McpParam("Z shift between copies in MM (default 0).")] public float offset_z_mm;
    }

    [Serializable]
    public class ParamsCloneElements
    {
        [McpParam("Clone operations. At least 1. Whole batch is ONE undo step.", Required = true, Min = 1)]
        public CloneOp[] ops = Array.Empty<CloneOp>();
    }

    [Serializable]
    public class AlignOp
    {
        [McpParam("Board to MOVE.", Required = true)] public string name = string.Empty;
        [McpParam("Which face of THIS board to align: left/right = X axis, bottom/top = Y axis, back/front = Z axis.",
            Required = true, Enum = new[] { "left", "right", "bottom", "top", "back", "front" })]
        public string face = string.Empty;
        [McpParam("Board to align AGAINST (it does not move).", Required = true)] public string target = string.Empty;
        [McpParam("Which face of the TARGET to align to. Must be on the same axis as 'face'.",
            Required = true, Enum = new[] { "left", "right", "bottom", "top", "back", "front" })]
        public string target_face = string.Empty;
        [McpParam("Gap between the two faces in MM (default 0 = flush contact).", Min = 0)]
        public float gap_mm;
    }

    [Serializable]
    public class ParamsAlignElements
    {
        [McpParam("Align operations, applied IN ORDER (later ops see earlier moves). At least 1. Whole batch is ONE undo step.",
            Required = true, Min = 1)]
        public AlignOp[] ops = Array.Empty<AlignOp>();
    }

    [Serializable]
    public class SnapDiagnoseOp
    {
        [McpParam("Exact board name.", Required = true)] public string name = string.Empty;
        [McpParam("Test X of the MINIMUM world corner in MM (default: current).")] public float? anchor_x_mm;
        [McpParam("Test Y of the MINIMUM world corner in MM (default: current).")] public float? anchor_y_mm;
        [McpParam("Test Z of the MINIMUM world corner in MM (default: current).")] public float? anchor_z_mm;
    }

    [Serializable]
    public class ParamsSnapDiagnose
    {
        [McpParam("Boards (and optional test positions) to diagnose. At least 1.", Required = true, Min = 1)]
        public SnapDiagnoseOp[] ops = Array.Empty<SnapDiagnoseOp>();
    }

    [Serializable]
    public class ParamsMenuPath
    {
        [McpParam("Menu path, e.g. 'Edit/Undo'.", Required = true)] public string menu_path = string.Empty;
    }

    [Serializable]
    public class ParamsLogCount
    {
        [McpParam("How many entries (max 200, default 50).", Min = 1, Max = 200)] public int count;
    }

    [Serializable]
    public class ParamsExportCsv
    {
        [McpParam("Full file path to write the CSV to.", Required = true)] public string path = string.Empty;
    }

    [Serializable]
    public class ParamsSaveProject
    {
        [McpParam("Full file path to write the project file to (created if missing, overwritten if it exists). Same on-disk format as the app's own Save/Save As.", Required = true)]
        public string path = string.Empty;
    }

    [Serializable]
    public class ParamsLoadProject
    {
        [McpParam("Full file path to read the project file from. Replaces the ENTIRE current scene.", Required = true)]
        public string path = string.Empty;
    }

    [Serializable]
    public class ParamsSetEnabled
    {
        [McpParam("Turn the feature on (true) or off (false).", Required = true)] public bool enabled;
    }

    [Serializable]
    public class ParamsSetProjectInstructions
    {
        [McpParam("New free-text project instructions (conventions: wall thicknesses, board thickness, gaps...). Replaces the whole text. Empty string clears it.", Required = true)]
        public string text = "";
    }

    [Serializable]
    public class ParamsSetAttr
    {
        [McpParam("Selector over the scene. Space-separated clauses (AND): name mask 'B4_*', 'name:PAT', 'type:board|wall|floor|window|door|drawer|facade|assembled_facade|radial_shelf|panel|table|pillar|screw_leg|pipe|pipe_elbow|pipe_coupling|pipe_tee|pipe_cap|pipe_supply|pipe_return|light', 'module:NAME', 'thickness==18' (also width/height/depth with == != >= <= > <), 'all_boards', 'all_modules', '*'.", Required = true)]
        public string selector = "";
        [McpParam("New thickness (dimZ) in MM for every matched board (e.g. change all 18 to 16).")] public int? thickness;
        [McpParam("New width (dimX) in MM.")] public int? width;
        [McpParam("New height (dimY) in MM.")] public int? height;
        [McpParam("New depth (dimZ) in MM.")] public int? depth;
        [McpParam("Material decor id/name to apply to all matched (see list_materials).")] public string? material;
        [McpParam("Lock (true) / unlock (false) all matched.")] public bool? locked;
    }

    [Serializable]
    public class ParamsMove
    {
        [McpParam("Selector (see set_attr).", Required = true)] public string selector = "";
        [McpParam("Shift along world X in MM.")] public float dx;
        [McpParam("Shift along world Y in MM.")] public float dy;
        [McpParam("Shift along world Z in MM.")] public float dz;
    }

    [Serializable]
    public class ParamsResizeModule
    {
        [McpParam("Module = exact group/module name (see get_modules).", Required = true)]
        public string module = "";
        [McpParam("World axis to resize along. Omit to use the module's stored width_axis.", Enum = new[] { "x", "y", "z" })]
        public string axis = "";
        [McpParam("Delta in MM: positive grows toward +axis, negative shrinks. The near side stays fixed; the server moves the far side and stretches spanning boards.", Required = true)]
        public float delta_mm;
    }

    [Serializable]
    public class ParamsScreenshot
    {
        [McpParam("How many times to render the frame before reading it back — a COUNT of renders, not a duration. 1 (the default) just takes the picture. A higher count turns the call into a cost measurement: the read-back is a fixed ~15 ms tax that hides the frame cost, so time a 1-render call and an 8-render call and divide the difference by 7 to get milliseconds per frame.", Min = 1)]
        public int? renders;
    }

    [Serializable]
    public class ParamsPhotoCamera
    {
        [McpParam("Point the camera looks at, X in MM. Omit to keep.")] public float? target_x_mm;
        [McpParam("Point the camera looks at, Y in MM — eye height above the floor. Omit to keep.")] public float? target_y_mm;
        [McpParam("Point the camera looks at, Z in MM. Omit to keep.")] public float? target_z_mm;
        [McpParam("Pitch in DEGREES: 0 is level, positive looks DOWN, negative looks UP. Omit to keep.")] public float? angle_x;
        [McpParam("Yaw in DEGREES around the vertical axis. Omit to keep.")] public float? angle_y;
        [McpParam("Distance from the target in MM. Clamped to the camera's own range. Omit to keep.", Min = 0)] public float? distance_mm;
    }

    [Serializable]
    public class ParamsSetSetting
    {
        [McpParam("Setting key.", Required = true, Enum = new[]
        {
            "snap_enabled", "snap_threshold", "grid_enabled", "grid_step",
            "block_on_violation", "auto_save", "auto_save_interval", "snap_verbose_log",
            "camera_pan_free", "edge_partial_threshold", "mouse_sensitivity",
            "wasd_speed", "arrow_speed",
            "photo_active", "photo_quality", "photo_shadows", "photo_soft_shadows", "photo_anti_aliasing",
            "photo_supersampling", "photo_ambient_occlusion", "photo_bloom", "photo_vignette",
            "photo_ceiling", "photo_ssgi", "photo_lamp_shadows", "photo_hdr", "photo_ao_full_res",
            "photo_ambient", "photo_floor_bounce", "photo_ambient_sky", "photo_ambient_equator",
            "photo_bounce_max", "photo_exposure", "photo_contrast", "photo_saturation",
            "photo_tonemap",
            "photo_bloom_strength", "photo_bloom_threshold", "photo_bloom_clamp",
            "photo_vignette_strength",
            "photo_sun_shadow_strength", "photo_shadow_distance_mm", "photo_render_scale",
            "photo_shadowmap", "photo_lights_per_object",
            "photo_ao_intensity", "photo_ao_radius", "photo_ao_direct", "photo_ao_falloff_mm",
            "photo_ssgi_strength", "photo_ssgi_radius", "photo_ssgi_samples",
            "photo_ssgi_resolution", "photo_ssgi_blur",
        })]
        public string name = string.Empty;
        [McpParam("New on/off value. Send this for the on/off settings (snap_enabled, grid_enabled, block_on_violation, auto_save, snap_verbose_log, camera_pan_free, and every photo_* key that names a toggle: photo_shadows, photo_soft_shadows, photo_anti_aliasing, photo_supersampling, photo_ambient_occlusion, photo_bloom, photo_vignette, photo_ceiling, photo_ssgi, photo_lamp_shadows, photo_hdr).")]
        public bool? value;
        [McpParam("New numeric value: snap_threshold and grid_step in MM, auto_save_interval in seconds, edge_partial_threshold in %, mouse_sensitivity / wasd_speed / arrow_speed as a multiplier. Photo mode: photo_quality 0=low 1=medium 2=high 3=custom; photo_ssgi_radius / photo_ao_radius / photo_shadow_distance_mm / photo_ao_falloff_mm in MM; photo_shadowmap in pixels; photo_ssgi_samples and photo_lights_per_object are counts; photo_ssgi_blur in pixels (0 = no denoise); photo_bloom_clamp caps how bright one pixel may contribute to the glow, in % of white — it is what stops an open sky from smearing over the whole frame; everything else in %. Send this instead of value for those keys.")]
        public float? number;
    }

    [Serializable]
    public class ParamsGetElements
    {
        [McpParam("Exact board names to fetch. Omit to select by filter (or everything).")]
        public string[]? names;

        [McpParam("Name filter: substring or wildcard with '*', case-insensitive (e.g. 'B4_upper*'). Omit to skip.")]
        public string? filter;

        [McpParam("true = compact one-line info per element (name, type, position, size, locked, hasViolations). Default false = full info.")]
        public bool summary;

        [McpParam("true = include facade validation fields (faceNormal, faceInward, faceObstructions, openingViolations) for facade elements. Default false.")]
        public bool facade_validation;
    }

    [Serializable]
    public class ParamsGetViolations
    {
        [McpParam("Only report violations of these boards. Omit for the whole scene.")]
        public string[]? names;
    }

    [Serializable]
    public class ParamsGetFreeSpace
    {
        [McpParam("Exactly 2 board names — returns the free box between them.", Required = true, Min = 2)]
        public string[] between = Array.Empty<string>();
    }

    [Serializable]
    public class ParamsDistributeEvenly
    {
        [McpParam("At least 3 board names. The two outermost (along the axis) stay; the middle ones move so center-to-center spacing is equal.", Required = true, Min = 3)]
        public string[] names = Array.Empty<string>();
        [McpParam("World axis to distribute along.", Required = true, Enum = new[] { "x", "y", "z" })]
        public string axis = string.Empty;
    }

    [Serializable]
    public class ParamsCreateModule
    {
        [McpParam("Module name, e.g. 'Тумба с ящиками'.", Required = true)] public string name = string.Empty;
        [McpParam("Board names (at least 2).", Required = true, Min = 2)] public string[] members = Array.Empty<string>();
        [McpParam("World width axis used by resize_module.", Enum = new[] { "x", "y", "z" })]
        public string width_axis = "x";
    }

    [Serializable]
    public class ParamsGroupV2
    {
        [McpParam("Stable group/module id (name).", Required = true)] public string id = "";
        [McpParam("Element names; declaration replaces the group's membership.", Required = true, Min = 1)]
        public string[] names = Array.Empty<string>();
        [McpParam("World width axis used by resize_module.", Enum = new[] { "x", "y", "z" })]
        public string width_axis = "x";
    }

    [Serializable]
    public class ParamsAlignSelection
    {
        [McpParam("Selector whose matched groups/elements move.", Required = true)] public string selector = "";
        [McpParam("Exact target element/wall name.", Required = true)] public string target = "";
        [McpParam("Moving selection face: left/right/bottom/top/back/front.", Required = true,
            Enum = new[] { "left", "right", "bottom", "top", "back", "front" })]
        public string face = "";
        [McpParam("Target face. Omit for the opposite face on the same axis.",
            Enum = new[] { "left", "right", "bottom", "top", "back", "front" })]
        public string target_face = "";
        [McpParam("Face gap in MM.")] public float gap_mm;
    }

    [Serializable]
    public class FloorplanPoint
    {
        [McpParam("Point id.", Required = true)] public string id = "";
        [McpParam("X in MM from floorplan origin.", Required = true)] public int x;
        [McpParam("Z in MM from floorplan origin.", Required = true)] public int z;
    }

    [Serializable]
    public class FloorplanWall
    {
        [McpParam("Stable wall id.", Required = true)] public string id = "";
        [McpParam("Start point id.", Required = true)] public string from = "";
        [McpParam("End point id.", Required = true)] public string to = "";
        [McpParam("Wall kind.", Required = true, Enum = new[] { "bearing", "partition" })]
        public string kind = "";
        [McpParam("Height in MM.", Required = true, Min = 1)] public int height_mm;
        [McpParam("Thickness in MM, overriding the kind's project instruction. Use for walls that " +
            "do not match the project default (e.g. a 150 mm facade in a 250/125 project).", Min = 1)]
        public int? thickness_mm;
    }

    [Serializable]
    public class FloorplanFloor
    {
        [McpParam("Stable floor id.", Required = true)] public string id = "";
        [McpParam("Polygon point ids.", Required = true, Min = 3)] public string[] poly = Array.Empty<string>();
        [McpParam("Top Y in MM.")] public int top_y_mm;
        [McpParam("Thickness in MM; omit to use floor_thickness_mm instruction.", Min = 1)] public int? thickness_mm;
    }

    [Serializable]
    public class FloorplanOpening
    {
        [McpParam("Stable opening id.", Required = true)] public string id = "";
        [McpParam("Wall id.", Required = true)] public string wall = "";
        [McpParam("Opening kind.", Required = true, Enum = new[] { "window", "door" })]
        public string kind = "";
        [McpParam("Offset from wall start in MM.", Required = true, Min = 0)] public int offset_mm;
        [McpParam("Width in MM.", Required = true, Min = 1)] public int width_mm;
        [McpParam("Height in MM.", Required = true, Min = 1)] public int height_mm;
        [McpParam("Sill height from wall base in MM. Must be 0 for a door: a door opening starts at the floor.", Min = 0)] public int sill_mm;
    }

    [Serializable]
    public class FloorplanRoom
    {
        [McpParam("Stable room id.", Required = true)] public string id = "";
        [McpParam("Room polygon point ids. Edges become shared/reused walls.", Required = true, Min = 3)]
        public string[] poly = Array.Empty<string>();
        [McpParam("Wall kind for generated room edges.", Enum = new[] { "bearing", "partition" })]
        public string kind = "partition";
        [McpParam("Generated wall height in MM.", Min = 1)] public int height = 2700;
        [McpParam("Floor top Y in MM.")] public int top_y_mm;
        [McpParam("Floor thickness in MM; omit to use floor_thickness_mm instruction.", Min = 1)] public int? thickness_mm;
    }

    [Serializable]
    public class ParamsFloorplanDeclaration
    {
        [McpParam("Stable declaration id.", Required = true)] public string id = "";
        [McpParam("World origin X in MM.")] public int origin_x_mm;
        [McpParam("World origin Z in MM.")] public int origin_z_mm;
        [McpParam("Named points.", Required = true, Min = 2)] public FloorplanPoint[] points = Array.Empty<FloorplanPoint>();
        [McpParam("Explicit walls.")] public FloorplanWall[] walls = Array.Empty<FloorplanWall>();
        [McpParam("Explicit floors.")] public FloorplanFloor[] floors = Array.Empty<FloorplanFloor>();
        [McpParam("Openings on explicit or room-generated walls.")] public FloorplanOpening[] openings = Array.Empty<FloorplanOpening>();
        [McpParam("Room wrappers; each creates a floor and reuses walls by undirected point-pair.")]
        public FloorplanRoom[] rooms = Array.Empty<FloorplanRoom>();
    }

    [Serializable]
    public class ParamsGetCompact
    {
        [McpParam("Exact element names.", Required = true, Min = 1)] public string[] names = Array.Empty<string>();
        [McpParam("Optional fields: name,kind,anchor,size,rotY,hasViolations,module,wallKind.")]
        public string[] fields = Array.Empty<string>();
    }

    [Serializable]
    public class ParamsModule
    {
        [McpParam("Module id (number) or name.", Required = true)] public string module = string.Empty;
    }

    [Serializable]
    public class ParamsModules
    {
        [McpParam("Module ids (numbers) or names. At least 1.", Required = true, Min = 1)]
        public string[] modules = Array.Empty<string>();
    }

    [Serializable]
    public class ParamsModuleElements
    {
        [McpParam("Module id or name.", Required = true)] public string module = string.Empty;
        [McpParam("Board names to add. At least 1.", Required = true, Min = 1)]
        public string[] names = Array.Empty<string>();
    }
}
