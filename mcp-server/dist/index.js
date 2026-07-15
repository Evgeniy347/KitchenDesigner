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
import { connect } from "net";
import { GEN_TOOLS } from "./tools.generated.js";
const UNITY_HOST = process.env.UNITY_MCP_HOST || "127.0.0.1";
const UNITY_PORT = parseInt(process.env.UNITY_MCP_PORT || "9337", 10);
const CALL_TIMEOUT_MS = 30000;
// ── Transport ────────────────────────────────────────────────────────────────
// Newline-delimited JSON over a loopback TCP socket to the in-app Unity bridge.
/** Connection-level failure. Safe to reconnect; read-only calls may retry. */
class ConnError extends Error {
}
let unitySocket = null;
let connecting = null;
let requestId = 0;
const pending = new Map();
const mcpCache = new Map();
let responseBuffer = "";
/** Read-only methods: safe to auto-retry once after a transient disconnect.
 *  Derived from the generated contract so it never drifts from the tool table. */
const READ_ONLY = new Set(GEN_TOOLS.filter((t) => t.kind === "read" && !t.staticText).map((t) => t.name));
function friendly(err) {
    const code = err.code || "";
    if (code === "ECONNREFUSED" || code === "ECONNRESET" || code === "ENOTFOUND") {
        return `Cannot reach Unity on ${UNITY_HOST}:${UNITY_PORT}. ` +
            `Start the Kitchen Designer app (its MCP bridge auto-starts on port ${UNITY_PORT}), ` +
            `or in the Unity Editor run menu "Kitchen Designer > MCP Bridge > Start".`;
    }
    return err.message || "Unity connection error";
}
function rejectAllPending(e) {
    for (const [, p] of pending)
        p.reject(e);
    pending.clear();
}
function connectToUnity() {
    return new Promise((resolve, reject) => {
        if (unitySocket) {
            try {
                unitySocket.destroy();
            }
            catch { /* ignore */ }
            unitySocket = null;
        }
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
        sock.on("data", (chunk) => {
            responseBuffer += chunk;
            for (;;) {
                const nl = responseBuffer.indexOf("\n");
                if (nl === -1)
                    break;
                const line = responseBuffer.slice(0, nl).trim();
                responseBuffer = responseBuffer.slice(nl + 1);
                if (!line)
                    continue;
                // Defer JSON parsing to keep the event loop responsive
                setImmediate(() => processLine(line));
            }
        });
        /** Parse one JSON line and resolve/reject the matching pending request. */
        function processLine(line) {
            let id = null;
            try {
                // Extract id from raw line BEFORE parse, so a parse error still rejects
                const idMatch = line.match(/"id"\s*:\s*"([^"]+)"/);
                if (idMatch)
                    id = idMatch[1];
                const msg = JSON.parse(line);
                const p = pending.get(msg.id);
                if (!p)
                    return;
                pending.delete(msg.id);
                if (msg.type === "error") {
                    const d = msg.data;
                    const m = typeof d?.message === "string" ? d.message : typeof d?.Message === "string" ? d.Message : null;
                    p.reject(new Error(m || "Unity returned an error"));
                }
                else {
                    p.resolve(msg);
                }
            }
            catch (e) {
                console.error("[unity-mcp] Parse error:", e.message);
                // Reject the pending promise so it doesn't hang for CALL_TIMEOUT_MS
                if (id) {
                    const p = pending.get(id);
                    if (p) {
                        pending.delete(id);
                        p.reject(new Error(`Parse error: ${e.message}`));
                    }
                }
            }
        }
        sock.on("error", (err) => {
            if (unitySocket === sock)
                unitySocket = null;
            const e = new ConnError(friendly(err));
            rejectAllPending(e);
            if (!settled) {
                settled = true;
                reject(e);
            }
        });
        sock.on("close", () => {
            if (unitySocket === sock)
                unitySocket = null;
            const e = new ConnError(`Unity connection closed (${UNITY_HOST}:${UNITY_PORT}).`);
            rejectAllPending(e);
            if (!settled) {
                settled = true;
                reject(e);
            }
        });
    });
}
/** Ensure a live socket, coalescing concurrent connect attempts into one. */
function ensureConnected() {
    if (unitySocket && !unitySocket.destroyed)
        return Promise.resolve();
    if (!connecting)
        connecting = connectToUnity().finally(() => { connecting = null; });
    return connecting;
}
function sendRequest(method, params, headers) {
    const id = `req-${++requestId}`;
    const body = { id, method, params };
    if (headers)
        body.headers = headers;
    const wire = JSON.stringify(body) + "\n";
    return new Promise((resolve, reject) => {
        const sock = unitySocket;
        if (!sock) {
            reject(new ConnError("Not connected to Unity"));
            return;
        }
        const timer = setTimeout(() => {
            if (pending.delete(id)) {
                reject(new ConnError(`Timeout after ${CALL_TIMEOUT_MS / 1000}s waiting for "${method}". ` +
                    `Is the Kitchen Designer app running and not frozen?`));
            }
        }, CALL_TIMEOUT_MS);
        pending.set(id, {
            resolve: (v) => { clearTimeout(timer); resolve(v); },
            reject: (e) => { clearTimeout(timer); reject(e); },
        });
        sock.write(wire, (err) => {
            if (err && pending.delete(id)) {
                clearTimeout(timer);
                reject(new ConnError(err.message));
            }
        });
    });
}
/** Call a Unity method with cache-aware ETag support. */
async function callCached(method, params = {}) {
    const cached = mcpCache.get(method);
    const headers = cached ? { "If-None-Match": cached.etag } : undefined;
    await ensureConnected();
    try {
        const msg = await sendRequest(method, params, headers);
        if (msg.type === "not_modified" && cached) {
            return cached.data;
        }
        if (msg.etag) {
            mcpCache.set(method, { data: msg.data, etag: msg.etag });
        }
        return msg.data;
    }
    catch (e) {
        if (e instanceof ConnError && READ_ONLY.has(method)) {
            if (unitySocket) {
                try {
                    unitySocket.destroy();
                }
                catch { /* ignore */ }
                unitySocket = null;
            }
            await ensureConnected();
            const msg = await sendRequest(method, params, headers);
            if (msg.type === "not_modified" && cached)
                return cached.data;
            if (msg.etag)
                mcpCache.set(method, { data: msg.data, etag: msg.etag });
            return msg.data;
        }
        throw e;
    }
}
/** Call a Unity method. Read-only calls auto-retry once after a transient drop. */
async function callUnity(method, params = {}) {
    await ensureConnected();
    try {
        const msg = await sendRequest(method, params);
        return msg.data;
    }
    catch (e) {
        if (e instanceof ConnError && READ_ONLY.has(method)) {
            if (unitySocket) {
                try {
                    unitySocket.destroy();
                }
                catch { /* ignore */ }
                unitySocket = null;
            }
            await ensureConnected();
            const msg = await sendRequest(method, params);
            return msg.data;
        }
        throw e;
    }
}
// ── MCP result helpers ───────────────────────────────────────────────────────
function textResult(data) {
    return { content: [{ type: "text", text: JSON.stringify(data, null, 2) }] };
}
function errorResult(message) {
    return { content: [{ type: "text", text: `ERROR: ${message}` }], isError: true };
}
/** Run a Unity call and turn any failure (domain OR connection) into a clear MCP error. */
async function safe(method, params = {}) {
    try {
        return textResult(await callUnity(method, params));
    }
    catch (e) {
        return errorResult(e.message);
    }
}
/** Like safe() but with response caching via ETag. For read-only methods returning large data. */
async function safeCached(method, params = {}) {
    try {
        return textResult(await callCached(method, params));
    }
    catch (e) {
        return errorResult(e.message);
    }
}
// ── Tool annotation presets (hints for the client/model) ─────────────────────
const READ = { readOnlyHint: true, openWorldHint: false };
const WRITE = { readOnlyHint: false, destructiveHint: false, openWorldHint: false };
const DESTRUCTIVE = { readOnlyHint: false, destructiveHint: true, openWorldHint: false };
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
const server = new McpServer({ name: "unity-kitchen", version: "2.0.0" }, { capabilities: { tools: {} }, instructions: INSTRUCTIONS });
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
  create_element {name, x, y, z, is_drawer: true, drawer_type: "B", drawer_length: 450}
                                                               -> a GTV drawer (sliding box)

