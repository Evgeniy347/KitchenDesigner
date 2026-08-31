# Hardening plan — make the rules enforce themselves

## Why this exists

The refactoring campaign moved a lot of code and, more importantly, produced a rule set. Every
rule in `CONVENTIONS.md` was paid for: each one came from something that actually broke. Nine
latent defects surfaced during the campaign, and not one of them was found by reading code —
they surfaced because "Deleting a comment is not free" forced a test to be written for every
`why` comment being removed, and the test disagreed with the comment.

That is the principle worth generalising: **a rule that lives only in a document comes back.**
`ContextMenuUI` reached 3580 lines under a rule set that said nothing about class size. The
colour rule has been in `UI-GUIDELINES.md` §10 for a long time and there are still 33 colour
literals outside `UIStyle`. The tolerance rule is MANDATORY in capitals and nothing checks it.

So the plan is not mainly "refactor more". It is: turn the rules we now have into things the
build refuses to violate, and only then keep refactoring.

## Where we are

| | |
|---|---|
| Tests | 2701 |
| Architecture tests | 4 (`GeometryArchitectureTests`, `UiElementTypeLadderTests`, `McpToolRegistryParityTests`, `UndoableCoverageTests`) + `ElementOnDestroyTests` |
| Comments left in `Assets/Scripts/Core` | **3142** |
| Colour literals outside `UIStyle` in `Core/UI` | 33 |
| Diagnostics silenced in `.editorconfig` | 29 |
| Naming rules in `.editorconfig` | none |
| Mutation testing | Stryker configured for `geometry/`, not gated |

Comments by directory — this is the remaining scope of the purge. `Elements`, `Persistence`,
`Infrastructure` and `Platform` are at zero.

```
UI 655 · Rendering 492 · Snap 318 · MCP 309 · Materials 287 · Geometry 242
Validation 193 · Measure 125 · Update 118 · Commands 108 · Analysis 104
Diagnostics 74 · Bulk 51 · Tools 48 · Interfaces 8 · Networking 8
```

**Count them with a pattern that catches trailing comments.** An anchored `^\s*(//|///|/\*)`
misses `int x = 5; // why` and undercounts — six such comments survived two separate purge
passes because of exactly this. Use:

```powershell
Select-String -Path <file> -Pattern '(?<!:)//|/\*' -AllMatches
```

The `(?<!:)` keeps `http://` inside string literals out of the count.

---

## Status — what is done, what is pending

Updated 2026-08-31. Keep this current: it is the only place that says where the work stands.

**Done from this plan:** A8 (generated-artefact parity guard) and B3 (server projects as strict
as Unity). Both offenders that A3c would catch are fixed, but the guard itself is not written.

**Not started:** everything else — A1 through A7, B1/B2/B4, all of C, most of D, all of E.

**A1 is the one that matters most and has not moved.** `Core` holds **3136** comments. The purge
covered `Elements`, `Persistence`, `Infrastructure` and `Platform` before this plan was written,
which is why they already read zero above; nothing has changed since. Remaining:
`UI` 655 · `Rendering` 492 · `Snap` 318 · `MCP` 309 · `Materials` 287 · `Geometry` 242 ·
`Validation` 193 · `Measure` 125 · `Update` 114 · `Commands` 108 · `Analysis` 104 ·
`Diagnostics` 74 · `Bulk` 51 · `Tools` 48 · `Interfaces` 8 · `Networking` 8.

**Recommended next step: C2.** It is marked "first of all" for a reason — every hour spent on
Part E without it pays the Unity tax on every iteration.

### Blocked on an idle machine

The performance work is prepared and needs a quiet window (~12 min, one command). Nothing here
is guesswork; the bench is built and the candidates are chosen:

1. Verification run (`-Filter` + full EditMode + PlayMode) — contract intact, and direct
   observation of the slashes `AssetImportWorker` receives when called with backslashes.
