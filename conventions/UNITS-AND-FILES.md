# Единицы, файловые операции, качество кода

## Units

**Inside the engine** — Unity's own representation, and the only place metres are legitimate:

| Quantity | Unit | Example |
|----------|------|---------|
| Position x/y/z | **metres** | `1.5` = 1.5 m |
| Size width/height/depth | **millimetres** | `600` = 600 mm |
| Rotation | degrees | `90` |

`1 m = 1000 mm` (Unity `AppConstants.MM_TO_UNITS = 0.001`). `dimZ` = board thickness.

**Fractional millimetres are `float` in millimetres, rounded to a tenth on the way out.** The
table above reads as if a millimetre were always whole, and most of ours are — but reference
data is not ours to round: a 3/4″ pipe is 26,8 mm across a 2,8 mm wall, and forcing those to
integers invents dimensions that no supplier sells. Store the tenth, show the tenth
(`McpJson.TenthMillimetreDecimals` is the precedent on the wire), and never turn a handbook
figure into a whole number just because the field next to it happens to be one.

**On the MCP wire** — the millimetre is the only unit, and the table above does NOT apply. A
metre reaching an agent-facing field is a defect, not a convention: the `position` row is an
INTERNAL representation, and reading it as the contract is exactly how six tools grew a metre
island while the surrounding 58 spoke millimetres. The unit belongs in the FIELD NAME (`x_mm`,
`offset_x_mm`), never in the tool's prose — `McpJson` deserialises with
`MissingMemberHandling.Error`, so renaming the field turns an old caller's metres into a loud
refusal by name instead of a silent shift of three orders of magnitude. Guessing the unit from
the magnitude (`x < 10` must be metres) is forbidden: that is a silent fallback, and a silent
fallback is an outage. The anchor convention that goes with it: **a position on the wire is the
MINIMUM world corner, never the centre** — `anchor_x_mm`/`anchor_y_mm`/`anchor_z_mm` in, `anchorMm`
out, so what a read returns is exactly what a write takes. The wording the agent itself is shown
lives in `Core/MCP/Contract/McpGuideTexts.cs` (search `MINIMUM world corner`). The metre island
is gone: every mutation op takes `anchor_*_mm`, and the one position that is deliberately NOT an
anchor is `set_position`, whose `x_mm`/`y_mm`/`z_mm` are the raw transform origin — still
millimetres, and the description says «raw» because that is what they are. The guards are
`McpUnitContractTests` — inputs, the closed `get` → `edit_elements` → `get` loop, and a
refusal-by-field-name on each of the six tools that ever took metres — and
`McpResponseUnitContractTests` for the way back out.

**The unit belongs in the name of RESPONSE fields too, and a scanner must prove it.** The input
side is the half everyone remembers; metres survived longest on the way back out, because
nothing looked there. Reflection over the response DTOs is not enough either — a good share of
this surface is anonymous objects built inline in the handlers, and reflection cannot see them,
so the scanner has to parse the object initialisers as well. `McpResponseUnitContractTests` is
the working pattern: two scans, a stated reason on every exception plus a check that the
exception still fires, and a self-test proving the scanner sees the contract at all.

## CRITICAL: File operations — NEVER delete permanently

When deleting files, ALWAYS move to trash first. Never use `rm -rf`, `Remove-Item -Force`, or `git clean` on source/config files.

- **Windows:** use `$null = (New-Object -ComObject Shell.Application).Namespace(0xA).MoveHere('path')` or move to a `_trash/` folder manually
- **Shell:** `gio trash <file>` (GNOME) or `trash-put <file>` (trash-cli)
- **Git:** `git rm` only after confirming the file is truly unwanted and backed up

