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
    description: "Full usage cheat-sheet: units, the core workflow, and worked examples. Call this first if unsure.",
    kind: "read",
    staticText: true,
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
    name: "get_specification",
    title: "Specification",
    description: "Cut list: every distinct board size with count and area (m2), plus totals.",
    kind: "read",
  },
  {
    name: "get_violations",
    title: "List violations",
    description: "List every element that currently overlaps another or is disconnected from the wall/floor structure. Call after each change; expect count 0.",
    kind: "read",
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
    description: "Create a new board (default), or a wall / facade / assembled facade / floor. x/y/z in METERS; width/height/depth in MILLIMETERS (defaults 800x400x18, assembled default 450x700x18). Set is_wall/is_facade/is_assembled/is_floor for other kinds. Prefer this over raw Unity object creation.",
    kind: "write",
    inputSchema: {
      name: z.string().min(1).describe("Name for the new element (becomes its board name)."),
      x: z.number().finite().optional().describe("Position X in METERS."),
      y: z.number().finite().optional().describe("Position Y in METERS."),
      z: z.number().finite().optional().describe("Position Z in METERS."),
      width: z.number().int().min(1).optional().describe("Size along X in MM (default 800)."),
      height: z.number().int().min(1).optional().describe("Size along Y in MM (default 400)."),
      depth: z.number().int().min(1).optional().describe("Thickness along Z in MM (default 18)."),
      radius: z.number().int().min(1).optional().describe("Radial shelf only: outer radius in MM (default 300)."),
      is_wall: z.boolean().optional().describe("Create as a WALL (structural anchor). Default false."),
      is_floor: z.boolean().optional().describe("Create the FLOOR plate. Ignores size/position. Default false."),
      is_facade: z.boolean().optional().describe("Create as a FACADE (door/front with gaps). Default false."),
      is_assembled: z.boolean().optional().describe("Create as an ASSEMBLED (framed) facade \u2014 real frame geometry. Default false. Pair with fill."),
      is_radial_shelf: z.boolean().optional().describe("Create as a RADIAL (corner) shelf. Default false. Pair with radius."),
      is_drawer: z.boolean().optional().describe("Create as a GTV DRAWER (sliding box). Default false. Pair with drawer_type/drawer_length/drawer_color/drawer_internal_width; width/height/depth are ignored."),
      drawer_type: z.enum(["A", "B", "C", "D"]).optional().describe("Drawer only: side height type \u2014 A=86, B=120, C=168, D=200 mm. Default A."),
      drawer_length: z.number().int().optional().describe("Drawer only: nominal slide length in MM, one of 250/300/350/400/450/500/550/600. Default 350."),
      drawer_color: z.enum(["anthracite", "white", "black"]).optional().describe("Drawer only: GTV color. Default anthracite."),
      drawer_internal_width: z.number().int().min(100).optional().describe("Drawer only: internal box width in MM (default 400, min 100)."),
      fill: z.enum(["blind", "glass", "open"]).optional().describe("Assembled facade only: center fill \u2014 blind (panel), glass (vitrine with glass), open (empty vitrine). Default blind."),
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
