using System;

// Parameter POCOs for every MCP tool. One class per tool shape. Each agent-facing
// field carries [McpParam] with its description + units; wire-only fields carry
// [McpIgnore]. See McpToolAttributes.cs for rules.
//
// SURFACE CONVENTION (2026-07 redesign): every element-addressing tool takes an
// ARRAY (names[] / ops[] / items[]) — there are NO single-element tools. Batches
// are atomic: if ANY entry is invalid, NOTHING is applied.
//
// Nested op/item classes are consumed by the codegen (Zod) and the server JSON
// schema, which do NOT support nested renames — nested field names MUST already
// be the agent-facing snake_case names.
//
// Coordinates x/y/z are nullable where "omit an axis = keep current" applies; a
// missing axis is NOT treated as 0 (see ResolveVec / ResolveDims in the handler).

namespace KitchenDesigner.Core.MCP.Contract
{
    [Serializable]
    public class ParamsGuide
    {
        [McpParam("Cheat-sheet topic. Omit for the workflow overview.",
            Enum = new[] { "workflow", "elements", "fields", "drawers", "violations" })]
        public string? topic;
    }

    // ── names[] / object_paths[] ─────────────────────────────────────────────

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

    // Shared by advanced set_position / set_rotation / set_scale. The meters-vs-
    // degrees nuance lives in each tool's description.
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

    // ── Floor (scene singleton — no element addressing) ──────────────────────

    [Serializable]
    public class ParamsResizeFloor
    {
        [McpParam("Floor width (X) in MM.", Required = true, Min = 1)] public int width;
        [McpParam("Floor length (Y) in MM.", Required = true, Min = 1)] public int height;
        [McpParam("Floor thickness (Z) in MM.", Required = true, Min = 1)] public int depth;
    }

    // ── edit_elements: THE universal property editor ─────────────────────────

    /// <summary>Одна операция edit_elements. Все указанные поля применяются к
    /// элементу разом. Типо-специфичные поля (зазоры, режимы, параметры ящика и
    /// т.д.) валидируются по фактическому типу элемента — чужое поле = ошибка
    /// всего батча.</summary>
    [Serializable]
    public class EditOp
    {
        [McpParam("Exact element name.", Required = true)] public string name = string.Empty;

        // Допустимый алфавит имени: ^[A-Za-z0-9_-]+$ (см. ElementNaming).
        [McpParam("Rename the element to this new name. Must be unique among all elements (case-insensitive) "
            + "and match ^[A-Za-z0-9_-]+$ — latin letters, digits, '-' and '_' only; no spaces, no cyrillic. Omit to keep.")]
        public string? new_name;

        // Geometry (any element; drawers reject width/height/depth).
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

        // Any element.
        [McpParam("Lock (true) / unlock (false). Unlock ONLY with the user's explicit permission. Omit to keep.")]
        public bool? locked;
        [McpParam("Material decor id or display name (see list_materials). Omit to keep.")]
        public string? material;

        // Facades (FacadeElement / AssembledFacadeElement).
        [McpParam("Facade only: left gap in MM. Omit to keep.", Min = 0)] public int? gap_left;
        [McpParam("Facade only: right gap in MM. Omit to keep.", Min = 0)] public int? gap_right;
        [McpParam("Facade only: top gap in MM. Omit to keep.", Min = 0)] public int? gap_top;
        [McpParam("Facade only: bottom gap in MM. Omit to keep.", Min = 0)] public int? gap_bottom;
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
        [McpParam("Facade/window/door: true = open, false = close. Omit to keep. For drawers use cycle_drawer_animation.")]
        public bool? is_open;

        // Radial shelf.
        [McpParam("Radial shelf only: corner rounding radius in MM (clamped to 1..min(width, depth)). Omit to keep.", Min = 1)]
        public int? corner_radius;

        // Grooves (plain board only).
        [McpParam("Plain board only: REPLACES the whole set of grooves. Comma-separated \"kind:side\" pairs, " +
                  "kind = through|blind, side = top|bottom|left|right (side names the edge the groove runs along, " +
                  "in the part's own frame). Example: \"through:top, blind:left\". Empty string removes all grooves. " +
                  "Groove size is fixed at 16*4*7 mm (offset*width*depth) by the CNC and cannot be changed. " +
                  "Duplicates of the same kind+side are rejected. Omit to keep.")]
        public string? grooves;

        // GTV drawer.
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
        [McpParam("Drawer only: exact name of the facade acting as this drawer's front. Empty string detaches. Omit to keep.")]
        public string? attached_facade_name;

        // Tables (TableElement / RadiusTableElement).
        [McpParam("Table only: inward offset of legs from corners along X and Z, in MM. Omit to keep.", Min = 0)]
        public int? leg_inset_mm;
        [McpParam("Table only: material id or display name for the tabletop (see list_materials). Omit to keep.")]
        public string? tabletop_material;
        [McpParam("Table only: material id or display name for the legs (see list_materials). Omit to keep.")]
        public string? legs_material;

        // Pillar.
        [McpParam("Pillar only: middle cylinder height in MM (clamped 50..100). Omit to keep.", Min = 50, Max = 100)]
        public int? mid_height_mm;

        // Window.
        [McpParam("Window only: glass tint — clear (transparent) or tinted (slightly darkened). Omit to keep.",
            Enum = new[] { "clear", "tinted" })]
        public string? tint;
        [McpParam("Window only: windowsill outward protrusion in MM (0..200). Omit to keep.", Min = 0, Max = 200)]
        public int? sill_protrusion_mm;

        // Door.
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

    // ── create_elements ──────────────────────────────────────────────────────

    /// <summary>Один создаваемый элемент. Тип задаётся полем type (не флагами).</summary>
    [Serializable]
    public class CreateItem
    {
        // Допустимый алфавит имени: ^[A-Za-z0-9_-]+$ (см. ElementNaming).
        [McpParam("Unique name for the new element (case-insensitive across the whole project). "
            + "Must match ^[A-Za-z0-9_-]+$ — latin letters, digits, '-' and '_' only; no spaces, no cyrillic.",
            Required = true)]
        public string name = string.Empty;

        [McpParam("Element type. Default board. wall = board acting as a structural anchor; floor ignores size/position.",
            Enum = new[] { "board", "wall", "floor", "facade", "assembled_facade", "radial_shelf", "panel", "drawer", "table", "radius_table", "pillar", "window", "door" })]
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
    }

    [Serializable]
    public class ParamsCreateElements
    {
        [McpParam("Elements to create. At least 1. Whole batch is ONE undo step.", Required = true, Min = 1)]
        public CreateItem[] items = Array.Empty<CreateItem>();
    }

    // ── convert / clone / align / rename ─────────────────────────────────────

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

    // ── Diagnostics ──────────────────────────────────────────────────────────

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
    public class ParamsSetSetting
    {
        [McpParam("Setting key.", Required = true, Enum = new[] { "lower_near_walls", "snap_enabled", "grid_enabled", "walls_enabled" })]
        public string name = string.Empty;
        [McpParam("New on/off value.", Required = true)] public bool value;
    }

    // ── Батч-чтения ──────────────────────────────────────────────────────────

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

    // ── Высокоуровневое размещение ───────────────────────────────────────────

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

    // ── Модули ───────────────────────────────────────────────────────────────

    [Serializable]
    public class ParamsCreateModule
    {
        [McpParam("Module name, e.g. 'Тумба с ящиками'.", Required = true)] public string name = string.Empty;
        [McpParam("Board names (at least 2).", Required = true, Min = 2)] public string[] members = Array.Empty<string>();
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
