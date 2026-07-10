// ============================================================================
//  Unity Kitchen Designer — MCP bridge server
// ----------------------------------------------------------------------------
//  Three layers:
//    LLM  ──stdio(MCP)──▶  THIS server  ──TCP :9337(JSON lines)──▶  Unity
//                                                     (McpCommandHandler.cs)
//
//  Design goal: be usable by a SMALL / WEAK model.
//    • Every tool has a short, plain-language description.
//    • Units are stated in EVERY position/size parameter (the #1 mistake).
//    • Server `instructions` teach the core workflow once, up front.
//    • Domain errors from Unity are surfaced as real MCP errors (isError).
//    • The tool set is kept in 1:1 sync with McpCommandHandler.cs.
//
//  UNITS (memorise this):
//    • Position x/y/z  → METERS  (Unity world units). 1.5 == 1.5 m.
//    • Size w/h/d      → MILLIMETERS.                 600 == 600 mm.
//    • 1 meter = 1000 mm.  (Unity: MM_TO_UNITS = 0.001)
//
//  When you add a method to McpCommandHandler.cs, add its tool here too.
// ============================================================================

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { connect } from "net";
import type { CreateElementParams } from "./types.js";

const UNITY_HOST = process.env.UNITY_MCP_HOST || "127.0.0.1";
const UNITY_PORT = parseInt(process.env.UNITY_MCP_PORT || "9337", 10);
const CALL_TIMEOUT_MS = 30000;

// ── Transport ────────────────────────────────────────────────────────────────
// Newline-delimited JSON over a loopback TCP socket to the in-app Unity bridge.

/** Connection-level failure. Safe to reconnect; read-only calls may retry. */
class ConnError extends Error {}

let unitySocket: ReturnType<typeof connect> | null = null;
let connecting: Promise<void> | null = null;
let requestId = 0;
const pending = new Map<string, { resolve: (v: unknown) => void; reject: (e: Error) => void }>();
let responseBuffer = "";

/** Read-only methods: safe to auto-retry once after a transient disconnect. */
const READ_ONLY = new Set<string>([
  "ping", "get_status", "get_scene_hierarchy", "get_all_elements", "get_specification",
  "get_undo_stack_info", "get_object_info", "get_element_info", "get_console_logs",
  "get_settings", "get_modules", "module_info", "get_floor_info", "get_violations",
  "get_element_debug", "get_element_gaps", "simulate_move", "simulate_resize",
  "snap_diagnose", "find_objects", "take_screenshot",
]);

function friendly(err: { message?: string; code?: string }): string {
  const code = err.code || "";
  if (code === "ECONNREFUSED" || code === "ECONNRESET" || code === "ENOTFOUND") {
    return `Cannot reach Unity on ${UNITY_HOST}:${UNITY_PORT}. ` +
      `Start the Kitchen Designer app (its MCP bridge auto-starts on port ${UNITY_PORT}), ` +
      `or in the Unity Editor run menu "Kitchen Designer > MCP Bridge > Start".`;
  }
  return err.message || "Unity connection error";
}

function rejectAllPending(e: Error): void {
  for (const [, p] of pending) p.reject(e);
  pending.clear();
}

