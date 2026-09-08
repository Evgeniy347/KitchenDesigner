# Как писать тест, который может упасть

## A test that cannot fail is worthless

After fixing a bug, **temporarily revert the fix and confirm the new test goes red.** Twice in
one session a check looked green only because it could never trigger. Revert cleanly (delete
the condition) — `if (true) continue;` will not compile: `csc.rsp` has `-warnaserror`, and
CS0162 "unreachable code" becomes an error. What CS0162 objects to is the code left stranded
after the jump, not the literal itself: `if (true) { ... }` with nothing unreachable behind it
compiles fine and is a perfectly good way to force a branch.

More on proving redness — unreachable guards, mutually-supporting pairs, tests that re-derive
the formula, negative assertions — in CONVENTIONS.md → "Test naming conventions".

**A fix made for ONE named element must ship a test for the ORDINARY case in the same commit.**
`0f4809fa` was written to make `A34K1_upper_door_L` open, and it brought `FacadeOpeningReproTests`
— about `A34K1` only. The same commit took a rank-and-file door from 96,4° to 12,0°, and nothing
went red: every existing test asked whether a door STOPS at an obstacle, none asked whether an
unobstructed one still OPENS. A repro test named after the element that prompted the work proves
that element, and nothing else. Ask what the fix does to a case nobody complained about, and
write that one down too — the pair is the deliverable, not the repro.

**Changing the coordinate system in a test's ARGUMENT changes the scenario, not the number.**
When the MCP contract moved from centre to minimum-corner, the mechanical translation `x = 0` →
`anchor_x_mm = 0` kept every test compiling and moved every element by half its own extent.
Three tests then failed on arithmetic — visible, cheap. The dangerous one PASSED: a wall placed
at `y = 1.25` as a centre sat INSIDE another wall, and the same number as a corner sat neatly on
top of it, touching on one plane. `CreateElement_Wall_..._WhenOverlappingWithAnotherWall` stopped
testing an overlap at all and went on reporting green. So after any such migration, re-read each
touched test asking «what does it assert NOW», and prefer an assertion that survives the change:
compare the MIN CORNER computed from `GetVertices()`, or compare two DELTAS rather than a
coordinate — neither has half an extent baked into it, so the next change of thickness or datum
leaves them alone.

## Two questions need OPPOSITE inputs: "was the value carried" and "was it built at all"

Every parity and duplication guard builds its element with NON-default numbers, and rightly so:
a value that happens to equal the default cannot be told from a value that was lost. But a
factory whose element has a COMPUTED size has nothing to assign, so its build hangs off the
form-property setters — and a setter handed the value already in the field returns without
touching anything. On factory defaults not one setter fires, `ApplyDimensions` is never called,
and the element leaves the factory with no mesh, no renderer and NO COLLIDER: it cannot be seen
or picked. Every existing guard stays green, because their non-default numbers built it on the
way past.

It shipped that way in four factories at once (wall mixer, shower column, socket, light switch)
and was invisible in the app, where `Awake` calls `ApplyDimensions` itself — EditMode never runs
`Awake`. So a new element type needs a second test that asks the other question with FACTORY
numbers: a fresh element AND a copy have a mesh with vertices, a renderer, a non-zero collider
and the computed size. `SanitaryFittingsBuildTests` and `WallDeviceBuildTests` are the pattern.

## `!= null` before touching a scene object is load-bearing, not style

Unity overrides `==` so that a DESTROYED object compares equal to null. Every guard of the form
`if (_thing != null) _thing.Foo()` scattered through this UI therefore does double duty: it skips
the not-yet-built case AND it swallows calls into objects the engine has already torn down. Read
as style, it looks like noise worth tidying. It is not.

`SettingsViewTab` subscribes to the STATIC `EditModeManager.Changed` and unsubscribes only in
`OnDestroy`, so a panel built twice leaves the first instance subscribed forever, firing a stale
delegate into a dead panel. That had been harmless for as long as every access inside it went
through a Unity null check. Adding ONE unguarded access — `_body?.Fit()`, where `_body` is a plain
C# object that is never Unity-null and reaches `Content.rect` on a dead `RectTransform` — turned
it into 62 failures across four test classes: the exception escaped TearDown, so `DestroyImmediate`
never ran, a live window leaked into later classes, and `SnapshotTests` recorded an open settings
window in scenes that build no UI at all. Two of those classes know nothing about settings.

