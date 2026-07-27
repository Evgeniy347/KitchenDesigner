// ===========================================================================
//  AUTO-GENERATED — DO NOT EDIT BY HAND.
//  Source of truth: Assets/Scripts/Core/MCP/Contract/*.cs (McpToolRegistry).
//  Regenerate with:  npm run gen:tools   (in mcp-server/)
// ===========================================================================
import { z } from "zod";

export type GenToolKind = "read" | "write" | "destructive";

export interface GenTool {
  name: string;
  title: string;
  description: string;
  kind: GenToolKind;
  cached?: boolean;
  staticText?: boolean;
  openWorld?: boolean;
  inputSchema?: Record<string, z.ZodTypeAny>;
  /** agent-facing param name -> Unity wire field name (applied when forwarding). */
  rename?: Record<string, string>;
}

export const GEN_TOOLS: GenTool[] = [
  {
    name: "guide",
    title: "Guide / cheat-sheet",
    description: "Usage cheat-sheet. Topics: workflow (default; units, batch editing, worked examples), elements (element types), fields (what each response field means), drawers (GTV drawers), violations (what counts as a violation). Call this first if unsure.",
    kind: "read",
    staticText: true,
    inputSchema: {
      topic: z.enum(["workflow", "elements", "fields", "drawers", "violations"]).optional().describe("Cheat-sheet topic. Omit for the workflow overview."),
    },
  },
  {
    name: "ping",
    title: "Ping",
    description: "Check that Unity is reachable. Returns Unity version.",
    kind: "read",
  },
  {
    name: "get_status",
    title: "Scene status",
    description: "Basic scene info: scene name, object count, play mode, platform.",
    kind: "read",
  },
  {
    name: "get_all_elements",
    title: "List elements",
    description: "List ALL boards/walls/floor with name, size (mm), position (m), rotation, AABB and hasViolations. Call this FIRST before editing.",
    kind: "read",
    cached: true,
  },
  {
    name: "get_scene_tree",
    title: "Scene tree (compact)",
    description: "COMPACT hierarchy for \u0027looking around\u0027 cheaply (use this before the verbose get_all_elements): modules (named groups) with member names \u002B world bbox, loose element names, and per-type counts. Then fetch details for a few names with get_elements.",
    kind: "read",
  },
  {
    name: "get_elements",
    title: "Get elements (batch)",
    description: "Info for one or SEVERAL elements in ONE call: pick by exact names and/or a name filter (substring or wildcard \u0027*\u0027). summary:true returns compact one-line info per element; facade_validation:true adds facade opening diagnostics.",
    kind: "read",
    inputSchema: {
      names: z.array(z.string().min(1)).optional().describe("Exact board names to fetch. Omit to select by filter (or everything)."),
      filter: z.string().optional().describe("Name filter: substring or wildcard with \u0027*\u0027, case-insensitive (e.g. \u0027B4_upper*\u0027). Omit to skip."),
      summary: z.boolean().optional().describe("true = compact one-line info per element (name, type, position, size, locked, hasViolations). Default false = full info."),
      facade_validation: z.boolean().optional().describe("true = include facade validation fields (faceNormal, faceInward, faceObstructions, openingViolations) for facade elements. Default false."),
    },
  },
  {
    name: "get_specification",
    title: "Specification",
    description: "Cut list: every distinct board size with count and area (m2), plus totals.",
    kind: "read",
  },
  {
    name: "get_violations",
    title: "List violations",
    description: "List elements that overlap another (with severity and penetration depth in mm) or are disconnected from the wall/floor structure. Also returns issues[]: coded scene analysis (level=error|warning, code, detail, message, target, secondary). Errors COL-xx = collisions; warnings GAP-01 near-contact (\u003C=8mm, not touching), SEAT-01 panel not fully seated in groove, FAC-01 facade gap \u003C1mm, DRW-01 drawer without facade. Warnings are analysis-only (no scene highlight). Optional names[] limits the report to those boards. Note: every mutation already returns its own violations \u2014 call this to check the WHOLE scene.",
    kind: "read",
    inputSchema: {
      names: z.array(z.string().min(1)).optional().describe("Only report violations of these boards. Omit for the whole scene."),
    },
  },
  {
    name: "get_element_gaps",
    title: "Element gaps (batch)",
    description: "For EACH given board: gap or overlap (in mm) to the nearest neighbour on each axis X/Y/Z. Negative gapMM = overlap.",
    kind: "read",
    inputSchema: {
      names: z.array(z.string().min(1)).min(1).describe("Exact board names (from get_all_elements). At least 1."),
    },
  },
  {
    name: "get_element_debug",
    title: "Element geometry (batch)",
    description: "Detailed geometry for EACH given element: AABB, face centers/normals, vertices, dims and effective dims (mm). For precise placement checks.",
    kind: "read",
    inputSchema: {
      names: z.array(z.string().min(1)).min(1).describe("Exact board names (from get_all_elements). At least 1."),
    },
  },
  {
    name: "get_floor_info",
    title: "Floor info",
    description: "Size (mm) and position (m) of the floor plate (BasePlate).",
    kind: "read",
  },
  {
    name: "get_settings",
    title: "Get settings",
    description: "Current project settings: snap on/off, snap threshold (mm), grid, autosave, verbose snap flag.",
    kind: "read",
  },
  {
    name: "get_project_instructions",
    title: "Get project instructions",
    description: "Return the project\u0027s free-text agent instructions (conventions: wall thicknesses, board thickness, gaps...). Call after connecting.",
    kind: "read",
  },
  {
    name: "set_project_instructions",
    title: "Set project instructions",
    description: "Replace the project\u0027s free-text agent instructions. Persists in the project file.",
    kind: "write",
    inputSchema: {
      text: z.string().min(1).describe("New free-text project instructions (conventions: wall thicknesses, board thickness, gaps...). Replaces the whole text. Empty string clears it."),
    },
  },
  {
    name: "set_attr",
    title: "Set attribute over a selection",
    description: "Bulk-change matched elements in ONE call by a selector: thickness/width/height/depth (MM), material, locked. Server picks the elements \u2014 you pass intent, not per-board numbers. Example: set_attr {selector:\u0027all_boards thickness==18\u0027, thickness:16}. Undoable (geometry). Returns matched/updated/sceneViolationCount.",
    kind: "write",
    inputSchema: {
      selector: z.string().min(1).describe("Selector over the scene. Space-separated clauses (AND): name mask \u0027B4_*\u0027, \u0027name:PAT\u0027, \u0027type:board|wall|floor|window|door|drawer|facade|assembled_facade|radial_shelf|panel|table|pillar|light\u0027, \u0027module:NAME\u0027, \u0027thickness==18\u0027 (also width/height/depth with == != \u003E= \u003C= \u003E \u003C), \u0027all_boards\u0027, \u0027all_modules\u0027, \u0027*\u0027."),
      thickness: z.number().int().optional().describe("New thickness (dimZ) in MM for every matched board (e.g. change all 18 to 16)."),
      width: z.number().int().optional().describe("New width (dimX) in MM."),
      height: z.number().int().optional().describe("New height (dimY) in MM."),
      depth: z.number().int().optional().describe("New depth (dimZ) in MM."),
      material: z.string().optional().describe("Material decor id/name to apply to all matched (see list_materials)."),
      locked: z.boolean().optional().describe("Lock (true) / unlock (false) all matched."),
    },
  },
  {
    name: "move",
    title: "Move a selection by a delta",
    description: "Shift every matched element by (dx,dy,dz) MM in world axes. Locked elements are skipped. ONE undo step. Returns matched/updated/sceneViolationCount.",
    kind: "write",
    inputSchema: {
      selector: z.string().min(1).describe("Selector (see set_attr)."),
      dx: z.number().finite().optional().describe("Shift along world X in MM."),
      dy: z.number().finite().optional().describe("Shift along world Y in MM."),
      dz: z.number().finite().optional().describe("Shift along world Z in MM."),
    },
  },
  {
    name: "resize_module",
    title: "Resize a module by a delta",
    description: "Grow/shrink a module (named group) by delta MM along a world axis. The server keeps the near side fixed, moves the far side and stretches spanning boards \u2014 you pass (module, axis, delta), not 15 coordinates. ONE undo step. Returns matched/updated/sceneViolationCount.",
    kind: "write",
    inputSchema: {
      module: z.string().min(1).describe("Module = exact group/module name (see get_modules)."),
      axis: z.enum(["x", "y", "z"]).describe("World axis to resize along."),
      delta_mm: z.number().finite().describe("Delta in MM: positive grows toward \u002Baxis, negative shrinks. The near side stays fixed; the server moves the far side and stretches spanning boards."),
    },
  },
  {
    name: "create_walls",
    title: "Create walls from corner coordinates",
    description: "Create/update a batch of bearing or partition walls from endpoint coordinates in MM. Geometry is derived server-side; thickness and optional material come from project instructions. Atomic, idempotent by segment name, ONE undo step.",
    kind: "write",
    inputSchema: {
      origin_x_mm: z.number().int().optional().describe("World X of declaration origin in MM."),
      origin_z_mm: z.number().int().optional().describe("World Z of declaration origin in MM."),
      base_y_mm: z.number().int().optional().describe("Floor/base Y in MM. Walls extend upward from it."),
      segments: z.array(z.object({ name: z.string().min(1).describe("Stable wall id/name."), from_x: z.number().int().describe("Start X in MM from origin."), from_z: z.number().int().describe("Start Z in MM from origin."), to_x: z.number().int().describe("End X in MM from origin."), to_z: z.number().int().describe("End Z in MM from origin."), kind: z.enum(["bearing", "partition"]).describe("Wall kind; thickness/material come from project instructions."), height: z.number().int().min(1).describe("Wall height in MM.") })).min(1).describe("Wall segments. Existing walls with the same names are updated (idempotent)."),
    },
  },
  {
    name: "create_floor",
    title: "Create a polygon floor",
    description: "Create/update a FloorElement from a simple polygon in MM. The top surface is corner-anchored; center/size/mesh are derived server-side. Atomic and idempotent by name, ONE undo step.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Stable floor id/name."),
      origin_x_mm: z.number().int().optional().describe("World X of declaration origin in MM."),
      origin_z_mm: z.number().int().optional().describe("World Z of declaration origin in MM."),
      top_y_mm: z.number().int().optional().describe("Top surface Y in MM."),
      thickness_mm: z.number().int().min(1).optional().describe("Floor thickness in MM. If omitted, floor_thickness_mm is required in project instructions."),
      poly: z.array(z.object({ x: z.number().int().describe("X in MM from the declaration origin."), z: z.number().int().describe("Z in MM from the declaration origin.") })).min(3).describe("Simple polygon vertices in MM from origin (clockwise or counter-clockwise)."),
    },
  },
  {
    name: "add_opening",
    title: "Add a wall opening",
    description: "Create/update a door or window by wall name, offset from its declared start, width/height/sill in MM. The server positions and attaches it and rebuilds the wall cutout. Atomic, idempotent by opening name, ONE undo step.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Stable opening id/name."),
      wall: z.string().min(1).describe("Exact wall name."),
      kind: z.enum(["window", "door"]).describe("Opening kind."),
      offset_mm: z.number().int().min(0).describe("Distance from wall start endpoint to opening left edge in MM."),
      width: z.number().int().min(1).describe("Opening width in MM."),
      height: z.number().int().min(1).describe("Opening height in MM."),
      sill_mm: z.number().int().min(0).optional().describe("Height from wall base to opening bottom in MM. Door usually uses 0."),
    },
  },
  {
    name: "snap_diagnose",
    title: "Diagnose snapping (batch)",
    description: "Explain why each given board does or does not snap to neighbours from its current (or a test) position: best face pair, gap vs threshold, overlap. x/y/z in METERS (optional, default = current position).",
    kind: "read",
    inputSchema: {
      ops: z.array(z.object({ name: z.string().min(1).describe("Exact board name."), x: z.number().finite().optional().describe("Test X in METERS (default: current)."), y: z.number().finite().optional().describe("Test Y in METERS (default: current)."), z: z.number().finite().optional().describe("Test Z in METERS (default: current).") })).min(1).describe("Boards (and optional test positions) to diagnose. At least 1."),
    },
  },
  {
    name: "get_free_space",
    title: "Free space between two boards",
    description: "The empty box between two boards: size (mm), bounds (m), center, and any elements already inside it. Use BEFORE creating or resizing something to fit between panels \u2014 no manual AABB math.",
    kind: "read",
    inputSchema: {
      between: z.array(z.string().min(1)).min(2).describe("Exactly 2 board names \u2014 returns the free box between them."),
    },
  },
  {
    name: "create_elements",
    title: "Create elements (batch)",
    description: "Create one or MANY elements in ONE call. Each item: unique name, type (board | wall | floor | facade | assembled_facade | radial_shelf | panel | drawer | table | radius_table | pillar | window | door, default board), x/y/z in METERS, width/height/depth in MILLIMETERS, plus type-specific fields (gaps, fill, drawer params, ...). panel = \u0414\u0412\u041F/\u0425\u0414\u0424 back panel whose gaps count toward its bounding box, so it seats into grooves. Atomic: if ANY item is invalid, NOTHING is created. Whole batch is ONE undo step.",
    kind: "write",
    inputSchema: {
      items: z.array(z.object({ name: z.string().min(1).describe("Unique name for the new element (case-insensitive across the whole project). Must match ^[A-Za-z0-9_-]\u002B$ \u2014 latin letters, digits, \u0027-\u0027 and \u0027_\u0027 only; no spaces, no cyrillic."), type: z.enum(["board", "wall", "floor", "facade", "assembled_facade", "radial_shelf", "panel", "drawer", "table", "radius_table", "pillar", "window", "door"]).optional().describe("Element type. Default board. wall = board acting as a structural anchor; floor ignores size/position."), x: z.number().finite().optional().describe("Position X in METERS."), y: z.number().finite().optional().describe("Position Y in METERS."), z: z.number().finite().optional().describe("Position Z in METERS."), width: z.number().int().min(1).optional().describe("Size along X in MM. Defaults: board 800, assembled facade 450, radial shelf 600, table 2000, window 900, door 900."), height: z.number().int().min(1).optional().describe("Size along Y in MM. Defaults: board 400, assembled facade 700, table 750, window 1200, door 2000."), depth: z.number().int().min(1).optional().describe("Thickness along Z in MM. Defaults: board 18, radial shelf 400 (its depth), table 1000, window/door 100.") })).min(1).describe("Elements to create. At least 1. Whole batch is ONE undo step."),
    },
  },
  {
    name: "edit_elements",
    title: "Edit element properties (batch)",
    description: "THE universal editor: change ANY user-editable properties of one or MANY elements in ONE transactional call. Each op: exact name \u002B any of x/y/z (METERS), width/height/depth (MM), rot_x/rot_y/rot_z (DEGREES), locked, material, facade gap_left/right/top/bottom (MM), opening mode, assembled-facade fill, is_open, corner_radius, drawer params (drawer_type/drawer_length/drawer_color/internal_width/is_double/is_upper/paired_drawer_name/attached_facade_name), table leg_inset_mm/tabletop_material/legs_material, pillar mid_height_mm, window tint/sill_protrusion_mm, door sash_type. Type-specific fields are validated against the element\u0027s actual type. Atomic: if ANY op is invalid, NOTHING is applied. Geometry changes are ONE undo step. dry_run:true simulates (applies, reports per-op state \u002B violations, reverts) \u2014 use it BEFORE risky moves/resizes.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ name: z.string().min(1).describe("Exact element name."), new_name: z.string().optional().describe("Rename the element to this new name. Must be unique among all elements (case-insensitive) and match ^[A-Za-z0-9_-]\u002B$ \u2014 latin letters, digits, \u0027-\u0027 and \u0027_\u0027 only; no spaces, no cyrillic. Omit to keep."), x: z.number().finite().optional().describe("Target X in METERS. Omit to keep."), y: z.number().finite().optional().describe("Target Y in METERS. Omit to keep."), z: z.number().finite().optional().describe("Target Z in METERS. Omit to keep."), width: z.number().int().min(1).optional().describe("New width (X) in MM. Omit to keep. Rejected for drawers (their size is parametric)."), height: z.number().int().min(1).optional().describe("New height (Y) in MM. Omit to keep. Rejected for drawers."), depth: z.number().int().min(1).optional().describe("New depth/thickness (Z) in MM. Omit to keep. Rejected for drawers."), rot_x: z.number().finite().optional().describe("Rotation around X in DEGREES. Omit to keep."), rot_y: z.number().finite().optional().describe("Rotation around Y in DEGREES. Omit to keep."), rot_z: z.number().finite().optional().describe("Rotation around Z in DEGREES. Omit to keep."), locked: z.boolean().optional().describe("Lock (true) / unlock (false). Unlock ONLY with the user\u0027s explicit permission. Omit to keep."), material: z.string().optional().describe("Material decor id or display name (see list_materials). Omit to keep."), gap_left: z.number().int().min(0).optional().describe("Facade only: left gap in MM. Omit to keep."), gap_right: z.number().int().min(0).optional().describe("Facade only: right gap in MM. Omit to keep."), gap_top: z.number().int().min(0).optional().describe("Facade only: top gap in MM. Omit to keep."), gap_bottom: z.number().int().min(0).optional().describe("Facade only: bottom gap in MM. Omit to keep."), mode: z.enum(["front_left", "front_right", "front_top", "front_bottom", "back_left", "back_right", "back_top", "back_bottom", "edge_top_left", "edge_top_right", "edge_bottom_left", "edge_bottom_right", "drawer_out", "drawer_in", "drawer_right", "drawer_left", "drawer_up", "drawer_down"]).optional().describe("Facade/window/door: opening mode. Facades accept all 18 modes; windows and doors accept front_* only. Omit to keep."), fill: z.enum(["blind", "glass", "open"]).optional().describe("Assembled facade only: center fill \u2014 blind (panel), glass (vitrine), open (empty). Omit to keep."), is_open: z.boolean().optional().describe("Facade/window/door: true = open, false = close. Omit to keep. For drawers use cycle_drawer_animation."), corner_radius: z.number().int().min(1).optional().describe("Radial shelf only: corner rounding radius in MM (clamped to 1..min(width, depth)). Omit to keep."), grooves: z.string().optional().describe("Plain board only: REPLACES the whole set of grooves. Comma-separated \u0022kind:side\u0022 pairs, kind = through|blind, side = top|bottom|left|right (side names the edge the groove runs along, in the part\u0027s own frame). Example: \u0022through:top, blind:left\u0022. Empty string removes all grooves. Groove size is fixed at 16*4*7 mm (offset*width*depth) by the CNC and cannot be changed. Duplicates of the same kind\u002Bside are rejected. Omit to keep."), drawer_system: z.enum(["gtv", "movento"]).optional().describe("Drawer only: runner system \u2014 gtv (bought metal box, one spec line) or movento (wooden box exploded into separate spec parts). Omit to keep."), drawer_type: z.enum(["A", "B", "C", "D"]).optional().describe("Drawer only: side height type \u2014 A=86, B=120, C=168, D=200 mm. Omit to keep."), drawer_length: z.number().int().optional().describe("Drawer only: nominal slide length in MM, one of 250/300/350/400/450/500/550/600. Omit to keep."), drawer_color: z.enum(["anthracite", "white", "black"]).optional().describe("Drawer only: GTV color. Omit to keep."), internal_width: z.number().int().min(100).optional().describe("Drawer only: internal box width in MM (min 100). Omit to keep."), is_double: z.boolean().optional().describe("Drawer only: mark as part of a DOUBLE drawer (two stacked boxes). Omit to keep."), is_upper: z.boolean().optional().describe("Double drawer only: this box is the UPPER one. Omit to keep."), paired_drawer_name: z.string().optional().describe("Double drawer only: exact name of the paired drawer element (link both ways). Empty string detaches. Omit to keep."), attached_facade_name: z.string().optional().describe("Drawer only: exact name of the facade acting as this drawer\u0027s front. Empty string detaches. Omit to keep."), leg_inset_mm: z.number().int().min(0).optional().describe("Table only: inward offset of legs from corners along X and Z, in MM. Omit to keep."), tabletop_material: z.string().optional().describe("Table only: material id or display name for the tabletop (see list_materials). Omit to keep."), legs_material: z.string().optional().describe("Table only: material id or display name for the legs (see list_materials). Omit to keep."), mid_height_mm: z.number().int().min(50).max(100).optional().describe("Pillar only: middle cylinder height in MM (clamped 50..100). Omit to keep."), tint: z.enum(["clear", "tinted"]).optional().describe("Window only: glass tint \u2014 clear (transparent) or tinted (slightly darkened). Omit to keep."), sill_protrusion_mm: z.number().int().min(0).max(200).optional().describe("Window only: windowsill outward protrusion in MM (0..200). Omit to keep."), sash_type: z.enum(["glass", "blind"]).optional().describe("Door only: sash type \u2014 glass (transparent) or blind (solid panel). Omit to keep.") })).min(1).describe("Operations to apply \u2014 one per element. Each op: exact name \u002B ANY editable properties (geometry, lock, material, facade gaps/mode/fill, drawer params, table/pillar/window/door params)."),
      dry_run: z.boolean().optional().describe("true = DRY-RUN: apply everything, report per-op state and violations, then revert. Use this INSTEAD of a separate simulate call. Default false."),
    },
  },
  {
    name: "convert_elements",
    title: "Convert element types (batch)",
    description: "Change the TYPE of one or MANY existing elements in place \u2014 board(part) \u003C-\u003E facade \u003C-\u003E assembled facade \u003C-\u003E radial shelf \u2014 keeping name, size, position and material. Atomic: if ANY op is invalid, NOTHING is converted. NOT undoable.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ name: z.string().min(1).describe("Exact element name to convert."), target: z.enum(["part", "facade", "assembled_facade", "radial_shelf"]).describe("Target type: part (plain board), facade (door/front), assembled_facade (framed facade), radial_shelf (corner shelf)."), fill: z.enum(["blind", "glass", "open"]).optional().describe("When target=assembled_facade: center fill \u2014 blind (panel), glass, open (empty). Default keeps/blind.") })).min(1).describe("Conversions to apply. At least 1."),
    },
  },
  {
    name: "clone_elements",
    title: "Clone elements (batch)",
    description: "For EACH op create COUNT copies of a board; copy N is shifted by N*offset (METERS) from the original. Copies are named \u003Cname\u003E_2, \u003Cname\u003E_3, \u2026 Whole batch is ONE undo step. Ideal for \u0027three identical shelves 300 mm apart\u0027.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ name: z.string().min(1).describe("Exact board name to clone."), count: z.number().int().min(1).max(50).optional().describe("How many copies (default 1, max 50)."), offset_x: z.number().finite().optional().describe("X shift between copies in METERS (default 0)."), offset_y: z.number().finite().optional().describe("Y shift between copies in METERS (default 0)."), offset_z: z.number().finite().optional().describe("Z shift between copies in METERS (default 0).") })).min(1).describe("Clone operations. At least 1. Whole batch is ONE undo step."),
    },
  },
  {
    name: "align_elements",
    title: "Align face to face (batch)",
    description: "For EACH op move a board so its FACE sits flush against (or gap_mm away from) a TARGET board\u0027s face \u2014 no manual coordinate math. Ops are applied IN ORDER, so later ops see earlier moves (chain alignments!). Example op: {name:\u0027Shelf1\u0027, face:\u0027left\u0027, target:\u0027Side_L\u0027, target_face:\u0027right\u0027}. Whole batch is ONE undo step.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ name: z.string().min(1).describe("Board to MOVE."), face: z.enum(["left", "right", "bottom", "top", "back", "front"]).describe("Which face of THIS board to align: left/right = X axis, bottom/top = Y axis, back/front = Z axis."), target: z.string().min(1).describe("Board to align AGAINST (it does not move)."), target_face: z.enum(["left", "right", "bottom", "top", "back", "front"]).describe("Which face of the TARGET to align to. Must be on the same axis as \u0027face\u0027."), gap_mm: z.number().finite().min(0).optional().describe("Gap between the two faces in MM (default 0 = flush contact).") })).min(1).describe("Align operations, applied IN ORDER (later ops see earlier moves). At least 1. Whole batch is ONE undo step."),
    },
  },
  {
    name: "distribute_evenly",
    title: "Distribute evenly",
    description: "Space 3\u002B boards evenly along a world axis: the two outermost stay, the middle ones move so center-to-center distances are equal. ONE undo step. Ideal for \u0027three shelves evenly between top and bottom\u0027.",
    kind: "write",
    inputSchema: {
      names: z.array(z.string().min(1)).min(3).describe("At least 3 board names. The two outermost (along the axis) stay; the middle ones move so center-to-center spacing is equal."),
      axis: z.enum(["x", "y", "z"]).describe("World axis to distribute along."),
    },
  },
  {
    name: "delete_elements",
    title: "Delete elements (batch)",
    description: "Delete one or MANY boards in ONE call. Atomic: if ANY element is missing or locked, NOTHING is deleted. Whole batch is ONE undo step.",
    kind: "destructive",
    inputSchema: {
      names: z.array(z.string().min(1)).min(1).describe("Exact board names (from get_all_elements). At least 1."),
    },
  },
  {
    name: "select_elements",
    title: "Select elements (batch)",
    description: "Select and highlight one or MANY boards in the app (visual only, no geometry change).",
    kind: "write",
    inputSchema: {
      names: z.array(z.string().min(1)).min(1).describe("Exact board names (from get_all_elements). At least 1."),
    },
  },
  {
    name: "cycle_drawer_animation",
    title: "Open / close drawers (batch)",
    description: "Animate GTV drawers: a single drawer toggles open/closed; a double drawer cycles Closed -\u003E BothOpen -\u003E LowerOnly -\u003E Closed (its paired drawer and attached facades move in sync). Returns isOpen and doubleState per drawer.",
    kind: "write",
    inputSchema: {
      names: z.array(z.string().min(1)).min(1).describe("Exact board names (from get_all_elements). At least 1."),
    },
  },
  {
    name: "list_materials",
    title: "List materials / textures",
    description: "List the available material decors / textures (id, display name, kind, whether it has a texture, and its physical tile size in MM). Use before edit_elements {material} to pick a valid id.",
    kind: "read",
  },
  {
    name: "reload_textures",
    title: "Reload external textures",
    description: "Re-scan the external textures folder (\u003Capp\u003E/Resources/Textures) and refresh the decor catalog WITHOUT restarting the app. Drop new image files there (named \u0027\u003Cname\u003E_\u003CwidthMM\u003E_\u003CheightMM\u003E.jpg\u0027 to set tile size), then call this. Returns how many were loaded and the folder path.",
    kind: "write",
  },
  {
    name: "resize_floor",
    title: "Resize floor",
    description: "Set the floor plate size in MILLIMETERS (the floor is a scene singleton). Undoable.",
    kind: "write",
    inputSchema: {
      width: z.number().int().min(1).describe("Floor width (X) in MM."),
      height: z.number().int().min(1).describe("Floor length (Y) in MM."),
      depth: z.number().int().min(1).describe("Floor thickness (Z) in MM."),
    },
  },
  {
    name: "get_modules",
    title: "List modules",
    description: "List all modules (named groups) with their member boards and bounding box.",
    kind: "read",
  },
  {
    name: "module_info",
    title: "Module info (batch)",
    description: "Full configuration of one or MANY modules: members, bounds, edit state.",
    kind: "read",
    inputSchema: {
      modules: z.array(z.string().min(1)).min(1).describe("Module ids (numbers) or names. At least 1."),
    },
  },
  {
    name: "create_module",
    title: "Create module",
    description: "Group two or more boards into a named module (they then move together).",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Module name, e.g. \u0027\u0422\u0443\u043C\u0431\u0430 \u0441 \u044F\u0449\u0438\u043A\u0430\u043C\u0438\u0027."),
      members: z.array(z.string().min(1)).min(2).describe("Board names (at least 2)."),
    },
  },
  {
    name: "dissolve_module",
    title: "Dissolve modules (batch)",
    description: "Ungroup one or MANY modules. The boards stay in the scene.",
    kind: "destructive",
    inputSchema: {
      modules: z.array(z.string().min(1)).min(1).describe("Module ids (numbers) or names. At least 1."),
    },
  },
  {
    name: "add_to_module",
    title: "Add to module (batch)",
    description: "Add one or MANY boards to an existing module.",
    kind: "write",
    inputSchema: {
      module: z.string().min(1).describe("Module id or name."),
      names: z.array(z.string().min(1)).min(1).describe("Board names to add. At least 1."),
    },
  },
  {
    name: "remove_from_module",
    title: "Remove from module (batch)",
    description: "Remove one or MANY boards from their modules.",
    kind: "write",
    inputSchema: {
      names: z.array(z.string().min(1)).min(1).describe("Exact board names (from get_all_elements). At least 1."),
    },
  },
  {
    name: "enter_module_edit",
    title: "Enter module edit",
    description: "Enter module edit mode (a single module at a time): only that module\u0027s boards are editable, the rest of the scene is locked/dimmed.",
    kind: "write",
    inputSchema: {
      module: z.string().min(1).describe("Module id (number) or name."),
    },
  },
  {
    name: "exit_module_edit",
    title: "Exit module edit",
    description: "Leave module edit mode.",
    kind: "write",
  },
  {
    name: "set_setting",
    title: "Change a setting",
    description: "Toggle one boolean project setting. name is one of: lower_near_walls | snap_enabled | grid_enabled | walls_enabled | camera_pan_free.",
    kind: "write",
    inputSchema: {
      name: z.enum(["lower_near_walls", "snap_enabled", "grid_enabled", "walls_enabled"]).describe("Setting key."),
      value: z.boolean().describe("New on/off value."),
    },
  },
  {
    name: "set_snap_verbose",
    title: "Verbose snap log",
    description: "Turn detailed snap logging in the Unity console on or off (debugging).",
    kind: "write",
    inputSchema: {
      enabled: z.boolean().describe("Turn the feature on (true) or off (false)."),
    },
  },
  {
    name: "get_console_logs",
    title: "Console logs",
    description: "Recent Unity console log entries (for debugging).",
    kind: "read",
    inputSchema: {
      count: z.number().int().min(1).max(200).optional().describe("How many entries (max 200, default 50)."),
    },
  },
  {
    name: "export_specification_csv",
    title: "Export CSV",
    description: "Export the specification (cut list) to a CSV file on disk.",
    kind: "write",
    inputSchema: {
      path: z.string().min(1).describe("Full file path to write the CSV to."),
    },
  },
  {
    name: "take_screenshot",
    title: "Screenshot",
    description: "Capture a screenshot of the app; returns the saved PNG file path.",
    kind: "read",
  },
  {
    name: "find_objects",
    title: "Find objects (advanced)",
    description: "ADVANCED. Find GameObjects by partial name. For kitchen boards prefer get_all_elements.",
    kind: "read",
    inputSchema: {
      name_filter: z.string().min(1).describe("Full or partial object name."),
    },
  },
  {
    name: "get_object_info",
    title: "Object info (advanced, batch)",
    description: "ADVANCED. Raw GameObject info (transform, components, children) for EACH given path. For boards prefer get_elements.",
    kind: "read",
    inputSchema: {
      object_paths: z.array(z.string().min(1)).min(1).describe("Object names or hierarchy paths (Parent/Child). At least 1."),
    },
  },
  {
    name: "get_scene_hierarchy",
    title: "Scene hierarchy (advanced)",
    description: "ADVANCED. Full GameObject hierarchy of the scene.",
    kind: "read",
  },
  {
    name: "set_object_active",
    title: "Show/hide objects (advanced, batch)",
    description: "ADVANCED. Enable or disable raw GameObjects.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ object_path: z.string().min(1).describe("Object name or path."), active: z.boolean().describe("Enable (true) or disable (false).") })).min(1).describe("Objects to enable/disable. At least 1."),
    },
  },
  {
    name: "delete_object",
    title: "Delete objects (advanced, batch)",
    description: "ADVANCED. Destroy raw GameObjects with NO undo. For boards prefer delete_elements (undoable).",
    kind: "destructive",
    inputSchema: {
      object_paths: z.array(z.string().min(1)).min(1).describe("Object names or hierarchy paths (Parent/Child). At least 1."),
    },
  },
  {
    name: "set_position",
    title: "Set positions (advanced, batch)",
    description: "ADVANCED. Set raw GameObject world positions in METERS, with NO undo/validation/snap. x/y/z are OPTIONAL \u2014 omit an axis to keep its current value. For boards prefer edit_elements.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ object_path: z.string().min(1).describe("Object name or path."), x: z.number().finite().optional().describe("Value for X. Omit to keep current."), y: z.number().finite().optional().describe("Value for Y. Omit to keep current."), z: z.number().finite().optional().describe("Value for Z. Omit to keep current.") })).min(1).describe("Objects to transform. At least 1."),
    },
  },
  {
    name: "set_rotation",
    title: "Set rotations (advanced, batch)",
    description: "ADVANCED. Set raw GameObject rotations (Euler DEGREES), no undo. x/y/z are OPTIONAL \u2014 omit an axis to keep its current value. For boards prefer edit_elements.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ object_path: z.string().min(1).describe("Object name or path."), x: z.number().finite().optional().describe("Value for X. Omit to keep current."), y: z.number().finite().optional().describe("Value for Y. Omit to keep current."), z: z.number().finite().optional().describe("Value for Z. Omit to keep current.") })).min(1).describe("Objects to transform. At least 1."),
    },
  },
  {
    name: "set_scale",
    title: "Set scales (advanced, batch)",
    description: "ADVANCED and RISKY. Sets raw Transform scales \u2014 this does NOT change a board\u0027s mm size and can distort meshes. To change a board size use edit_elements instead.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ object_path: z.string().min(1).describe("Object name or path."), x: z.number().finite().optional().describe("Value for X. Omit to keep current."), y: z.number().finite().optional().describe("Value for Y. Omit to keep current."), z: z.number().finite().optional().describe("Value for Z. Omit to keep current.") })).min(1).describe("Objects to transform. At least 1."),
    },
  },
  {
    name: "execute_menu_item",
    title: "Run editor menu (advanced)",
    description: "ADVANCED (Editor only). Execute a Unity Editor menu command by path, e.g. \u0027Edit/Undo\u0027.",
    kind: "write",
    openWorld: true,
    inputSchema: {
      menu_path: z.string().min(1).describe("Menu path, e.g. \u0027Edit/Undo\u0027."),
    },
  },
  {
    name: "enter_play_mode",
    title: "Enter play mode (advanced)",
    description: "ADVANCED (Editor only). Enter Unity Play Mode.",
    kind: "write",
  },
  {
    name: "exit_play_mode",
    title: "Exit play mode (advanced)",
    description: "ADVANCED (Editor only). Exit Unity Play Mode.",
    kind: "write",
  },
];

