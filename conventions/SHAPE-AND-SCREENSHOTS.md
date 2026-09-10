# Форма элемента и изометрические скриншоты

## A mesh and its metadata must describe the SAME shape

`CapsuleTableMesh.Build` drew an ellipse while `GetLegPositions` computed seating from a
"stadium". Both are round, both agree on a square, and the disagreement only appeared on a
stretched table — where the legs ended up outside the top.

When two functions describe one contour, their agreement needs a test on an ASYMMETRIC input.
A square, a cube, a 1:1 ratio — these hide the whole class of defect, and a test that uses one
is green against code that is wrong everywhere else.

The validation box counts as one of those descriptions. `ScrewLegElement` draws a Ø25 pad with
a Ø6 thread above it, while validation saw a single 25×58×25 AABB — and COL-01 is decided
ENTIRELY by AABB overlap, with no SAT behind it. A correctly installed leg therefore produced
two COL-01 and two GAP-02 against three neighbours it never touches: 3 of those 4 were pure
artefacts of the box, none of them reachable by being careful.

So: a new element type whose shape is NOT described by ONE box must declare a second body in
`ValidationSnapshot` in the same commit, and cover it with a test on an ASYMMETRIC neighbour —
one that sits inside the gross box and clear of the real geometry. `ValidationElement.RecessedBody`
is the existing precedent and the only sanctioned road; `GetVertices`/`GetFaces` are NOT it —
215 call sites depend on their 8-vertex shape, `EdgeOutlineRenderer` among them.

**A mesh CAP is cut by every opening that reaches it — at both edges, not just the bottom.** The
same door opening was fixed three times and came back twice, because each fix carried a sensor
that watched a different plane and none of them said so. `6613e108` cut the bottom cap and
guarded it on the floor plane (y = −0,5); `77fa83fd` cut the front face and guarded it at
z = +0,5; the horizontal slab at y = +0,5 — the wall's own TOP — belonged to neither, so it stayed
whole. At full height that slab sits at 2500 mm and nobody ever sees it. Lowering a wall does not
rebuild it, it SQUEEZES the mesh to `WallManager.LoweredHeightMM`, and the slab lands 100 mm above
the floor, 900 mm wide, straight across the doorway.

Two habits follow. Put the cut arithmetic in ONE place both caps call with their own edge
(`WallCapSpans.Reaches`), rather than writing it a second time per edge. And when a sensor
measures a single plane, **name in its comment the planes it does NOT see** — «this guards the
floor plane; the top cap is not covered here» would have made the third bug a lookup instead of
an investigation.

**A rule that has never fired proves nothing about itself.** Turning a field from hand-entered
into DERIVED switches on every rule that reads it, all at once, and those rules were written
against the hand-entered case. The screw leg's `attachedToName` was empty in practice, so
ATT-01 («the attached part came away from its parent») and LEG-01 (centring) had simply never
run on a leg. Deriving the link fired both on the first correctly installed leg in the project:
ATT-01 looked for a shared FACE, which a leg SCREWED INTO its host never has, and LEG-01 judged
displacement from the middle with a ±0,5 mm tolerance that knows neither the host's thickness
nor the thread's diameter. Neither was a new defect; both were rules that had been silent long
enough to look correct. So in the same commit that starts deriving a field, re-read every rule
that looks at it and ask what it does the FIRST time it actually fires.

**And a rule that judges a FRACTIONAL quantity must name its rounding and its tolerance.** LEG-01
compares the wall of material left beside the insert against a 3 mm threshold. Computed from
scene coordinates, a genuine 3,0 mm arrives as 2,9999998 — so a floor to whole millimetres turns
a correct installation red on the seventh significant digit, and it reads as a code regression
rather than as arithmetic. The floor and the epsilon that protects it (`ScrewLegSpec.FloorMM`,
`MM_ROUNDING_EPSILON = 0,001`) live in ONE place, are named, and are pinned by their own pair of
tests — «a whole millimetre is not lost to float error» and «2,99 is still two». Whoever states
the threshold states the rounding with it; otherwise the threshold silently floats.

**A bounding box cannot tell a correct cap from an inside-out one — judge a mesh by whether its
WINDING agrees with the normal it declares.** Six pipe fittings shipped with every end cap wound
against its own normal: the triangles existed, the vertices sat exactly where the box said, and
back-face culling ate all of them, so a plug you could see straight through passed 67 green
frames. No vertex- or box-based test can catch this; a per-triangle check that
`Cross(e1, e2)` points the same way as the declared normal catches it in milliseconds. Every
procedural mesh deserves that check, not only the one where it was paid for.

And read an isometric frame with its projection in mind before calling a defect: a box shows its
DIAGONAL — `(w + d)·cos30` — while a cylinder inside it shows only its diameter, so a correct
box looks far too large for a correct tube. That reading cost one wrong instruction here; the
real inflation was latent and appeared only when the derived bore exceeded the nominal frame.