So: a pure ADDITION to a method reachable from a static event is a behaviour change. Before adding
a call there, ask whether the object can already be destroyed — and prefer fixing the leak at all
three points (guard the delegate, unsubscribe on replace, make the new API a no-op on a dead
object) rather than the one that happens to be red.

**A `MissingReferenceException` in a class that never destroys anything is a state leak from
SOME OTHER class, and the cure belongs in the shared collection, not in either class.**
`PipeEndFittingsMaximizeLinksTests` died on an element pulled from `PartRegistry` — a static
singleton that lives for the WHOLE EditMode run. Two workers patched the crashing class; neither
patch helped, because the defect was never there. Nor was one leaker: a sensor built to name the
dead entries counted **43** of them, from several classes at once. Tidying up after each of them
would have been thirty teardowns and one forgotten. What worked was making `PartRegistry` itself
refuse to hand out or keep a destroyed object, so no consumer has to remember. When a crash names
a collection that outlives its readers, build the sensor that COUNTS the bad entries before
choosing a fix — the count tells you whether you are hunting one caller or an invariant.
Same lesson as the `SettingsViewTab` leak above, one layer up.

## A defect the platform cannot report stays invisible until you build the sensor

Twice in one session the same shape of bug: content silently grew past the window it lives in —
the sidebar catalogue (1280 px of items in 960 px of panel, the bottom group unreachable, and it
had already been broken by 80 px BEFORE the wave that made it obvious) and then the settings
window (the photo tab: 27 rows = 1026 px inside 696 px, ten rows hidden). Nobody noticed either.

The reason is not carelessness. **uGUI has no `scrollHeight`.** On the web the check is one line —
`el.scrollHeight > el.clientHeight` — and in Unity that property does not exist on `RectTransform`
at all, so overflow throws nothing, logs nothing and renders almost normally. There is no signal
to miss.

The fix is not «remember to add a ScrollRect». It is to BUILD the missing sensor and then guard
with it: `Core/Pure/UI/ContentExtent.Measure` returns content height, the overflow flag and the
NAME of the lowest node in one call, `Core/UI/RectSpans` walks the tree to feed it, and
`WindowOverflowGuardTests` fails with the window, the node and the pixel count. Both the panel
that lays out and the test that judges call the SAME function — a second description of one
contour is exactly how the sidebar lied (positions computed in one place, height hard-coded in
another).

Generalise it: when a class of defect produces no error, no log line and no red test, ask what
the missing measurement is and write it once, rather than fixing the instances one by one.

And the second half of that lesson: **a sensor must read the same source the production code
reads.** The handle gizmo was measured against the pixel height of `Camera.main` while the
render test photographed it with its OWN 512×512 camera — two cameras, one number, and the
sensor measures something the app never draws. The fix is to parameterise the sensor by that
source (pass the camera being drawn for), not to freeze a value inside the test: freezing keeps
the two sources and hides the disagreement, exactly as the sidebar hid its height by computing
positions in one place and hard-coding the total in another.

Concretely, for anything that converts pixels to world: **take the camera as a PARAMETER.** A
global `Camera.main` reached for inside the layout makes every frame test a hostage of the run's
resolution — the handles were laid out against the main camera's pixel height while the render
test photographed them with its own 512×512, and the suite passed only because those two numbers
happened to agree on this machine. Pass the camera in, and the pick that measures the grab radius
takes the SAME one: the grab point already carries the layout's scale, so measuring the distance
to it in a different camera is once again two meters for one quantity.

A second example of exactly that class, found the same way: **one renderer instead of all of
them.** `GetComponent<MeshRenderer>()` on a composite element returns the ROOT's mesh and
`GetComponentInChildren<MeshRenderer>()` returns the FIRST one in the subtree — neither throws,
neither logs, and no test went red. So selection tinted a chair's seat and left its legs, and a
decor reached one door casing out of seven; a door, a window and a toilet have no root mesh at
all, so they highlighted as nothing whatsoever. Eight reported "bugs", one missing measurement:
«the set of renderers that make up this element». It now exists as `ElementRenderers.BodyOf`,
and `ElementTintCoverageTests` walks every type through the real factory, so a new type falls
under the check by itself.

