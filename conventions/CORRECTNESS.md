# Корректность: жизненный цикл, атомарность, стражи, долги

## CRITICAL: `OnDestroy` is a Unity message, not an override

Unity calls `OnDestroy` by name. Declare one in a subclass and the private base `OnDestroy`
stops being called — silently, with no compiler warning and no override keyword to notice in
review. Everything the base did goes away: in this project that is deregistration from
`PartRegistry`, and two element classes shipped broken that way until a test found them.

**A subclass of `KitchenElement` may not declare `OnDestroy` at all.** There is exactly one in the
layer — the base one — and it deregisters, destroys the element's own mesh and then calls the hook
`protected virtual void OnElementDestroyed()`. Extra cleanup (children, materials, mounts) goes in
that override; being a real `override`, it cannot silently replace the base work. "Repeat the
deregistration in your own `OnDestroy`" was the earlier, weaker cure: it relied on eleven copies of
one line, and it did nothing for the mesh, which the base destroys through a member the subclasses
could not even reach.

A procedural mesh belongs to the base too: build it and hand it over with
`protected void AdoptOwnedMesh(Mesh)`, which destroys the one it replaces. A `Mesh` is a
`UnityEngine.Object` — the GC never collects it, so a subclass keeping its own field leaks one mesh
per rebuild and one more per destroyed element.

`UnityMessageShadowingTests` (source scan, `Assets/Tests/EditMode/Geometry/`) fails the build on any
subclass that declares a Unity message its base declares; it carries tests for its own scanner,
because a scan over a path that stopped resolving passes while checking nothing.
`ElementMeshLifetimeTests` (`Assets/Tests/PlayMode/`) checks the same thing by object lifetime —
after the element is destroyed, its mesh and its material must be destroyed too. **That check must
live in PlayMode**: outside Play mode Unity calls neither `Awake` nor `OnDestroy`, so under EditMode
it is green no matter what the code does.

The same trap applies to any Unity message the base class implements privately — `Awake`,
`OnEnable`, `OnDisable`. Before adding one to a subclass, check whether the base has it.

### `public new` over a non-virtual base member is the same trap, with a keyword

`new` does not replace the base member, it hides it — and only for callers who happen to hold
the subclass type. Everyone holding the BASE type still reads the base field, so the object now
has two independent values for one concept.

`TableElement` declared `public new string MaterialId` because the base property was not virtual.
The tabletop and legs slots wrote the subclass field; the whole rendering layer holds elements as
`KitchenElement` and read the base one, which nothing ever updated. That single shadow produced
three separate user-visible bugs — the wrong colour applied on click, editing the legs repainting
the tabletop, and the decor vanishing on deselect.

If a subclass needs to change a base member's behaviour, make the base member `virtual` and
`override` it. `new` on a member the base actually uses is a defect; if you cannot make the base
virtual, that is the problem to solve, not to route around.

## A gate that can only refuse must have somewhere to fall back to

A check shaped "do not trust this candidate" turns working behaviour into NO behaviour whenever
the fallback it assumes does not exist. Rejecting an edge-contact snap left parts that had moved
inside a neighbour with no snap at all — 19 regressions that the targeted tests did not see and
only the brute-force sweep caught. Before adding a refusal, name the result it falls back to and
assert that it exists.

**And narrowing a condition is not taking a subset.** Replacing `A && B` with `A && C` reads like
tightening, but `C` is true in places where `B` was false, so the change both removes and adds
behaviour. A change of that shape needs the sweep, not a handful of pointed tests.

**Шлюз «не пущу» обязан спрашивать про РАЗНИЦУ, а не про состояние.** `BlockOnViolation`
спрашивал «нарушает ли деталь ПОСЛЕ правки» — и деталь, которая уже нарушает (висит в воздухе,
не на что опереться), перестала редактироваться вовсе: отступать шлюзу было некуда, а починить
такую деталь можно только правкой. Сравнивай снимки до и после по СОСТАВУ — пара (деталь, код
нарушения), а не число находок, — и блокируй только тот отпечаток, которого раньше не было.
Пересечение записывается диагностикой один раз на пару, поэтому в снимок оно кладётся в обе
стороны: иначе исход зависит от того, каким индексом деталь попала в широкую фазу.
**И называй отказ вслух:** молчаливый возврат полей к прежним значениям неотличим от «панель не
работает», а пользователь не может найти этому следов ни в сцене, ни в истории.

