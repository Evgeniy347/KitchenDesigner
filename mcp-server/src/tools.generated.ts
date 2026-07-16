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
    name: "get_element_info",
    title: "Element info",
    description: "Full info for one element by name: size (mm), position (m), rotation, module, hasViolations, AABB.",
    kind: "read",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
    },
  },
  {
    name: "get_elements",
    title: "Get elements (batch)",
    description: "Info for SEVERAL elements in ONE call: pick by exact names and/or a name filter (substring or wildcard \u0027*\u0027). summary:true returns compact one-line info per element. Prefer this over repeated get_element_info calls.",
    kind: "read",
    inputSchema: {
      names: z.array(z.string().min(1)).optional().describe("Exact board names to fetch. Omit to select by filter (or everything)."),
      filter: z.string().optional().describe("Name filter: substring or wildcard with \u0027*\u0027, case-insensitive (e.g. \u0027B4_upper*\u0027). Omit to skip."),
      summary: z.boolean().optional().describe("true = compact one-line info per element (name, type, position, size, locked, hasViolations). Default false = full info."),
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
    description: "List elements that overlap another (with severity and penetration depth in mm) or are disconnected from the wall/floor structure. Optional names[] limits the report to those boards. Note: every mutation already returns its own violations \u2014 call this to check the WHOLE scene.",
    kind: "read",
    inputSchema: {
      names: z.array(z.string().min(1)).optional().describe("Only report violations of these boards. Omit for the whole scene."),
    },
  },
  {
    name: "get_element_gaps",
    title: "Element gaps",
    description: "Gap or overlap (in mm) to the nearest neighbour on each axis X/Y/Z. Negative gapMM = overlap.",
    kind: "read",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
    },
  },
  {
    name: "get_element_debug",
    title: "Element geometry",
    description: "Detailed geometry of one element: AABB, face centers/normals, vertices, dims and effective dims (mm). For precise placement checks.",
    kind: "read",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
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
    name: "simulate_move",
    title: "Simulate move (dry-run)",
    description: "DRY-RUN of a move: does NOT move anything. Returns simulatedAABB, overlapsWith and wouldHaveViolations. Call BEFORE move_element. x/y/z in METERS. Each axis is OPTIONAL \u2014 omit an axis to keep the board\u0027s current value on it (a missing axis is NOT treated as 0).",
    kind: "read",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name."),
      x: z.number().finite().optional().describe("Target X in METERS. Omit to keep current X."),
      y: z.number().finite().optional().describe("Target Y in METERS. Omit to keep current Y."),
      z: z.number().finite().optional().describe("Target Z in METERS. Omit to keep current Z."),
    },
  },
  {
    name: "simulate_resize",
    title: "Simulate resize (dry-run)",
    description: "DRY-RUN of a resize: does NOT change size. Returns simulatedAABB, overlapsWith and wouldHaveViolations. Call BEFORE resize_element. width/height/depth in MILLIMETERS. Each is OPTIONAL \u2014 omit a dimension to keep the board\u0027s current size on it (a missing dimension is NOT treated as 0).",
    kind: "read",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name."),
      width: z.number().int().min(1).optional().describe("Target width (X) in MM. Omit to keep current width."),
      height: z.number().int().min(1).optional().describe("Target height (Y) in MM. Omit to keep current height."),
      depth: z.number().int().min(1).optional().describe("Target depth/thickness (Z) in MM. Omit to keep current depth."),
    },
  },
  {
    name: "snap_diagnose",
    title: "Diagnose snapping",
    description: "Explain why a board does or does not snap to neighbours from its current (or a test) position: best face pair, gap vs threshold, overlap. x/y/z in METERS (optional, default = current position).",
    kind: "read",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name."),
      x: z.number().finite().optional().describe("Test X in METERS (default: current)."),
      y: z.number().finite().optional().describe("Test Y in METERS (default: current)."),
      z: z.number().finite().optional().describe("Test Z in METERS (default: current)."),
    },
  },
  {
    name: "create_element",
    title: "Create element",
    description: "Create a new board (default), or a wall / facade / assembled facade / floor / table. x/y/z in METERS; width/height/depth in MILLIMETERS (defaults 800x400x18, assembled default 450x700x18, table default 1200x750x600). Set is_wall/is_facade/is_assembled/is_floor/is_table for other kinds. Prefer this over raw Unity object creation.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Name for the new element (becomes its board name)."),
      x: z.number().finite().optional().describe("Position X in METERS."),
      y: z.number().finite().optional().describe("Position Y in METERS."),
      z: z.number().finite().optional().describe("Position Z in METERS."),
      width: z.number().int().min(1).optional().describe("Size along X in MM (default 800)."),
      height: z.number().int().min(1).optional().describe("Size along Y in MM (default 400)."),
      depth: z.number().int().min(1).optional().describe("Thickness along Z in MM (default 18)."),
      corner_radius: z.number().int().min(1).optional().describe("Radial shelf only: corner rounding radius in MM (default 200, clamped to min(width, depth))."),
      is_wall: z.boolean().optional().describe("Create as a WALL (structural anchor). Default false."),
      is_floor: z.boolean().optional().describe("Create the FLOOR plate. Ignores size/position. Default false."),
      is_facade: z.boolean().optional().describe("Create as a FACADE (door/front with gaps). Default false."),
      is_assembled: z.boolean().optional().describe("Create as an ASSEMBLED (framed) facade \u2014 real frame geometry. Default false. Pair with fill."),
      is_radial_shelf: z.boolean().optional().describe("Create as a RADIAL shelf \u2014 a rectangular board with ONE rounded corner (default 600x400x18, corner_radius 200). Default false. Pair with corner_radius."),
      is_drawer: z.boolean().optional().describe("Create as a GTV DRAWER (sliding box). Default false. Pair with drawer_type/drawer_length/drawer_color/drawer_internal_width; width/height/depth are ignored."),
      is_table: z.boolean().optional().describe("Create as a TABLE (legs \u002B tabletop). Default false. width/height/depth are table dimensions. Pair with leg_inset_mm."),
      is_radius_table: z.boolean().optional().describe("Create as a RADIUS TABLE (capsule-shaped top \u002B 4 legs). Default false. width/height/depth are table dimensions."),
      drawer_type: z.enum(["A", "B", "C", "D"]).optional().describe("Drawer only: side height type \u2014 A=86, B=120, C=168, D=200 mm. Default A."),
      drawer_length: z.number().int().optional().describe("Drawer only: nominal slide length in MM, one of 250/300/350/400/450/500/550/600. Default 350."),
      drawer_color: z.enum(["anthracite", "white", "black"]).optional().describe("Drawer only: GTV color. Default anthracite."),
      drawer_internal_width: z.number().int().min(100).optional().describe("Drawer only: internal box width in MM (default 400, min 100)."),
      fill: z.enum(["blind", "glass", "open"]).optional().describe("Assembled facade only: center fill \u2014 blind (panel), glass (vitrine with glass), open (empty vitrine). Default blind."),
      leg_inset_mm: z.number().int().min(0).optional().describe("Table only: inward offset of legs from corners along X and Z, in MM (default 100)."),
      gap_left: z.number().int().min(0).optional().describe("Facade only: left gap in MM (default 2)."),
      gap_right: z.number().int().min(0).optional().describe("Facade only: right gap in MM (default 2)."),
      gap_top: z.number().int().min(0).optional().describe("Facade only: top gap in MM (default 2)."),
      gap_bottom: z.number().int().min(0).optional().describe("Facade only: bottom gap in MM (default 2)."),
    },
    rename: { gap_left: "gapLeft", gap_right: "gapRight", gap_top: "gapTop", gap_bottom: "gapBottom" },
  },
  {
    name: "convert_element",
    title: "Convert element type",
    description: "Change the TYPE of an existing element in place \u2014 board(part) \u003C-\u003E facade \u003C-\u003E assembled facade \u2014 keeping its name, size, position and material. Use this to turn a regular facade into an assembled (framed) one, or vice versa. NOT undoable.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact element name to convert."),
      target: z.enum(["part", "facade", "assembled_facade", "radial_shelf"]).describe("Target type: part (plain board), facade (door/front), assembled_facade (framed facade), radial_shelf (corner shelf)."),
      fill: z.enum(["blind", "glass", "open"]).optional().describe("When target=assembled_facade: center fill \u2014 blind (panel), glass, open (empty). Default keeps/blind."),
    },
  },
  {
    name: "batch_edit",
    title: "Batch edit (one undo step)",
    description: "Apply MANY changes in ONE transactional call. Each op: exact name \u002B any of x/y/z (METERS), width/height/depth (MM), rot_x/rot_y/rot_z (DEGREES), locked, material \u2014 all given fields apply together. Atomic: if ANY op is invalid, NOTHING is applied. Undo reverts the whole batch. dry_run:true simulates, reports per-op violations and reverts. PREFER this over a series of move/resize/rotate calls.",
    kind: "write",
    inputSchema: {
      ops: z.array(z.object({ name: z.string().min(1).describe("Exact board name."), x: z.number().finite().optional().describe("Target X in METERS. Omit to keep."), y: z.number().finite().optional().describe("Target Y in METERS. Omit to keep."), z: z.number().finite().optional().describe("Target Z in METERS. Omit to keep."), width: z.number().int().min(1).optional().describe("New width (X) in MM. Omit to keep."), height: z.number().int().min(1).optional().describe("New height (Y) in MM. Omit to keep."), depth: z.number().int().min(1).optional().describe("New depth/thickness (Z) in MM. Omit to keep."), rot_x: z.number().finite().optional().describe("Rotation around X in DEGREES. Omit to keep."), rot_y: z.number().finite().optional().describe("Rotation around Y in DEGREES. Omit to keep."), rot_z: z.number().finite().optional().describe("Rotation around Z in DEGREES. Omit to keep."), locked: z.boolean().optional().describe("Lock (true) / unlock (false). Omit to keep."), material: z.string().optional().describe("Material id or display name (see list_materials). Omit to keep.") })).min(1).describe("Operations to apply. Each op: exact name \u002B any of x/y/z (METERS), width/height/depth (MM), rot_x/rot_y/rot_z (DEGREES), locked, material."),
      dry_run: z.boolean().optional().describe("true = DRY-RUN: apply, report per-op violations, then revert everything. Default false."),
    },
  },
  {
    name: "clone_element",
    title: "Clone element",
    description: "Create COUNT copies of a board; copy N is shifted by N*offset (METERS) from the original. Copies are named \u003Cname\u003E_2, \u003Cname\u003E_3, \u2026 Whole clone is ONE undo step. Ideal for \u0027three identical shelves 300 mm apart\u0027.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name to clone."),
      count: z.number().int().min(1).max(50).optional().describe("How many copies (default 1, max 50)."),
      offset_x: z.number().finite().optional().describe("X shift between copies in METERS (default 0)."),
      offset_y: z.number().finite().optional().describe("Y shift between copies in METERS (default 0)."),
      offset_z: z.number().finite().optional().describe("Z shift between copies in METERS (default 0)."),
    },
  },
  {
    name: "move_element",
    title: "Move element",
    description: "Move a board to an absolute position. Undoable, validated, snaps to neighbours. x/y/z in METERS. Each axis is OPTIONAL \u2014 omit an axis to keep the board\u0027s current value on it (a missing axis is NOT treated as 0), so you can move on one axis only. Fails if the element is locked. Run simulate_move first.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name."),
      x: z.number().finite().optional().describe("Target X in METERS. Omit to keep current X."),
      y: z.number().finite().optional().describe("Target Y in METERS. Omit to keep current Y."),
      z: z.number().finite().optional().describe("Target Z in METERS. Omit to keep current Z."),
    },
  },
  {
    name: "resize_element",
    title: "Resize element",
    description: "Set a board\u0027s size in MILLIMETERS. Undoable and validated. depth is the thickness (Z). Each dimension is OPTIONAL \u2014 omit one to keep the board\u0027s current size on it (a missing dimension is NOT treated as 0), so you can change one dimension only. Fails if locked. Run simulate_resize first.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name."),
      width: z.number().int().min(1).optional().describe("New width (X) in MM. Omit to keep current width."),
      height: z.number().int().min(1).optional().describe("New height (Y) in MM. Omit to keep current height."),
      depth: z.number().int().min(1).optional().describe("New depth/thickness (Z) in MM. Omit to keep current depth."),
    },
  },
  {
    name: "rotate_element",
    title: "Rotate element",
    description: "Set a board\u0027s rotation as Euler angles in DEGREES. Undoable. Common: rotate (0,90,0) to swap width and thickness. Each axis is OPTIONAL \u2014 omit an axis to keep the board\u0027s current angle on it (a missing axis is NOT treated as 0). Fails if locked.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name."),
      x: z.number().finite().optional().describe("Rotation around X in DEGREES. Omit to keep current."),
      y: z.number().finite().optional().describe("Rotation around Y in DEGREES. Omit to keep current."),
      z: z.number().finite().optional().describe("Rotation around Z in DEGREES. Omit to keep current."),
    },
  },
  {
    name: "align_element",
    title: "Align face to face",
    description: "Move a board so its FACE sits flush against (or gap_mm away from) a TARGET board\u0027s face \u2014 no manual coordinate math. Example: {name:\u0027Shelf1\u0027, face:\u0027left\u0027, target:\u0027Side_L\u0027, target_face:\u0027right\u0027} presses the shelf against the panel. Undoable; returns the same envelope as move_element.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Board to MOVE."),
      face: z.enum(["left", "right", "bottom", "top", "back", "front"]).describe("Which face of THIS board to align: left/right = X axis, bottom/top = Y axis, back/front = Z axis."),
      target: z.string().min(1).describe("Board to align AGAINST (it does not move)."),
      target_face: z.enum(["left", "right", "bottom", "top", "back", "front"]).describe("Which face of the TARGET to align to. Must be on the same axis as \u0027face\u0027."),
      gap_mm: z.number().finite().min(0).optional().describe("Gap between the two faces in MM (default 0 = flush contact)."),
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
    name: "get_free_space",
    title: "Free space between two boards",
    description: "The empty box between two boards: size (mm), bounds (m), center, and any elements already inside it. Use BEFORE creating or resizing something to fit between panels \u2014 no manual AABB math.",
    kind: "read",
    inputSchema: {
      between: z.array(z.string().min(1)).min(2).describe("Exactly 2 board names \u2014 returns the free box between them."),
    },
  },
  {
    name: "delete_element",
    title: "Delete element",
    description: "Delete a board. Undoable with undo. Fails if the element is locked.",
    kind: "destructive",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
    },
  },
  {
    name: "select_element",
    title: "Select element",
    description: "Select and highlight a board in the app (visual only, no geometry change).",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
    },
  },
  {
    name: "set_element_lock",
    title: "Lock / unlock element",
    description: "Lock or unlock a board. Locked boards cannot be moved/resized/deleted. Set locked:false ONLY with the user\u0027s explicit permission.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name."),
      locked: z.boolean().describe("true = lock (protect), false = unlock (allow editing)."),
    },
  },
  {
    name: "set_facade_mode",
    title: "Facade open mode",
    description: "Change how a facade element opens: hinge (door swinging around one edge) or drawer (sliding along a face). 18 modes. Fails if the element is not a facade.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact facade element name."),
      mode: z.enum(["front_left", "front_right", "front_top", "front_bottom", "back_left", "back_right", "back_top", "back_bottom", "edge_top_left", "edge_top_right", "edge_bottom_left", "edge_bottom_right", "drawer_out", "drawer_in", "drawer_right", "drawer_left", "drawer_up", "drawer_down"]).describe("Opening mode: front_*/back_* (hinged on a face edge), edge_* (hinged on the thickness edge), drawer_* (sliding along an axis)."),
    },
  },
  {
    name: "set_drawer_properties",
    title: "Set drawer properties",
    description: "Change a GTV drawer\u0027s parameters: type (A/B/C/D side height), nominal length, color, internal width, double-drawer pairing and attached facade. Every field is optional \u2014 omit to keep current. Fails if the element is not a drawer. Use this instead of resize_element for drawers.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact drawer element name."),
      drawer_type: z.enum(["A", "B", "C", "D"]).optional().describe("Side height type: A=86, B=120, C=168, D=200 mm."),
      drawer_length: z.number().int().optional().describe("Nominal slide length in MM, one of 250/300/350/400/450/500/550/600. Invalid values are ignored."),
      drawer_color: z.enum(["anthracite", "white", "black"]).optional().describe("GTV color."),
      internal_width: z.number().int().min(100).optional().describe("Internal box width in MM (min 100)."),
      is_double: z.boolean().optional().describe("Mark as part of a DOUBLE drawer (two stacked boxes)."),
      is_upper: z.boolean().optional().describe("Double drawer only: this box is the UPPER one."),
      paired_drawer_name: z.string().optional().describe("Double drawer only: exact name of the paired drawer element (link both ways for sync)."),
      attached_facade_name: z.string().optional().describe("Exact name of the facade element acting as this drawer\u0027s front \u2014 it opens/closes together with the drawer. Empty string detaches."),
    },
  },
  {
    name: "set_radial_shelf_properties",
    title: "Set radial shelf properties",
    description: "Change a radial shelf\u0027s corner rounding radius in MM (clamped to 1..min(width, depth)). Board size is changed via resize_element. Fails if the element is not a radial shelf.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact radial shelf element name."),
      corner_radius: z.number().int().min(1).optional().describe("Corner rounding radius in MM (clamped to 1..min(width, depth)). Omit to keep current."),
    },
  },
  {
    name: "set_table_properties",
    title: "Set table properties",
    description: "Change a table\u0027s parameters: leg inward offset from corners along X and Z axes, in MM. Every field is optional \u2014 omit to keep current. Fails if the element is not a table.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact table element name."),
      leg_inset_mm: z.number().int().min(0).optional().describe("Inward offset of legs from corners along X and Z, in MM (min 0)."),
      tabletop_material_id: z.string().optional().describe("Material id for the tabletop (see list_materials)."),
      legs_material_id: z.string().optional().describe("Material id for the legs (see list_materials)."),
    },
  },
  {
    name: "cycle_drawer_animation",
    title: "Open / close drawer",
    description: "Animate a GTV drawer: a single drawer toggles open/closed; a double drawer cycles Closed -\u003E BothOpen -\u003E LowerOnly -\u003E Closed (its paired drawer and attached facades move in sync). Returns isOpen and doubleState.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
    },
  },
  {
    name: "list_materials",
    title: "List materials / textures",
    description: "List the available material decors / textures (id, display name, kind, whether it has a texture, and its physical tile size in MM). Use before set_material to pick a valid id.",
    kind: "read",
  },
  {
    name: "set_material",
    title: "Set material / texture",
    description: "Assign a material decor / texture to a board or facade. When a non-default decor is set, the object shows that texture instead of the flat validation tint. Undoable via re-set; call list_materials first for valid ids.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board/facade name."),
      material: z.string().min(1).describe("Material id (e.g. \u0027oak\u0027) or its display name (e.g. \u0027\u0414\u0443\u0431 \u0441\u043E\u043D\u043E\u043C\u0430\u0027). See list_materials."),
    },
  },
  {
    name: "reload_textures",
    title: "Reload external textures",
    description: "Re-scan the external textures folder (\u003Capp\u003E/Resources/Textures) and refresh the decor catalog WITHOUT restarting the app. Drop new image files there (named \u0027\u003Cname\u003E_\u003CwidthMM\u003E_\u003CheightMM\u003E.jpg\u0027 to set tile size), then call this. Returns how many were loaded and the folder path.",
    kind: "write",
  },
  {
    name: "add_wall_component",
    title: "Make element a wall",
    description: "Turn an existing board into a wall (structural anchor). No-op if it is already a wall.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
    },
  },
  {
    name: "resize_floor",
    title: "Resize floor",
    description: "Set the floor plate size in MILLIMETERS. Undoable.",
    kind: "write",
    inputSchema: {
      width: z.number().int().min(1).describe("Floor width (X) in MM."),
      height: z.number().int().min(1).describe("Floor length (Y) in MM."),
      depth: z.number().int().min(1).describe("Floor thickness (Z) in MM."),
    },
  },
  {
    name: "undo",
    title: "Undo",
    description: "Undo the last edit. Returns ok:false if there is nothing to undo.",
    kind: "write",
  },
  {
    name: "redo",
    title: "Redo",
    description: "Redo the last undone edit.",
    kind: "write",
  },
  {
    name: "get_undo_stack_info",
    title: "Undo stack info",
    description: "Whether undo/redo are available and a description of the next undo.",
    kind: "read",
  },
  {
    name: "get_modules",
    title: "List modules",
    description: "List all modules (named groups) with their member boards and bounding box.",
    kind: "read",
  },
  {
    name: "module_info",
    title: "Module info",
    description: "Full configuration of one module: members, bounds, edit state.",
    kind: "read",
    inputSchema: {
      module: z.string().min(1).describe("Module id (number) or name."),
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
    title: "Dissolve module",
    description: "Ungroup a module. The boards stay in the scene.",
    kind: "destructive",
    inputSchema: {
      module: z.string().min(1).describe("Module id (number) or name."),
    },
  },
  {
    name: "add_to_module",
    title: "Add to module",
    description: "Add one board to an existing module.",
    kind: "write",
    inputSchema: {
      module: z.string().min(1).describe("Module id or name."),
      name: z.string().min(1).describe("Board name to add."),
    },
  },
  {
    name: "remove_from_module",
    title: "Remove from module",
    description: "Remove one board from its module.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Exact board name (from get_all_elements)."),
    },
  },
  {
    name: "enter_module_edit",
    title: "Enter module edit",
    description: "Enter module edit mode: only that module\u0027s boards are editable, the rest of the scene is locked/dimmed.",
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
    description: "Toggle one boolean project setting. name is one of: lower_near_walls | snap_enabled | grid_enabled | walls_enabled.",
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
    title: "Object info (advanced)",
    description: "ADVANCED. Raw GameObject info (transform, components, children). For boards prefer get_element_info.",
    kind: "read",
    inputSchema: {
      object_path: z.string().min(1).describe("Object name or hierarchy path (Parent/Child)."),
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
    title: "Show/hide object (advanced)",
    description: "ADVANCED. Enable or disable a raw GameObject.",
    kind: "write",
    inputSchema: {
      object_path: z.string().min(1).describe("Object name or path."),
      active: z.boolean().describe("Enable (true) or disable (false)."),
    },
  },
  {
    name: "delete_object",
    title: "Delete object (advanced)",
    description: "ADVANCED. Destroy a raw GameObject with NO undo. For boards prefer delete_element (undoable).",
    kind: "destructive",
    inputSchema: {
      object_path: z.string().min(1).describe("Object name or hierarchy path (Parent/Child)."),
    },
  },
  {
    name: "set_position",
    title: "Set position (advanced)",
    description: "ADVANCED. Set a raw GameObject world position in METERS, with NO undo/validation/snap. x/y/z are OPTIONAL \u2014 omit an axis to keep its current value. For boards prefer move_element.",
    kind: "write",
    inputSchema: {
      object_path: z.string().min(1).describe("Object name or path."),
      x: z.number().finite().optional().describe("Value for X. Omit to keep current."),
      y: z.number().finite().optional().describe("Value for Y. Omit to keep current."),
      z: z.number().finite().optional().describe("Value for Z. Omit to keep current."),
    },
  },
  {
    name: "set_rotation",
    title: "Set rotation (advanced)",
    description: "ADVANCED. Set a raw GameObject rotation (Euler DEGREES), no undo. x/y/z are OPTIONAL \u2014 omit an axis to keep its current value. For boards prefer rotate_element.",
    kind: "write",
    inputSchema: {
      object_path: z.string().min(1).describe("Object name or path."),
      x: z.number().finite().optional().describe("Value for X. Omit to keep current."),
      y: z.number().finite().optional().describe("Value for Y. Omit to keep current."),
      z: z.number().finite().optional().describe("Value for Z. Omit to keep current."),
    },
  },
  {
    name: "set_scale",
    title: "Set scale (advanced)",
    description: "ADVANCED and RISKY. Sets raw Transform scale \u2014 this does NOT change a board\u0027s mm size and can distort meshes. To change a board size use resize_element instead.",
    kind: "write",
    inputSchema: {
      object_path: z.string().min(1).describe("Object name or path."),
      x: z.number().finite().optional().describe("Value for X. Omit to keep current."),
      y: z.number().finite().optional().describe("Value for Y. Omit to keep current."),
      z: z.number().finite().optional().describe("Value for Z. Omit to keep current."),
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
  drawers: "GTV DRAWERS (DrawerElement)\n\nA drawer is a parametric sliding box. Its geometry is DERIVED from parameters -\nresize_element is rejected; use set_drawer_properties instead.\n\nPARAMETERS\n  drawer_type      Side height: A=86, B=120, C=168, D=200 mm.\n  drawer_length    Nominal slide length MM: 250/300/350/400/450/500/550/600.\n  internal_width   Internal box width in MM (min 100).\n  drawer_color     anthracite | white | black.\n\nCREATE:  create_element {name, x, y, z, is_drawer:true, drawer_type:\u0022B\u0022,\n                         drawer_length:450, drawer_internal_width:400}\n\nFRONTS:  attach a facade with set_drawer_properties {attached_facade_name:\u0022F1\u0022} -\n         the facade then slides together with the drawer. Empty string detaches.\n\nDOUBLE DRAWERS: two stacked boxes moving as one system:\n  set_drawer_properties {is_double:true, paired_drawer_name:\u0022OtherDrawer\u0022}\n  (link BOTH drawers to each other; mark the upper one with is_upper:true).\n\nANIMATION: cycle_drawer_animation toggles a single drawer open/closed; a double\ndrawer cycles Closed -\u003E BothOpen -\u003E LowerOnly -\u003E Closed. State is in\nelement.drawer: isOpen, doubleState.\n\nVALIDATION: drawer problems (bad length, missing pair, ...) appear as\nkind:\u0022drawer_invalid\u0022 entries in the violations list of mutation responses\nand in get_violations.",
  violations: "VIOLATIONS - WHAT COUNTS AND WHAT DOES NOT\n\nSTRUCTURAL (make hasViolations true):\n  overlap        Two boards occupy the same volume. Reported with severity:\n                   touching          \u003C 0.5 mm  - NOT a violation (flush contact)\n                   minor_overlap     0.5..2 mm - tiny intrusion, usually a mistake\n                   overlap           2..10 mm  - real intersection\n                   deep_penetration  \u003E 10 mm   - board is INSIDE another; never OK\n                 penetrationMm = depth of intrusion; overlapX/Y/Zmm = extent per axis.\n  disconnected   The board is not face-to-face connected (within 0.5 mm) to the\n                 wall/floor structure. Exempt: facades with gap \u003E 0 (they float in\n                 their opening) and drawers (they live inside a cabinet).\n\nFACADE-ONLY (reported per element, do not flip hasViolations):\n  facade_facing_inward   Front face points INTO the cabinet - rotate 180 deg.\n  face_obstruction       Something sits right in front of the facade (within 100 mm).\n  opening_collision      The door/drawer trajectory hits a neighbour\n                         (collisionAtProgress: 0..1 of the opening travel).\n\nDRAWER-ONLY: drawer_invalid with a message (bad parameters/pairing).\n\nTOLERANCES: everything below 0.5 mm is float noise and is filtered out server-side.\nAll numbers arrive rounded to 0.1 mm. Boards standing flush report touching:true,\ngapMM:0 - treat that as a GOOD fit.\n\nCHECKING: every mutation response carries the changed element\u0027s violations plus\nsceneViolationCount. get_violations {names:[...]} checks specific boards;\nget_violations {} audits the whole scene.",
};