And when the same sensor is carried to a NEIGHBOURING surface, **re-derive its bounds; never
carry the number across.** The overlay handles have the identical disease as the transform
gizmo, and the floor came out the same (it depends only on the pick radius) — but the ceiling
moved, 49 px to 48, because that arrow's cone is wider relative to its shorter shaft. Its grab
point sat at 0,6 of the length rather than the middle, which alone would have capped it at
41 px and silently excluded the chosen 42. And its second figure — a cube, which the transform
gizmo does not have — needed two DIFFERENT worst cases for its two bounds: half the space
diagonal above (√3/2, true from any angle), the face diagonal below (√2, what you see head-on).
Swap one for the other and the window widens by 15% with nothing to notice it, so each factor
is pinned by its own test.

## A brute-force sweep writes the WHOLE list to a file; a cap in the message is a defect

A sweep that truncates its own findings costs an extra run every time and, worse, makes two
different diagnoses look identical. `SnapMutationTests` printed «Snap mutation errors (200)»
— and 200 was the ceiling of `MaxErrors`, not a count. One defective part filled the list before
the sweep reached anything else, so «200 violations» and «one part violating 200 times» read the
same, and the obvious next question — is this the code or the fixture? — could not be answered
without running the 335-second set again.

So: the full list goes to a file under `test-results/`, uncapped, and the test message
carries a SUMMARY — the count, the breakdown by kind, and the breakdown **by subject**. That last
one is what answers the question in one glance: one name across the whole list means the code,
many different names mean the scene.

## Snapshot baselines — accepting candidates

`Snapshot.Match` writes `*.candidate.json` on mismatch. Accepting means renaming it over
`*.verified.json`. Three traps, all hit in practice:

- **`ui_*.verified.json` belong to PlayMode** (`IsoScreenshotTests` etc.). A bulk
  `Get-ChildItem *.candidate.json | Move-Item` silently overwrites them from an EditMode run.
  Accept by explicit list, or `git checkout -- Assets/Tests/EditMode/Snapshots/ui_*.verified.json`
  afterwards.
- **`Move-Item` preserves the source timestamp**, so a clobbered baseline still shows an old
  mtime. Verify what changed with `git status` / `git diff` — never by file date.
- **Diff every candidate before accepting**, field by field. Confirm only the fields you
  expected changed; anything else means a different bug.
- **A baseline has a canonical form on disk**: UTF-8 without a BOM, exactly one trailing
  newline. When acceptance changes the FIRST or the LAST line of a file, that is a defect in the
  WRITER, not a change in content — and it is the most expensive kind of noise, because it lands
  exactly at the moment a human is separating an expected shift from a real regression. One
  acceptance showed 27 files and 131 insertions of which only a handful were real. Both the
  writer and the 104 baselines were brought to that form on 2026-09-08; if the shape drifts
  again, fix the writer before accepting anything.

Snapshots serialize GLOBAL state (`KitchenSettings`, `ResizeHandleManager.Mode`), so a suite
that mutates those must reset them in `[SetUp]` and restore in `[TearDown]`. Acceptance test:
`-testFilter SnapshotTests` alone and the full run MUST give the same result.


## A sweep can demand a defect — check that its requirement is still the product's

A brute-force sweep encodes a requirement, and a requirement ages. `SnapMutationTests` asked
«facing faces + gap within threshold + overlap ⇒ must snap», which was true when it was written.
Then the oracle it reads got BETTER — `Diagnose` learned the collector's own rules and began
reporting the best pair rather than only the head-on ones — and the sweep started seeing
co-directed pairs at zero gap and demanding they snap. They must not: a window and a door are
nested inside the wall's box, and that «snap» is a far-edge alignment that drives the wall into
the window, which `SnapCoreContractTests` explicitly forbids. Twenty-eight findings, every one of
them the sweep demanding a defect.

Two things follow. A sweep that goes red after an ORACLE improvement is suspect before the code
is: ask what the oracle started seeing that it could not see before. And the fix is never to
silence the pair — it is to measure the reason in the same function that decides (here
`landsInsideNeighbour`, computed before the «already in place» exit) and to invert the
requirement into its opposite: the sweep now fails if the snap DOES drag such a part
(`ALIGN-PULL`), the same shape the screw leg got as `MOUNT-PULL`. A silenced pair is a sensor
deleted; an inverted one is a sensor kept.