**Накладка, лежащая на детали, не имеет права кончаться в её плоскости.** Стекло и панель
управления духовки заканчивались ровно там же, где плита дверцы, и смотрели туда же — пять пар
копланарных граней и мерцание на лице прибора (z-fighting). Накладка либо утапливает несущую
деталь на свою толщину, либо выступает вперёд; общая толщина при этом не меняется, меняется кто
держит лицо. «Сдвинуть на 0,1 мм, чтобы не мерцало» — не починка, а отложенный тот же дефект.
Сенсор дешёвый и элементо-независимый (`OvenCoplanarSurfaceTests`): пара граней, смотрящих в ОДНУ
сторону, ближе N мм друг к другу и перекрывающихся площадью. Встречные нормали законны — их
съедает отсечение задних граней, и сенсор обязан их пропускать, иначе утонет в стыках коробок.

Сенсор обобщён в `CoplanarSurfaceDetector` и прогоняется по всем типам элементов
(`CoplanarSurfaceCoverageTests`, список типов — из `EveryElementType.Declared()`, а не руками);
посудомойка оказалась вторым случаем ровно того же дефекта. У круглых деталей боковая грань AABB —
касательная, а не плоскость: такие находки разбирают по мешу, а не заносят в исключения не глядя.

## Isometric screenshot tests (REQUIRED for new elements)

Every new element type MUST have a PlayMode isometric screenshot test. This visual regression test ensures the element renders correctly.

**File:** `Assets/Tests/PlayMode/IsoScreenshotTests.cs`

**A frame suite MUST derive from `ElementFrameTests`.** That base class is where the frame is
actually taken: it asserts the element is free of violations, THEN switches the validation tint
off, and restores it in `TearDown`. Skip the base and your suite photographs the validation
INDICATOR instead of the material — which is what every iso frame did for months, showing a
green tint for a valid element and a pink one for an invalid one, and the real chrome, oak or
enamel never once. `ElementFrameCoverageTests` enforces this: no suite may read pixels itself
or touch `ViolationTintVisible`, and it proves it found the suites before trusting an empty result.

**Pattern:**
```csharp
[UnityTest]
public IEnumerator Iso{ElementType}_{Variant}()
{
    var dims = new Vector3Int(width, height, depth); // mm
    Vector3 pos = new Vector3(0f, dims.y * 0.5f * AppConstants.MM_TO_UNITS, 0f);
    var go = ElementFactory.Create{ElementType}(dims, "Iso{ElementType}", pos);
    _spawned.Add(go);
    Assert.IsNotNull(go.GetComponent<{ElementType}Element>(), "почему это должен быть тот тип");

    yield return RenderElementIso(go, "iso_{element_type}_{variant}.png", 2.5f);
}
```

**Stand the element on the floor slab, and give a small one its own camera.** Two mistakes that
have now been paid for twice each — by the socket and the light switch, then by all six pipe
fittings at once. First: creating the element at `Vector3.zero` is COL-01 against `BasePlate`,
which occupies y from −18 to 0 mm, so the lower half of the validation box sits INSIDE the slab
and the frame test refuses to shoot. Use `StandOnFloor`, which lifts by the VALIDATION box, not
by half the physical height. Second: the shared iso camera clamps its distance to 0,5 m, so its
frame is never narrower than ~414 mm — a Ø33,5 coupling in it is a twelfth of the width, and
«I cannot see it» then says nothing about the mesh. Anything under ~150 mm takes
`CreateCloseUpCamera`. Both lessons lived only in summaries inside `IsoScreenshotTests.cs`,
where the author of the NEXT frame never looks.

**Frame the shot from the world AABB of the renderers, never from the root.** `RenderElementIso`
does this: it waits a frame (children are built in `ApplyDimensions`/`Start`), unions the bounds
of every renderer under the object, builds the camera from that, and then **asserts that all
eight corners of the box land inside the frame**. A screenshot test that only writes a PNG
cannot fail; the fit assertion is what makes it a test.

The older version of this template took `GetComponent` on the ROOT and computed the frame from
the root's `dims`. That is wrong for a composite element, and three of them are composite:

- `Cooktop`, `Oven`, `Dishwasher` are built by `ElementRoot.NewEmpty`, so the root carries **no
  renderer at all** — there is nothing for the template's `GetComponent` to measure.
- Their child boxes are not centred on the pivot: the cooktop hangs entirely *below* it (a 5 mm
  plate above zero, the cutout box under it), so a camera aimed at the root position looks over
  the top of the part.
- Geometry may leave the declared box: the oven's handle protrudes `HANDLE_PROTRUSION_MM / 2`
  past half of `DEPTH_MM`, so a frame sized from `dims` crops it.

`IsoAppliances_KeepTheirGeometryInChildren_NotOnTheRoot` pins all three facts, so the shortcut
cannot come back quietly.

**Output:** PNG files go to `test-results/` (gitignored).

**Naming:** `Iso{Feature}_{Variant}` — e.g. `IsoTable_1200x750x600`, `IsoDrawer_TypeC_500`.

