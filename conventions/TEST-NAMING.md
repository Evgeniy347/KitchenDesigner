# Имена тестов и доказательство их красноты

## Test naming conventions

- `{Class}_{Method}_{Scenario}` — EditMode tests
- `Iso{Feature}_{Variant}` — PlayMode isometric screenshot tests (REQUIRED for new elements)
- `Snapshot_{Type}_{Variant}` — snapshot tests

Since production source carries no comments (see "Comments live in tests"), the test suite
IS the documentation. Therefore:

- The test name must read as a sentence stating the behaviour:
  `Subject_Condition_ExpectedResult`. Existing good examples:
  `Wall_TextureOrderArrows_AreDisabledAtListEnds`, `ThresholdMin1mm_GapAboveIt_NoSnap`,
  `Window_Open_FrameStays_SashRotates`. `Foo_Works` and `Test1` are defects.
- Every `Assert` guarding a non-obvious reason MUST carry a message (Russian, like the rest
  of the suite) explaining WHY. This is where a former source comment lands.
- A regression test for a specific bug goes in a `*ReproTests.cs` file (existing practice:
  `ContextMenuRefreshBugTests`, `GrooveSceneReproTests`, `RadialShelfSnapBugTests`) and its
  Assert message names the bug it reproduces.

### A field list written out more than twice gets a parity test

Fields declared once and then re-listed in `Reset`, `ToData`, `ApplyFrom`, a JSON DTO and a
settings panel are five lists that drift apart. `KitchenSettings` carried four such lists of the
same 37 fields, and one had already diverged — it survived in production purely to serve two
tests. Compare the lists by reflection over the fields, with an explicit exception list, a reason
on every exception, and a test for the scan itself.

The same settings grew a SIXTH list on the agent side, and it was the one that rotted: the keys
`set_setting` accepts lived at once in the contract enum, in the prose of the tool description
and in the handler's own switch. Three hand-written copies, none derived from the others, and
they drifted until `get_settings` reported thirteen values of which `set_setting` could change
three. Better than a parity test over three copies is ONE copy: `SettingKeys` is now the table
both directions read, and `McpSettingsParityTests` guards only what genuinely cannot be derived
from it — the contract enum, the prose, and the settings panel.

**Prefer collapsing the copies to guarding them.** A parity test over three hand-written lists
keeps three lists; it only makes their divergence loud. Reach for it when the copies cannot be
merged — a compile-time attribute argument, prose meant for a human, a file in another language.

### Take the path the USER takes, not the one that is easy to call

A test can be green for years because it exercises the single code path where the bug is absent.
`Table_Material_KeptAfterDeselect_WhenChangedViaApply` drove `MaterialManager.Apply`, which does
set the base field — while the user goes through the Tabletop and Legs slots, which do not, and
through selecting and deselecting, which read it back. The defect sat between those paths,
untouched.

When a feature has an entry point a person uses and a convenience method a test can call, test
the entry point. If it is hard to reach from a test, that difficulty is the finding.

### A test that re-derives the calculation is not testing anything

If the body of a test computes the expected value by repeating the production formula, the
production code was never called. Two `Pillar_AutoAdjust_*` tests did the seating arithmetic by
hand and passed for as long as they existed, while the real `AutoAdjustPillar` — a private
method on a `MonoBehaviour` — went entirely unexercised. Assert against a value the code
returns, and if the code is unreachable from a test, that is the defect to fix first.

### When the guard turns out to be unreachable

Sometimes the comment describes a guard that cannot fire — a merge of boundaries that integer
input makes impossible, a depth counter no traversal reaches, a check the next line already
covers. Three of these turned up in one directory. Do not fake a red proof: pin the RESULT with
a test and say plainly in the commit message that the redness was proven for the behaviour, not
for that line, and that the line looks unreachable. Then it is a documented candidate for
deletion instead of a silent mystery.

### Two guards that complement each other need two runs

When one check and its negation back each other up, a single revert cannot turn both red — the
break that proves one hides the other. Do two runs, revert each side separately, and record
both in the commit message rather than bending one break to cover both.

### Prove the harness before you trust what it measures

A test bench can be wrong in a way that makes every result meaningless while every result still
looks plausible. A mutex experiment ran four clean rounds and reported "no exception thrown" —
but the holder script had been generated through bash, which ate a backslash, so the two
processes were waiting on two DIFFERENT mutexes. Nothing was ever shared, and "it returned
immediately" meant nothing at all. It surfaced only because a separate command returned in 0.2 s
where it was obliged to wait 12.

So: assert first that the thing you are measuring is the thing you think it is — that the handle
is shared, the file is the one on disk, the process is the one you started — and only then
assert anything about its behaviour. Put that check first in the bench, not in your head.

The same applies to a green test suite: before believing a number, confirm the run actually
exercised what you meant. `Total: 0 | Failed: 0` is the classic version of this, and the gateway
already treats it as an error.