2. `ai.assistant`: baseline → without the package → **back to baseline as a reproducibility
   control**. Columns `Foreign` (Unity by `-projectPath`) and `Busy` (CPU) in every table —
   an empty `Foreign` column alone does not prove a quiet window, as one ruined series showed.
3. Whether killing at `Cleanup mono` leaves `Library` dirty — compare the NEXT run against a
   normal one. `-DoneGraceSeconds` is committed but set to 0, i.e. off, until this says yes.
4. Cost of Search indexing.

### Measured, load-independent facts

- Cold first import of a fresh copy: **809 s**. That is deployment cost, not run cost.
- Floor: an empty project of the same Unity version runs the same 5 tests in **8.1 s**; this
  project needs **18.1 s**. The ~10 s difference is TWO roughly equal holes — domain reload #2
  (+4.1 s) and the stretch from "project loaded" to the first test (+4.4 s).
- Removing `com.unity.ai.assistant` drops exactly two packages of 38: itself (86 files with
  `[InitializeOnLoad]`, against 17 for the next-largest) and its private `com.unity.2d.sprite`.
  It is in `manifest.json` explicitly, nothing pulls it, and the repository has no reference to
  it. Worth removing regardless of the seconds — it is a preview editor assistant.
- `com.unity.test-framework.performance` is UNTOUCHABLE: it arrives through
  URP → render-pipelines.core → collections. Its `IPrebuildSetup` runs on every test run and
  cannot be removed without removing URP.
- `com.unity.testtools.codecoverage` is USED — manually, documented in
  `GEOMETRY-EXTRACTION-PLAN.md`, and the baseline coverage numbers come from it. Not dead weight.
- Search indexing is controlled by `UserSettings/Search.settings` → `indexOnEditorStartup`, and
  it indexes all 3914 assets on every cold start for a search window that batch mode does not
  have. **`UserSettings/` is gitignored**, so editing the file fixes one machine only. If the
  measurement confirms ~2 s, the fix belongs in the gateway, which can normalise the flag before
  a cold batch — and may only do so while no GUI editor is open, which it already checks.

## Part A — Guards, so the rules stop depending on memory

Highest value in the whole plan. Each item is one architecture test, written the way
`UiElementTypeLadderTests` is: an allow-list with a REASON per entry, a test that every listed
file still exists, and a test for the scanner itself.

### A1. No comments in production source — **the missing guard for the campaign's own rule**

Nothing enforces the central rule. The moment the purge finishes, the count starts climbing
again. Because 3998 comments cannot disappear in one commit, use a **ratchet**: a checked-in
budget file with the count per directory, and a test that fails when any directory EXCEEDS its
budget. Lowering a budget is part of the commit that removes the comments; raising one is
impossible.

The ratchet is the honest form of this rule — it never blocks unrelated work, and it makes
progress irreversible. Same shape as `.verified.json` snapshots the repo already uses.

Allowed and excluded from the count: `#pragma`, `#if`/`#endif`, `// ReSharper disable`, and
generated files.

### A2. No colour literals outside `UIStyle`

`UI-GUIDELINES.md` §10 with teeth. 33 current offenders — same ratchet, or fix them in one
sweep first (they are mechanical) and start the guard at zero.

### A3. No hard-coded geometric tolerances

`CONVENTIONS.md` → "Tolerance constants — MANDATORY" has no check. Scan `Core/**` for float
literals compared against distances (`0.001f`, `1e-4f`, `0.5f`, `1e-5f`) outside `Tolerance.cs`
and the documented per-algorithm constants. Expect false positives at first; the allow-list with
reasons absorbs them.

### A3b. No normalized UVs on a surface that can carry a decor

Decors tile at a physical millimetre size; a mesh that builds `0..1` UVs stretches one copy
across the part instead. `TEXTURES.md` stated this for the decor definition but never as a
requirement on the geometry consuming it, and a stretched tabletop shipped because of it. Scan
the mesh builders for UVs derived from part size rather than from `MaterialManager.TileMM`, and
pair the guard with a per-builder test asserting the UV span for a part whose size differs from
the tile.