function connectToUnity(): Promise<void> {
  return new Promise((resolve, reject) => {
    if (unitySocket) { try { unitySocket.destroy(); } catch { /* ignore */ } unitySocket = null; }
    responseBuffer = "";
    let settled = false;

    const sock = connect({ host: UNITY_HOST, port: UNITY_PORT });
    sock.setEncoding("utf8");

    sock.once("connect", () => {
      settled = true;
      unitySocket = sock;
      console.error(`[unity-mcp] Connected to Unity at ${UNITY_HOST}:${UNITY_PORT}`);
      resolve();
    });

    sock.on("data", (chunk: string) => {
      responseBuffer += chunk;
      for (;;) {
        const nl = responseBuffer.indexOf("\n");
        if (nl === -1) break;
        const line = responseBuffer.slice(0, nl).trim();
        responseBuffer = responseBuffer.slice(nl + 1);
        if (!line) continue;
        try {
          const msg = JSON.parse(line);
          const p = pending.get(msg.id);
          if (!p) continue;
          pending.delete(msg.id);
          if (msg.type === "error") {
            const m = msg.data && (msg.data.message ?? msg.data.Message);
            p.reject(new Error(m || "Unity returned an error"));
          } else {
            p.resolve(msg.data);
          }
        } catch (e) {
          console.error("[unity-mcp] Parse error:", (e as Error).message);
        }
      }
    });

    sock.on("error", (err: NodeJS.ErrnoException) => {
      if (unitySocket === sock) unitySocket = null;
      const e = new ConnError(friendly(err));
      rejectAllPending(e);
      if (!settled) { settled = true; reject(e); }
    });

    sock.on("close", () => {
      if (unitySocket === sock) unitySocket = null;
      const e = new ConnError(`Unity connection closed (${UNITY_HOST}:${UNITY_PORT}).`);
      rejectAllPending(e);
      if (!settled) { settled = true; reject(e); }
    });
  });
}

/** Ensure a live socket, coalescing concurrent connect attempts into one. */
function ensureConnected(): Promise<void> {
  if (unitySocket && !unitySocket.destroyed) return Promise.resolve();
  if (!connecting) connecting = connectToUnity().finally(() => { connecting = null; });
  return connecting;
}

function sendRequest<T>(method: string, params: T): Promise<unknown> {
  const id = `req-${++requestId}`;
  // Send params as a direct JSON object (no double-serialization).
  const wire = JSON.stringify({ id, method, params }) + "\n";
  return new Promise((resolve, reject) => {
    const sock = unitySocket;
    if (!sock) { reject(new ConnError("Not connected to Unity")); return; }

    const timer = setTimeout(() => {
      if (pending.delete(id)) {
        reject(new ConnError(
          `Timeout after ${CALL_TIMEOUT_MS / 1000}s waiting for "${method}". ` +
          `Is the Kitchen Designer app running and not frozen?`));
      }
    }, CALL_TIMEOUT_MS);

    pending.set(id, {
      resolve: (v) => { clearTimeout(timer); resolve(v); },
      reject: (e) => { clearTimeout(timer); reject(e); },
    });

    sock.write(wire, (err) => {
      if (err && pending.delete(id)) { clearTimeout(timer); reject(new ConnError(err.message)); }
    });
  });
}

/** Call a Unity method. Read-only calls auto-retry once after a transient drop. */
async function callUnity<T>(method: string, params: T = {} as T): Promise<unknown> {
  await ensureConnected();
  try {
    return await sendRequest(method, params);
  } catch (e) {
    if (e instanceof ConnError && READ_ONLY.has(method)) {
      if (unitySocket) { try { unitySocket.destroy(); } catch { /* ignore */ } unitySocket = null; }
      await ensureConnected();
      return await sendRequest(method, params);
    }
    throw e;
  }
}

// ── MCP result helpers ───────────────────────────────────────────────────────

function textResult(data: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }] };
}

function errorResult(message: string) {
  return { content: [{ type: "text" as const, text: `ERROR: ${message}` }], isError: true };
}

/** Run a Unity call and turn any failure (domain OR connection) into a clear MCP error. */
async function safe<T>(method: string, params: T = {} as T) {
  try {
    return textResult(await callUnity(method, params));
  } catch (e) {
    return errorResult((e as Error).message);
  }
}

// ── Tool annotation presets (hints for the client/model) ─────────────────────
const READ = { readOnlyHint: true, openWorldHint: false } as const;
const WRITE = { readOnlyHint: false, destructiveHint: false, openWorldHint: false } as const;
const DESTRUCTIVE = { readOnlyHint: false, destructiveHint: true, openWorldHint: false } as const;

// ── Server ───────────────────────────────────────────────────────────────────

