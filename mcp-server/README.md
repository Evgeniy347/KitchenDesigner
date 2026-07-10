# Unity Kitchen Designer — MCP bridge

MCP server that lets an AI model drive the Kitchen Designer app running in Unity.

```
 LLM ──stdio (MCP)──▶ this server ──TCP :9337 (JSON lines)──▶ Unity
                                                    McpCommandHandler.cs
```

The server is intentionally built to be usable by a **small / weak model**:

- Every tool has a short, plain-language description.
- **Units are stated in every position/size parameter** — the single most common mistake.
- Server `instructions` teach the core workflow once, up front (sent at `initialize`).
- The `guide` tool returns a full cheat-sheet with worked examples on demand.
- Domain errors from Unity are surfaced as real MCP errors (`isError: true`), not swallowed.

## Units (memorise)

| Quantity                | Unit        | Example        |
|-------------------------|-------------|----------------|
| position `x/y/z`        | **meters**  | `1.5` = 1.5 m  |
| size `width/height/depth` | **millimeters** | `600` = 600 mm |
| rotation `x/y/z`        | degrees     | `90`           |

`1 m = 1000 mm` (Unity `AppConstants.MM_TO_UNITS = 0.001`). `dimZ` is always the board
thickness (smallest side, usually 18 mm).

## Core editing loop

1. `get_all_elements` — read current state (names, sizes, positions, violations).
2. `simulate_move` / `simulate_resize` — dry-run; check `wouldHaveViolations` / `overlapsWith`.
3. `move_element` / `resize_element` / `rotate_element` — apply one change.
4. `get_violations` — confirm nothing broke.

## Build & run

```bash
npm install
npm run check      # verify tool list matches the C# handler (see below)
npm run build      # runs check, then tsc -> dist/index.js
npm start          # node dist/index.js
```

Registered in `../.mcp.json` as the `unity-kitchen` server. Environment:
`UNITY_MCP_HOST` (default `127.0.0.1`), `UNITY_MCP_PORT` (default `9337`).

The Unity side (the in-app TCP bridge, `UnityTcpBridge`) auto-starts with the app on
port 9337. In the Editor it can be toggled via menu **Kitchen Designer ▸ MCP Bridge ▸
Start / Stop**. If the app is not running, tool calls return a clear connection error.

## Keeping tools in sync — IMPORTANT

The tool list here must stay **1:1** with the `switch` in
`Assets/Scripts/Core/MCP/McpCommandHandler.cs`. When you add a method there, add a
matching `server.registerTool(...)` here (with units in the description) and rebuild.
Parameter field names must match the C# `Params*` classes in `McpModels.cs`
(e.g. facade gaps are sent as `gapLeft/gapRight/gapTop/gapBottom`).

This is enforced automatically: `scripts/check-parity.mjs` compares the tool names
here against the C# dispatcher and **`npm run build` fails** (via the `prebuild` hook)
if anything is missing or extra, naming exactly what to fix. So adding a method is
still two edits (C# handler + this file), but you can never *silently* forget the
second one. Intentional exceptions (`get_methods`, `guide`) are listed in that script.
Full end-to-end code generation is deliberately avoided: the hand-written
descriptions carry the units and wording a weak model needs, which a generator
cannot invent.