### A3c. No `new` over a base member

`public new` on a member the base actually uses gives the object two independent values for one
concept — see CONVENTIONS.md. Grep for `\b(public|protected|internal)\s+new\s+`, the same shape
as `ElementOnDestroyTests`. The two offenders that existed are fixed, so this guard can start at
zero.

### A4. No reflection into private members from tests

`GetField` / `GetMethod` / `BindingFlags.NonPublic` under `Assets/Tests/**`. This one can start
at zero — the campaign already removed the three offenders.

### A5. Unity message shadowing

`ElementOnDestroyTests` covers `OnDestroy`. Generalise to every Unity message the base class
implements privately — `Awake`, `OnEnable`, `OnDisable`, `Start`. The trap is identical and
equally silent.

### A5b. `ElementKind` is built in one place — and nothing checks it

`AGENTS.md` says `Validation/ValidationSnapshot.cs:KindOf` is the only file allowed to decide an
element's role for the core. That was verified by hand and holds today, but it is the one
architectural rule of that shape with no guard. Note the constraint: this test cannot live in
`Assets/Tests/EditMode/Geometry/` — that directory is compiled a second time by the `dotnet`
build and pulls no scene types. It belongs one level up.

### A6. Layer dependency direction (Dependency Inversion, enforced)

Declare the allowed dependency graph and fail on any edge that is not in it:

```
Geometry  → (nothing; no UnityEngine at all — already guarded)
Elements  → Geometry, Commands
Persistence → Elements, Geometry           (NOT UI)
MCP       → Elements, Geometry, Commands   (NOT UI)
UI        → everything
```

Today nothing stops `Persistence` or `MCP` from reaching into `UI`, and one such edge is enough
to make the core untestable outside Unity again. This is the rule that keeps `dotnet test` and
Stryker alive — a path that was silently broken until this campaign fixed it (`5917ac94`).

### A7. New-element-type completeness (Open/Closed, enforced)

`CONVENTIONS.md` → "Adding a new element type" is a seven-step checklist that a human must
remember. Turn it into one test that enumerates every registry — create, duplicate, destroy,
serialize, restore, MCP contract, validation — and fails naming the registries a type is missing
from. Parity tests of exactly this shape already found three latent defects in one go
(`6b819773`) and two contract defects in MCP.

### A8. Generated artefacts match their source

`mcp-server/src/tools.generated.ts` was three commits stale and nothing noticed. Run the
generator in memory during `KitchenServer.Tests` and compare with the committed file.

---

## Part B — Let the compiler carry what it can

Cheaper than any test: it fails at build time, on the offending line, for everyone.

### B1. Naming rules in `.editorconfig`

There are currently **none** — no `dotnet_naming_*` at all, so `_camelCase` for private fields,
`PascalCase` for members and `SCREAMING_CASE` for constants are held up by habit alone. Encode
the conventions the code already follows, at `severity = error`.

### B2. Re-examine the 29 silenced diagnostics

Some are legitimate for Unity (`CS0649` on `[SerializeField]`). Others are exactly what this
campaign has been finding by hand — `CA1822` (member can be static: a strong smell that a method
does not belong to its class), `CA1031` (catch-all), `CA2213` (undisposed field). Go through the
list one at a time, turn on what pays, and write the reason next to each that stays off.

### B3. Close the nullable and warnings-as-errors holes on the server side

`csc.rsp` gives Unity `-nullable:enable -warnaserror`. The `server/` projects have `Nullable`
enabled but no `TreatWarningsAsErrors`, so the same class of defect is an error in Unity and a
warning next door. Make them symmetric.

### B4. `max_line_length`

Not set. Pick the width the code already uses (~100) and let the formatter, not review, enforce
it.

---

## Part C — Make "this test can fail" automatic

The campaign proved every new test by hand: revert the guarded behaviour, watch it go red, put
it back. That ritual is exactly what **mutation testing** automates, and Stryker is already
configured for `geometry/`.

