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

Updated 2026-09-01. Keep this current: it is the only place that says where the work stands.

### Where the day left it

| | |
|---|---|
| Guards in `Assets/Tests/EditMode/Geometry/` | **12**: comment ratchet, engine boundary, colour literals, tolerance literals, decor UVs, `new` over base, Unity message shadowing, `ElementKind` single source, layer direction, new-element-type completeness, test-quality ratchet, mutation baseline |
| Mutation gate — `geometry/mutation-baseline.txt` | `core` **72,42 %** / порог 72 (1039 мутантов, 2 мин 44 с), `pure` **57,26 %** / порог 57 (463 мутанта, 1 мин 27 с) |
| Inner loop (`dotnet`, core + pure) | **446 tests, 0,3 s** — was 226 |
| EditMode / PlayMode | **2860 / 92**, both green |
| Cold Unity: one class / full EditMode / PlayMode | **~11 s / ~73 s / ~91 s** — was 18,4 / 79 / 101 |
| Comments left in `Core` | **3131**, ceilinged per directory by `CommentRatchetTests.Budgets` |

**Done from this plan:** A1, A2, A3, A3b, A3c, A4, A5, A5b, A6, A8, B1, B2, B3, B4, C1, C2, C3.
**Left: D, E** — see "What is actually left" at the end of this section.

**Defects found and fixed today, none of them by reading code:**

- Two LIVE mesh leaks (`PillarElement`, `RadiusTableElement` rebuilt a `Mesh` per
  `ApplyDimensions` and destroyed neither the old one nor the last). The defect the task
  described — grooves — was latent; these were not. `21570682`.
- **Every floor in every project** tiled its decor 20× too dense: `DecorSurfaceMM` fell back to
  `(dims.x, dims.y)`, and a floor's second axis is depth, not the 100 mm slab thickness.
  `55c3d52e`.
- Two leaks on the server side, both hidden behind a silenced `CA1001`: a `SemaphoreSlim` per
  closed editor tab and the cleanup timer. `4fb68669`.
- `UpdateScrollSmooth(dt)` ignored its own `dt`. `d32e6209`.

**Two facts about TESTS that cost more than the fixes:**

1. **EditMode does not run Unity lifecycle messages.** A mesh-lifetime test written there is
   green forever — the registry still held all seven "destroyed" elements. Such tests live in
   PlayMode; now written down in CONVENTIONS.md.
2. **A test that reads a clock is not a test.** Three scroll tests passed alone and failed in the
   full run, and two runs of the same code gave 0,2396 and 0,2785 for an expected 0,3. Drift
   means the environment is an input; a broken behaviour gives a stable wrong answer.

**Done from this plan:** A8 (generated-artefact parity guard), B3 (server projects as strict
as Unity), **the first two slices of C2** (`5b53ae45` and this commit), and **A3c, A5, A5b**
— три сторожа на ловушки, каждая из которых уже стоила боевого бага:
`NewOverBaseMemberTests` (никакого `new` над членом базы, стартовал с нуля нарушений),
`UnityMessageShadowingTests` (сообщения Unity целиком, а не один `OnDestroy`; знает, что
обязан повторить наследник, и запрещает пару, для которой это не записано) и
`ElementKindSingleSourceTests` (роль элемента собирается только в `ValidationSnapshot`).
Все три читают ИСХОДНИКИ и лежат в `Assets/Tests/EditMode/Geometry/`, поэтому идут и под
`dotnet` — краснота каждого доказана временным нарушением.

**A6 — храповик, а не запрет с нуля.** `LayerDependencyDirectionTests` считает ссылки каждого
слоя ядра на слой UI (`using KitchenDesigner.Core.UI` или квалификатор `UI.` — UI и MCP живут
в своих пространствах имён, поэтому ребро видно точно) и на слой MCP. Сегодня таких ссылок
**30**, потолки записаны по слоям с причиной у каждого долга: Rendering 9, Snap 6, Persistence 4,
Update 4, Elements 2, Infrastructure 2, Measure 2, Commands 1, остальные 0. Превысил — красное с
именами строк; стало меньше, а потолок не опущен — тоже красное. Проверяются ВСЕ каталоги слоёв,
а не строки таблицы, поэтому новый слой попадает под правило сам, с нулём. Про MCP не знает
никто, кроме `Bootstrap`.

**Not started:** everything else — A1, A2, A3, A3b, A4, B1/B2/B4, C1, C3, most of D,
all of E.

**C2, first slice — the home exists and is guarded.** `Assets/Scripts/Core/Pure` +
`Assets/Tests/EditMode/Pure`, globbed by `geometry/pure/Pure.csproj` and
`geometry/pure-tests/Pure.Tests.csproj`; `mutation-test.ps1 -TestsOnly` now runs **354 tests in
0.25 s** (239 core + 115 pure). Moved: `ExpressionParser`, `VersionUtil`, `DragGesture`,
`SceneSettleThrottle`, `WallCutaway`, `UpdateCoordinator` + `UpdateInterfaces` + `UpdateStrings`,
`EventBus`. Guarded by `PureSources_DoNotTouchTheEngine` and its two siblings.
**Still owed the Unity run** — the machine was busy; nothing in the slice touches scene code,
but the folder `.meta` files Unity generates on import are not committed yet.

The plan's estimate of 31 movable files was too optimistic, and the reason is worth keeping:
it counted test files that name no scene type, but what decides is whether the PRODUCTION class
loads under CoreCLR. Three walls, in order of how often they will come up again:

- **`JsonUtility` does not compile at all** on the second path — it is in
  `UnityEngine.JSONSerializeModule`, not `CoreModule`. That is what holds back `TextureIndex`
  and `ReleaseManifestParser`. Splitting the DATA type out is the move that works
  (`ReleaseManifest` did exactly that and let its coordinator through).
- **`Debug.Log` is an ECall** and throws at runtime. `UpdateCoordinator` now takes an
  `Action<string>`; the same fix applies wherever else a pure class logs.
- **A pure class reaching into a `MonoBehaviour` for a constant or an event payload.**
  `SidebarCatalog` needs `PillarElement.TopDiameterMM`; `EventBus`'s event structs carry
  `KitchenElement` — that one split cleanly, the constant has not been dealt with.

**C2, second slice — the settings and the grid.** `KitchenSettings`, `ViewPreset`, `ViewField`,
`PhotoQualityPreset`, `WorldBounds`, `KitchenSettingsData`, `CommandRecord`, the `EditMode` enum
and `GridManager` are on the fast path; the loop runs **380 tests** (239 core + 141 pure).