const INSTRUCTIONS = `This server controls a 3D kitchen / furniture BOARD designer running in Unity.

UNITS — READ FIRST (the most common mistake):
- Position x/y/z are in METERS (Unity world units). Example: 1.5 = 1.5 m.
- Size width/height/depth are in MILLIMETERS. Example: 600 = 600 mm.
- 1 meter = 1000 mm. Never mix them.

IDENTITY:
- Every board has a unique text "name". Call get_all_elements first to learn the names.
- dimZ (depth) is always the board THICKNESS (its smallest side, usually 18 mm).

CORE EDITING LOOP (do this every time you change something):
1. get_all_elements      - read the current state (names, sizes, positions, violations).
2. simulate_move / simulate_resize - DRY-RUN; check "wouldHaveViolations" and "overlapsWith".
3. move_element / resize_element / rotate_element - apply ONE change at a time.
4. get_violations        - confirm nothing broke.

For normal editing you usually need only these tools:
  get_all_elements, get_element_info, simulate_move, move_element,
  simulate_resize, resize_element, rotate_element, get_violations, create_element.
Everything else is advanced / for debugging.

SAFETY:
- A LOCKED element rejects move/resize/delete. Unlock with set_element_lock(locked:false)
  ONLY when the user explicitly allowed it.
- Prefer the *_element tools over the raw set_position / set_scale / delete_object tools.
- delete_element is undoable with undo.

IF A CALL FAILS:
- "Element not found" -> call get_all_elements to get the exact name, then retry.
- A connection error means the Kitchen Designer app is not running - ask the user to start it.

Call the "guide" tool any time for a full cheat-sheet with examples.`;

const server = new McpServer(
  { name: "unity-kitchen", version: "2.0.0" },
  { capabilities: { tools: {} }, instructions: INSTRUCTIONS }
);

// ── guide: self-contained cheat-sheet (no Unity call) ────────────────────────
const GUIDE = `KITCHEN DESIGNER — MCP CHEAT-SHEET

UNITS
  position x/y/z  = METERS      (1.5 -> 1.5 m)
  size w/h/d      = MILLIMETERS (600 -> 600 mm)
  1 m = 1000 mm.  dimZ = board thickness (smallest side, usually 18 mm).

STEP-BY-STEP: move a board 20 cm to the right (+X)
  1. get_all_elements                          -> find the board name, read its posX
  2. simulate_move {name, x: posX+0.20, y, z}  -> check wouldHaveViolations == false
  3. move_element  {name, x: posX+0.20, y, z}  -> apply
  4. get_violations                            -> expect count == 0

STEP-BY-STEP: make a board 50 mm wider
  1. get_all_elements                          -> read dimX/dimY/dimZ (mm)
  2. simulate_resize {name, width: dimX+50, height: dimY, depth: dimZ}
  3. resize_element  {name, width: dimX+50, height: dimY, depth: dimZ}
  4. get_violations

CREATE
  create_element {name, x, y, z, width, height, depth}         -> a plain board
  create_element {name, x, y, z, is_wall: true}                -> a wall (anchor)
  create_element {name, x, y, z, is_facade: true, gap_left: 2} -> a facade with gaps
  create_element {name, is_floor: true}                        -> the floor plate (ignores size/pos)

VIOLATIONS
  A "violation" = an element that overlaps another OR is not connected to the
  wall/floor structure. Facades that float in their opening (gap > 0) are exempt.
  After every change call get_violations and expect count 0.

LOCKED ELEMENTS
  move/resize/delete on a locked element fails on purpose. Only if the user allows:
  set_element_lock {name, locked: false}  then retry, and optionally re-lock after.

TOOL GROUPS
  Read:   get_all_elements, get_element_info, get_specification, get_violations,
          get_element_gaps, get_element_debug, get_floor_info, get_settings, get_modules
  Edit:   create_element, move_element, resize_element, rotate_element, delete_element
  Check:  simulate_move, simulate_resize, snap_diagnose
  Undo:   undo, redo, get_undo_stack_info
  Groups: create_module, dissolve_module, add_to_module, remove_from_module,
          enter_module_edit, exit_module_edit, module_info
  Advanced (raw Unity objects, no undo/validation — avoid unless necessary):
          set_position, set_rotation, set_scale, delete_object, set_object_active,
          get_object_info, find_objects, get_scene_hierarchy`;

