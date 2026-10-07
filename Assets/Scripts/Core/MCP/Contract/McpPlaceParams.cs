using System;

namespace KitchenDesigner.Core.MCP.Contract
{
    [Serializable]
    public class PlaceAgainstOp
    {
        [McpParam("Exact name of the element to stand against (a wall, a neighbour cabinet, a window...).", Required = true)]
        public string target = string.Empty;

        [McpParam("Which face of the TARGET the part touches: left|right (X), bottom|top (Y), back|front (Z). "
            + "front is the +Z side, back the -Z side: a part standing in the room in front of a wall uses that wall's "
            + "front (or back) face, whichever looks into the room. right-of-a-neighbour means face 'right' of the NEIGHBOUR.",
            Required = true, Enum = new[] { "left", "right", "bottom", "top", "back", "front" })]
        public string face = string.Empty;

        [McpParam("Gap in MM between the two faces; default 0 = flush contact.", Min = 0)]
        public float gap_mm;
    }

    [Serializable]
    public class PlaceAlignOp
    {
        [McpParam("Exact name of the element to line the part up with.", Required = true)]
        public string target = string.Empty;

        [McpParam("World axis to line up along: x (left-right), y (up), z (back-front).",
            Required = true, Enum = new[] { "x", "y", "z" })]
        public string axis = string.Empty;

        [McpParam("Which point of the part meets the SAME point of the target: min (left/bottom/back edge), "
            + "center (the middle - e.g. a cabinet centred under a window) or max (right/top/front edge).",
            Required = true, Enum = new[] { "min", "center", "max" })]
        public string at = string.Empty;

        [McpParam("Shift in MM after lining up (may be negative). Default 0.")]
        public float offset_mm;
    }

    [Serializable]
    public class PlaceItem
    {
        [McpParam("Name of the part. A NEW name creates the part (type and size apply); the name of an EXISTING part "
            + "moves it to the new place (use this to correct a mistake instead of deleting and re-creating).",
            Required = true)]
        public string name = string.Empty;

        [McpParam("Element type for a NEW part, as in create_elements (default board). window, door, wall and floor are "
            + "refused here: use add_opening / create_walls / create_floor.")]
        public string? type;

        [McpParam("Width (X before rotation) in MM for a new part; type defaults apply when omitted.", Min = 1)] public int? width;
        [McpParam("Height (Y) in MM for a new part.", Min = 1)] public int? height;
        [McpParam("Depth (Z before rotation) in MM for a new part.", Min = 1)] public int? depth;

        [McpParam("Rotation around the vertical axis in DEGREES. Omit to keep (a new part starts at 0).")]
        public float? rot_y;

        [McpParam("What the part stands on: \"floor\" (the floor level of the current storey; the DEFAULT for a new part) "
            + "or the exact name of another element (the part then rests on its top). The server computes the height.")]
        public string? on;

        [McpParam("Lift in MM of the part's bottom above what it stands on (a wall cabinet hangs lift_mm above the floor). Default 0.")]
        public float lift_mm;

        [McpParam("Sit against other parts: each entry sets one coordinate (the axis of the named face). A cabinet in a "
            + "row: against the wall's room-side face and against the neighbour's left or right face.")]
        public PlaceAgainstOp[] against = Array.Empty<PlaceAgainstOp>();

        [McpParam("Line the part up with other parts on an axis the against entries do not fix (e.g. centre a cabinet "
            + "on a window along x).")]
        public PlaceAlignOp[] align = Array.Empty<PlaceAlignOp>();
    }

    [Serializable]
    public class ParamsPlace
    {
        [McpParam("Parts to place, IN ORDER: a later item may refer to an earlier one (against / align / on). "
            + "At least 1. Whole batch is atomic and ONE undo step; every axis of a NEW part must be fixed by on / "
            + "against / align, otherwise the refusal names the missing axis.", Required = true, Min = 1)]
        public PlaceItem[] items = Array.Empty<PlaceItem>();

        [McpParam(McpRefText.Output)] public string? @ref;

        [McpParam("true = DRY-RUN: place everything, report the resulting placements and sceneViolationDelta, then revert. Default false.")]
        public bool dry_run;
    }
}
