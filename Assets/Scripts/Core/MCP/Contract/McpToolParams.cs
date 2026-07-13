using System;

// Parameter POCOs for every MCP tool. One class per tool shape. Each agent-facing
// field carries [McpParam] with its description + units; wire-only fields (legacy
// aliases, template_name) carry [McpIgnore]. See McpToolAttributes.cs for rules.
//
// Coordinates x/y/z are nullable where "omit an axis = keep current" applies; a
// missing axis is NOT treated as 0 (see ResolveVec / ResolveDims in the handler).

namespace KitchenDesigner.Core.MCP.Contract
{
    // ── name-only / path-only ────────────────────────────────────────────────

    [Serializable]
    public class ParamsName
    {
        [McpParam("Exact board name (from get_all_elements).", Required = true)]
        public string name;
    }

    [Serializable]
    public class ParamsObjectPath
    {
        [McpParam("Object name or hierarchy path (Parent/Child).", Required = true)]
        public string object_path;

        // Legacy fallback: some callers still send `name`; handler accepts either.
        [McpIgnore] public string name;
    }

    [Serializable]
    public class ParamsFindObjects
    {
        [McpParam("Full or partial object name.", Required = true)]
        public string name_filter;
    }

    [Serializable]
    public class ParamsSetActive
    {
        [McpParam("Object name or path.", Required = true)]
        public string object_path;

        [McpParam("Enable (true) or disable (false).", Required = true)]
        public bool active;
    }

    // Shared by advanced set_position / set_rotation / set_scale. The meters-vs-
    // degrees nuance lives in each tool's description.
    [Serializable]
    public class ParamsSetTransform
    {
        [McpParam("Object name or path.", Required = true)]
        public string object_path;

        [McpParam("Value for X. Omit to keep current.")] public float? x;
        [McpParam("Value for Y. Omit to keep current.")] public float? y;
        [McpParam("Value for Z. Omit to keep current.")] public float? z;
    }

    // ── move / resize / rotate ───────────────────────────────────────────────

    [Serializable]
    public class ParamsMoveElement
    {
        [McpParam("Exact board name.", Required = true)] public string name;
        [McpParam("Target X in METERS. Omit to keep current X.")] public float? x;
        [McpParam("Target Y in METERS. Omit to keep current Y.")] public float? y;
        [McpParam("Target Z in METERS. Omit to keep current Z.")] public float? z;
    }

    [Serializable]
    public class ParamsResizeElement
    {
        [McpParam("Exact board name.", Required = true)] public string name;
        [McpParam("New width (X) in MM. Omit to keep current width.", Min = 1)] public int? width;
        [McpParam("New height (Y) in MM. Omit to keep current height.", Min = 1)] public int? height;
        [McpParam("New depth/thickness (Z) in MM. Omit to keep current depth.", Min = 1)] public int? depth;

        // Legacy dimension aliases — accepted on the wire, not exposed to the agent.
        [McpIgnore] public int? dimX;
        [McpIgnore] public int? dimY;
        [McpIgnore] public int? dimZ;
    }

    [Serializable]
    public class ParamsResizeFloor
    {
        [McpParam("Floor width (X) in MM.", Required = true, Min = 1)] public int width;
        [McpParam("Floor length (Y) in MM.", Required = true, Min = 1)] public int height;
        [McpParam("Floor thickness (Z) in MM.", Required = true, Min = 1)] public int depth;
    }

    [Serializable]
    public class ParamsRotateElement
    {
        [McpParam("Exact board name.", Required = true)] public string name;
        [McpParam("Rotation around X in DEGREES. Omit to keep current.")] public float? x;
        [McpParam("Rotation around Y in DEGREES. Omit to keep current.")] public float? y;
        [McpParam("Rotation around Z in DEGREES. Omit to keep current.")] public float? z;
    }

    [Serializable]
    public class ParamsElementLock
    {
        [McpParam("Exact board name.", Required = true)] public string name;
        [McpParam("true = lock (protect), false = unlock (allow editing).", Required = true)] public bool locked;
    }

