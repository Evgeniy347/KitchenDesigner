# Структура: SRP, быстрый путь, проверки типа

## Class responsibility (SRP)

A class does ONE thing. There is no line limit here — there is a list of symptoms. ANY of
these means the class MUST be split before the change lands:

- Two or more independent sections that you would name with different nouns (edges,
  grooves, textures, gaps, drawers, materials…) live in the same class.
- A method builds or configures more than one section.
- A method takes 3+ boolean parameters that describe WHICH KIND of object it received.
  That is flags instead of polymorphism — replace with a type, a strategy or a flags enum.
- The same `if (x is A) … else if (x is B)` type ladder appears in more than one method.
- The fields of the class form disjoint groups: the methods of group 1 never touch the
  fields of group 2.
- Two near-identical families of methods exist (`RebuildXOptions`/`XIndex`/`SetXValue`
  next to `RebuildYOptions`/`YIndex`/`SetYValue`) — extract the shared binder. **But diff the
  family line by line before collapsing it.** Eighteen `Spawn*` methods looked like copies and
  were in fact two rules that differ only in the ORDER of two lines — one snaps the height to
  the grid, the other must not. Merging them "by the obvious template" silently changes half of
  them. When the difference is an order, it is TWO rules: give each a name and a test.
- The class is `partial`, and one of its parts holds methods that never touch the other parts
  (pure functions over their parameters). That part is not a part of the class — it is a
  separate type hiding behind `partial`. Watch for this: `partial` removes the file-size
  signal, which is the one thing that would otherwise have forced the split. `McpCommandHandler`
  accumulated a wire codec, AABB geometry and an element factory exactly this way.
- A method takes 8+ parameters, half of them `ref`/`out`. That is not a method, it is a class
  turned inside out — its fields are travelling on the stack. `SnapCore.Collect` had eleven.
  The cure is a result object (`SnapCandidates`), not a shorter signature.
- The same iteration appears more than twice. Four copies of one 6×6 face loop lived in
  `ValidationCore`. Collapse them into a parameterised **struct** enumerator (`FaceContactScan`):
  validation runs every frame, so `yield return` would allocate and `IEnumerable<T>` would box —
  `foreach` over a struct enumerator does neither.
- `GetComponentInChildren<T>()` means "the first one found", not "the one I meant". On a
  composite element the order of children decides who gets the material or the
  `MaterialPropertyBlock` — `RefreshTiling` landed on the first LEG because the rectangular
  table deletes its root renderer in `ApplyDimensions`. An element must NAME its own decor
  surface rather than let a search guess it.
- A method acts on a DIFFERENT object than its name says — typically through
  `transform.parent` or `GetComponentInParent<T>()`, assuming a wrapper that the layout no
  longer has. This does not crash, it silently does nothing: `UpdateModeDropdownEnabled`
  disabled `dropdown.transform.parent`, which was the whole panel, and `Open()` switched it
  back on two lines later. Any method reaching for a parent needs a test on that structure,
  or the next layout edit turns it into a no-op without a word.
- A "can I?" method and an "apply it" method over the same input branch on type in TWO places.
  They must branch once: two `is X` ladders over one model drift apart, and the new field gets
  added to one and forgotten in the other. `drawer_system` was in the apply ladder and missing
  from the validate ladder for exactly this reason.

Rules that follow from it:

- Shared object setup is a TYPE, not something to copy. The factory repeated the same
  boilerplate — tag, kinematic body, `Normalize`, registration, `RefreshHighlights` — twelve
  times; skipping one step there breaks nothing loudly, so the copies drift unnoticed. If the
  same opening sequence appears three times, it is a constructor or a builder.
- One public type per file.
- Adding a feature to a class that already owns a separate zone for that feature is
  FORBIDDEN — create the class instead of appending to the file.
- Extracted sections are plain C# classes, not `MonoBehaviour`s, owned by the component.
- Still in force: one method one job (~40 lines max).

`ContextMenuUI` at 3580 lines, with a 480-line `Build()` and a `Layout()` taking 11
booleans, is the anti-example this section exists to prevent.

## A class without a scene lives on the fast path

`Assets/Scripts/Core/Pure/**` and `Assets/Tests/EditMode/Pure/**` are compiled a SECOND
time by `geometry/pure/Pure.csproj` and `geometry/pure-tests/Pure.Tests.csproj`, next to
the core's own second build. Unity compiles the same files; nothing is duplicated.

**A newly extracted class with no scene dependency goes there, and its tests with it, in
the SAME commit as the extraction.** Not "later": the moment it lands in `Core/UI` next
to the `MonoBehaviour`s, it costs the 30-second Unity tax on every future iteration, and
nobody comes back for it. The SRP campaign produces such classes continuously — that is
what makes the rule cheap to follow.

The inner loop is then:

```powershell
.\tools\mutation-test.ps1 -TestsOnly   # core + pure, 354 tests, 0.25 s
.\build.cmd -RunTests                  # everything, ~80 s, before committing
```

What "no scene dependency" means, exactly — the second build runs under CoreCLR, where
every Unity `extern` throws `SecurityException: ECall methods must be packaged into a
system module`:

