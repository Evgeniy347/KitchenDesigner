# Unity Kitchen Designer — MCP bridge (DEPRECATED, to be removed)

> **This bridge is obsolete.** The application speaks MCP itself, over Streamable
> HTTP on `http://127.0.0.1:9337/mcp`, and needs no Node at all:
>
> ```
> claude mcp add --transport http unity-kitchen http://127.0.0.1:9337/mcp
> ```
>
> See `Assets/StreamingAssets/MCP-CONNECT.md`. This directory, together with
> `tools/McpContractGen` and `Assets/Scripts/Core/MCP/UnityTcpBridge.cs`, is
> waiting for the owner's confirmation before deletion. Until then the line
> protocol it talks to has moved to port **9338**, so an old configuration needs
> `UNITY_MCP_PORT=9338` to keep working.

MCP server that lets an AI model drive the Kitchen Designer app running in Unity.

```
 LLM ──stdio (MCP)──▶ this server ──TCP :9338 (JSON lines)──▶ Unity
                                                    McpCommandHandler.cs
```

The server is intentionally built to be usable by a **small / weak model**:

- Every tool has a short, plain-language description.
- **Units are stated in every position/size parameter** — the single most common mistake.
- Server `instructions` teach the core workflow once, up front (sent at `initialize`).
- The `guide` tool is a topic-based mini-reference: `workflow` (default), `elements`,
  `fields`, `drawers`, `violations`.
- **Every mutation returns a uniform envelope** `{ok, element, violations,
  sceneViolationCount}` — the model sees the result and its problems immediately,
  no follow-up query needed.
- **Batch-first**: `get_elements` (names/filter/summary), `batch_edit` (many ops,
  atomic, one undo step, `dry_run`), `clone_element`, `align_element`,
  `distribute_evenly`, `get_free_space` — one call instead of a series.
- All floats are rounded server-side to 0.1 mm; sub-0.5 mm overlaps are filtered out
  as float noise (flush contact reports `touching: true`, not a violation).
- Domain errors from Unity are surfaced as real MCP errors (`isError: true`), not swallowed.

## Units (memorise)

| Quantity                | Unit        | Example        |
|-------------------------|-------------|----------------|
| position `x/y/z`        | **meters**  | `1.5` = 1.5 m  |
| size `width/height/depth` | **millimeters** | `600` = 600 mm |
| rotation `x/y/z`        | degrees     | `90`           |

`1 m = 1000 mm` (Unity `AppConstants.MM_TO_UNITS = 0.001`). `dimZ` is always the board
thickness (smallest side, usually 18 mm).

## Core editing loop (batch-first)

1. **Read**: `get_elements {filter:"B4_*", summary:true}` — targeted and compact
   (`get_all_elements` returns everything and is large).
2. **Write**: `batch_edit {ops:[{name, x?, width?, rot_y?, locked?, material?}...]}`
   — many changes in one transactional call, single undo step; `dry_run:true` to
   preview. Single-element tools (`move_element` etc.) also work.
3. **Check**: the mutation response already carries the changed element's
   `violations` (`[]` = clean) and `sceneViolationCount`. If the counter grew,
   `get_violations {names?}` shows details.

Placement without coordinate math: `align_element` (face flush to face + gap),
`get_free_space` (empty box between two boards), `clone_element`,
`distribute_evenly`.

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

The tool surface is **contract-first**. The single source of truth is
`Assets/Scripts/Core/MCP/Contract/` (`McpToolRegistry` + `Params*` classes +
`McpGuideTexts`). From it:

- `npm run gen:tools` regenerates `src/tools.generated.ts` (Zod tool table +
  guide texts) via `tools/McpContractGen`.

Adding a method = three steps: (1) params class + registry entry in the Contract,
(2) a `case` + handler in `McpCommandHandler.cs`, (3) `npm run gen:tools`.
`scripts/check-parity.mjs` compares the generated tool table against the C#
dispatcher and **`npm run build` fails** (via the `prebuild` hook) if anything is
missing or extra. Unity-side tests (`McpToolRegistryParityTests`) guard the same
invariant from the C# side.