**A guard that measures a SHARE needs a test for its denominator.** «What fraction of the
silhouette turned yellow» is false exactly as far as «the silhouette» is false. The frame tests
counted every pixel differing from the camera's clear colour as part of the element, so the
FLOOR — three quarters of the frame — went into the denominator, and so did the gizmo arrows,
which only exist in the «after» shot. The chair reported 0,39 and the door 0,23 against a
threshold of 0,9 while both were, in fact, painted whole: measured against the element's own
pixels the door came out at 0,99. Nothing was wrong with the tint at all.

Two habits follow. Make the denominator explicit and guard it — the frame tests now disable
every renderer except the element's own body, and a separate test shoots the same frame with and
without the scene to prove the exclusion does something. And read the NUMBER, not just the
verdict: a failure that lands far from its threshold — 0,23 against 0,9 — is the shape of a
broken measurement, while a near miss is the shape of broken behaviour. Chasing the behaviour
first costs a whole round.

**Isolation for a measurement must be decided at DRAW time, not written onto other objects
beforehand.** The frame bench first tried `renderer.enabled = false` on everything but the
subject. It changed nothing, byte for byte, because `CameraController.UpdateFloorVisibility`
re-asserts the floor's `enabled` every single frame — the flag lived for a fraction of a frame
and was back to `true` before the camera shot. The second leak was newer still: the list of
things to hide was taken once, and the gizmo handles are BUILT in the next frame's `Update`, so
they were never in it. Anything you stamp onto someone else's object, their `Update` may
overwrite before the frame; anything you enumerate once misses whatever is born after.

What survives both: put YOUR object on a private layer and give the capture camera a
`cullingMask` of that layer alone. The decision is then re-made on every draw and keyed on the
object, so no foreign `Update` can undo it and nothing born later can sneak in. Watch for the
one hole a mask does not close — immediate-mode drawing from `OnRenderObject` (`GL` calls)
ignores layers entirely and has to be switched off separately.

### If a test would not go red, DELETE it

The proof is not a formality. When a test stays green after you revert the behaviour it claims to
guard, it does not guard it — remove the test and record the uncovered knowledge as a debt in the
commit message. A green test that cannot fail is worse than no test at all: it makes everyone
believe the case is covered, so nobody writes the test that would have caught it.

### A test that contradicts itself LICENSES the defect

Worse than a missing test is one that states the requirement and then pins the behaviour that
breaks it. `ElementOutlineTests.UnlitChain_FallsBackToAShaderThePartsThemselvesUse` asserted
first that "the outline must not depend on lighting", then asserted `chain[0] ==
"Universal Render Pipeline/Unlit"` — and its own message explained that this shader is stripped
from the build and `Shader.Find` returns null there. Requirement and pinned behaviour
contradicted each other INSIDE ONE TEST, and in every build the behaviour won: the outline was
drawn by the lit fallback and darkened with the room. The test did not miss the defect; it
issued it a licence, and it stayed green for as long as the defect lived.

When two assertions in one test pull in opposite directions, one of them is wrong. Find out
which before adding a third. And when you invert such a test, say in the commit message WHAT
contradicted WHAT — "test updated" hides exactly the thing the next reader needs.

### A failure message that cannot tell expected from actual is half a test

A red test whose message reads `Expected: <X> But was: <X>` sends the reader to debug the test
instead of the code. This happened with materials: `MaterialManager` never names them, so both
`ToString()` and `.name` print the SHADER name, identical for every material. The test was
correct, red for the right reason, and unreadable. Compare something that distinguishes — here,
the decor id the renderer's material maps back to (by reference), never the slot field, which is
what the original hole was.

Check your own red output before you call the proof done. If you cannot tell from the message
which side is which, neither can the person who hits it in six months.

### The reader of a red guard is an agent sent to do something else

The person in six months is not the only reader, and for the architecture guards they are not
the usual one. The usual one is an agent that came to add a property, hit an unrelated-looking
divergence, and now has to decide whether fixing it is even in scope. Left to guess, it guesses
«not my task», stops and asks a user who may not be at the keyboard — so a correct, red,
perfectly readable test still costs a session.

So a guard's message carries three things, in this order:

1. **The rule, as a rule.** Not what the scan found — what the project requires, and that
   honouring it is part of the same change and needs nobody's permission. The parity guards share
   one constant (`McpUiParityRule.Rule`) for exactly this: one rule, one wording, four tests.
2. **What is broken, and what it costs the user.** The consequence, not the set difference.
3. **What to do, with addresses.** The files and members to add, and the alternative — a reason
   in the exception list — with the condition under which the alternative is the right answer.

Then, last, the offending names. This is not decoration: the guards that lacked it were answered
with a question instead of a fix, and the ones that carry it were answered with a fix.

### A PlayMode pixel test measures the camera you actually have

Two traps, both paid for in full:

- **`Bootstrap` gives `Camera.main` a `CameraController`, and it re-places the camera every
  `Update`** on an orbit around the world origin. A position set in `[UnitySetUp]` lives until
  the first frame and no longer. Use your own camera object, and **assert that it is still where
  you put it** — as a test, not as a comment.
