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
import { GEN_TOOLS, GUIDE_TEXTS, GUIDE_DEFAULT_TOPIC } from "./tools.generated.js";
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
- Every board has a unique text "name". Use get_elements {filter/names} or
  get_all_elements to learn the names.
- dimZ (depth) is the board's LOCAL thickness; worldDimX/Y/Z are the world-axis
  sizes (use those when a board is rotated).

HOW TO EDIT (batch-first):
1. READ:  get_elements {filter:"B4_*", summary:true} — targeted and compact.
2. WRITE: batch_edit {ops:[...], dry_run:true} to preview, then without dry_run.
   One op = name + any of x/y/z (m), width/height/depth (mm), rot_* (deg),
   locked, material. The whole batch is atomic and is ONE undo step.
   Single tools (move_element / resize_element / rotate_element) also work.
3. CHECK: every mutation response already contains "violations" for the changed
   element ([] = clean) and sceneViolationCount for the whole scene. If the
   counter grew, get_violations {names?} shows details.

PLACEMENT WITHOUT MATH:
- align_element — press a face flush against (or gap_mm away from) another board's face.
- get_free_space — the empty box between two boards (size, bounds, blockers).
- clone_element — N copies with a step offset; distribute_evenly — equal spacing.

SAFETY:
- locked:true in element info means move/resize/delete are rejected. Unlock with
  set_element_lock {locked:false} ONLY when the user explicitly allowed it.
- Prefer the *_element tools over raw set_position / set_scale / delete_object.
- delete_element, batch_edit and clone_element are undoable with undo.

IF A CALL FAILS:
- "Element not found" -> get_elements {filter:...} to find the exact name, retry.
- A connection error means the Kitchen Designer app is not running - ask the user to start it.

Call guide {topic:"workflow"|"elements"|"fields"|"drawers"|"violations"} any time.`;
const server = new McpServer({ name: "unity-kitchen", version: "2.0.0" }, { capabilities: { tools: {} }, instructions: INSTRUCTIONS });
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
        if (tool.staticText) {
            const topic = typeof args.topic === "string" ? args.topic : GUIDE_DEFAULT_TOPIC;
            const text = GUIDE_TEXTS[topic] ?? GUIDE_TEXTS[GUIDE_DEFAULT_TOPIC];
            // Сырой текст, не JSON.stringify — шпаргалка должна читаться как есть.
            return { content: [{ type: "text", text }] };
        }
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
