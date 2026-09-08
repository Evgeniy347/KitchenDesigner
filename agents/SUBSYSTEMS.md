# Подсистемы: снэп, валидация, скриншоты, MCP

## Snap subsystem — two implementations, one geometry

Moving and resizing use SEPARATE code: `SnapSystem` (`TrySnap`/`Collect`) and `ResizeSnap`
(`SnapDelta`). They must agree on what counts as contact — edge/corner touch, two-sided
planes, minimum overlap. **Change one, check the other**, and cover the pair with a test;
divergence shows up as «растягивается, но не перетаскивается».

Face order is a contract: index/2 = axis (0=X, 1=Y, 2=Z), even index = positive direction.
Any `GetFaces()` override MUST keep it — `ResizeHandleManager` derives the resize axis from it.

## Validation — rules live in the core, not in the scene

`Core/Geometry/ValidationCore.cs` holds the rules (overlap, contacts, connectivity, opening
height) and runs WITHOUT Unity. `Validation/ConstraintValidator.cs` is only a scene adapter.
Element roles reach the core as `ElementKind` flags built in ONE place —
`Validation/ValidationSnapshot.cs`; that is the only file allowed to ask «is this a floor / a
sink / a drawer». Never add a `GetComponent` or an `is XxxElement` check to the core: Unity
compiles it, `dotnet` dies at runtime with `SecurityException` (guarded by
`GeometryArchitectureTests`).

Acceptance gate for any change here: `ValidationInvariantTests` — ten counters over
`docs/example.save.json` that must match to the unit.

This is not a core-only rule. EVERY layer decides an element's type in exactly one place and
passes the answer on as data — see CONVENTIONS.md → "Element type checks live in ONE place per
layer" for how to remove a type ladder and how to guard the layer with an architecture test.

## Screenshots (PlayMode tests, NOT MCP)

**When user asks for a screenshot — generate it via PlayMode test, NOT via `unity-kitchen` MCP `take_screenshot`.** No project key needed. Screenshots are PNG files saved to `test-results/`.

| User asks for | Run PlayMode test | Output PNG |
|---------------|-------------------|------------|
| 3D element: board, facade, drawer, table, radial, room | `IsoScreenshotTests` | `iso_*.png` |
| Context menu panel (part/facade/assembled/radial/drawer/table/radius_table) | `ElementPropertyDiagramTests` | `contextmenu_*.png` |
| Floor settings panel | `ElementPropertyDiagramTests` | `floorsettings.png` |
| Sidebar panel | `ElementPropertyDiagramTests` | `sidebar.png` |
| Specification panel | `SpecificationDiagramTests` | `specification_table.png` |
| Settings panel | `SettingsPanelLayoutDiagramTests`, `SettingsPanelTabDiagramTests` | `settings_*.png` |

All PlayMode test files: `Assets/Tests/PlayMode/`. Launch via `build.cmd -RunPlayMode` or Unity Test Runner.

Every class in that table runs in the normal PlayMode suite. The `docs/` artifacts —
`overview.png`, `gaps_overview.png`, `drawer_animation.gif` — do NOT: they are `[Explicit]`
generators, see `tools\artifacts.ps1`.

When a MODEL looks wrong in one of these frames, do not describe it to the worker and ask for
a fix — the defect is often the camera, not the mesh. See «Judging a MODEL: give the worker the
picture, not a verdict».

## MCP bridge

- Connect: «подключись к кухне» → remote MCP `unity-kitchen` at `kitchendesigner.duckdns.org:8081` — нужен `authenticate` с project key
- Connect: «подключись к кухне локально» → приложение САМО говорит по MCP Streamable HTTP,
  посредник не нужен: запусти desktop через `run-desktop.cmd`, затем
  `claude mcp add --transport http unity-kitchen http://127.0.0.1:9337/mcp`.
  **Аутентификации нет**, слушает только петлю. Эндпоинт — `McpHttpBridge` +
  `McpRpcRouter` + `McpRequestGate` в `Assets/Scripts/Core/MCP/`.
- Board naming: `{Module}_{Side}`. Frame depth = module − 18mm
- Workflow: Snapshot → Simulate → Apply → Verify

## New property checklist

When adding a new property/parameter to an element type:

0. **Check the size of what you are about to grow.** If the property adds more than ~100
   lines to a class that already exists, or lands in a class that already owns a separate
   zone for this feature, extract that zone into its own class FIRST, then add the property.
   See CONVENTIONS.md → "Class responsibility (SRP)". Appending to a class that is already
   doing several jobs is how ContextMenuUI reached 3580 lines.

1. **Element class** — `[SerializeField]` field + property with `ApplyDimensions()` trigger.
   The property MUST carry `[Undoable]` or `[NotUndoable("reason")]` — `UndoableCoverageTests`
   fails the build otherwise. `[Undoable]` is enough to get undo/redo: `ContextMenuUI.Apply`
   snapshots every marked property before/after and pushes the diff as `SetPropertiesCommand`
2. **Panel row** — a row in the element's `*FieldsEditor` (`Assets/Scripts/Core/UI/`): build it in
   `Build()`, fill it in `Show()`, write it back in `Apply()`, re-read it in `Refresh()`. Shared
   rows (name, size, position, material, gaps, edges, grooves) live in `ContextMenuUI` and its
   `ContextMenu*Section` classes instead