DRAWERS (GTV)
  A drawer's size comes from its parameters, NOT resize_element:
  set_drawer_properties {name, drawer_type, drawer_length, internal_width, ...}
  cycle_drawer_animation {name}  -> open/close (single) or cycle states (double)
  Attach a facade front with set_drawer_properties {name, attached_facade_name} —
  it then slides together with the drawer.

VIOLATIONS
  A "violation" = an element that overlaps another OR is not connected to the
  wall/floor structure. Facades that float in their opening (gap > 0) are exempt.
  After every change call get_violations and expect count 0.

LOCKED ELEMENTS
  move/resize/delete on a locked element fails on purpose. Only if the user allows:
  set_element_lock {name, locked: false}  then retry, and optionally re-lock after.

TOOL GROUPS
  Read:   get_all_elements, get_element_info, get_specification, get_violations,
          get_element_gaps, get_element_debug, get_floor_info, get_settings, get_modules,
          list_materials
  Edit:   create_element, move_element, resize_element, rotate_element, delete_element,
          set_material, reload_textures, set_drawer_properties, cycle_drawer_animation
  Check:  simulate_move, simulate_resize, snap_diagnose
  Undo:   undo, redo, get_undo_stack_info
  Groups: create_module, dissolve_module, add_to_module, remove_from_module,
          enter_module_edit, exit_module_edit, module_info
  Advanced (raw Unity objects, no undo/validation — avoid unless necessary):
          set_position, set_rotation, set_scale, delete_object, set_object_active,
          get_object_info, find_objects, get_scene_hierarchy`;
// ── Register tools from the generated contract table ─────────────────────────
// Every tool comes from Assets/Scripts/Core/MCP/Contract (McpToolRegistry) via
// tools.generated.ts — no hand-written tool defs here. `guide` answers locally;
// everything else forwards to Unity (with optional param renames).
const ADVANCED_OPEN_WORLD = { readOnlyHint: false, openWorldHint: true };
function applyRename(args, rename) {
    const out = {};
    for (const [k, v] of Object.entries(args))
        out[rename[k] ?? k] = v;
    return out;
}
for (const tool of GEN_TOOLS) {
    const annotations = tool.kind === "read" ? READ
        : tool.kind === "destructive" ? DESTRUCTIVE
            : tool.openWorld ? ADVANCED_OPEN_WORLD
                : WRITE;
    const config = { title: tool.title, description: tool.description, annotations };
    if (tool.inputSchema)
        config.inputSchema = tool.inputSchema;
    server.registerTool(tool.name, config, async (args = {}) => {
        if (tool.staticText)
            return textResult(GUIDE);
        const params = tool.rename ? applyRename(args, tool.rename) : args;
        return tool.cached ? safeCached(tool.name, params) : safe(tool.name, params);
    });
}
// ── Start ────────────────────────────────────────────────────────────────────
try {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error("[unity-mcp] Server ready on stdio (Unity target " + UNITY_HOST + ":" + UNITY_PORT + ")");
}
catch (e) {
    console.error("[unity-mcp] Fatal startup error:", e);
    process.exit(1);
}