server.registerTool("guide",
  { title: "Guide / cheat-sheet", description: "Full usage cheat-sheet: units, the core workflow, and worked examples. Call this first if unsure.", annotations: READ },
  async () => textResult(GUIDE));

// ── Read: scene & elements ───────────────────────────────────────────────────

server.registerTool("ping",
  { title: "Ping", description: "Check that Unity is reachable. Returns Unity version.", annotations: READ },
  async () => safe("ping"));

server.registerTool("get_status",
  { title: "Scene status", description: "Basic scene info: scene name, object count, play mode, platform.", annotations: READ },
  async () => safe("get_status"));

server.registerTool("get_all_elements",
  { title: "List elements", description: "List ALL boards/walls/floor with name, size (mm), position (m), rotation, AABB and hasViolations. Call this FIRST before editing.", annotations: READ },
  async () => safe("get_all_elements"));

server.registerTool("get_element_info",
  { title: "Element info", description: "Full info for one element by name: size (mm), position (m), rotation, module, hasViolations, AABB.", annotations: READ,
    inputSchema: { name: z.string().min(1, "Required").describe("Exact board name (from get_all_elements).") } },
  async (a) => safe("get_element_info", a));

server.registerTool("get_specification",
  { title: "Specification", description: "Cut list: every distinct board size with count and area (m2), plus totals.", annotations: READ },
  async () => safe("get_specification"));

server.registerTool("get_violations",
  { title: "List violations", description: "List every element that currently overlaps another or is disconnected from the wall/floor structure. Call after each change; expect count 0.", annotations: READ },
  async () => safe("get_violations"));

server.registerTool("get_element_gaps",
  { title: "Element gaps", description: "Gap or overlap (in mm) to the nearest neighbour on each axis X/Y/Z. Negative gapMM = overlap.", annotations: READ,
    inputSchema: { name: z.string().min(1, "Required").describe("Exact board name.") } },
  async (a) => safe("get_element_gaps", a));

server.registerTool("get_element_debug",
  { title: "Element geometry", description: "Detailed geometry of one element: AABB, face centers/normals, vertices, dims and effective dims (mm). For precise placement checks.", annotations: READ,
    inputSchema: { name: z.string().min(1, "Required").describe("Exact board name.") } },
  async (a) => safe("get_element_debug", a));

server.registerTool("get_floor_info",
  { title: "Floor info", description: "Size (mm) and position (m) of the floor plate (BasePlate).", annotations: READ },
  async () => safe("get_floor_info"));

server.registerTool("get_settings",
  { title: "Get settings", description: "Current project settings: snap on/off, snap threshold (mm), grid, autosave, verbose snap flag.", annotations: READ },
  async () => safe("get_settings"));

// ── Check (dry-run) ──────────────────────────────────────────────────────────

server.registerTool("simulate_move",
  { title: "Simulate move (dry-run)", description: "DRY-RUN of a move: does NOT move anything. Returns simulatedAABB, overlapsWith and wouldHaveViolations. Call BEFORE move_element. x/y/z in METERS.", annotations: READ,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Exact board name."),
      x: z.number().finite().min(0, "Must be >= 0").describe("Target X in METERS."),
      y: z.number().finite().min(0, "Must be >= 0").describe("Target Y in METERS."),
      z: z.number().finite().min(0, "Must be >= 0").describe("Target Z in METERS."),
    } },
  async (a) => safe("simulate_move", a));

server.registerTool("simulate_resize",
  { title: "Simulate resize (dry-run)", description: "DRY-RUN of a resize: does NOT change size. Returns simulatedAABB, overlapsWith and wouldHaveViolations. Call BEFORE resize_element. width/height/depth in MILLIMETERS.", annotations: READ,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Exact board name."),
      width: z.number().int().positive("Must be positive").describe("Target width (X) in MM."),
      height: z.number().int().positive("Must be positive").describe("Target height (Y) in MM."),
      depth: z.number().int().positive("Must be positive").describe("Target depth/thickness (Z) in MM."),
    } },
  async (a) => safe("simulate_resize", a));