    [Serializable]
    public class ParamsSetFacadeMode
    {
        [McpParam("Exact facade element name.", Required = true)] public string name;
        [McpParam("Opening mode: front_*/back_* (hinged on a face edge), edge_* (hinged on the thickness edge), drawer_* (sliding along an axis).",
            Required = true, Enum = new[] {
                "front_left", "front_right", "front_top", "front_bottom",
                "back_left", "back_right", "back_top", "back_bottom",
                "edge_top_left", "edge_top_right", "edge_bottom_left", "edge_bottom_right",
                "drawer_out", "drawer_in", "drawer_right", "drawer_left", "drawer_up", "drawer_down" })]
        public string mode;
    }

    [Serializable]
    public class ParamsSetMaterial
    {
        [McpParam("Exact board/facade name.", Required = true)] public string name;
        [McpParam("Material id (e.g. 'oak') or its display name (e.g. 'Дуб сонома'). See list_materials.", Required = true)]
        public string material; // id ИЛИ отображаемое имя
    }

    [Serializable]
    public class ParamsCreateElement
    {
        [McpParam("Name for the new element (becomes its board name).", Required = true)]
        public string name;

        // template_name is a legacy wire field: the handler uses `name` when it is
        // absent. Kept for backward compatibility, hidden from the agent.
        [McpIgnore] public string template_name;

        [McpParam("Position X in METERS.")] public float x;
        [McpParam("Position Y in METERS.")] public float y;
        [McpParam("Position Z in METERS.")] public float z;

        [McpParam("Size along X in MM (default 800).", Min = 1)] public int width;
        [McpParam("Size along Y in MM (default 400).", Min = 1)] public int height;
        [McpParam("Thickness along Z in MM (default 18).", Min = 1)] public int depth;
        [McpParam("Radial shelf only: outer radius in MM (default 300).", Min = 1)] public int radius = 300;

        [McpParam("Create as a WALL (structural anchor). Default false.")] public bool is_wall;
        [McpParam("Create the FLOOR plate. Ignores size/position. Default false.")] public bool is_floor;
        [McpParam("Create as a FACADE (door/front with gaps). Default false.")] public bool is_facade;
        [McpParam("Create as an ASSEMBLED (framed) facade — real frame geometry. Default false. Pair with fill.")] public bool is_assembled;
        [McpParam("Create as a RADIAL (corner) shelf. Default false. Pair with radius.")] public bool is_radial_shelf;
        [McpParam("Create as a GTV DRAWER (sliding box). Default false. Pair with drawer_type/drawer_length/drawer_color/drawer_internal_width; width/height/depth are ignored.")]
        public bool is_drawer;

        [McpParam("Drawer only: side height type — A=86, B=120, C=168, D=200 mm. Default A.", Enum = new[] { "A", "B", "C", "D" })]
        public string drawer_type;
        [McpParam("Drawer only: nominal slide length in MM, one of 250/300/350/400/450/500/550/600. Default 350.")]
        public int drawer_length = 350;
        [McpParam("Drawer only: GTV color. Default anthracite.", Enum = new[] { "anthracite", "white", "black" })]
        public string drawer_color;
        [McpParam("Drawer only: internal box width in MM (default 400, min 100).", Min = 100)]
        public int drawer_internal_width = 400;

        [McpParam("Assembled facade only: center fill — blind (panel), glass (vitrine with glass), open (empty vitrine). Default blind.", Enum = new[] { "blind", "glass", "open" })]
        public string fill;

        [McpParam("Facade only: left gap in MM (default 2).", Name = "gap_left", Min = 0)] public int gapLeft = 2;
        [McpParam("Facade only: right gap in MM (default 2).", Name = "gap_right", Min = 0)] public int gapRight = 2;
        [McpParam("Facade only: top gap in MM (default 2).", Name = "gap_top", Min = 0)] public int gapTop = 2;
        [McpParam("Facade only: bottom gap in MM (default 2).", Name = "gap_bottom", Min = 0)] public int gapBottom = 2;
    }

