using System;

namespace KitchenDesigner.Core.MCP.Contract
{
    [Serializable]
    public class RunModule
    {
        [McpParam("Unique name of the cabinet. It becomes the name of the part (latin letters, digits, '-' and '_'). Sending the SAME name again updates that cabinet in place.", Required = true)]
        public string name = string.Empty;

        [McpParam("What it is: base (stands on the floor), wall (hangs on the wall above the worktop) or tall (floor to high). The kind picks the default height, depth and how high it hangs.",
            Required = true, Enum = new[] { "base", "wall", "tall" })]
        public string kind = string.Empty;

        [McpParam("Width in MM along the wall.", Required = true, Min = 1)]
        public int width_mm;

        [McpParam("Height in MM. Omit for the default of the kind (see guide topic run).", Min = 1)]
        public int? height_mm;

        [McpParam("Depth in MM, measured away from the wall. Omit for the default of the kind (see guide topic run).", Min = 1)]
        public int? depth_mm;
    }

    [Serializable]
    public class ParamsApplyRun
    {
        [McpParam("Id of the run (latin letters, digits, '-' and '_'). Idempotent: sending the same id again updates that run in place - a changed width shifts the cabinets after it, cabinets missing from the list are removed, an identical call changes nothing.", Required = true)]
        public string id = string.Empty;

        [McpParam("Exact name of the wall the run stands against (a wall running along x or along z). The cabinets stand on the side that looks into the room.", Required = true)]
        public string wall = string.Empty;

        [McpParam("Where the run starts: left (the end of the wall with the smaller x, or the smaller z for a wall along z), right (the opposite end, the first cabinet of the list is the rightmost) or the exact name of a part (a fridge, a wall) - the run then starts right after its far side and grows towards larger x/z. Default left.")]
        public string? from;

        [McpParam("Distance in MM from the chosen start (the wall end, or the far side of the named part) to the first cabinet. Default 0.", Min = 0)]
        public float start_mm;

        [McpParam("Gap in MM between neighbouring cabinets. Default 0 = flush.", Min = 0)]
        public float gap_mm;

        [McpParam("Height in MM of the floor the run stands on. Default: the floor of the current level.")]
        public float? base_y_mm;

        [McpParam("Which face of the wall looks into the room: front|back for a wall along x, left|right for a wall along z. Omit to let the server find the room by the floor beside the wall; it asks for this value only when it cannot tell.",
            Enum = new[] { "front", "back", "left", "right" })]
        public string? room_side;

        [McpParam("The cabinets IN ORDER along the run, from the start outward. At least 1.", Required = true, Min = 1)]
        public RunModule[] modules = Array.Empty<RunModule>();

        [McpParam(McpRefText.Output)] public string? @ref;
        [McpParam(McpVerbosity.Syntax, Enum = new[] { McpVerbosity.Terse, McpVerbosity.Full })] public string? verbosity;

        [McpParam(McpDryRunText.Param)]
        public bool dry_run;
    }
}