server.registerTool("snap_diagnose",
  { title: "Diagnose snapping", description: "Explain why a board does or does not snap to neighbours from its current (or a test) position: best face pair, gap vs threshold, overlap. x/y/z in METERS (optional, default = current position).", annotations: READ,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Exact board name."),
      x: z.number().finite().min(0, "Must be >= 0").optional().describe("Test X in METERS (default: current)."),
      y: z.number().finite().min(0, "Must be >= 0").optional().describe("Test Y in METERS (default: current)."),
      z: z.number().finite().min(0, "Must be >= 0").optional().describe("Test Z in METERS (default: current)."),
    } },
  async (a) => safe("snap_diagnose", a));

// ── Edit elements ────────────────────────────────────────────────────────────

server.registerTool("create_element",
  { title: "Create element", description: "Create a new board (default), or a wall / facade / floor. x/y/z in METERS; width/height/depth in MILLIMETERS (defaults 800x400x18). Set is_wall/is_facade/is_floor for other kinds. Prefer this over raw Unity object creation.", annotations: WRITE,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Name for the new element (becomes its board name)."),
      x: z.number().finite().min(0, "Must be >= 0").default(0).describe("Position X in METERS."),
      y: z.number().finite().min(0, "Must be >= 0").default(0).describe("Position Y in METERS."),
      z: z.number().finite().min(0, "Must be >= 0").default(0).describe("Position Z in METERS."),
      width: z.number().int().positive("Must be positive").optional().describe("Size along X in MM (default 800)."),
      height: z.number().int().positive("Must be positive").optional().describe("Size along Y in MM (default 400)."),
      depth: z.number().int().positive("Must be positive").optional().describe("Thickness along Z in MM (default 18)."),
      is_wall: z.boolean().optional().describe("Create as a WALL (structural anchor). Default false."),
      is_facade: z.boolean().optional().describe("Create as a FACADE (door/front with gaps). Default false."),
      is_floor: z.boolean().optional().describe("Create the FLOOR plate. Ignores size/position. Default false."),
      gap_left: z.number().int().min(0, "Must be >= 0").optional().describe("Facade only: left gap in MM (default 2)."),
      gap_right: z.number().int().min(0, "Must be >= 0").optional().describe("Facade only: right gap in MM (default 2)."),
      gap_top: z.number().int().min(0, "Must be >= 0").optional().describe("Facade only: top gap in MM (default 2)."),
      gap_bottom: z.number().int().min(0, "Must be >= 0").optional().describe("Facade only: bottom gap in MM (default 2)."),
    } },
  async (a) => {
    const params: CreateElementParams = { template_name: a.name, name: a.name, x: a.x, y: a.y, z: a.z };
    if (a.width !== undefined) params.width = a.width;
    if (a.height !== undefined) params.height = a.height;
    if (a.depth !== undefined) params.depth = a.depth;
    if (a.is_wall !== undefined) params.is_wall = a.is_wall;
    if (a.is_facade !== undefined) params.is_facade = a.is_facade;
    if (a.is_floor !== undefined) params.is_floor = a.is_floor;
    if (a.gap_left !== undefined) params.gapLeft = a.gap_left;
    if (a.gap_right !== undefined) params.gapRight = a.gap_right;
    if (a.gap_top !== undefined) params.gapTop = a.gap_top;
    if (a.gap_bottom !== undefined) params.gapBottom = a.gap_bottom;
    return safe("create_element", params);
  });

server.registerTool("move_element",
  { title: "Move element", description: "Move a board to an absolute position. Undoable, validated, snaps to neighbours. x/y/z in METERS. Fails if the element is locked. Run simulate_move first.", annotations: WRITE,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Exact board name."),
      x: z.number().finite().min(0, "Must be >= 0").describe("Target X in METERS."),
      y: z.number().finite().min(0, "Must be >= 0").describe("Target Y in METERS."),
      z: z.number().finite().min(0, "Must be >= 0").describe("Target Z in METERS."),
    } },
  async (a) => safe("move_element", a));