The wall here was not compilation — it was CoreCLR. `KitchenSettings` was a `ScriptableObject`,
and under `dotnet` even `new KitchenSettings()` throws `SecurityException`: the base constructor
calls `Internal_CreateScriptableObject`, and `Resources.Load` and `ScriptableObject.CreateInstance`
are ECall too. So a `ScriptableObject` COMPILES on the second path and cannot be INSTANTIATED
there — add that to the list of three walls above, it is the fourth and the least obvious.

It was resolved by dropping the `ScriptableObject`, because the asset carried nothing:
`Assets/Resources/KitchenSettings.asset` held the field initializers value for value, plus three
keys (`_edgeOutline`, `_wallsEnabled`, `_lowerNearWalls`) whose fields had been gone for a while.
Settings persist through `ProjectData.settings`, not through the asset. `KitchenSettings.Instance`
now news up a plain singleton, the asset and `ProjectSetup.EnsureKitchenSettings` are deleted, and
`CameraController`s three "no asset — use a factor of 1" fallbacks became unreachable and went.

**C2, third slice — the save format is on the fast path.** `ElementData` and `ProjectData` (with
`GrooveEntry`, `TextureOverlayEntry`, `GroupData`, `RoomData`, `FloorplanScopeData`,
`WindowStateData`, `CameraState`) now live in `Assets/Scripts/Core/Pure/Persistence`; the loop runs
**150 pure tests** (was 141) and **434 in total**. Not one field name changed — a field name is a
JSON key, and the whole point of the move was that the format stays byte-identical.

What actually held them was the scene reflection plus three types the second build could not see:

- `ElementData.FromElement(KitchenElement)` — 205 lines of `is XxxElement` / `GetComponent` — is now
  `ElementCapture.FromElement` in `Core/Persistence`. Data and the assembly of data from a scene are
  two responsibilities; only the first one can leave Unity.
- `TextureOverlaySpec` (a serialized field type of `ElementData`) moved to `Core/Pure/Elements` with
  it — plain arithmetic over `RectInt`, nothing scene-side.
- `LampShape`, `LampShadow` and the light constants left `LightSourceElement` for `LampSpec` in
  `Core/Pure/Elements`. That is the "a pure class reaching into a `MonoBehaviour` for a constant"
  wall from the first slice, cured rather than routed around.
- `MaterialCatalog.DefaultId` is now `AppConstants.DEFAULT_MATERIAL_ID` and the catalog reads it from
  there. The catalog binds `Texture2D` and stays in Unity; the default id is data.

`SaveFormatDefaultsTests` (pure) guards the format the way "Snapshot tests" asks: every reference
field of every save-format type must carry an initializer, every list-shaped array must start
empty, the coordinate tuples keep their length, and the numeric defaults are compared against the
same constants the elements use. Redness proven three ways — `grooves = null!`, a light default
changed by hand, `rotation` shortened to three floats. Two exception lists, each entry with a
reason and a test that the named fields still exist.

`GameContext` still needs its `*Instance` construction inverted — `InitializeWithDefaults` news up
concrete Unity-side services.

**A1 is the one that matters most and has not moved.** `Core` holds **3131** comments. The purge
covered `Elements`, `Persistence`, `Infrastructure` and `Platform` before this plan was written,
which is why they already read zero above; nothing has changed since. Remaining:
`UI` 641 · `Rendering` 479 · `Snap` 318 · `MCP` 309 · `Materials` 287 · `Geometry` 242 ·
`Validation` 193 · `Measure` 125 · `Pure` 89 · `Commands` 100 · `Analysis` 104 ·
`Diagnostics` 74 · `Update` 53 · `Bulk` 51 · `Tools` 48 · `Interfaces` 8 · `Networking` 8.
The live ceilings are in `CommentRatchetTests.Budgets`; that table, not this paragraph, is what
fails the build.

### What is actually left

In the order I would take it:

1. **A1's second half — spend the ratchet.** The guard exists, the count does not move on its
   own: 3131 comments, of which `UI` 641 · `Rendering` 479 · `Snap` 318 · `MCP` 309 ·
   `Materials` 287 · `Geometry` 242. Purging is what Part E's refactoring produces as a side
   effect, so pair them by directory rather than running a separate purge campaign. Every
   deletion still owes case (a)/(b)/(c), and the ceiling drops in the same commit.
2. **C2's fourth slice.** `ElementData` and `ProjectData` are done (third slice above).
   `GameContext` is what is left: `InitializeWithDefaults` news up concrete Unity-side services
   (`PartRegistryInstance` and the other `*Instance` types), so the class does not load under
   CoreCLR. It needs the construction inverted, not the class moved.
3. ~~**A7 — new-element-type completeness.**~~ **Done** — `ElementTypeCompletenessTests`.
   The type list is DERIVED from the sources (every class whose inheritance chain reaches
   `KitchenElement`), so a new type falls under the rule without editing the test. Six
   mandatory places, each with a key read out of the code rather than out of the checklist:
   the factory (`CreateXxx`), the duplicate registry, scene restore, `ElementSelector.TypeOf`,
   the MCP layer, the isometric screenshot test. It found eight real gaps on the first run —
   six types with no isometric screenshot and two the MCP layer cannot create — held by a
   ratchet in which a new gap and a stale debt entry fail equally. Two checklist items turned
   out NOT to be completeness requirements and are documented as such instead of enforced:
   `ElementKind` (no role is a legitimate answer) and `ElementTypeConverter` (a forgotten type
   defaults to «not convertible», the safe direction).
   The six screenshot gaps are now closed; the panel among them could not have had a
   screenshot at all, because `ElementFactory.CreatePanel` did not exist — see A7 below.
4. **Spend the mutation ratchet — C1 gates it, nothing raises it.** The gate is in
   (`geometry/mutation-baseline.txt`, `core` 72 %); what the survivors named is in
   "What the survivors said" under C1. In order: the recessed-body union in
   `ValidationBroadPhase` (a real hole, not a missing assertion), `BoxGaps` (its tests are in
   the wrong build), the detent LABELS in `EdgeDetents`, and a boundary case per threshold —
   97 of the 299 survivors are `<` ↔ `<=`.