## Atomic means two phases, not a sentence in the description

A tool that advertises "if ANY item is invalid, NOTHING is applied" must validate in a phase
that completes BEFORE the first mutation. Validating and applying in one loop leaves the
already-applied items behind on the first rejection, and the description keeps promising
otherwise. `create_elements` carried that promise in its text for years while spawning inside
the validation loop.

The proof is a specific test: a batch of one BAD item plus one GOOD one, asserting the good one
is **not** in the scene afterwards. A promise in a tool description without that test is prose.

## Deleting or inverting a TEST is not free either

A test that pins deliberate behaviour cannot be quietly rewritten when the behaviour changes.
Say in the commit message that the test was inverted, what it used to assert, and whose
decision changed it. The cost is the same as for deleting a comment: without the note, the
history shows a behaviour change that looks like an oversight.

## Error message ORDER is part of the contract

When several validation errors are joined into one string (` | `), the client sees their
order. A refactor that moves a check must keep its position — which is why the rule set has to
be an ORDERED array (`EditFieldRules`), not a dictionary and not a scatter of separate methods.
A test that asserts only the SET of messages will not catch a reordering; assert the string.

Where a new rule goes in the array is not a matter of taste: **it takes the position its field
has in the params type**. `EditFieldRules` mirrors the field order of `EditOp`; put the check
anywhere else and the client sees the messages in an order that no longer matches the shape it
sent.

## Known debts — recorded, not blessed

These are real and none of them is an accident of ignorance: each was found, measured and left
deliberately, because closing it belongs to its own change. A guard holds every one of them, so
none can quietly grow; what a ratchet cannot express is written here.

- **`PillarElement`'s UVs.** The side is unwrapped as `i/Segments` around the CIRCUMFERENCE while
  `BaseMap_ST` scales by the DIAMETER — the decor is stretched by a factor of π. `DecorSurfaceMM`
  alone does not cure it: the disagreement lives in the mesh, so the fix is `u = arc length /
  diameter`, the way the floor's edge band was fixed. `RadialShelfElement` has the same disease in
  a milder form and IS cured by `DecorSurfaceMM = (width, depth)`.
- **A floor's UV origin is not rebuilt when the slab is MOVED.** The unwrap is anchored to the
  world origin on purpose (so growing the contour does not slide the tiling), but moving the slab
  leaves the old anchor, and the first contour edit after a move shifts the picture once. Floors
  are moved rarely; the cure is rebuilding the mesh on a transform change.
- **`ContextMenuMaterialSection.ApplyLegsChoice` bypasses `CommandStack`** — that edit is not
  undoable.
- **`Assets/Scripts/Tests` is compiled by Unity without `-warnaserror` and `-nullable`** — it has
  no `csc.rsp` of its own. `Core/Geometry` had the same hole and is now covered by the second build.
- **~40 style rules in `.editorconfig` are declared `:error` and enforced NOWHERE**
  (`EnforceCodeStyleInBuild` is unset). Switching them on costs ~2864 violations in the core alone
  — its own campaign, not a side task. `IDE1006` (naming) is the one rule already at zero.
- **`BoxGaps` has 19 mutants and 0% mutation coverage** — the class sits in `Core/Geometry`, but
  its tests build `GameObject`s and therefore live in the Unity assembly. The test is in the wrong
  build, which is a different defect from having no test.
- **`ElementData.midHeightMM` defaults to the literal `75`**, a copy of
  `PillarElement.MidHeightMM_Default` that the pure build cannot reference because the constant
  sits on a `MonoBehaviour`. Nothing pins the two together; the cure is a pure `PillarSpec` beside
  `LampSpec`.
- **28 layer→UI references**, ceilinged per layer by `LayerDependencyDirectionTests` (that test
  is the source of truth, not this line: its budgets only ever fall, and the biggest is
  `Rendering` at 9).
- **`GameContext` cannot move to the fast path**, and the reason is not its construction — that
  was inverted. Five service interfaces speak in scene types (`KitchenElement` in four of them,
  `GameObject` and concrete element types in `IElementFactory`, 25 mentions). Moving the class
  would trade one impossibility for another; the real cure is the snapshot therapy `ValidationCore`
  already received, and it is a campaign of its own.