server.registerTool("resize_element",
  { title: "Resize element", description: "Set a board's size in MILLIMETERS. Undoable and validated. depth is the thickness (Z). Fails if locked. Run simulate_resize first.", annotations: WRITE,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Exact board name."),
      width: z.number().int().positive("Must be positive").describe("New width (X) in MM."),
      height: z.number().int().positive("Must be positive").describe("New height (Y) in MM."),
      depth: z.number().int().positive("Must be positive").describe("New depth/thickness (Z) in MM."),
    } },
  async (a) => safe("resize_element", a));

server.registerTool("rotate_element",
  { title: "Rotate element", description: "Set a board's rotation as Euler angles in DEGREES. Undoable. Common: rotate (0,90,0) to swap width and thickness. Fails if locked.", annotations: WRITE,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Exact board name."),
      x: z.number().finite("Must be finite").describe("Rotation around X in DEGREES."),
      y: z.number().finite("Must be finite").describe("Rotation around Y in DEGREES."),
      z: z.number().finite("Must be finite").describe("Rotation around Z in DEGREES."),
    } },
  async (a) => safe("rotate_element", a));

server.registerTool("delete_element",
  { title: "Delete element", description: "Delete a board. Undoable with undo. Fails if the element is locked.", annotations: DESTRUCTIVE,
    inputSchema: { name: z.string().min(1, "Required").describe("Exact board name.") } },
  async (a) => safe("delete_element", a));

server.registerTool("select_element",
  { title: "Select element", description: "Select and highlight a board in the app (visual only, no geometry change).", annotations: WRITE,
    inputSchema: { name: z.string().min(1, "Required").describe("Exact board name.") } },
  async (a) => safe("select_element", a));

server.registerTool("set_element_lock",
  { title: "Lock / unlock element", description: "Lock or unlock a board. Locked boards cannot be moved/resized/deleted. Set locked:false ONLY with the user's explicit permission.", annotations: WRITE,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Exact board name."),
      locked: z.boolean().describe("true = lock (protect), false = unlock (allow editing)."),
    } },
  async (a) => safe("set_element_lock", a));

server.registerTool("add_wall_component",
  { title: "Make element a wall", description: "Turn an existing board into a wall (structural anchor). No-op if it is already a wall.", annotations: WRITE,
    inputSchema: { name: z.string().min(1, "Required").describe("Exact board name.") } },
  async (a) => safe("add_wall_component", a));

server.registerTool("resize_floor",
  { title: "Resize floor", description: "Set the floor plate size in MILLIMETERS. Undoable.", annotations: WRITE,
    inputSchema: {
      width: z.number().int().positive("Must be positive").describe("Floor width (X) in MM."),
      height: z.number().int().positive("Must be positive").describe("Floor length (Y) in MM."),
      depth: z.number().int().positive("Must be positive").describe("Floor thickness (Z) in MM."),
    } },
  async (a) => safe("resize_floor", a));

// ── Undo / redo ──────────────────────────────────────────────────────────────

server.registerTool("undo",
  { title: "Undo", description: "Undo the last edit. Returns ok:false if there is nothing to undo.", annotations: WRITE },
  async () => safe("undo"));

server.registerTool("redo",
  { title: "Redo", description: "Redo the last undone edit.", annotations: WRITE },
  async () => safe("redo"));

server.registerTool("get_undo_stack_info",
  { title: "Undo stack info", description: "Whether undo/redo are available and a description of the next undo.", annotations: READ },
  async () => safe("get_undo_stack_info"));

// ── Modules (named groups of boards) ─────────────────────────────────────────

server.registerTool("get_modules",
  { title: "List modules", description: "List all modules (named groups) with their member boards and bounding box.", annotations: READ },
  async () => safe("get_modules"));