**This just became far more attractive.** The `dotnet` path was silently broken for months: a
scene test sitting in `Assets/Tests/EditMode/Geometry/` — a directory the geometry `.csproj`
globs whole — failed with `CS0246` under `dotnet` while Unity compiled it happily, so nobody
noticed. It is fixed and guarded (`5917ac94`), and the core cycle is back to **226 tests in
0.2 seconds** against 90 seconds for a cold Unity batch. A 450× faster feedback loop changes what
is worth automating.

### C1. Gate the geometry mutation score

Record the current score and fail the build when it drops. Same ratchet as A1: a new test that
does not kill any mutant will not raise it, and code that loses coverage cannot land quietly.

### C2. Move every scene-free class and its tests onto the `dotnet` build

This is the single highest-leverage item in the plan, and it is mostly clerical.

Measured today:

| | Files | Tests |
|---|---|---|
| Runs under `dotnet` (`Assets/Tests/EditMode/Geometry/`) | 21 | **226** |
| Unity-only, but the test never mentions a scene type | 31 | **268** |
| Unity-only, builds real `GameObject`s | 177 | 2282 |
| PlayMode | 22 | 83 |

So `dotnet` covers **8%** of EditMode. The 226 are not separate tests — that directory is
compiled twice, by Unity and by `dotnet`.

The 31 scene-free files can move with no rewriting at all. Their production code was checked
and is plain C#: `ExpressionParser` (66 tests), `SidebarCatalog` (19), `TextureIndex` (14),
`UpdateCoordinator` (12), `VersionUtil` (10), `DragGesture` (10), `ReleaseManifestParser` (8),
`GameContext` (8), `EventBus` (7). Moving them takes coverage to **494 of 2776 — 18%**.

**The obstacle is how the projects include code.** `geometry/core/Geometry.csproj` globs
`Assets/Scripts/Core/Geometry/**` and `geometry/tests/Geometry.Tests.csproj` globs
`Assets/Tests/EditMode/Geometry/**` — whole directories. The scene-free classes are scattered
through `Core/UI`, `Core/Update`, `Core/Elements`, `Core/Materials`, which are full of
`MonoBehaviour`s, so those directories cannot be globbed.

The fix is a **home for scene-free logic** — a directory (and matching test directory) that the
`dotnet` projects glob, into which pure classes move. The campaign already produces them
continuously: `ContextMenuLayout`, `EditFieldRules`, `IssueTableSelection`, `PartPlane`,
`PartMount`, `SnapCandidatePicker`, `DropDoor` are all plain C# by construction. That was a side
effect of SRP; it is also exactly what makes them fast to test and mutation-testable.

**Standing rule from here on:** a newly extracted class with no scene dependency goes into that
directory, and its tests go into the `dotnet`-visible test directory. Not "later" — in the same
commit as the extraction.

### C2b. What must stay in Unity, and why we are not deleting it

Deleting the Unity suites would be a mistake, and it is worth writing down why.

The 2282 scene tests verify what does not exist outside Unity: component wiring, real element
hierarchies, TMPro widgets. Crucially, they find widgets by GameObject name
(`transform.Find("CtxGrooves")`) — and **that name contract is what made this whole campaign
safe**. It is the only thing that kept taking `ContextMenuUI` from 3580 lines to 714 from
quietly breaking the interface. PlayMode is the only place rendering and the isometric
screenshots are checked at all.

One sobering fact belongs here too: the 226 `dotnet` tests were broken for months and nobody
noticed, because Unity compiled the same files and stayed green. That argues for the second
build path having its own guard — which it now has — not against having it. But a path that is
run rarely rots quietly.

Two loops, not a replacement.

### C3. Cheap test-quality guards meanwhile

- Fail on test names that state nothing: `Test1`, `Foo_Works`, anything without `_`.
- Require a message on `Assert` in the suites that carry migrated `why` knowledge.

---

## Part D — SOLID past SRP