5. ~~**A4 — test-quality guards.**~~ **Done** — C3 is `TestQualityRatchetTests`, A4 is
   `PrivateReflectionRatchetTests`. It did not start at zero: twelve files reached into
   private members, seven were fixed for real and five are ceilinged with a reason (four of
   them behind other agents' territory). See A4 above for the forms a text scan cannot see.
6. **Part E** — the refactoring still owed. Cheaper now: what it extracts is exactly what
   belongs in `Core/Pure`, so the loop for it is 0,3 s rather than 73 s.

**Known debts, recorded not blessed:** ``ElementData.midHeightMM`` defaults to the literal ``75``, a
copy of ``PillarElement.MidHeightMM_Default`` that the pure build cannot reference (the constant sits
on a ``MonoBehaviour``, next to the pillar geometry ``SidebarCatalog`` also needs). It predates the
move and nothing pins the two together; the cure is a pure ``PillarSpec`` beside ``LampSpec``. 30 layer→UI references under the A6 ratchet;
`PillarElement`'s UVs (`u = i/Segments` around the circumference against an `ST` that scales by
diameter — a factor of π, and `DecorSurfaceMM` alone does not cure it); `RadialShelfElement`'s
(cured by `DecorSurfaceMM = (width, depth)`); a floor's UV origin not rebuilt when the slab is
MOVED; `ContextMenuMaterialSection.ApplyLegsChoice` bypassing `CommandStack`;
`Assets/Scripts/Tests` compiled by Unity without `-warnaserror`/`-nullable`; the ~40 style
rules in `.editorconfig` declared `:error` and enforced nowhere (2864 violations if switched on
— its own campaign); commit `21570682` does not build standalone, because two agents edited one
file (history is fine at HEAD, `git bisect` is not).

### Cold start — measured on an idle machine, 2026-09-01

The quiet window came and the whole prepared series ran. The bench (`F:\kd-bench\bench.ps1`,
mirror project in `F:\kd-bench\mirror`) records for every run: wall time, the gateway's own
time, `Foreign` (any `Unity.exe` whose `-projectPath` is not this project) and `Busy` (CPU
average and maximum, sampled while the run is alive). Every claim below is an A/B/A series —
the control has to come BACK to the starting value, or the number is thrown away.

**What was applied (three commits).**

| Change | Targeted run (5 tests) | How it was proven |
|---|---|---|
| `com.unity.ai.assistant` out of `manifest.json` | 15.9 → 13.5 s (**−2.4 s**) | mirror A/B/A, control returned to 15.9 s |
| Gateway polls the process every 250 ms, not 2 s | **−0…2 s**, ~1 s on average | values stopped being multiples of two seconds |
| `-DoneGraceSeconds` on by default (20 s) | insurance, not speed | three kills at `Cleanup mono` → `Imports: total=0` |

On the main project the targeted run went **18.4 s → 13.2 s**, the full EditMode suite
**79.2 s → 73.1 s** (2847 tests, 0 failed), PlayMode **101 s → 91 s** (84 tests, 0 failed).

**The stand had to be proven first, and it caught two errors before the numbers did.**

- The gateway was called from `bash`, which ate the backslashes and turned
  `F:\repos\...\kd-repose` into `F:\repos...kd-repose`. The run died on a path that does not
  exist — the same class of defect as the mutex experiment in CONVENTIONS.md.
- The first `mirror-base` series read 21.3 s and its control read 18.6 s. The difference was
  NOT the package under test: the gateway polled the child process once every two seconds, so
  a run that was already finished sat waiting up to two more. The Unity log proved it —
  `[Project] Loading completed` differed by 0.17 s between a "21.3 s" run and an "18.6 s" one.
  **The bimodality of 16.3/18.4 s was ours, not Unity's.**

**AssetImportWorker gets forward slashes** — observed directly, not deduced. The gateway passes
`-projectPath F:\kd-bench\mirror`; the child worker's command line reads
`-projectPath "F:/kd-bench/mirror"`. `Get-UnityProcesses` already normalises for this, and the
observation confirms the comment there: matching the raw string would make an orphaned importer
invisible to `status` and to `stop`, while it holds `Library` just as hard as the main process.

**Killing at `Cleanup mono` does NOT leave `Library` dirty.** Three trials: a watcher killed
Unity the instant that line appeared, then a normal cold run followed. All three reported
`Imports: total=0 (actual=0)`, `CompileScripts` 0.87–0.92 s and 15.9–16.5 s against the usual
15.9 s. That is what unblocked `-DoneGraceSeconds`.

### Вторая серия того же дня — исключения Defender, стенд, лицензия

Серия снималась после того, как пользователь добавил в Windows Defender исключения на
`F:\repos\KitchenDesigner2`, `Library`, `Temp` и `Unity.exe`, а второй сканер (Kaspersky) был
выключен более суток. Стенд — `F:\kd-bench\bench2.ps1`, зеркало и основной проект.

**Исключения Defender не дали НИЧЕГО ни на одном из трёх видов прогона.**

| Прогон | До серии | После исключений | Разница |
|---|---|---|---|
| прицельный (`-Filter`, 5 тестов) | ~11 с (13,2 с по стенду) | **12,4 / 12,4 / 12,4 с** по стенду, **10,9 с** прямым вызовом Unity | 0 |
| полный EditMode | 73,1 с (82,3 с по стенду) | **74,1 / 79,5 с**, 2909 тестов | 0 |
| PlayMode | 91 с | **94,8 / 90,5 с**, 92 теста, 0 упавших | 0 |

Гипотеза «~4 с в `ImportOutOfDateAssets` — это антивирус» **ОПРОВЕРГНУТА в обеих формах**:
исключения дали ноль, а второй сканер и так был выключен. Фаза при этом воспроизводится:
4407 мс при `Asset File Changes: new=0, changed=0, deleted=0` и `Imports: 0`. Значит это
собственный обход дерева ассетов Unity, а не проверка файлов кем-то снаружи.

**`ImportOutOfDateAssets` — НЕ константа, и вчерашняя оценка «4 с ждут ускорения» завышена.**
На одном и том же неизменном зеркале: 1745 мс вчера, 4407 мс сегодня. На основном проекте в один
день: 806 мс на прицельном прогоне и 2908 мс на полном, при одинаковом числе ассетов. Разброс
0,8–4,4 с без единого импорта. Планировать экономию на верхней точке разброса нельзя.
Побочно: у основного проекта теперь 2542 ассета против 3914 у зеркала — удаление
`com.unity.ai.assistant` срезало треть дерева, и часть его −2,4 с идёт именно отсюда.

**Стенд был неправ, и это главный вывод серии про методику.** Старый `bench.ps1` считал
`Win32_Processor.LoadPercentage`, усреднённый по всем 6 ядрам. Одноядерный пожиратель поднимает
это среднее всего на ~17 пунктов и тонет в собственном шуме Unity — измерено прямо: один занятый
поток даёт `TotalAvg=12` при `CoreMax=50`. А стоит такой сосед дорого: контрольный прогон со
специально запущенным одноядерным спиннером дал **44,3 с против 13–16 с**, то есть ВТРОЕ.
`bench2.ps1` добавляет две колонки: `CoreMax` (максимум по ОДНОМУ ядру) и `Hogs` — список
процессов с дельтой CPU-времени за интервал прогона. Он немедленно поймал и спиннер
(`powershell=40,9s`), и настоящих чужаков (`Everything=19,6s` в прогоне, выбившемся до 45,3 с).

