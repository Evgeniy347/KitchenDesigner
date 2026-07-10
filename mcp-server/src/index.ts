import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { connect } from "net";

const UNITY_HOST = process.env.UNITY_MCP_HOST || "127.0.0.1";
const UNITY_PORT = parseInt(process.env.UNITY_MCP_PORT || "9337", 10);

let unitySocket: ReturnType<typeof connect> | null = null;
let requestId = 0;
const pending = new Map<string, { resolve: (v: unknown) => void; reject: (e: Error) => void }>();
let responseBuffer = "";

function connectToUnity(): Promise<void> {
  return new Promise((resolve, reject) => {
    if (unitySocket) { unitySocket.destroy(); unitySocket = null; }
    const sock = connect({ host: UNITY_HOST, port: UNITY_PORT }, () => {
      unitySocket = sock;
      responseBuffer = "";
      console.error(`[unity-mcp] Connected to Unity at ${UNITY_HOST}:${UNITY_PORT}`);
      resolve();
    });
    sock.setEncoding("utf8");
    sock.on("data", (chunk: string) => {
      responseBuffer += chunk;
      while (true) {
        const nl = responseBuffer.indexOf("\n");
        if (nl === -1) break;
        const line = responseBuffer.slice(0, nl).trim();
        responseBuffer = responseBuffer.slice(nl + 1);
        if (!line) continue;
        try {
          const msg = JSON.parse(line);
          const p = pending.get(msg.id);
          if (p) {
            pending.delete(msg.id);
            p.resolve(msg.type === "error" ? { error: msg.data } : msg.data);
          }
        } catch (e) {
          console.error("[unity-mcp] Parse error:", e);
        }
      }
    });
    sock.on("error", (err) => {
      console.error("[unity-mcp] Socket error:", err.message);
      unitySocket = null;
      for (const [, p] of pending) p.reject(new Error("Unity disconnected"));
      pending.clear();
    });
    sock.on("close", () => { console.error("[unity-mcp] Unity disconnected"); unitySocket = null; });
  });
}

async function callUnity(method: string, params: Record<string, unknown> = {}): Promise<unknown> {
  if (!unitySocket) await connectToUnity();
  const id = `req-${++requestId}`;
  // Unity-сторона (McpRequest) ждёт поле `parameters` как JSON-СТРОКУ.
  const msg = JSON.stringify({ id, method, parameters: JSON.stringify(params) }) + "\n";
  return new Promise((resolve, reject) => {
    pending.set(id, { resolve, reject });
    unitySocket!.write(msg, (err) => { if (err) { pending.delete(id); reject(err); } });
    setTimeout(() => {
      if (pending.has(id)) { pending.delete(id); reject(new Error(`Timeout: ${method}`)); }
    }, 30000);
  });
}

function textResult(data: unknown) {
  return { content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }] };
}

async function safe(method: string, params: Record<string, unknown> = {}) {
  try { return textResult(await callUnity(method, params)); }
  catch (e) { return { content: [{ type: "text" as const, text: `Error: ${(e as Error).message}` }], isError: true }; }
}