- `Vector3`, `Vector2`, `Vector3Int`, `Rect`, `Bounds`, `Mathf`, `Color` are DATA and work.
- `Debug.Log` does NOT. If a class logs, the reason it logs is a dependency: inject an
  `Action<string>` and let the Unity-side adapter pass `Debug.Log`. `UpdateCoordinator`
  did exactly this — and the injection turned out to be the only path by which a failure
  reason reached anyone, which two tests now hold in place.
- `JsonUtility` does not even COMPILE there: it lives in `UnityEngine.JSONSerializeModule`,
  not `CoreModule`. A class that binds JSON stays behind; split its DATA type out and move
  that instead (`ReleaseManifest` left `ReleaseManifestParser`).
- `Quaternion.Euler/AngleAxis/LookRotation/Inverse` and `Matrix4x4` are ECall too.

`GeometryArchitectureTests` enforces all of it over both directories, and asserts that its
own scan actually sees files — a grep down a wrong path is green and checks nothing.

An `internal` class moving to `Core/Pure` needs `Assets/Scripts/Core/Pure/PureAssemblyInfo.cs`
to name the test assembly (`Pure.Tests`); the Unity-side `InternalsVisibleTo` in
`Core/AssemblyInfo.cs` covers only Unity's own test assembly.

### Lifting a type off the scene: resolve the pose through the paired API, never through `transform`

Moving an element's state into a plain C# type is how a scene test becomes a fast one — the scene
is an ALLOCATOR for most of them, not an environment. The trap is in the first line of the new
type: it must take the pose from `ValidationPositionAt(transform.position)` / `ValidationRotation`,
the same pair `GetVerticesAt` / `GetFacesAt` uses, and never read `transform` directly. While a
child rides an animated parent (`AttachRider`), its `transform` carries the ANIMATED pose and the
logical one lives in `AttachRestPosition` / `AttachRestRotation` — so a direct read is right in
every test anyone bothers to write, and wrong exactly when the furniture moves.

This is not hypothetical: it happened in the first class lifted, and the mirror test that was
supposed to catch it was green on nothing, because it never put the part in the ridden state. So
the checklist for each type is two lines: resolve through the pair, and write a test that mirrors
the pure type against `GetVertices()` WHILE ridden — or prove the type cannot be a child and say
what proved it.

## Element type checks live in ONE place per layer

The core already has this rule (AGENTS.md → "Validation"): element roles arrive as
`ElementKind` flags built in `Validation/ValidationSnapshot.cs`, the only file allowed to ask
«is this a floor / a sink / a drawer», and `GeometryArchitectureTests` fails the build if an
`is XxxElement` check appears anywhere else in the core.

**The same rule applies to every other layer** — UI, MCP, persistence. In each layer exactly
one place may ask what type an element is:

- decide the type ONCE and turn the answer into data (a facet set, a handler, an editor);
- everything downstream consumes that data, never re-asks.

`ContextMenuUI` is the cautionary tale: the same `if (target is X)` ladder ran in FIVE methods,
so every new element type meant five edits and any one of them could be forgotten.

### How to remove a type ladder

In order of preference:

1. **Ask the element.** If the ladder computes a value that describes the element — a display
   name, a default, a capability — that value belongs ON the element as a virtual property.
   A ladder that ends in `element is OvenElement ? OvenElement.MODEL : …` is the element
   answering a question about itself through a middleman.
2. **Use an existing interface.** Check before writing a ladder: `IOpenable`, `IFacadeHost`
   and friends exist precisely so callers do not repeat `is DoorElement / WindowElement / …`.
   A ladder over types that already share an interface is a defect, not a style choice.
3. **Introduce an interface** for the shared behaviour (`IWallMounted.SnapToWall()`), not a
   base class — an element is several things at once (a window is openable AND wall-mounted),
   and single inheritance cannot express that.
4. **Register a collaborator** keyed by `Handles(element)` when the behaviour is a whole zone
   of UI or a whole command, not one call (`ElementFieldsEditor` and its subclasses).
5. **A flags enum** when the answer feeds visibility or filtering (`ElementFacet`). Name each
   facet after a CAPABILITY, never after a type. `ElementFacet.Oven` and `.Dishwasher` exist
   only to show one button row each — they are the type ladder again, wearing a flag. A facet
   called `Openable`, with the label supplied by the element, is the shape to aim for.

Abstract base classes for the HOST are the wrong tool. One panel serves every element type and
the selection changes at runtime; a `MonoBehaviour` cannot change class between clicks, and
rebuilding it would drop focus, scroll and expanded sections.

### A capability table has a twin in the contract — cross-check them with a test

Whenever the implementation keeps a table of what it can do (a spawner registry, a rule set, a
handler map) and the contract keeps a list of what it ADVERTISES (an enum, a tool description,
a generated schema), the two drift. Not "may drift" — they drift, silently, and each direction
is a different defect: the implementation doing more than it declares hides a working feature,
the implementation doing less returns a silent success for something it never applied.

Both of the MCP contract defects found in this campaign were exactly this, in opposite
directions. The fix is one test per pair that compares the two sets and fails on a difference
IN EITHER DIRECTION, printing both diffs. **Write that test in the same commit as the table** —
a registry without its parity test is the defect, already committed.