SRP got the attention because the symptoms were loud. The rest is worth naming.

- **Open/Closed** — A7 is the enforcement. The measure of success: adding an element type
  touches only registries, never a `switch`.
- **Liskov** — the `OnDestroy` trap is a Liskov violation with no compiler help; A5 generalises
  the guard.
- **Interface Segregation** — `ElementFieldsEditor` has six phases and `Show`/`Refresh` differ
  only in whether a focused field is skipped. Collapse to one call with a flag. `PartMount` is
  configured through a five-argument `Configure` including a delegate and a callback — the
  agent that wrote it said plainly it is the lesser of two evils and it is not sure it chose
  right. Revisit both.
- **Dependency Inversion** — A6.

---

## Part E — Refactoring still owed

In rough value order. Each is one agent, one directory, the shape the campaign has settled into.

1. **Comment purge, remaining 3998** — the largest single item, and the one that keeps finding
   defects. `Elements` (1096) and `UI` (580) first: they have the most `why` per line.
2. **`SidebarUI`** — a 19-branch `if (item.kind == ...)` ladder, the same shape already removed
   from `UIManager`.
3. **`ElementTypeConverter`** — 21 type checks in two ladders (`GroupOf` and `ChoiceOf`) that
   will drift apart; it is in the allow-list on a technicality.
4. **`Elements.Mutation.cs`** — five handlers repeating one algorithm (collect → validate →
   execute → describe). A `BatchMutation<TOp>` makes each ~15 lines, but it touches `Bulk.cs`
   and `Modules.cs` and needs a per-tool test on error text and ORDER.
5. **`McpCommandHandler.Handle`** — a 60-arm switch that should be a dictionary built from
   `McpToolRegistry`, so contract and dispatcher cannot diverge. The parity test (`f1d0c00f`)
   now covers the divergence; this removes the possibility.
6. **`CooktopSpec` / `DishwasherSpec`** — pull the model tables and clamp policies out of the
   element classes.
7. **`ToolbarUI` (280)** — assembly and per-frame state are two zones already.
8. **`ResizeSnap` next to `SnapCore`** — `AGENTS.md` says "change one, check the other", which is
   the documentation of a duplication. After the split, `SnapCore` is 75 lines over
   `SnapCandidateCollector` / `SnapCandidatePicker` / `EdgeDetents`; `ResizeSnap` shares none of
   them and still carries 41 comments.
9. **`ContextMenuMaterialSection.ApplyLegsChoice` bypasses `CommandStack`** — it runs from
   `ApplyFields` on every Enter and every lost focus, applying the legs dropdown outside a
   command, against UI-GUIDELINES rule 2. It is also a duplicate: the choice already goes
   through `Choose(MaterialSlot.Legs, …)` as a command. And it carries a latent path — when the
   legs decor id is missing from the catalogue, `MaterialOptions.IndexOf` returns 0, so the next
   edit of any field silently repaints the legs with the first decor in the catalogue.
10. **Normalized UVs still in two mesh builders** — `PillarMesh` (caps `cos*0.5+0.5`, sides
    `i/Segments`) and `RadialShelfMesh` (cap normalized by `width, thickness` while the cap's
    second axis is depth). Both fall under "a decor tiles, it never stretches"; guard A3b covers
    finding the rest.
11. **`SnapSystem.Diagnose` holds a THIRD copy of the candidate loop** — alongside
   `SnapCandidateCollector` and `ResizeSnap`. The failure mode is "Diagnose says one thing,
   TrySnap does another", and one such case is already described in its comments.

## Snap defects — both FIXED

Recorded here as open; closed in `4c69fe6b`, `bcaec410`, `cf3ffb18`. Kept for the reasoning.

1. **`SnapMutationTests` demanded a snap past its own threshold** — the sweep derived a probe
   position from the measured gap, and on a pair sitting at the threshold the clamp pushed the
   part 1 mm BEYOND it, then asserted it snapped. A defect of the test. Fixed: the pair is
   skipped when the derived position leaves the range where the behaviour is defined.