// ── Server ──────────────────────────────────────────────────────────────────
const server = new McpServer(
  { name: "unity-mcp-bridge", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

// ── No-param tools ───────────────────────────────────────────────────────────
server.tool("ping", "Check Unity connectivity", async () => safe("ping"));
server.tool("get_status", "Get basic scene info", async () => safe("get_status"));
server.tool("get_scene_hierarchy", "Full GameObject hierarchy", async () => safe("get_scene_hierarchy"));
server.tool("get_all_elements", "List all KitchenElement boards", async () => safe("get_all_elements"));
server.tool("get_specification", "Specification data with dimensions and counts", async () => safe("get_specification"));
server.tool("get_undo_stack_info", "Undo/redo stack state", async () => safe("get_undo_stack_info"));
server.tool("undo", "Undo last CommandStack command", async () => safe("undo"));
server.tool("redo", "Redo last undone command", async () => safe("redo"));
server.tool("take_screenshot", "Capture screenshot from Unity", async () => safe("take_screenshot"));
server.tool("enter_play_mode", "Enter Play Mode (Editor only)", async () => safe("enter_play_mode"));
server.tool("exit_play_mode", "Exit Play Mode (Editor only)", async () => safe("exit_play_mode"));

// ── String-param tools ───────────────────────────────────────────────────────
server.tool("find_objects", "Find GameObjects by partial name", {
  name_filter: z.string().describe("Name or partial name to search for")
}, async (args) => safe("find_objects", args));

server.tool("get_object_info", "Get detailed GameObject info", {
  object_path: z.string().describe("Object name or hierarchy path")
}, async (args) => safe("get_object_info", args));

server.tool("set_object_active", "Enable/disable a GameObject", {
  object_path: z.string(), active: z.boolean()
}, async (args) => safe("set_object_active", args));

server.tool("delete_object", "Destroy a GameObject", {
  object_path: z.string().describe("Object name or path")
}, async (args) => safe("delete_object", args));

server.tool("set_position", "Set world position of a GameObject", {
  object_path: z.string(), x: z.number(), y: z.number(), z: z.number()
}, async (args) => safe("set_position", args));

server.tool("set_rotation", "Set world rotation (Euler angles)", {
  object_path: z.string(), x: z.number(), y: z.number(), z: z.number()
}, async (args) => safe("set_rotation", args));

server.tool("set_scale", "Set local scale of a GameObject", {
  object_path: z.string(), x: z.number(), y: z.number(), z: z.number()
}, async (args) => safe("set_scale", args));

server.tool("get_element_info", "Get info about a specific kitchen element", {
  name: z.string().describe("Board name")
}, async (args) => safe("get_element_info", args));

server.tool("move_element", "Move element through CommandStack", {
  name: z.string(), x: z.number(), y: z.number(), z: z.number()
}, async (args) => safe("move_element", args));

server.tool("resize_element", "Resize element dimensions in mm", {
  name: z.string(), width: z.number().int(), height: z.number().int(), depth: z.number().int()
}, async (args) => safe("resize_element", args));

server.tool("rotate_element", "Rotate element by Euler angles", {
  name: z.string(), x: z.number(), y: z.number(), z: z.number()
}, async (args) => safe("rotate_element", args));

server.tool("delete_element", "Delete element via CommandStack (undoable)", {
  name: z.string()
}, async (args) => safe("delete_element", args));

server.tool("select_element", "Select and highlight an element", {
  name: z.string()
}, async (args) => safe("select_element", args));

server.tool("get_console_logs", "Recent Unity console logs", {
  count: z.number().optional().describe("Number of entries (max 200, default 50)")
}, async (args) => safe("get_console_logs", args));

server.tool("get_settings", "Current KitchenSettings (snap/grid/autosave) and snap verbose flag", async () => safe("get_settings"));

server.tool("set_snap_verbose", "Toggle verbose snap logging in Unity console", {
  enabled: z.boolean()
}, async (args) => safe("set_snap_verbose", args));

server.tool("snap_diagnose", "Explain why a board does or does not snap: per-neighbor best face pair, gap vs threshold, overlap, intersection", {
  name: z.string().describe("Board name"),
  x: z.number().optional().describe("Test position X (default: current)"),
  y: z.number().optional().describe("Test position Y (default: current)"),
  z: z.number().optional().describe("Test position Z (default: current)")
}, async (args) => safe("snap_diagnose", args));

server.tool("execute_menu_item", "Execute Unity Editor menu command", {
  menu_path: z.string().describe("e.g. 'Edit/Undo'")
}, async (args) => safe("execute_menu_item", args));

server.tool("export_specification_csv", "Export specification to CSV", {
  path: z.string().describe("Full file path to save CSV")
}, async (args) => safe("export_specification_csv", args));

server.tool("create_element", "Create a new kitchen element", {
  template_name: z.string().describe("'Board', 'Cabinet', etc."),
  x: z.number(), y: z.number(), z: z.number(),
  width: z.number().optional(), height: z.number().optional(), depth: z.number().optional()
}, async (args) => safe("create_element", args as Record<string, unknown>));

// ── Start ────────────────────────────────────────────────────────────────────
try {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("[unity-mcp] Server ready on stdio");
} catch (e) {
  console.error("[unity-mcp] Fatal startup error:", e);
  process.exit(1);
}