Отсюда правило, которое шире стенда: **параллельная работа других агентов делает ЛЮБОЙ замер
времени Unity недействительным.** Это ограничение рабочего процесса, а не разовая помеха. Числа
первой серии (−2,4 с на `ai.assistant`, ~1 с на шаге опроса, 2,3 с на Burst) снимались старым
стендом, который одноядерного соседа не видел; они не опровергнуты, но их доверительный интервал
шире записанного. Все три подтверждались A/B/A с возвратом контроля, поэтому переснимать их
незачем — но новые числа снимаются только `bench2.ps1`.

`Hogs` требует одной оговорки, иначе даёт ложные тревоги: Unity 6 гоняет Roslyn через `dotnet`,
а ещё порождает `VBCSCompiler`, `bee_backend`, `Unity.ILPP.Runner` и `UnityShaderCompiler`. Все
они появляются в колонке как «пожиратели» и являются СВОИМИ. Отличать по родителю процесса;
чужое — это `dotnet-stryker`, `testhost`, `Everything`, `com.docker.backend` и чужой `Unity.exe`
(он же ловится колонкой `Foreign` по `-projectPath`).

**Чистота окна теперь доказывается поимённо.** `ts-run.ps1` снимает по каждому процессу дельту
CPU-времени и дискового обмена за интервал прогона. Эталонная строка тихого окна (прицельный
прогон, 10,9 с):

```
Unity.exe                  10,0 с CPU   443,5 МБ ввода-вывода
Unity.Licensing.Client.exe  1,5 с         0,3 МБ
UnityPackageManager.exe     1,0 с         8,1 МБ
MsMpEng.exe                 0,8 с         0,0 МБ
pwsh.exe (сам сборщик)      1,5 с         0,0 МБ
```

Процессов `avp` в списке нет вообще — Kaspersky действительно спит. `MsMpEng` (Defender) тратит
0,8 с CPU и это всё, что от него осталось после исключений.

### Разложение холодного старта по фазам — актуальное, `-timestamps`

Прежнее разложение («две ямы») снималось на зеркале с `ai.assistant` и до выключения Burst и
**устарело**. На основном проекте сегодня, прицельный прогон, 10,9 с прямым вызовом Unity:

| Фаза | Время |
|---|---|
| **лицензирование** (до старта движка) | **2,24 с** |
| старт движка | 0,23 с |
| domain reload #1 | 1,03 с |
| asset refresh целиком | 2,95 с |
| ├ domain reload #2 | 1,72 с |
| ├ `CompileScripts` | 0,81 с |
| ├ `ImportOutOfDateAssets` (0 импортов) | 0,81 с |
| └ `Untracked` | 1,09 с |
| refresh end → «project loaded» | 0,48 с |
| старт TestRunner и сбор 2909 тестов | 1,60 с |
| 5 тестов, отчёт, выход | 0,90 с |

`ImportOutOfDateAssets` здесь 0,81 с, а не 4 с. **Крупнейшая одиночная яма теперь —
лицензирование**, и её никто до сих пор не разбирал.

**Лицензирование: 2,2 с на КАЖДОМ холодном прогоне, любого вида.** Воспроизводимость идеальная:
2,21 / 2,27 / 2,39 / 2,22 с в четырёх прогонах подряд (три EditMode и один PlayMode). В каждом
логе первая строка — `Channel LicenseClient-<user> doesn't exist`: клиент лицензирования
стартует заново каждый раз.

```
+0,000  Trying to connect to existing licensing client channel... / doesn't exist
+0,007  Successfully launched the LicensingClient
+0,380  IPC-канал подключён (connect: 0,37 с)
+2,219  Handshaking with LicensingClient — handshake: 1,79 с, total connection time: 2,21 с
+2,470  Initialize engine version 6000.4.3f1        ← движок начинает работу только здесь
```

Из чего состоят эти 1,79 с — по собственному логу клиента
(`%LOCALAPPDATA%\Unity\Unity.Licensing.Client.log`):

```
+0,32  PackageEnforcer: Trying to update PACL
+0,32  GET https://license.unity3d.com/licenses/v1/packages/acl        ← ходит в СЕТЬ
+0,65  ответ 304 Not Modified, «Next update in 24.00 hours»
+0,65  Loading remote PACL, revision 52
+1,87  Received handshake from UnityEditor/6000.4.3f1                  ← редактор ждал всё это
```

То есть 0,33 с сетевого похода на `license.unity3d.com` плюс ~1,2 с локальной обработки PACL, и
только потом клиент отвечает редактору. «Next update in 24 hours» не спасает: процесс каждый раз
новый. Сети он не дожидается — `Error: Access token is unavailable; failed to update` и
`[Code: 401] Token not found in cache` проходят мгновенно, лицензия Unity Personal резолвится
локально. И тот же лог показывает, что клиент не переживает прогон:
`Releasing mutex Unity-LicenseClient-<user>` за 1,8 с до старта следующего.

**Оба кандидата ПРОВЕРЕНЫ и оба ОПРОВЕРГНУТЫ.** Ниже — что именно измерено, чтобы через месяц
никто не пошёл этой дорогой снова.

**Живой клиент лицензирования не даёт ничего — 0,06 с.** У клиента действительно есть
документированный в его `--help` ключ `--disable-auto-shutdown`, а редактор запускает клиент БЕЗ
него — потому тот и умирает вместе с прогоном. Серия A/A/B/B/A на зеркале, клиент прогрет
заранее и жив всё время B:

| Прогон | `Channel doesn't exist` | connect | handshake | лицензирование целиком |
|---|---|---|---|---|
| A1 | да | 0,38 с | 1,87 с | 2,65 с |
| A2 | да | 0,38 с | 2,00 с | 2,65 с |
| B1 (клиент прогрет) | **нет** | **0,00 с** | 2,12 с | **2,59 с** |
| B2 (клиент прогрет) | **нет** | **0,00 с** | 1,90 с | 2,5 с |
| A3 (контроль) | да | 0,39 с | 2,04 с | 2,66 с |

Механизм сработал — строка «канала нет» исчезла, запуск клиента и `connect` 0,38 с ушли в ноль.
Но `handshake` ровно на столько же ВЫРОС, и лицензирование целиком осталось 2,6 с. Значит эти
2,6 с — **не холодный старт клиента**, как выглядело по его логу, а что-то в самом модуле
лицензирования редактора: он получает `Licensing is not yet initialized`, ждёт и переспрашивает.
Секундомер это подтверждает: 12,9 с против 13,0 / 13,2 с у контроля.

