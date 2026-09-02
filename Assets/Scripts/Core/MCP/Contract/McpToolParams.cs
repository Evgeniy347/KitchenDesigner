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
    public class TransformOp
    {
        [McpParam("Object name or path.", Required = true)]
        public string object_path = string.Empty;

        [McpParam("Value for X. Omit to keep current.")] public float? x;
        [McpParam("Value for Y. Omit to keep current.")] public float? y;
        [McpParam("Value for Z. Omit to keep current.")] public float? z;
    }

    [Serializable]
    public class ParamsTransformOps
    {
        [McpParam("Objects to transform. At least 1.", Required = true, Min = 1)]
        public TransformOp[] ops = Array.Empty<TransformOp>();
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

        [McpParam("Target X in METERS. Omit to keep.")] public float? x;
        [McpParam("Target Y in METERS. Omit to keep.")] public float? y;
        [McpParam("Target Z in METERS. Omit to keep.")] public float? z;
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

        [McpParam("Chair and sofa only: height of the seat TOP above the floor in MM. Chair: " +
                  "clamped to 80..height-50 — the legs and the backrest each keep at least 50 mm. " +
                  "Sofa: it is the height of the solid base block, clamped to 150..height-200. " +
                  "Omit to keep.",
            Min = 0)]
        public int? seat_height;

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
        [McpParam("Sheet board only: suppress the EDG-01 error about an end that is only PARTIALLY covered " +
                  "by another part. Omit to keep.")]
        public bool? edge_skip_validation;

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
        [McpParam("Screw leg only: how deep the thread goes into the part it is screwed to, in MM (1..thread length). Below the part thickness the thread sticks out the other side, which is legal. Omit to keep.", Min = 1, Max = 1000)]
        public int? screw_insertion_mm;
        [McpParam("Screw leg only: diameter of the foot in MM (5..200) — width and depth are always equal. Omit to keep.", Min = 5, Max = 200)]
        public int? screw_base_diameter_mm;
        [McpParam("Screw leg only: height of the foot in MM (1..200). Omit to keep.", Min = 1, Max = 200)]
        public int? screw_base_height_mm;

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
            Enum = new[] { "board", "wall", "floor", "facade", "assembled_facade", "radial_shelf", "panel", "drawer", "movento_drawer", "table", "radius_table", "stool", "chair", "sofa", "pouffe", "bed", "pillar", "screw_leg", "window", "door", "sink", "cooktop", "oven", "dishwasher" })]
        public string? type;

        [McpParam("Position X in METERS.")] public float x;
        [McpParam("Position Y in METERS.")] public float y;
        [McpParam("Position Z in METERS.")] public float z;

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
        [McpParam("Height from wall base to opening bottom in MM. Door usually uses 0.", Min = 0)]
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
        [McpParam("X shift between copies in METERS (default 0).")] public float offset_x;
        [McpParam("Y shift between copies in METERS (default 0).")] public float offset_y;
        [McpParam("Z shift between copies in METERS (default 0).")] public float offset_z;
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
        [McpParam("Test X in METERS (default: current).")] public float? x;
        [McpParam("Test Y in METERS (default: current).")] public float? y;
        [McpParam("Test Z in METERS (default: current).")] public float? z;
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
        [McpParam("Selector over the scene. Space-separated clauses (AND): name mask 'B4_*', 'name:PAT', 'type:board|wall|floor|window|door|drawer|facade|assembled_facade|radial_shelf|panel|table|pillar|screw_leg|light', 'module:NAME', 'thickness==18' (also width/height/depth with == != >= <= > <), 'all_boards', 'all_modules', '*'.", Required = true)]
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
    public class ParamsSetSetting
    {
        [McpParam("Setting key.", Required = true, Enum = new[] { "snap_enabled", "grid_enabled", "camera_pan_free" })]
        public string name = string.Empty;
        [McpParam("New on/off value.", Required = true)] public bool value;
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
        [McpParam("Height in MM.", Required = true, Min = 1)] public int height;
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
        [McpParam("Width in MM.", Required = true, Min = 1)] public int width;
        [McpParam("Height in MM.", Required = true, Min = 1)] public int height;
        [McpParam("Sill/bottom height from wall base in MM.", Min = 0)] public int sill_mm;
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