**The rules corpus lives INSIDE the repository now, and that is what makes it recoverable.**
`AGENTS.md`, `LEAD-AGENT.md`, `CONVENTIONS.md`, `agents/`, `conventions/` and `FEATURES.md` sit
at the root of `kd-repose/` and are versioned like any source file: `git rm`, `git checkout` and
history all apply. They used to live one level up, outside git, with no undo — and the day that
ended, a batch edit had already turned every `|` in `FEATURES.md` into a space and the map was
saved only because an agent still remembered its contents. What is still outside and still has
no net: `tasks/`, `GTV/`, `sonar/`, and anything else in `F:\repos\KitchenDesigner2\`. Those go
to the RECYCLE BIN, never to `rm`. Inside the repo the bin is optional, because history is the net.

**A batch edit outside the repo copies the file FIRST.** No net means no `git checkout` either,
and a scripted sweep does not fail loudly — it succeeds at the wrong thing. One did: PowerShell
flattens `@(@("a","b"))` into a plain array, so a replacement written per line ran per CHARACTER
and turned every `|` in `FEATURES.md` into a space, wrecking the feature map, plus every `,` in
a script and one letter in a document. It was reconstructed only because the agent had read the
files minutes earlier. So: copy to the scratchpad before the sweep, and have the script assert
`Contains(<the exact old text>)` before each replacement — a sweep that cannot find what it is
replacing must stop, not proceed.

**Deleting a document is not one edit — it is one edit per reference, in the SAME commit.**
A finished plan is deleted, not archived (`893437de` deleted `HARDENING-PLAN.md` and moved its
numbers into the rules), but every pointer at it dies with it. Both halves of this have already
been paid for: that deletion left a dangling `docs/HARDENING-PLAN.md` in `UNITY-GATEWAY.md` for
weeks, and deleting `mcp-v2/PLAN.md` would have orphaned the anchor convention this very file
pointed at. That plan HAS since been deleted, and correctly: the convention had already been
moved up into this file (§«Units», the minimum-corner anchor), so nothing was left pointing at it. So before deleting: `grep -rn '<filename>' --include=*.md --include=*.cs .`, and
decide for each hit whether the pointer goes away or the KNOWLEDGE moves here. A reference to a
file that no longer exists is worse than no reference: it reads as "the answer is written down
somewhere" and sends the next agent looking for it.

例外: test results (`test-results/`), build artifacts (`Build/`, `Builds/`, `Library/`, `Temp/`) — these can be deleted directly.

## Code quality

- One method one job (~40 lines max)
- Named constants, no magic numbers
- Read file first, copy existing style
- Validate inputs, clear error messages
- No dead code, no copy-paste
- **No comments in production source** — see "Comments live in tests" below
- **One class one responsibility** — see "Class responsibility (SRP)" below

### Tolerance constants — MANDATORY

All geometric thresholds MUST come from `Tolerance.cs` (`KitchenDesigner.Core.Tolerance`):

| Constant | Value | Use for |
|----------|-------|---------|
| `ContactMm` | 0.5 mm | contact / gap detection |
| `EpsilonUnits` | 1e-4 (0.1 mm) | float equality, coordinate comparison |
| `SnapEpsilon` | 1e-5 (0.01 mm) | inclusive snap threshold |
| `ClearanceMm` | 0.1 mm | clearance tolerance |
| `EpsilonSqr` | 1e-6 | squared magnitude guard before normalize |

A numeric literal sitting next to `>=` or `<=` in a returned expression is a THRESHOLD, even
when it reads like a plain ratio — `coverage >= 0.5f` in `PanelEngagesSeat` was one, and the
"never hard-code 0.001f / 1e-4f / 0.5f" wording did not catch it. Multiplying by `0.5f` is not a
threshold; comparing against it is. Give it a name on the owning class (`MinSeatCoverageRatio`)
so a test can reference the same value.

**Never hard-code `0.001f`, `1e-4f`, `0.5f` etc. for geometric comparisons.** If `Tolerance` has no suitable constant, add one there first with a doc comment explaining the physical meaning. Algorithm-specific constants (e.g. normalized-coordinate thresholds in mesh builders) should be `public const` on the owning class so tests can reference the same value.