**Если метрика красиво упала, а итог не изменился — измеряли не то, чего ждали.**

Правило общее, но заработано оно здесь, поэтому и записано здесь. Прогрев клиента лицензирования
сработал ПОЛНОСТЬЮ: строка `Channel LicenseClient-<user> doesn't exist` исчезла, запуск клиента и
поле `connect` упали с 0,38 с ровно в ноль. Отчётная метрика — `total connection time` — рухнула
с 2,43 с до нуля. И при этом `handshake` вырос ровно на столько же, лицензирование целиком
осталось 2,6 с, а секундомер не сдвинулся: 12,9 с против 13,0 / 13,2 с у контроля.

То есть измерялась не та величина, которую ждёт потребитель. `total connection time` описывает
подключение к клиенту; ждёт же редактор готовности модуля лицензирования, а это расстояние от
первой строки лога до `Initialize engine version`. Первое красиво падает и ничего не меняет,
второе — то, что стоит времени. Вывод по механизму получился обратный первоначальному: 2,6 с — это
ожидание ВНУТРИ модуля лицензирования редактора («Licensing is not yet initialized», пауза,
переспрос), а вовсе не холодный старт клиента, как выглядело по логу самого клиента.

Проверка, которую стоит делать всегда, а не только тут: **у улучшения должны сдвинуться ДВА
числа — внутренняя метрика И итог.** Сдвинулась только внутренняя — значит она не лежит на
критическом пути, и работа перетекла в соседнюю фазу, где её никто не смотрит. Ровно этот
механизм чуть не превратил ноль в «−2 с на каждом прогоне» и не привёл к висящему фоновому
процессу, которого владелец проекта не хотел.

Родственные ловушки того же дня в этом файле: `bench.ps1`, который усреднял загрузку по шести
ядрам и не видел одноядерного соседа ценой в тройное время прогона, и `ImportOutOfDateAssets`,
чьё «постоянные 4 с» оказались разбросом 0,8–4,4 с. Каждый раз число было настоящим, а величина
— не той.

**Лицензирование полностью блокирующее.** `Initialize engine version` во ВСЕХ прогонах приходит
после `Licensing is initialized`, с отрывом 0,19–0,40 с. Перекрытия с другими фазами нет: это
чистые 2,4–2,8 с в начале каждого холодного старта, до того как движок начал работу.

**Исключения Defender на пути лицензирования тоже дали ноль.** После того как в исключения были
добавлены `C:\Program Files\Unity\Hub\Editor`, `%LOCALAPPDATA%\Unity` и процесс
`Unity.Licensing.Client.exe`, блок лицензирования остался 2,44–2,79 с — ровно там же, где был.
Внешний ориентир (0,0–0,66 с рукопожатия в чужих логах) объясняется, стало быть, не сканером.

**`-assemblyNames` не даёт ничего.** Ключ документирован и должен отсекать целые тестовые сборки
до построения дерева тестов, в отличие от `-testFilter`. A/B/A на зеркале, фаза «старт TestRunner
и сбор тестов» (от `Running tests for ExecutionSettings` до `Executing IPrebuildSetup`):

| | A | A | B (`-assemblyNames`) | B | A (контроль) |
|---|---|---|---|---|---|
| сбор тестов | 1,82 с | 1,87 с | 2,05 с | 1,68 с | 1,65 с |
| прогон целиком | 14,2 с | 13,0 с | 13,8 с | 12,4 с | 12,1 с |

Разброс внутри A перекрывает разницу с B. Тесты во всех пяти прогонах — 5, 0 упавших, то есть
фильтр не выродился в пустой прогон.

Что закрыто по документации, без замера: **плавающие лицензии и Unity Licensing Server** — платный
продукт, для Unity Personal недоступен; **документированного ключа «пропустить лицензирование в
batch» не существует**, крупные CI-фермы просто платят эту цену каждым прогоном.

Итого лицензирование остаётся крупнейшей одиночной ямой (2,4–2,8 с из ~11 с), и **ни один из
проверенных рычагов её не трогает**. Единственное, что осталось непроверенным, — профилировщик
самого Defender (`New-MpPerformanceRecording` → `Get-MpPerformanceReport`), который называет файлы
и миллисекунды поимённо; он требует прав администратора и остаётся за пользователем.

Ниже — как это выглядело ДО проверки, оставлено ради механизма:

- **Живой клиент лицензирования между прогонами.** Запустить
  `…\Editor\Data\Resources\Licensing\Client\Unity.Licensing.Client.exe --namedPipe Unity-LicenseClient-<user>`
  заранее; если редактор найдёт живой канал, 2,2 с должны схлопнуться. Это −2 с на ВСЕХ трёх
  видах прогона, включая полный EditMode и PlayMode, где ни `ai.assistant`, ни Burst не помогают.
  Оговорка честности: это висящий фоновый процесс, а фоновый демон отвергнут. Но это не редактор
  Unity, не сокет-мост, он не держит состояние проекта и не блокирует `Library`; Unity сама
  запускает его при каждом открытии Хаба. Решение за пользователем.
- **Исключения Defender не покрывают лицензирование.** Клиент лежит в
  `C:\Program Files\Unity\Hub\Editor\…\Licensing\Client`, его лицензии и логи — в
  `%LOCALAPPDATA%\Unity`; ни один из этих путей в исключения не попал. В чужих логах то же
  рукопожатие занимает 0,0–0,66 с против наших 1,79 с. Проверяется тремя прогонами после
  добавления двух путей (нужны права администратора — это к пользователю).

### Measured and REFUTED — do not spend the machine on these again