3. **MCP** — field in `EditOp` for editing, permission in `EditFieldRules`, the write itself in
   `ElementEditAppliers`, field in `CreateItem` if it can be set at creation, and the value in
   `ElementInfo` so the agent can read back what it wrote.
   **This step is MANDATORY, not «if the property seems worth exposing».** Whatever a person can
   change in the panel, an agent must be able to change too, and the reverse —
   `McpUiPropertyParityTests` fails the build otherwise, in BOTH directions. If the property
   genuinely must stay one-sided, that is a product decision and it is recorded as an entry with
   a reason in that test's `UiOnly` / `McpOnly`, never by widening the silence
4. **ElementData** — serialization field in `ElementData`, write in `ElementCapture.FromElement()`, restore in `SaveLoadManagerInstance.RestoreScene()`
5. **Tests** — screenshot test if element has a context menu panel + snapshot/round-trip test (see CONVENTIONS.md, Snapshot tests)

Steps 2 and 3 are machine-enforced and you will meet them as a red test rather than as a review
comment. Four guards watch this seam, and all four state the rule in their own failure text, so
a red one is an instruction, not a question to escalate:

| Guard | Asks |
|---|---|
| `McpUiPropertyParityTests` | does every property the panel edits also edit through `edit_elements`, and the reverse |
| `McpUiCreationParityTests` | is every object the sidebar creates also creatable through `create_elements` |
| `SidebarSpawnRouterTests` | does every sidebar button actually reach its own factory call, or fall through to a plain board |
| `McpSettingsParityTests` | is every setting the panel changes also settable through `set_setting` |

Neither surface is derived from the other — the panel row is hand-written in a `*FieldsEditor`,
the wire field is hand-written in the contract — so both guards take both surfaces by EXPERIMENT:
they drive one widget, or send one wire field, and diff the element's properties before and after.
Nothing in them is a list of property names, which is why a new property and a new element type
fall under the check by themselves. See CONVENTIONS.md → "Adding a new setting" for the settings
seam, and → "The reader of a red guard is an agent sent to do something else" for why those
messages are written the way they are.



**A panel row that MOVES the element applies in `ApplyAfterPosition`, not in `Apply`.** The
absolute x/y/z row runs later and silently overwrites anything `Apply` did to the transform, so a
row like «distance from the host's left edge» — which expresses a position indirectly — must run
after it and before `ResizeCommand`, or the value is accepted, the element does not move, and
nothing reports a problem. The seam was found by reading `ApplyFields`, not by a test; the row
that revealed it now has one, and the next such row will meet it as a red assertion instead of a
puzzle.

## A rule added to candidate SELECTION must reach `Diagnose` in the same commit

The snap subsystem has two implementations of one geometry, and the rule above («changed one,
check the other») has a third face that cost 78 false findings: `SnapSystem.Diagnose`. Selection
learned that a part which centres on its target offers ONLY the faces of its mounting axis, and
that the mounting face has no shift along its normal by construction. `Diagnose` learned
neither, so it went on measuring an ordinary plane gap for faces the collector never hands to
the snap at all.

That is not test plumbing. `Diagnose` is what an agent receives from the MCP tool
`snap_diagnose`, so the divergence answers «it will snap» about pairs the snap will never
consider, and «this one is closer» about a part the choice did not examine — a confident wrong
answer, the worst kind. Any oracle built on it inherits the lie, which is exactly how the sweep
produced 78 findings that were never defects.

So the rule is not «remember to update Diagnose» — that is the sort of instruction that decays.
The condition lives in ONE function both call, and a test goes red when a third place starts
knowing it its own way.

**And «two implementations» is really «two descriptions of one rule» — the oracle counts as a
consumer.** Selection knew five rules `Diagnose` had never heard of, and the cure was not to
teach it five times: one function turns a pair of faces into an offer, and both the collector
and the diagnosis are built from that. An oracle fed by a separate copy of the rule starts
lying with total confidence BEFORE the code itself breaks, and everything downstream — a sweep,
a validator, an agent reading `snap_diagnose` — inherits the lie without a single red test.

**One shared function is not enough — the oracle needs every measurement, not only the ones
selection uses.** `SnapPairOffer.For` returned as soon as a pair failed the threshold, an early
exit saved for the hot path. Selection did not care: it reads overlap only on offers it accepts.
The diagnosis did care, and got the one genuinely facing pair with «no overlap», which dropped it
to the same rank as two coplanar side faces whose plane distance is 0 — and those won the
tie-break on a «gap» that is not a gap. `snap_diagnose` then reported «0 mm apart, too little
overlap» about parts standing 100 mm from each other, while `TrySnap` was right the whole time.
Measure first, reject after — **for the ORACLE**. That was first written as one rule for one
function, and it turned out to be two: the report needs every measurement, the per-frame path
does not, and on this scene 80% of the candidate pairs (1914 of 2388) are rejected by the
threshold before anyone looks at their overlap. So the same decision gets two entrances — `For`,
which measures everything, and `ForSelection`, which skips what the threshold has already
refused — and what keeps them honest is not discipline but three tests: an equivalence check
comparing role, rejection, gap, shift and resulting position across all 7164 pairs of a whole
scene; a test that the oracle still names the overlap on a pair the threshold rejected; and a
scan forbidding anyone but the collector to use the cheaper entrance, so the report cannot go
blind again by someone «optimising» it.

The cost, measured rather than assumed: the port-seat rule added almost nothing to the face
arithmetic (0,636 → 0,659 ms over 200 parts). What it did add was **scene snapshots** — three
builds of the moved part's geometry instead of two, and `Diagnose` building the whole scene three
times, or 2N when it already knows the snap. That is where a 64% slowdown of the brute-force
sweep came from: not from the new rule, from re-walking the scene to feed it. Count the
snapshots, not the branches.