server.registerTool("module_info",
  { title: "Module info", description: "Full configuration of one module: members, bounds, edit state.", annotations: READ,
    inputSchema: { module: z.string().min(1, "Required").describe("Module id (number) or name.") } },
  async (a) => safe("module_info", a));

server.registerTool("create_module",
  { title: "Create module", description: "Group two or more boards into a named module (they then move together).", annotations: WRITE,
    inputSchema: {
      name: z.string().min(1, "Required").describe("Module name, e.g. 'Тумба с ящиками'."),
      members: z.array(z.string().min(1, "Required")).min(2).describe("Board names (at least 2)."),
    } },
  async (a) => safe("create_module", a));

server.registerTool("dissolve_module",
  { title: "Dissolve module", description: "Ungroup a module. The boards stay in the scene.", annotations: DESTRUCTIVE,
    inputSchema: { module: z.string().min(1, "Required").describe("Module id or name.") } },
  async (a) => safe("dissolve_module", a));

server.registerTool("add_to_module",
  { title: "Add to module", description: "Add one board to an existing module.", annotations: WRITE,
    inputSchema: { module: z.string().min(1, "Required").describe("Module id or name."), name: z.string().min(1, "Required").describe("Board name to add.") } },
  async (a) => safe("add_to_module", a));

server.registerTool("remove_from_module",
  { title: "Remove from module", description: "Remove one board from its module.", annotations: WRITE,
    inputSchema: { name: z.string().min(1, "Required").describe("Board name to remove.") } },
  async (a) => safe("remove_from_module", a));

server.registerTool("enter_module_edit",
  { title: "Enter module edit", description: "Enter module edit mode: only that module's boards are editable, the rest of the scene is locked/dimmed.", annotations: WRITE,
    inputSchema: { module: z.string().min(1, "Required").describe("Module id or name.") } },
  async (a) => safe("enter_module_edit", a));

server.registerTool("exit_module_edit",
  { title: "Exit module edit", description: "Leave module edit mode.", annotations: WRITE },
  async () => safe("exit_module_edit"));

// ── Settings / diagnostics / export ──────────────────────────────────────────

server.registerTool("set_setting",
  { title: "Change a setting", description: "Toggle one boolean project setting. name is one of: lower_near_walls | snap_enabled | grid_enabled | walls_enabled.", annotations: WRITE,
    inputSchema: {
      name: z.enum(["lower_near_walls", "snap_enabled", "grid_enabled", "walls_enabled"]).describe("Setting key."),
      value: z.boolean().describe("New on/off value."),
    } },
  async (a) => safe("set_setting", a));

server.registerTool("set_snap_verbose",
  { title: "Verbose snap log", description: "Turn detailed snap logging in the Unity console on or off (debugging).", annotations: WRITE,
    inputSchema: { enabled: z.boolean() } },
  async (a) => safe("set_snap_verbose", a));

server.registerTool("get_console_logs",
  { title: "Console logs", description: "Recent Unity console log entries (for debugging).", annotations: READ,
    inputSchema: { count: z.number().int().min(1, "Must be >= 1").max(200, "Max 200").optional().describe("How many entries (max 200, default 50).") } },
  async (a) => safe("get_console_logs", a));

server.registerTool("export_specification_csv",
  { title: "Export CSV", description: "Export the specification (cut list) to a CSV file on disk.", annotations: WRITE,
    inputSchema: { path: z.string().min(1, "Required").describe("Full file path to write the CSV to.") } },
  async (a) => safe("export_specification_csv", a));

server.registerTool("take_screenshot",
  { title: "Screenshot", description: "Capture a screenshot of the app; returns the saved PNG file path.", annotations: READ },
  async () => safe("take_screenshot"));

// ── Advanced: raw Unity objects (no undo / no validation — prefer *_element) ──

server.registerTool("find_objects",
  { title: "Find objects (advanced)", description: "ADVANCED. Find GameObjects by partial name. For kitchen boards prefer get_all_elements.", annotations: READ,
    inputSchema: { name_filter: z.string().min(1, "Required").describe("Full or partial object name.") } },
  async (a) => safe("find_objects", a));