/** guide topic -> cheat-sheet text (served locally by the bridge). */
export const GUIDE_DEFAULT_TOPIC = "workflow";
export const GUIDE_TEXTS: Record<string, string> = {
  workflow: "KITCHEN DESIGNER \u2014 WORKFLOW CHEAT-SHEET\nOther guide topics: guide {topic:\u0022elements\u0022 | \u0022fields\u0022 | \u0022drawers\u0022 | \u0022violations\u0022}\n\nUNITS (the #1 mistake)\n  position x/y/z  = METERS      (1.5 -\u003E 1.5 m)\n  size w/h/d      = MILLIMETERS (600 -\u003E 600 mm)\n  1 m = 1000 mm.  dimZ = board thickness (smallest side, usually 18 mm).\n\nREADING THE SCENE (prefer ONE batch call over many single calls)\n  get_elements {filter:\u0022B4_*\u0022, summary:true}  -\u003E compact list of one cabinet\n  get_elements {names:[\u0022A\u0022,\u0022B\u0022]}                -\u003E full info for exactly these\n  get_all_elements                              -\u003E everything (large!)\n  get_free_space {between:[\u0022Side_L\u0022,\u0022Side_R\u0022]} -\u003E the empty box between two panels\n\nEDITING (every mutation returns: element info \u002B ITS violations \u002B sceneViolationCount)\n  batch_edit {ops:[{name:\u0022P1\u0022, x:1.2}, {name:\u0022P2\u0022, width:600, rot_y:90}]}\n     - MANY changes in ONE transactional call, single undo step.\n     - dry_run:true = simulate first, nothing is kept.\n  align_element {name:\u0022Shelf\u0022, face:\u0022left\u0022, target:\u0022Side_L\u0022, target_face:\u0022right\u0022}\n     - face-to-face placement WITHOUT coordinate math (gap_mm optional).\n  clone_element {name:\u0022Shelf\u0022, count:2, offset_y:0.3}  -\u003E Shelf_2, Shelf_3\n  distribute_evenly {names:[...3\u002B...], axis:\u0022y\u0022}\n  Single-element tools also exist: move_element / resize_element / rotate_element.\n\nCHECKING\n  Look at \u0022violations\u0022 in EVERY mutation response: [] means this element is clean.\n  If sceneViolationCount grew after your change, call get_violations (optionally\n  with names:[...]) to see what else broke.\n  severity in overlaps: touching \u003C minor_overlap \u003C overlap \u003C deep_penetration.\n  deep_penetration means the board is INSIDE another one - that is never OK.\n\nSTEP-BY-STEP EXAMPLE: three shelves between two panels\n  1. get_free_space {between:[\u0022Side_L\u0022,\u0022Side_R\u0022]}   -\u003E inner width/position\n  2. create_element {name:\u0022Shelf1\u0022, x:.., y:.., z:.., width:.., height:.., depth:18}\n  3. align_element  {name:\u0022Shelf1\u0022, face:\u0022left\u0022, target:\u0022Side_L\u0022, target_face:\u0022right\u0022}\n  4. clone_element  {name:\u0022Shelf1\u0022, count:2, offset_y:0.3}\n  5. get_violations {names:[\u0022Shelf1\u0022,\u0022Shelf1_2\u0022,\u0022Shelf1_3\u0022]}  -\u003E expect []\n\nSAFETY\n  LOCKED elements (locked:true in element info) reject changes. Unlock with\n  set_element_lock {locked:false} ONLY if the user explicitly allowed it.\n  delete_element / batch_edit / clone_element are undoable with undo.",
  elements: "ELEMENT TYPES (field \u0022type\u0022 in responses)\n\nKitchenElement        Plain board. The default of create_element.\n                      Size = resize_element / batch_edit (width/height/depth, MM).\nWall (component)      A board that is a structural ANCHOR (create_element {is_wall:true}\n                      or add_wall_component). Other boards must connect to a wall/floor.\nBasePlate (floor)     The floor plate: create_element {is_floor:true}, resize_floor.\nFacadeElement         Door/front with gaps (gap_left/right/top/bottom, MM). It FLOATS in\n                      its opening: a facade with gap \u003E 0 is exempt from connectivity.\n                      Opening mode: set_facade_mode (18 modes, see facadeMode field).\nAssembledFacadeElement Framed (assembled) facade with real frame geometry.\n                      create_element {is_assembled:true, fill:\u0022blind|glass|open\u0022}.\nRadialShelfElement    Board with ONE rounded corner: create_element {is_radial_shelf:true,\n                      corner_radius:..}. Radius via set_radial_shelf_properties.\nDrawerElement         GTV drawer (sliding box). SIZE COMES FROM ITS PARAMETERS -\n                      resize is REJECTED; use set_drawer_properties (type A/B/C/D,\n                      drawer_length, internal_width). See guide {topic:\u0022drawers\u0022}.\nTableElement          Table (tabletop \u002B 4 legs): create_element {is_table:true}.\n                      leg_inset_mm and materials via set_table_properties.\nRadiusTableElement    Capsule-shaped table: create_element {is_radius_table:true}.\n\nCONVERSIONS: convert_element switches board \u003C-\u003E facade \u003C-\u003E assembled_facade \u003C-\u003E\nradial_shelf in place, keeping name/size/position/material.\n\nMODULES: named groups that move together (create_module, add_to_module, ...).\nAn element\u0027s module is in moduleId/moduleName of its info.",
  fields: "RESPONSE FIELD SEMANTICS (element info)\n\nname                  Unique text id. All tools address elements by exact name.\ntype                  Element class - see guide {topic:\u0022elements\u0022}.\ndimX/dimY/dimZ        LOCAL size in MM (dimZ = thickness). Does NOT change when\n                      the board is rotated.\nworldDimX/Y/Z         WORLD-axis extents in MM (from AABB). USE THESE when the\n                      board is rotated: after rot_y=90 a 600x18 board has\n                      worldDimX=18, worldDimZ=600.\nposX/posY/posZ        Center position in METERS (world).\nrotX/rotY/rotZ        Euler angles in DEGREES.\naabbMin*/aabbMax*     World bounding box in METERS.\neffectiveDim*         dim \u002B facade gaps (facades only). NOT rotation-aware -\n                      prefer worldDim* for world-space reasoning.\nlocked                true = move/resize/delete will be rejected (set_element_lock).\nhasViolations         true = this element overlaps something or is disconnected.\nfaceGaps              Per-axis nearest OPPOSITE neighbour: {axis, neighbor, gapMM,\n                      touching, isOverlap}. touching=true means flush contact\n                      (|gap| \u003C 0.5 mm) - that is GOOD, not a violation.\n                      Axes with no facing neighbour are omitted.\nmoduleId/moduleName   Group membership (0/absent = not grouped).\nmaterialId            Decor id (list_materials).\nfacadeMode            Facade opening mode (\u0022front_left\u0022, \u0022drawer_out\u0022, ...).\ndrawer / table / radiusTable   Type-specific sub-objects, absent otherwise.\n\nMUTATION RESPONSES (move/resize/rotate/create/align/...) always return:\n  { ok, element: \u003Cfull info above\u003E, violations: [\u003CTHIS element\u0027s problems\u003E],\n    sceneViolationCount: \u003Cstructural violations in the WHOLE scene\u003E }\nviolations kinds: overlap (with severity \u002B penetrationMm), disconnected,\nfacade_facing_inward, face_obstruction, opening_collision, drawer_invalid.",
  drawers: "GTV DRAWERS (DrawerElement)\n\nA drawer is a parametric sliding box. Its geometry is DERIVED from parameters -\nresize_element is rejected; use set_drawer_properties instead.\n\nPARAMETERS\n  drawer_type      Side height: A=86, B=120, C=168, D=200 mm.\n  drawer_length    Nominal slide length MM: 250/300/350/400/450/500/550/600.\n  internal_width   Internal box width in MM (min 100).\n  drawer_color     anthracite | white | black.\n\nCREATE:  create_element {name, x, y, z, is_drawer:true, drawer_type:\u0022B\u0022,\n                         drawer_length:450, drawer_internal_width:400}\n\nSYSTEMS (element.drawer.system): \u0022gtv\u0022 (default, bought metal box - one spec\nline) or \u0022movento\u0022 (wooden box - explodes into separate spec parts: sides,\nfront, back, bottom; parts are automatic, not selectable). Create a Movento\ndrawer with create_elements type:\u0022movento_drawer\u0022.\n\nFRONTS:  attach a facade with set_drawer_properties {attached_facade_name:\u0022F1\u0022} -\n         the facade then slides together with the drawer. Empty string detaches.\n\nDOUBLE DRAWERS: two stacked boxes moving as one system:\n  set_drawer_properties {is_double:true, paired_drawer_name:\u0022OtherDrawer\u0022}\n  (link BOTH drawers to each other; mark the upper one with is_upper:true).\n\nANIMATION: cycle_drawer_animation toggles a single drawer open/closed; a double\ndrawer cycles Closed -\u003E BothOpen -\u003E LowerOnly -\u003E Closed. State is in\nelement.drawer: isOpen, doubleState.\n\nVALIDATION: drawer problems (bad length, missing pair, ...) appear as\nkind:\u0022drawer_invalid\u0022 entries in the violations list of mutation responses\nand in get_violations.",
  violations: "VIOLATIONS - WHAT COUNTS AND WHAT DOES NOT\n\nSTRUCTURAL (make hasViolations true):\n  overlap        Two boards occupy the same volume. Reported with severity:\n                   touching          \u003C 0.5 mm  - NOT a violation (flush contact)\n                   minor_overlap     0.5..2 mm - tiny intrusion, usually a mistake\n                   overlap           2..10 mm  - real intersection\n                   deep_penetration  \u003E 10 mm   - board is INSIDE another; never OK\n                 penetrationMm = depth of intrusion; overlapX/Y/Zmm = extent per axis.\n  disconnected   The board is not face-to-face connected (within 0.5 mm) to the\n                 wall/floor structure. Exempt: facades with gap \u003E 0 (they float in\n                 their opening) and drawers (they live inside a cabinet).\n\nFACADE-ONLY (reported per element, do not flip hasViolations):\n  facade_facing_inward   Front face points INTO the cabinet - rotate 180 deg.\n  face_obstruction       Something sits right in front of the facade (within 100 mm).\n  opening_collision      The door/drawer trajectory hits a neighbour\n                         (collisionAtProgress: 0..1 of the opening travel).\n\nDRAWER-ONLY: drawer_invalid with a message (bad parameters/pairing).\n\nTOLERANCES: everything below 0.5 mm is float noise and is filtered out server-side.\nAll numbers arrive rounded to 0.1 mm. Boards standing flush report touching:true,\ngapMM:0 - treat that as a GOOD fit.\n\nCHECKING: every mutation response carries the changed element\u0027s violations plus\nsceneViolationCount. get_violations {names:[...]} checks specific boards;\nget_violations {} audits the whole scene.",
};