| Candidate | Expected | Measured | Verdict |
|---|---|---|---|
| `indexOnEditorStartup = false` in `UserSettings/Search.settings` | ~2 s | 18.6 s vs 18.6 s | **nothing.** Indexing starts AFTER the test run begins and finishes on a worker process; it never blocks the run. The only visible effect is that no `AssetImportWorker` spawns. The plan's promise to normalise this flag in the gateway is withdrawn |
| `-nographics` | some | 15.7–16.2 vs 15.9 s | nothing |
| `-disable-assembly-updater` | some | 16.4 vs 15.9 s | nothing |
| `m_RefreshImportMode: 1` (`OutOfProcessPerQueue`) | parallel import | 12.7–13.2 vs 12.9–13.0 s | nothing — there is nothing to import (`actual=0`), and the 4 s goes on CHECKING |
| removing `com.unity.testtools.codecoverage` | some | 13.5 → 13.0 s | **0.5 s**, and the package is used. Not worth it |
| исключения Windows Defender на проект, `Library`, `Temp`, `Unity.exe` | ~4 с (`ImportOutOfDateAssets`) | 12,4 / 74–80 / 90–95 с — ровно как было | **ноль на всех трёх видах прогона.** Второй сканер (Kaspersky) был выключен ещё до серии, так что гипотеза про антивирус закрыта в обеих формах |
| `-noUpm` (Package Manager 2,06 с внутри `Project init`) | ~2 с | прогон падает: нет `TestResults.xml`, один domain reload вместо двух | **ломает прогон.** Классический «стал быстрее, но это другой прогон» |
| живой клиент лицензирования (`--disable-auto-shutdown`) | ~2 с | 2,59 с против 2,65 с | **0,06 с.** Запуск клиента и `connect` уходят в ноль, `handshake` ровно на столько же растёт |
| исключения Defender на `C:\Program Files\Unity\Hub\Editor` и `%LOCALAPPDATA%\Unity` | 0,2–1 с | блок лицензирования 2,44–2,79 с — как был | ноль |
| `-assemblyNames` вместо/вместе с `-testFilter` | 0,5–1 с на сборе тестов | сбор 1,82/1,87 против 2,05/1,68, контроль 1,65 | ноль, разброс внутри A перекрывает разницу |
| плавающие лицензии / Unity Licensing Server | убрать лицензирование | — | недоступно: платный продукт, не для Unity Personal. Ключа «пропустить лицензирование в batch» не существует |

### The two holes, decomposed

Both holes were split into their parts with `-timestamps` (Unity writes a timestamp per line
with that flag) and the `Domain Reload Profiling` tree. The empty project of the same Unity
version was measured the same way, as the floor.

**Hole 1 — domain reload #2. Belongs to the packages, not to us.**

| Phase of reload #2 | with `ai.assistant` | after removal | Burst compilation off | empty project |
|---|---|---|---|---|
| `BeginReloadAssembly` | 0.39 s | 0.39 s | 0.37 s | — |
| `LoadAllAssembliesAndSetupDomain` | 0.43 s | 0.43 s | 0.47 s | — |
| ├ `LoadAssemblies` (ALL of them) | 0.35 s | 0.35 s | 0.37 s | — |
| └ `TypeCache.Refresh` | 0.16 s | 0.16 s | 0.17 s | — |
| **`ProcessInitializeOnLoadAttributes`** | **3.2 s** | **2.17 s** | **0.18 s** | — |
| `ProcessInitializeOnLoadMethodAttributes` | 0.21 s | 0.21 s | 0.18 s | — |
| **total** | **5.26 s** | **3.79 s** | **1.85 s** | **1.40 s** |

So the whole hole is static constructors, and they are somebody else's: **Burst ≈ 2.0 s**
(`UNITY_BURST_DISABLE_COMPILATION=1` removes it, A/B/A on the mirror: 12.9 / 16.5 / 12.9 s, and
on the main project 10.8 / 13.2 / 10.8 s), **`ai.assistant` ≈ 1.0 s** (removed). Everything
else, our single `[InitializeOnLoad]` included, fits inside 0.18 s. Loading all assemblies —
the number the six `.asmdef` files could influence — is 0.35 s in total, so splitting or merging
assemblies cannot buy anything here.

With Burst compilation off, reload #2 is 1.85 s against an EMPTY project's 1.40 s. **There is
0.45 s left in this hole, and none of it is ours.**

**Hole 2 — "project loaded" to the first test. Half ours, and the half is small.**

Timeline of a targeted run, from the timestamped log:

```
+2.44 s  engine init and licensing        (before anything of the project)
+1.04 s  domain reload #1                 (1.05 s in the EMPTY project too — a constant)
+1.18 s  asset refresh up to reload #2
+5.26 s  domain reload #2                 (hole 1)
+0.80 s  refresh end, project loaded
+0.17 s  → "Running tests for ExecutionSettings"
+2.53 s  → "Executing IPrebuildSetup"     (hole 2)
+0.44 s  the 5 tests themselves (0.107 s of NUnit time) and the report
+0.85 s  → "Cleanup mono", then exit
```

The 2.53 s is test COLLECTION, and it was measured against a project with the same code but
only one test class (228 test files moved into an ignored `Excluded~` folder): **2.53 s for
2847 tests, 1.55 s for 5.** So ~1.0 s scales with our suite and ~1.55 s is the TestRunner
starting up — `IPrebuildSetup` of `test-framework.performance` (unremovable, comes through URP),
the test scene, the runner itself.

Search indexing lands inside this window (`Start Indexing on Editor startup`, then an
`AssetImportWorker` spawns) but costs nothing measurable — see the refuted table.

**Both domain reloads happen in the EMPTY project too** (1.05 s + 1.40 s), so the second one is
structural: reload #1 runs before the asset refresh with only precompiled engine assemblies,
reload #2 loads `Library/ScriptAssemblies`. There is no supported way to skip it, and skipping
it would mean not loading the project's code at all. The hole can only be made cheaper, and
after the two package findings it is 0.45 s away from the floor.

### What is left, and what it is worth

- **`UNITY_BURST_DISABLE_COMPILATION=1` for test runs: −2.3 s on a targeted run, 0 on the full
  EditMode suite** (79.9 s with, 79.8 s without, measured back to back under identical load).
  Burst is not compiled at startup any more, but the work comes back during a long suite. Worth
  it for the `-Filter` loop, worthless for the full run — not applied, awaiting a decision.
- Windows Defender exclusions for the project directories and `Unity.exe` are the most
  plausible remaining lever for the ~4 s of `ImportOutOfDateAssets` (which does zero imports and
  spends its time on `stat`ting several thousand files). It is a MACHINE setting, not a project
  one — it belongs in the user's hands, not in a commit.
- The floor is real: an empty project of the same Unity version does the same 5 tests in 8.1 s,
  and 2.44 s of that is licensing and engine init before the project is even touched. Our
  targeted run is now 13.2 s (10.8 s with Burst off). What remains between us and the floor is
  ~4 s of asset-database checking over 3914 assets (two thirds of which are package assets) and
  ~1 s of collecting 2847 tests.

### Measured, load-independent facts

- Cold first import of a fresh copy: **809 s**. That is deployment cost, not run cost.
- Floor: an empty project of the same Unity version does the same work in **8.1 s**, of which
  2.44 s is licensing and engine init and 1.05 s is domain reload #1 — both constants. This
  project was 18.1 s and is now 13.2 s; the decomposition of the remaining difference is in
  "The two holes, decomposed" above.
- `com.unity.ai.assistant` is REMOVED (commit `df6b9f5e`). It dropped exactly two packages of
  38: itself (86 files with `[InitializeOnLoad]`, against 17 for the next-largest) and its
  private `com.unity.2d.sprite`. Nothing pulled it, nothing in the repository referenced it.
  Measured worth: −2.4 s per cold run.