The same shape appears whenever a client picks a FILE by name while reading the version from a
different field. "Take the first match, preferring the one that agrees with the tag" is a wish,
not a check: agreement must be a CONDITION of selection, or a release tagged 0.720 carrying a
0.662 installer downgrades the user and then offers the same update forever. Compare on
delimiters, never as a substring — `0.70` matches inside `0.700`.

This is not only about a contract facing outwards. Registries that must agree with EACH OTHER
count too: the element factory keeps three — "can create", "can duplicate", "can destroy" — and
one parity test over them turned up three latent defects at once (a duplicated panel came back
as a plain board; appliances went to the board pool instead of being destroyed).

### The same treatment for colours in the UI layer

`UI-GUIDELINES.md` §10 already forbids local colour constants — every colour comes from
`UIStyle`. Like the type-ladder rule, it held only as long as someone remembered it: two panels
carried a byte-identical copy of the selected-row colour, and one tab colour was a literal copy
of `UIStyle.SurfaceActive`. There are still ~39 colour literals in `Core/UI` outside `UIStyle`.
A rule this mechanical belongs in an architecture test, not in a document.

**A second implementation of a UI rule is a defect, not an alternative.** The rule «a disabled
control stays readable» had two: the pillar HID its size rows, the sidebar dimmed its captions
by hand in a different colour than the rest of the app. Both looked like they worked, and both
were past the rule — a hidden row cannot be read at all, and a second shade of «off» makes the
first one meaningless. Two implementations do not merely duplicate; they DRIFT, and they drift
in the method, not only in the constant. So when a UI policy already has a mechanism, the second
place adopts it rather than reproducing it, and the guard is a scan FOR the mechanism (nobody
builds a button outside `UIFactory`) rather than a list of the places you happened to fix — a
list goes stale the moment somebody adds the next button.

### `*.csproj` in `.gitignore` is a lifetime trap

`.gitignore` blanket-ignores `*.csproj` — Unity legacy, since Unity regenerates its own. It also
applies to every server-side project. Two of them, `KitchenServer.McpContract.csproj` and
`tools/McpContractGen/McpContractGen.csproj` (both since deleted), were **never in git**: a fresh clone could not
build `KitchenServer.McpContract` at all, and `git status` said nothing, because an ignored file
is not untracked, it is invisible.

Every `.csproj` outside `Assets/` goes in with `git add -f`. The check:
`git ls-files "*.csproj"` must list every project in the solution.

### Build strictness is inherited, not optional

A new project outside Unity either inherits `server/Directory.Build.props` or declares its own:
`TreatWarningsAsErrors` and `Nullable` are mandatory. A project that compiles files LINKED from
`Assets/` must carry the same switches as `csc.rsp`, or the identical defect is an error in Unity
and a warning next door. `KitchenServer.McpContract` opted out of nullable and hid 52 warnings
and three real defects that way.

### Generated artefacts go stale silently

A file produced by a generator is not covered by the compiler and not covered by the tests, so
nothing forces anyone to regenerate it. `mcp-server/src/tools.generated.ts`, emitted from
`Assets/Scripts/Core/MCP/Contract/**`, stayed three commits behind the contract without a single
red light. Any change under a contract directory MUST end with the generator run, and the parity
between generator output and the committed file belongs in a test — not in a `prebuild` step that
the Unity build never invokes.

**The cheaper cure is to delete the generated file.** That table is now built at runtime:
`tools/list` is served straight from `McpToolRegistry` through `McpJsonSchema`, so there is
nothing to regenerate and nothing to go stale, and the guard is
`McpRpcRouterTests.ToolsList_ListsEveryRegistryTool_InRegistryOrder` — it compares the answer
the agent actually receives against the registry. Prefer «derive it on the spot» over «generate
it and guard the artefact» whenever the derivation is cheap.

**Write the generator so it can RETURN its result, not only write a file.** A top-level script
with all the logic in its entry point makes the parity test impossible without spawning a
process and creating a temp file. Split out `Emit() → string` from the start: the test then
calls it in memory, needs no `node` and no `dotnet run`, and costs milliseconds.

### Guard it with an architecture test

A rule that only lives in a document comes back. Mirror `GeometryArchitectureTests`: a test
that greps the layer for `is XxxElement` / `GetComponent<XxxElement>` and fails on anything
outside the one file allowed to ask. Add the allowed file to the test's own allow-list, so
widening it is a deliberate, reviewed edit.

Two things the allow-list needs, or it rots:

- **A reason per entry, and a test that every listed file still exists.** Without the reason,
  in six months nobody can tell a deliberate exemption from an oversight. Without the existence
  check, an entry outlives its file and silently starts exempting the next file that takes the
  name.
- **A test for the test.** A grep over a path that no longer resolves passes cheerfully while
  checking nothing — the "a test that cannot fail is worthless" trap, one level up. Assert that
  the scan actually found the layer AND that a known offender-prone file is in the scanned set
  and NOT in the allow-list. `UiElementTypeLadderTests` carries all three of these guards.