- **Commit `21570682` does not build on its own** — two agents edited one file in a shared tree.
  HEAD is fine; `git bisect` through that point is not.

## MCP board conventions

- Naming: `{Module}_{Side}`. `dimZ` = board thickness
- Frame depth = module − 18mm
- Side panels: 720mm height. Facade height = 716mm. No back panels

## A silent fallback is an outage, not a fallback

A chain of "try this shader, then that one, then the other" looks defensive and is the opposite:
it converts a hard failure into a quiet behaviour change that no test and no user can name.

`HandleMaterials.FindShader()` tried `Hidden/KD/HandleOverlay`, then `Resources.Load`, then
`Universal Render Pipeline/Unlit`, then `URP/Lit`, then `Sprites/Default`. The first two are the
overlay shader with `ZTest Always`; the last three are ordinary depth-tested shaders. Falling
through meant the transform handles stopped being visible through geometry — the one property
that makes them usable — with no error, because the only log line covered "nothing found at all",
not "found the wrong thing". `ElementOutline` had the same chain and DID fall through in every
build.

Rules:

- **A fallback that changes behaviour must be loud or must not exist.** If the substitute cannot
  do the job, throw or log an error; do not degrade in silence.
- **`Shader.Find` only sees shaders that reached the build.** A `Hidden/` shader referenced by no
  material in any scene is stripped. Putting it under `Assets/Resources/` keeps it — that is
  proven, both `Hidden/KD/HandleOverlay` and `Hidden/KD/UnlitColor` survive that way.
- **In the editor everything is found, so the editor cannot answer this question.** Verify
  against the built player:

  ```bash
  grep -rasl "Hidden/KD/UnlitColor"            Build_Debug/KitchenDesigner_Data
  grep -rasl "Universal Render Pipeline/Unlit" Build_Debug/KitchenDesigner_Data   # must print NOTHING
  ```

  A shader that is present appears in `resources.assets` and `globalgamemanagers`. `URP/Unlit`
  prints nothing at all — that absence is the evidence, and it is why a chain starting with it
  never once did what it claimed.
- `SideHighlighterShaderTests.FallbackShaders_ExcludeTheBuiltInPipelineOnes` already guarded one
  such chain. The diagnosis was paid for, written down, and then repeated in a second file that
  nobody checked. **When you fix a class of defect, grep for the pattern, not for the symptom.**

## Правило связи живёт там, где ВЫБИРАЕТСЯ посадка, а не там, где о ней докладывают

Геометрия садит устье в устье по зазору и встречной оси и про запреты не знает. `PipeConnectionRule`
(«труба не соединяется с трубой») спрашивали `PipeJoint.Connects` и `PipeRunFit.ForRun` — то есть
уже ПОСЛЕ того, как посадка случилась. Магнит радостно стыковал две трубы, сеть отказывалась видеть
связь, и пользователь получал два `PIP-01` «открытый конец» на стыке, который сделал руками и не мог
убрать перетаскиванием; вдобавок оба порта числились свободными, поэтому ремонт по сетке пересаживал
эти трубы друг на друга снова и снова.

Спрашивать правило нужно при построении **списка кандидатов** — тогда ему подчиняются все пути
разом: отпускание мыши, покадровый магнит, снэп при ресайзе, доседание после спавна, ремонт по
сетке и оракул `snap_diagnose`. Последний важен отдельно: если правило не дошло до него,
диагностика начнёт врать раньше, чем сломается код. Запрещённому партнёру снимают УСТЬЯ, а не
выбрасывают его целиком, — иначе детали перестанут липнуть друг к другу гранями.

И тот же вопрос рядом: «а не занято ли устье?». Список устий строится из СЕТИ, а не из сцены —
иначе два претендента садятся на одно устье, сеть выбирает одного, а второй молча становится
открытым концом. Исключение одно: устье, на котором деталь сидит сама, для неё остаётся
предложенным, иначе подгонка пролёта перестаёт работать там, где она и нужна.

## A guard must ask its question of the thing it protects