- `com.unity.test-framework.performance` is UNTOUCHABLE: it arrives through
  URP → render-pipelines.core → collections. Its `IPrebuildSetup` runs on every test run and
  cannot be removed without removing URP. It sits inside the fixed 1.55 s of TestRunner startup.
- `com.unity.testtools.codecoverage` is USED — manually, documented in
  `GEOMETRY-EXTRACTION-PLAN.md`, and the baseline coverage numbers come from it. It also costs
  only 0.5 s (measured), so there is nothing to weigh against its usefulness.
- `com.unity.burst` (1.8.29, arrives through URP → collections) spends **~2.0 s** of every cold
  start inside `ProcessInitializeOnLoadAttributes`, starting its compiler service.
  `UNITY_BURST_DISABLE_COMPILATION=1` removes that; see "What is left".
- Search indexing (`UserSettings/Search.settings` → `indexOnEditorStartup`) costs NOTHING in
  batch mode — measured, see the refuted table. The earlier plan to normalise that flag in the
  gateway is withdrawn: `UserSettings/` is gitignored, and there is nothing to gain by touching it.

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

**Done — `PrivateReflectionRatchetTests`.** It could NOT start at zero: `Assets/Tests/**` held
twelve files reaching into private members, not three. Seven were fixed for real (`SnapToWall`
instead of a private `RegisterWithNearestWall`; an `internal` setter on `SelectionManager.Instance`
and `ElementHighlighter.Instance`; the already-public `CreateMaterials` plus an `internal
MaterialsReady` instead of five private material fields), five are ceilinged with a reason —
four of them because they live in `Core/UI` and `Core/Pure`, which other agents were cleaning —
and one, `KitchenSettingsContractTests`, is not a debt at all.

Three forms of the offence carry no `BindingFlags` in the text and were invisible to the first
draft of the scan: `GetSetMethod(nonPublic: true)` (used in four files), `Type.InvokeMember`, and
`SendMessage` as a way to call a private `Awake`. A fourth is invisible to any text scan —
`PropertyInfo.SetValue` reaches a private setter through a property bound as `Public` — which is
why the two `Instance` setters were made `internal` rather than ceilinged. The guard counts
reach-ins separately from named lookups (`Get*("name")`), because only the latter freezes a name;
reflection over PUBLIC members is not counted at all, and a test pins that on two real files —
the guards themselves are built on it.

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

**Done — `ElementTypeCompletenessTests`.** Two of the registries named above turned out not to
be completeness requirements once read in the code, and the guard says so rather than enforcing
a rule that is not true: `destroy` is no longer a registry at all (since `6b819773` the element
declares its own `KitchenElement.Disposal`, so there is nothing per-type to forget), and
`validation` has no per-type entry either — `ElementKind` is a set of ROLES, and having none is
the correct answer for a plain part. What replaced them came out of the code: the duplicate
registry and `ElementSelector.TypeOf`, both of which fail SILENTLY by treating the forgotten
type as an ordinary board.

**The last debt on this guard is closed, and closing it turned one entry into a finding.** The
six types with no isometric screenshot — assembled facade, cooktop, dishwasher, lamp, oven,
panel — now have one, and the `KnownGaps` rows went in the same commit (`1cdcf352`). Two things
came out of it that the ratchet entry did not say:

- **`PanelElement` had no screenshot because it COULD NOT have one.** `ElementFactory.CreatePanel`
  did not exist: `CreatePanel` was the only method of `IElementFactory` with no wrapper on the
  public facade, and `ElementFactory.Instance` is `internal` with `InternalsVisibleTo` granted to
  the EditMode assembly only. So the PlayMode suite — the one place isometric screenshots live —
  was physically unable to create a panel. The debt read like laziness and was a symptom: a
  missing test whose cause was a missing entry point. The wrapper is added, and
  `ElementFactoryFacadeTests` now compares the two lists and fails in BOTH directions (a method
  with no wrapper hides a working capability; a wrapper with no method is dead code the compiler
  cannot find, because the facade delegates by name).
- **The screenshot template in `CONVENTIONS.md` was wrong for composite elements**, and half of
  the six are composite. It took `GetComponent` on the ROOT and sized the frame from the root's
  `dims`. `Cooktop`, `Oven` and `Dishwasher` are built by `ElementRoot.NewEmpty`, so the root
  carries no renderer at all; their child boxes are not centred on the pivot (the cooktop hangs
  entirely below it); and the oven's handle protrudes past the declared `DEPTH_MM`. The shared
  helper now frames from the world AABB of every renderer and **asserts that all eight corners of
  that box land inside the frame** — a screenshot test that only writes a PNG cannot fail at all.
  `IsoAppliances_KeepTheirGeometryInChildren_NotOnTheRoot` pins the three facts so the shortcut
  cannot come back quietly, and the template in `CONVENTIONS.md` was rewritten to match.

The lesson generalises past this guard: **a ratchet entry records that something is missing, not
why.** Read as a to-do list it invites the cheapest possible closure; read as a symptom list it
occasionally names a hole somewhere else entirely. Six entries here, one of which was an
inaccessible API.

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

### C1. Gate the geometry mutation score — **DONE**

The number lives in `geometry/mutation-baseline.txt`, one line per mutated project:
`<проект> <замер> <порог>`. `tools/mutation-test.ps1` reads it and hands each threshold to
Stryker as `--break-at`; below it the run exits non-zero and the script says which project
fell, from what to what, how long it took and where the survivors report is.
`MutationBaselineTests` (fast path, milliseconds) guards the file itself — that every project
Stryker mutates has a line, that the threshold has not fallen more than 1 % behind its
measurement (a threshold that lags stops firing), and that the script still reads the file
rather than carrying its own copy of the number.

**Baseline, Stryker 4.16.0, 2026-09-01:**

| Project | Tests | Mutants tested | Score | Gate | Run | Commit |
|---|---|---|---|---|---|---|
| `geometry/core` | 284 | 1039 | **72,42 %** | 72 | 2 мин 44 с | `469f2e0f` |
| `geometry/pure` | 150 | 463 | **57,26 %** | 57 | 1 мин 27 с | `4456d308` |

The two rows were measured on different commits, and that is itself the argument: between
them `ElementData` and `ProjectData` moved into `Core/Pure`. The composition of the pure layer
changed WHILE the baseline was being taken.

**Two thresholds, not one**, and this is the decision, not an omission:

- Stryker mutates ONE project per run (`--project`) against its own test project. A single
  blended number would have to be computed by hand from two reports, and nothing would verify
  the arithmetic — the failure mode this plan exists to prevent.