    [Serializable]
    public class ParamsConvertElement
    {
        [McpParam("Exact element name to convert.", Required = true)] public string name;
        [McpParam("Target type: part (plain board), facade (door/front), assembled_facade (framed facade), radial_shelf (corner shelf).",
            Required = true, Enum = new[] { "part", "facade", "assembled_facade", "radial_shelf" })]
        public string target;
        [McpParam("When target=assembled_facade: center fill — blind (panel), glass, open (empty). Default keeps/blind.", Enum = new[] { "blind", "glass", "open" })]
        public string fill;
    }

    [Serializable]
    public class ParamsMenuPath
    {
        [McpParam("Menu path, e.g. 'Edit/Undo'.", Required = true)] public string menu_path;
    }

    [Serializable]
    public class ParamsLogCount
    {
        [McpParam("How many entries (max 200, default 50).", Min = 1, Max = 200)] public int count;
    }

    [Serializable]
    public class ParamsExportCsv
    {
        [McpParam("Full file path to write the CSV to.", Required = true)] public string path;
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
        public string name;
        [McpParam("New on/off value.", Required = true)] public bool value;
    }

    [Serializable]
    public class ParamsSnapDiagnose
    {
        [McpParam("Exact board name.", Required = true)] public string name;
        [McpParam("Test X in METERS (default: current).")] public float? x;
        [McpParam("Test Y in METERS (default: current).")] public float? y;
        [McpParam("Test Z in METERS (default: current).")] public float? z;
    }

    [Serializable]
    public class ParamsSimulateMove
    {
        [McpParam("Exact board name.", Required = true)] public string name;
        [McpParam("Target X in METERS. Omit to keep current X.")] public float? x;
        [McpParam("Target Y in METERS. Omit to keep current Y.")] public float? y;
        [McpParam("Target Z in METERS. Omit to keep current Z.")] public float? z;
    }

    [Serializable]
    public class ParamsSimulateResize
    {
        [McpParam("Exact board name.", Required = true)] public string name;
        [McpParam("Target width (X) in MM. Omit to keep current width.", Min = 1)] public int? width;
        [McpParam("Target height (Y) in MM. Omit to keep current height.", Min = 1)] public int? height;
        [McpParam("Target depth/thickness (Z) in MM. Omit to keep current depth.", Min = 1)] public int? depth;
        [McpIgnore] public int? dimX;
        [McpIgnore] public int? dimY;
        [McpIgnore] public int? dimZ;
    }

    [Serializable]
    public class ParamsCreateModule
    {
        [McpParam("Module name, e.g. 'Тумба с ящиками'.", Required = true)] public string name;
        [McpParam("Board names (at least 2).", Required = true, Min = 2)] public string[] members;
    }

    [Serializable]
    public class ParamsModule
    {
        [McpParam("Module id (number) or name.", Required = true)] public string module;
    }

    [Serializable]
    public class ParamsModuleElement
    {
        [McpParam("Module id or name.", Required = true)] public string module;
        [McpParam("Board name to add.", Required = true)] public string name;
    }

    [Serializable]
    public class ParamsSetDrawerProperties
    {
        [McpParam("Exact drawer element name.", Required = true)] public string name;
        [McpParam("Side height type: A=86, B=120, C=168, D=200 mm.", Enum = new[] { "A", "B", "C", "D" })] public string drawer_type;
        [McpParam("Nominal slide length in MM, one of 250/300/350/400/450/500/550/600. Invalid values are ignored.")] public int? drawer_length;
        [McpParam("GTV color.", Enum = new[] { "anthracite", "white", "black" })] public string drawer_color;
        [McpParam("Internal box width in MM (min 100).", Min = 100)] public int? internal_width;
        [McpParam("Mark as part of a DOUBLE drawer (two stacked boxes).")] public bool? is_double;
        [McpParam("Double drawer only: this box is the UPPER one.")] public bool? is_upper;
        [McpParam("Double drawer only: exact name of the paired drawer element (link both ways for sync).")] public string paired_drawer_name;
        [McpParam("Exact name of the facade element acting as this drawer's front — it opens/closes together with the drawer. Empty string detaches.")] public string attached_facade_name;
    }
}