A rule bought with one symptom will be written at whatever granularity that symptom had, and
that granularity is usually too coarse. Selection restored an element's materials only
`if (!MaterialManager.HasCustomDecor(element))` — bought honestly by `ef4cd6ba`, where picking a
texture on a SELECTED table had the remembered material undo it on deselect. The fix asked about
the ELEMENT; the thing it protects is a RENDERER. So a drawer, the one type whose factory assigns
a non-default decor at birth (`gtv_white`), matched "has its own decor" from its first second
alive and never got its material back — the yellow stayed forever. In the app the same rule hit
every element on which a texture had ever been chosen: the decorated mesh was repainted, the
glass, the chrome and the legs stayed yellow, silently.

Both answers are "correct", which is why no test noticed: «does this element have a decor» and
«is our tint still sitting on this renderer» are both true statements about the world, and only
the second one is about the thing being restored. When a guard turns an action off, check that
its subject is the same subject the action has. If the action is per-renderer, the guard is
per-renderer; if the action is per-field, so is the guard.

## Reading `renderer.material` is a MUTATION, not an observation

Unity replaces the renderer's material with a private copy the moment you touch the `material`
property, and hands you the copy. So any code that later compares `sharedMaterial` **by
reference** against a material it put there sees a stranger — and skips whatever it was going to
do.

This cost two rounds. Selection remembers the tint it applied and restores the original only if
that tint is still on the renderer, which is right (see «A guard must ask its question of the
thing it protects»). But `ElementMover.SaveDragMaterial` does `_dragOriginalMaterial =
renderer.material` at the start of EVERY drag, and a single line in a test — `renderer.material
.GetColor(...)` — does the same. After either, the reference check fails, restore is skipped, and
the yellow stays on the object forever. In the app that meant any part ever dragged with the
mouse.

So recognise your own material by its NAME, not its address: the clone inherits the name (with
` (Instance)` appended), the reference it does not. Sign what you paint
(`SelectionManager.TintMaterialName`) and match on the prefix. And when a test needs to read a
colour off a renderer, know that the read itself changed the scene — assert with `Assume` that
the copy happened, so the test goes inconclusive rather than falsely green if Unity ever stops
doing it.

## State a rule by its MECHANISM, not as a list of the cases you happened to fix

«Selection, decor and validity tint must paint the whole element» reads like a rule and behaves
like an inventory: it fixes exactly the three things it names. Dragging paints the element too,
through the same `GetComponent<MeshRenderer>()` and the same read-then-restore of
`renderer.material` — and it stayed broken through four commits that were all, individually,
about "painting the whole element". A chair being dragged lit up its seat alone; a door, a
window, a toilet, a socket and a light switch lit up nothing at all, because with no mesh on the
root the method returned immediately.

So write the rule about the mechanism: **any paint this app puts on an element covers
`ElementRenderers.BodyOf` and is given back per renderer, recognised by the NAME it was signed
with.** Phrased that way, the next consumer of the same mechanism is covered before it is
written, and the guard that enforces it can walk every element type through the real factory
instead of naming the three you remembered.

One detail that phrasing must keep: each kind of paint signs itself separately
(`ElementTint.SelectionName` vs `DragName`). A single shared signature would turn «is this MY
paint» into «is this ANY of ours», and dropping a dragged element would strip the selection tint
underneath it and put back a material that had gone stale.

## Not only the guard — every READER of a state must ask through one function

The rule above is about a guard turning an action off. Its mirror image bites when a state gains
a new value: whoever READS that state must read it through a single function, or the new value
appears in some readers and not others.

«Is there edge banding on this side» was asked in four places independently — the specification,
the MCP codec, the dark strip drawn on the 3D model, and the panel's own colouring. Adding a
«remove it» state would have reached three of them; the side would have vanished from the cut
list while still being drawn on the model and still reported by `get_element`, and every existing
test would have stayed green, because each reader was individually correct.

So when a state grows a value, first count its readers — `grep` for the question, not for the
field — collapse them onto one function, and put an architecture test on that function so the
fifth reader cannot appear beside it.

## A derived field needs ONE writer and one occasion to call it

The mirror of the rule above. A value computed from the scene — the screw leg's host, and the
insertion depth that follows from it — was written in three places somebody had remembered:
scene restore, duplication, and the end of a mouse drag. Everything else moved the geometry
without telling it. Dragging the HOST instead of the leg, resizing the host out from under the
thread, a group drag where the leg is not the drag target, a leg carried along as a follower,
undo, redo, and every one of the fourteen MCP mutation tools left the field holding an answer
that used to be true.