- The composition of the pure layer is changing right now (the `ElementData` migration moves
  classes into `Core/Pure`). A blended score would move with every FILE MOVE while saying
  nothing about test quality: the gate would be falsely red after a clean refactor, and the
  cure would be lowering it — which is how a ratchet dies.
- A blended number hides a regression: a loss in the 1039-mutant core is covered by a rise in
  the smaller layer. Masking is precisely what the ratchet is for.
- Красное должно называть сборку. With one number the first step after a failure is finding
  out where the coverage went.

And the numbers settle it: 72 against 57. One threshold would have been set by the weaker of
the two builds and would have stopped constraining the stronger one entirely.

#### What the survivors said — 299 of them, and they are not evenly spread

A surviving mutant is a place where the test exists and the CHECK does not. Sorted by what
they are worth reading:

1. **`ValidationBroadPhase.SolidBoundsIncludingRecessedBody` — the union with the recessed
   body is never asserted.** Both `Vector3.Max(max, e.RecessedBody.Max)` → `Vector3.Min` and
   → `e.RecessedBody.Min` survive. `ValidationRecessedBodyTests` exists and runs over this
   line; no case there has a recessed body that actually EXTENDS the solid bounds, so the
   union can be replaced by an intersection and every test stays green. That is the whole
   point of the recessed body (a cooktop's box, a sink's body): it must widen the bounds
   overlap validation sees. This one deserves a test today.
2. **`BoxGaps` — 19 mutants, all `NoCoverage`, 0 %.** The class sits in `Core/Geometry`, so
   the fast path mutates it; its tests (`GapSideTests`) build `GameObject`s and live on the
   Unity side, so the fast path never runs them. `NonZeroCount` drives the "Зазоры" section
   header and nothing on this path touches it. Not a hole in the code — a test in the wrong
   build. **Open debt:** the cure is splitting the scene-free half of `GapSideTests` into
   `Assets/Tests/EditMode/Geometry/`; it was left alone deliberately, as it moves another
   agent's tests between builds.
3. **`EdgeDetents` — 58,1 %, the lowest score among covered files.** All five detent LABELS
   (`кромка-`, `кромка+`, `центр`, `паз-`, `паз+`) can be replaced by `""` and nothing goes
   red: the `out string label` is never asserted, only the delta. And `abs <= threshold` →
   `abs < threshold` survives (nothing is tested AT the threshold), as does
   `abs < bestAbs` → `<=` (the tie-break between two equidistant detents is undefined by the
   suite).
4. **`SnapCore` — 60,5 %.** `pass < FillInPasses` → `pass <= FillInPasses` survives: no test
   in the fast suite needs more than one fill-in pass, so the multi-pass loop is effectively
   untested there. Every `logSink != null` guard survives too — the diagnostic log is
   produced and never asserted.
5. **The dominant kind, across all files: 97 of the 299 are `Equality` mutations** —
   `<` ↔ `<=`, `>` ↔ `>=`. That is CONVENTIONS.md → "Test a rule with a value that is NOT
   already at the boundary" read from the other side: the suite tests comfortably inside and
   comfortably outside, and almost never AT the tolerance. The cheapest way to move the score
   is a boundary case per threshold, not more scenarios.

`SnapCandidateCollector` is the "high coverage, low kill" class the brief asked about: 105
covered mutants, 61,7 %, 34 survivors — including `moved.Min` ↔ `moved.Max` on line 127.

**Two of these are already spent** (`4c3645b0`, `6e1449e3`): the recessed-body union now has
its own test with an asymmetric body — a body that fits inside the part horizontally makes
`Max(bounds, body.Max)` and `Max(bounds, body.Min)` return the same answer, which is why every
existing fixture was green — and `EdgeDetents` got the suite it never had: five labels, the
inclusive threshold AT the boundary, the tie-break, and the groove-coordinate dedupe. Neither
turned out to be a live defect; both were missing checks over correct code.

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

### C3. Cheap test-quality guards meanwhile — **DONE**

`TestQualityRatchetTests`, same shape as the comment ratchet: ceilings, red in BOTH
directions, tests for its own scanner.

- **Names.** A test name must be a sentence about behaviour. Offenders: no `_` at all, or a
  vacuous tail (`_Works`, `_Valid`, `_Basic`, …). Debt taken at `469f2e0f`: `EditMode` 14,
  `EditMode/Pure` 1, `PlayMode` 4, `EditMode/Geometry` **0** — the one offender there
  (`CycleCompletesInThreeSteps`) was renamed in the same commit, so the fast path is now at
  zero and any new name is red immediately. The 19 remaining live in other agents'
  directories; the ceiling holds them and drops when they are renamed.
- **`Assert` without a message.** Per-FILE ceilings over `Assets/Tests/EditMode/Geometry`
  (a per-directory total would let a silent assert into one file be paid for by cleaning
  another). 246 today across 20 files, the biggest being `ValidationCoreTests` 51,
  `DrawerConstantsTests` 48, `ToleranceTests` 32. **`Assets/Tests/EditMode/Pure` is
  deliberately not in the table yet** — another agent is filling that directory right now
  (the `ElementData` migration), and per-file ceilings there would break their run on every
  added test. It joins when that move settles; ~202 silent asserts are waiting there.

Two traps the scanners hit, both now pinned by their own tests: a naive line scan attributes
the attribute of `[Test] public void X() => Helper(a, b);` to `Helper`, and a scan that does
not mask string literals reports its own examples as violations — this one counted 7
offenders inside itself.

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
.\tools\mutation-test.ps1 -TestsOnly # 354 tests (core + pure), 0.25 s — the inner loop
.\build.cmd -RunTests               # 2847 tests, 73 s — before committing
.\build.cmd -RunTests -RunPlayMode  # + 84 tests, 91 s — before a release or any UI layout change
```

The numbers are why. A cold Unity batch costs **~13 seconds before a single test runs**, and the
breakdown is now known to the phase — engine init and licensing 2.4 s, domain reload #1 1.0 s,
asset refresh ~6 s (of which 4 s is CHECKING 3914 assets that need no import), domain reload #2
~3.8 s, test collection ~2.5 s. A targeted `-Filter` run does not avoid any of it: it costs
13.2 s for five tests. The `dotnet` path has none of it: 354 tests in 0.25 s, which is 0.7 ms
per test against 23 ms under Unity.

The old "26–36 s for eight tests" in this section was partly the gateway's own doing: it polled
the child process once every two seconds and so sat waiting up to two seconds after Unity had
already exited. That is fixed (`dc7540e3`), and the 16.3/18.4 s bimodality it produced was never
a property of Unity — it was ours.

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