2. **Snap lost face-to-face contact between rotated parts.** `git log -S` recovered a deleted
   comment showing the "centre inside" check was made a POINT test on purpose, precisely to
   avoid the fat AABB at 45°. The bug was narrower than it looked: the rotation of the MOVING
   part was accounted for and the neighbour's was not. The check now takes the sign against the
   neighbour's six faces — identical results for an unrotated neighbour.

**The "0.5 mm artefact" belongs to neither.** It was recalled as the reason the centre check had
been abandoned; `git log -S` places it in `f72f1099` instead — `ResizeMath` rounded the size to
whole millimetres and could overshoot THROUGH the neighbour's plane, and horizontal dragging
re-rounded Y to the grid. Rounding, not the centre check. The resize half was already covered;
the drag half was not, and now is.

Two claims from the earlier survey were also wrong: `ResizeSnap` never carried this check (resize
does not drive a centre inside a neighbour), and `SnapSystem.Diagnose` does not produce
positions at all — it ranks pairs as a deliberately independent oracle for the sweep and must
NOT be merged into the collector.

---

## Open questions for the product owner

Not refactoring decisions — these change behaviour and need a person.

1. **Half the element types snap their height to the grid and half do not.** An oven at 595 mm
   would sit 2.5 mm into the floor if rounded, which is why appliances are exempt. The split is
   now named and tested, but whether it is the RIGHT split is a product question.
2. **`IsClosedPose` for the oven and dishwasher.** Unifying it means dimension fields stop
   refreshing while their door is open. For the drawer the same change was a fix; for these two
   it is a mild regression, uncovered by tests.
3. **Row order in the assembled-facade panel** — "Fill" moved above the Open buttons, closer to
   `UI-GUIDELINES` rule 6 (parameters, then actions), but it looks different now.

---

## The working loop — `dotnet` first, Unity last

**Start every iteration with the `dotnet` run. Use Unity only as the final check before a
commit.**

```powershell
.\tools\mutation-test.ps1 -TestsOnly   # 226 tests, 0.2 s — the inner loop
.\build.cmd -RunTests               # 2776 tests, 79 s — before committing
.\build.cmd -RunTests -RunPlayMode  # + 83 tests, 101 s — before a release or any UI layout change
```

The numbers are why. A cold Unity batch costs **~30 seconds of fixed overhead before a single
test runs** — asset pipeline refresh (~17.6 s), two domain reloads (~5.6 s), script compilation
and scene import (~7 s) — and only then ~60 s of actual test execution. A targeted
`-Filter` run does not avoid that: it still costs 26–36 s for eight tests. The `dotnet` path has
none of it: 226 tests in 0.2 s, which is 0.9 ms per test against 23 ms under Unity.

Over one six-hour session with four agents, 58 full and 86 filtered Unity runs consumed **131
minutes of gateway time, of which roughly 72 minutes was fixed overhead paid over and over.**
That is the budget C2 is buying back.

Rules of the loop:

- Iterate on `dotnet`. Reach for Unity when the change touches scene behaviour, or when you are
  about to commit.
- `-Filter` takes ONE class; `|` is mangled by `cmd.exe`.
- Never pipe a run through `Select-Object -Last N` — it eats the `Total:` line and the head of
  the failure list. Write to a file under `test-results/` and grep it.
- PlayMode before a release and after any change to panel layout. It went unrun for the entire
  refactoring campaign; that was the largest blind spot in it.

## How to run this

Order matters: **Part A before Part E.** Guards first, then the work they protect — otherwise
the purge finishes and the count starts climbing behind it. Part B is independent and can go in
parallel.

**Part C2 should go first of all**, before the remaining refactoring: every hour spent on Part E
without it pays the 30-second Unity tax on every iteration, and the classes Part E extracts are
exactly the ones that belong on the fast path.

One agent per item, one directory per agent, the working agreement in `AGENTS.md` →
"When several agents share the tree".
