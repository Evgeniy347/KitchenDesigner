// Parity guard: the generated tool table (src/tools.generated.ts) MUST match the
// dispatcher switch in McpCommandHandler.cs 1:1. Run automatically before
// `npm run build` (prebuild) and manually via `npm run check`. Fails with a clear
// list of what to add/remove so a forgotten method can never ship.
//
// The tool table is generated from the C# contract (McpToolRegistry) via
// `npm run gen:tools`. This guard is the cheap, dotnet-free cross-check that the
// committed generated file still lines up with the Unity handler switch. The C#
// side additionally guards registry<->switch parity in McpToolRegistryParityTests.

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const CS = resolve(here, "..", "..", "Assets", "Scripts", "Core", "MCP", "McpCommandHandler.cs");
const TS = resolve(here, "..", "src", "tools.generated.ts");

// Intentional, documented exceptions:
const CS_ONLY = new Set([]);        // C# switch cases deliberately not exposed to the model (none currently)
const TS_ONLY = new Set(["guide"]); // static cheat-sheet tool, no Unity method behind it

function readOrDie(path, label) {
  try { return readFileSync(path, "utf8"); }
  catch { console.error(`check-parity: cannot read ${label}: ${path}`); process.exit(2); }
}

// C# dispatcher cases look like:  case "move_element": return HandleMoveElement(request);
const csText = readOrDie(CS, "McpCommandHandler.cs");
const csMethods = new Set(
  [...csText.matchAll(/case\s+"([^"]+)"\s*:\s*return\s+Handle\w+\(/g)].map((m) => m[1])
);

// Generated tool table entries look like:  name: "move_element",
const tsText = readOrDie(TS, "tools.generated.ts");
const tsTools = new Set(
  [...tsText.matchAll(/^\s*name:\s*"([^"]+)",/gm)].map((m) => m[1])
);

const missingInTs = [...csMethods].filter((m) => !tsTools.has(m) && !CS_ONLY.has(m)).sort();
const extraInTs = [...tsTools].filter((t) => !csMethods.has(t) && !TS_ONLY.has(t)).sort();

if (missingInTs.length === 0 && extraInTs.length === 0) {
  const shared = [...csMethods].filter((m) => !CS_ONLY.has(m)).length;
  console.log(`check-parity: OK — ${shared} methods in sync (C# handler <-> index.ts).`);
  process.exit(0);
}

console.error("check-parity: FAILED — index.ts is out of sync with McpCommandHandler.cs\n");
if (missingInTs.length) {
  console.error("  Present in C# but NOT exposed to the model (add server.registerTool in src/index.ts):");
  for (const m of missingInTs) console.error(`    - ${m}`);
  console.error("");
}
if (extraInTs.length) {
  console.error("  Exposed in index.ts but NO C# handler (typo, or add a case in the switch):");
  for (const t of extraInTs) console.error(`    - ${t}`);
  console.error("");
}
console.error("  (Intentional exceptions: C#-only = {" + [...CS_ONLY].join(", ") +
  "}, TS-only = {" + [...TS_ONLY].join(", ") + "}. Edit scripts/check-parity.mjs to change.)");
process.exit(1);