server.registerTool("get_object_info",
  { title: "Object info (advanced)", description: "ADVANCED. Raw GameObject info (transform, components, children). For boards prefer get_element_info.", annotations: READ,
    inputSchema: { object_path: z.string().min(1, "Required").describe("Object name or hierarchy path (Parent/Child).") } },
  async (a) => safe("get_object_info", a));

server.registerTool("get_scene_hierarchy",
  { title: "Scene hierarchy (advanced)", description: "ADVANCED. Full GameObject hierarchy of the scene.", annotations: READ },
  async () => safe("get_scene_hierarchy"));

server.registerTool("set_object_active",
  { title: "Show/hide object (advanced)", description: "ADVANCED. Enable or disable a raw GameObject.", annotations: WRITE,
    inputSchema: { object_path: z.string().min(1, "Required").describe("Object name or path."), active: z.boolean() } },
  async (a) => safe("set_object_active", a));

server.registerTool("delete_object",
  { title: "Delete object (advanced)", description: "ADVANCED. Destroy a raw GameObject with NO undo. For boards prefer delete_element (undoable).", annotations: DESTRUCTIVE,
    inputSchema: { object_path: z.string().min(1, "Required").describe("Object name or path.") } },
  async (a) => safe("delete_object", a));

server.registerTool("set_position",
  { title: "Set position (advanced)", description: "ADVANCED. Set a raw GameObject world position in METERS, with NO undo/validation/snap. For boards prefer move_element.", annotations: WRITE,
    inputSchema: { object_path: z.string().min(1, "Required"), x: z.number().finite().min(0, "Must be >= 0").describe("X in METERS."), y: z.number().finite().min(0, "Must be >= 0").describe("Y in METERS."), z: z.number().finite().min(0, "Must be >= 0").describe("Z in METERS.") } },
  async (a) => safe("set_position", a));

server.registerTool("set_rotation",
  { title: "Set rotation (advanced)", description: "ADVANCED. Set a raw GameObject rotation (Euler DEGREES), no undo. For boards prefer rotate_element.", annotations: WRITE,
    inputSchema: { object_path: z.string().min(1, "Required"), x: z.number().finite("Must be finite").describe("X in DEGREES."), y: z.number().finite("Must be finite").describe("Y in DEGREES."), z: z.number().finite("Must be finite").describe("Z in DEGREES.") } },
  async (a) => safe("set_rotation", a));

server.registerTool("set_scale",
  { title: "Set scale (advanced)", description: "ADVANCED and RISKY. Sets raw Transform scale — this does NOT change a board's mm size and can distort meshes. To change a board size use resize_element instead.", annotations: WRITE,
    inputSchema: { object_path: z.string().min(1, "Required"), x: z.number().positive("Must be positive"), y: z.number().positive("Must be positive"), z: z.number().positive("Must be positive") } },
  async (a) => safe("set_scale", a));

server.registerTool("execute_menu_item",
  { title: "Run editor menu (advanced)", description: "ADVANCED (Editor only). Execute a Unity Editor menu command by path, e.g. 'Edit/Undo'.", annotations: { readOnlyHint: false, openWorldHint: true },
    inputSchema: { menu_path: z.string().min(1, "Required").describe("Menu path, e.g. 'Edit/Undo'.") } },
  async (a) => safe("execute_menu_item", a));

server.registerTool("enter_play_mode",
  { title: "Enter play mode (advanced)", description: "ADVANCED (Editor only). Enter Unity Play Mode.", annotations: WRITE },
  async () => safe("enter_play_mode"));

server.registerTool("exit_play_mode",
  { title: "Exit play mode (advanced)", description: "ADVANCED (Editor only). Exit Unity Play Mode.", annotations: WRITE },
  async () => safe("exit_play_mode"));

// ── Start ────────────────────────────────────────────────────────────────────
try {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("[unity-mcp] Server ready on stdio (Unity target " + UNITY_HOST + ":" + UNITY_PORT + ")");
} catch (e) {
  console.error("[unity-mcp] Fatal startup error:", e);
  process.exit(1);
}