- **`WorldToScreenPoint` projects into the camera's current `pixelRect`.** Clearing
  `targetTexture` before projecting silently switches that rect to the batch-run screen while you
  read pixels from a 512×512 render texture. Keep the texture assigned for the whole
  measurement, and assert `pixelWidth`.

Both of these produced a red test on correct code, and the "proof" that the occluder stood
between camera and target was itself computed from a CONSTANT camera position — so it would have
been green wherever the camera actually was. That is the shape of the trap: the check meant to
validate the bench was part of what the bench got wrong. See "Prove the harness before you trust
what it measures" above; this is that rule with a rendering bill attached.

### How a test reaches the thing it tests

- **Never reach into a private member by reflection.** `GetField` / `GetMethod` freeze private
  names as a public API: the member can no longer be renamed or moved, and the test goes green
  on a NullReferenceException instead of red on a real failure. Three suites did this and
  blocked every rename in ContextMenuUI. Instead: drive the UI the way a user does (find the
  widget by its GameObject name under the panel), or make the member `internal` and call it —
  `Assets/Scripts/Core/AssemblyInfo.cs` already grants `InternalsVisibleTo` to the EditMode suite.
- **A UI test must not depend on `Time.frameCount` advancing.** EditMode runs a whole test inside
  a single frame, so a frame-based guard is only half-testable there. Extract the counter into a
  small object the test can step by hand (see `FrameThrottle`) instead of asserting through Unity's
  clock.
- **A dropdown whose index is cast to an enum needs an order test.** `(OverlaySide)dropdown.value`
  silently breaks the day someone inserts an option. Assert the option list against the enum,
  do not describe the coupling in a comment.
- **A "X did NOT change" test cannot go red on its own.** Deleting the guarded code leaves it
  green, so the usual proof does not apply. Pair it with a positive control — a test that goes
  red on the SAME revert — or prove it by a break that ADDS the forbidden behaviour instead of
  removing the guard. Say in the commit message which of the two you used.
- **A "X does not get in the way" test needs a positive control on the SAME geometry.** Asserting
  that the door opens past its own drawer proves nothing if nothing there could block it — the
  test is green over empty space. Put a FOREIGN object in that exact spot in a paired test and
  assert it DOES block. Two such tests here were green against broken code until the controls
  were added.
- **Test a "does not get rounded / snapped / clamped" rule with a value that is NOT already at
  the boundary.** A test asserting "the grid leaves this height alone" was green against broken
  code because 900 mm happens to be a multiple of the 50 mm grid step — rounding could not move
  it either way. Pick a value that the transform WOULD visibly change (an oven at 595 mm sits at
  297.5), and pin the choice with an `Assume`, so the test starts failing if the constants ever
  make it degenerate again.
- **The tolerance of a test must be SMALLER than the artefact it guards against.** A test written
  with `Tol = 1 mm` physically cannot see a 0.5 mm overshoot — it passes over the very defect it
  was written for. Check not only what you assert but what you measure with.
- **Compare sweep results only between runs with an identical fixture `mtime`.**
  `docs/example.save.json` was rewritten by the running app three times during one measurement
  session (its size went from 1956428 to 1965547 bytes). Two sweep numbers taken across such a
  change are not comparable — see AGENTS.md for the full trap.
- **A fast `dotnet` cycle does not replace compiling under Unity.** A private method in a test
  subclass shadowing a base member is CS0108: `dotnet` stayed silent, Unity with `-warnaserror`
  failed. Compile under Unity before committing, even when the fast loop is green.
- **A sweep must not demand the impossible at its own boundary.** `SnapMutationTests` computes a
  trial position from the measured gap; on a pair sitting exactly at the threshold the arithmetic
  clamps to moving the part 1 mm FURTHER away — past the threshold — and then asserts that it
  snaps. The code correctly refuses, and the sweep reports a failure that is its own. Any test
  that derives a probe position from a measurement must assert that the derived position still
  lies where the expected behaviour is defined; otherwise the sweep is a false-alarm generator.
- **A test under `Assets/Tests/EditMode/Geometry/` is a CORE test, not a scene test.** That
  directory is globbed whole by `geometry/tests/Geometry.Tests.csproj`, so everything in it is
  compiled twice — once by Unity, once by plain `dotnet`. One scene test placed there broke
  `dotnet test` and Stryker with `CS0246` and stayed broken for months, because Unity compiled the
  same file happily and nobody ran the second build. The core's banned-symbol list applies to
  these test sources too, and `GeometryArchitectureTests.CoreTestSources_DoNotTouchTheEngine`
  enforces it. Scene tests go one directory up.
- **Feed a degeneracy check the exact degenerate value, built by hand.** `Quaternion.AngleAxis(180, axis)`
  does not produce a mathematically zero twist (w ≈ −4e-8), so a `len < 1e-8` guard never fired and
  the test could not go red. Write the literal — `new Quaternion(1, 0, 0, 0)` — instead of a value
  the API "should" return.