That is not a list of missing calls; it is a missing occasion. The scene already knew when to
ask — `SceneChangeTracker.Poll` watches `transform.hasChanged` every frame, and the MCP surface
already funnelled every mutation through one post-step. One function called from those two
places covers all of it, including the cases nobody has thought of yet.

**An effect that leaves the process gets ONE adapter, and "are we under a run?" is asked only
there.** Sound, network, writes into someone else's file — anything the machine notices — must
pass through a single seam, because a list of tests that "remembered to turn it off" is not a
mechanism and goes stale at the next test. The music player started itself in `Start()`, so
every scene a test raised began playing out loud on the user's machine, and the restore path
started it a second time. The cure was not a `Stop()` in each `[SetUp]` but one adapter that
owns the `AudioSource`, plus a scan proving the whole app touches that type in exactly one file.
Note which fact the seam asks for: not «is this a test» sprinkled through the code, but one
door, and preferably learned two ways — a framework hook that nobody has to remember to call,
and `Application.isBatchMode`, which the gateway makes true and a shipped build never does.

**A derived value must never move the thing it was derived FROM.** One writer and one occasion
guarantee freshness, not stability: close the loop and the same discipline produces an
oscillation instead. A pipe fitting takes its bore from the pipes that meet its mouths; had the
mouths then moved to match that bore, the joint they were measured at would come apart on the
next pass, the bore would go blank, the frame would snap back to nominal, and the fitting would
flicker between DN20 and DN50 forever — with every rule about writers and occasions obeyed. So
the frame stays put and only the body follows. The sensor for this is cheap and belongs on any
derived value that feeds geometry: **a second pass over an unchanged scene must change exactly
nothing.**

**The composition of the scene is an occasion too, not only the poses in it.** Both occasions
above watch things that MOVE, and an element that has LEFT the registry moves nothing — nobody
touched a transform, so nobody asked, and the leg went on naming a host that no longer exists.
The mirror case is worse because it looks healthy: an element that ENTERS the registry was
settled only because it usually drags its transform along the way, which is luck wearing the
costume of a rule. Registration and unregistration are the occasion; say so, and the field stops
depending on how the element happened to arrive.

Two things this buys beyond correctness. The panel needs no special code: it re-reads the field
every frame, so a field that is right is displayed right. And the cost is knowable — hoisting the
candidate list out of the per-leg loop turned `L × N` geometry builds into `N`, which is about
half of ONE of the two full validations a drag already pays for per frame, and exactly zero when
nothing moved. A field updated «where we remembered» differs from a stale one only by luck.

## Откат без сказанного вслух отказа — это дефект, а не тихий успех

Перетаскивание и ресайз откатывались МОЛЧА: деталь возвращалась на место, и для пользователя
это выглядело как «инструмент не работает». Текст отказа при этом существовал, но был частной
собственностью панели свойств. Общим обязано стать РЕШЕНИЕ и его формулировка — `EditGate.Refuses`
одним вызовом отвечает «отказано» и выдаёт текст, — а не способ отката: панель откатывает снимок
свойств, `ElementMover` ещё и пересборку трубных пролётов, и сводить их силой в одну процедуру
значит потерять часть отката.

И сенсор на полноту отката снимает отпечаток ВСЕЙ сцены до и после, а не тех полей, которые
кто-то вспомнил: у каждого из трёх путей был свой тест про свои поля, и ни один не спрашивал
про соседа, поэтому разница в полноте не ловилась ничем.

## Деталь стоит на том, что под её ПЯТНОМ

`PillarAutoFit.FloorUnder` считал полом всё, что проходит в 50 мм от ЦЕНТРА опоры, — и
загрузка сажала опору на дно соседнего цокольного ящика, поднимая её на 20 мм молча, без
команды и без отмены. Первое же сохранение закрепляло подмену, а пользователь вручную
стаскивал ноги обратно. Допуск от точки превращает соседа в подставку: сравнивай ГАБАРИТЫ,
а не точку с припуском.

## Краска, которая обязана читаться СКВОЗЬ геометрию, берёт шейдер у ручек

`Hidden/KD/HandleOverlay` с `_BaseColor` — то, чем ручки трансформации видны через детали.
Шейдер накладки кромок (`Hidden/OverlayLine`) для этого не годится: он возвращает ВЕРШИННЫЙ
цвет, а на мешах элементов цветового потока нет, и прокрас выходит никакой.
