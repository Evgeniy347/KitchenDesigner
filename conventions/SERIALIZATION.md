# Сериализация: снапшоты, новые типы и настройки, декор

## Snapshot tests (REQUIRED for ANY serialization change)

When adding a persistable property to `ElementData`, `KitchenSettings`, or `ProjectData`:

1. Field with safe default (backward compat — missing field must not break loading)

**CRITICAL — `JsonUtility` never leaves a nested serializable object null.** It constructs one
even when the JSON has no such block, so `if (data.settings != null)` in a loader checks
nothing: the branch is always taken and every field inside arrives at its *type* default. The
only thing protecting an old project is a **field initializer**. A serializable field without
one is a defect, not a style choice.

This cost real user data: ten fields of `KitchenSettingsData` had no initializers, and opening a
project saved before the settings block existed silently turned OFF snapping, the grid and
autosave, and reset the snap threshold from 50 mm to 1. The comment above the field said "null
for old saves" — it had been wrong from the day it was written, and nothing tested it.

2. Write in `ElementCapture.FromElement`
3. Restore in `SaveLoadManagerInstance.RestoreScene`
4. Snapshot test in `Assets/Tests/EditMode/SnapshotTests.cs`
5. Round-trip test: `FromElement → Deserialize(Serialize(...)) → value preserved`

To reset snapshots: delete `.verified.json` and re-run. To accept all: run `UpdateAllSnapshots`.

## CRITICAL: Golden rule — serialize SOURCE OF TRUTH

NOT the animated/offset transform. The logical state from which pose is computed:

- **Door** → `FacadeElement.ClosedPosition`/`ClosedRotation` + `doorOpen` flag
- **Wall** → `Wall.FullPosition`
- **Drawer** → `DrawerElement.ClosedPosition`/`ClosedRotation`

If you save the current (offset) transform, loading will re-apply the offset and the object drifts.

## CRITICAL: Read a value back only AFTER EndCapture

Inside a `CommandStack.BeginCapture` / `EndCapture` pair the element is in an intermediate
state: the setter has run but the command that normalises and records it has not. Writing that
intermediate value back into a UI field shows the user what they typed instead of what the
element accepted — a clamped size, a snapped angle, a corrected cutout all appear unclamped.

- Write inside the capture, read after it closes.
- Refresh the field from the ELEMENT after `EndCapture`, never from the text the user entered.
- This defect is invisible until a test asserts on the field text, so assert on the text, not
  only on the element property.

Found the hard way in the cooktop cutout fields, which wrote the typed number back from inside
`BeginCapture` and so never showed the clamped result.

## Adding a new element type

1. Class : `KitchenElement` in `Assets/Scripts/Core/Elements/`
2. Factory method in `ElementFactoryInstance`
3. Fields in `ElementData` + `FromElement` + `RestoreScene`
4. Round-trip test + snapshot test
5. **Isometric screenshot test** in `Assets/Tests/PlayMode/IsoScreenshotTests.cs` (REQUIRED)
6. **Sidebar entry** — item in `SidebarCatalog` + branch in `SidebarSpawnRouter` (REQUIRED if a person
   may create the type at all)
7. **MCP support** — type in `CreateItem.type` + branch in `ElementSpawners.ByType` (REQUIRED,
   not «if needed»: whatever a person can create from the sidebar, an agent must be able to
   create too. `McpUiCreationParityTests` and `SidebarSpawnRouterTests` fail the build otherwise)
8. Validation in appropriate validator
9. **UVs from physical millimetres — decors TILE, they never STRETCH** (see below)

The mechanical half of this list is enforced by `ElementTypeCompletenessTests`
(`Assets/Tests/EditMode/Geometry/`): it derives the type list from the sources — every class
whose inheritance chain reaches `KitchenElement` — and fails naming the registries a type is
missing from. It covers the five places where a miss is SILENT rather than loud: the factory
(2), scene restore (3), the duplicate registry, `ElementSelector.TypeOf`, the MCP layer (7),
and the isometric screenshot test (5).

Steps 6 and 7 carry two more guards, and they answer a different question than completeness
does. `SidebarSpawnRouterTests` asks whether the BUTTON reaches the factory at all: a kind with
no branch in `SidebarSpawnRouter` falls through and quietly produces a plain board, so the
button works, the click works, and the wrong object appears. `McpUiCreationParityTests` asks
whether what the sidebar offers is also offered to an agent. Completeness alone misses both —
it is satisfied the moment the type exists in the registries, whatever the UI does with it.
Steps 1, 4, 8 and 9 stay on the reader.

Two items of this list are deliberately NOT completeness requirements, and the guard says so
rather than pretending: an element needs no `ElementKind` flag in `ValidationSnapshot` (having
no role is a legitimate answer for a plain part), and `ElementTypeConverter.GroupOf` defaults a
forgotten type to «not convertible», which is the safe direction.

Exactly ONE debt is recorded there now, ceilinged with a reason and not blessed: the MCP layer
cannot create a lamp, because whether an agent may spawn a light source is a product decision
rather than a missed line. The other debts this paragraph used to list are closed — the sink
reached `create_elements` in `cbb3c67f`, and the six types with no isometric screenshot got one
in `1cdcf352`. **Read `KnownGaps` in the test, never this paragraph**: the test fails when a
listed debt is closed and the entry outlives it, and nothing at all guards prose.

## Adding a new setting

1. `[SerializeField]` field WITH an initializer + property in `KitchenSettings`
2. Slot in `KitchenSettingsData` + `ToData` + `ApplyFrom` + `ResetToDefaults`
   (`KitchenSettingsContractTests` fails the build otherwise)
3. **Panel row** in the matching `Settings*Tab` — a setting nobody can reach is not a setting
4. **Agent key** — one line in `SettingKeys.All` plus the same key in the `ParamsSetSetting.name`
   enum and in the `set_setting` description. `get_settings`, `set_setting` and the JSON schema
   all read the table; the enum and the prose cannot (an attribute argument must be a compile-time
   constant, and prose is for a human), so those two are guarded instead by
   `McpSettingsParityTests`
5. Round-trip test (settings ride in the project file, so this is a serialization change)

Step 4 is REQUIRED for the same reason as the element checklist's: a setting a person can change
in the panel and an agent cannot change at all makes the two ways of working diverge silently.
The exceptions are photo mode and the view presets, and they are exceptions on the merits — they
change the picture, not the project. They are written down in `PanelOnly` with the reason and the
test that owns the decision; do not widen that list to make a red guard go green.

## CRITICAL: a decor tiles, it never stretches

Every surface that can carry a decor builds its UVs from the PHYSICAL size of the tile, not
from the size of the part. A 2400 mm panel wearing a 2000 mm decor shows the picture 1.2 times;
it does not show one copy pulled across the whole part. That is the entire reason the decors are
made seamless — `docs/TEXTURES.md` §1 and §2, and `tools/make-seamless.ps1` exist for it.

- The tile size comes from `MaterialManager.TileMM(def)` — the only thing that can resolve it,
  because it is the only thing that can reach the image. `MaterialDef.TileHeightMM` does not see
  the picture and is a square fallback for colour-only decors.
- Normalized `0..1` UVs on a surface that can take a decor are a DEFECT, not a default. They
  look right only while the part happens to match the tile.
- This applies to every new mesh builder, not only to new element types. A stretched decor on a
  tabletop is the same bug as a stretched decor on a wall.

`TEXTURES.md` stated the rule for the decor DEFINITION (`tileWidthMM` in `index.json`) but never
as a requirement on the geometry that consumes it, and the new-element checklist had no
texturing step at all. That gap is how a stretched tabletop shipped.

**Take the axes of the SURFACE, not of the bounding box.** `dims.x × dims.y` is right for an
upright panel and wrong for a horizontal top, where the second axis is depth, not height. An
element should expose the physical size of its decor surface explicitly, the same way it should
name its decor renderer.

Cover it with a test the way the campaign covers everything else: assert the UV span for a part
whose size differs from the tile, so a normalized rebuild goes red.

## The NAME of a serialized field is part of the format

`[SerializeField] private float _moveSpeed;` is a key in the scene and in every prefab, exactly
as a DTO field name is a key in JSON. Renaming it breaks no build and produces no warning: Unity
simply fails to find the old key and substitutes the default. Everything tuned by hand in the
scene is silently zeroed, and the only way to see it is to look in the editor — a test on the
code cannot catch it, because the default in code is usually what "should" be there anyway.

So a `[SerializeField]` field is renamed ONLY together with `[FormerlySerializedAs("<old>")]`,
and that attribute stays as long as any scene or prefab saved before the rename still exists.
The same holds for `public` fields of a `MonoBehaviour` or `ScriptableObject`, and for fields of
`[Serializable]` classes that reach the project file.

**Renaming such a field to remove a comment is NOT case (c).** The comment on a serialized field
is paid off through (a) or (b); the name stays. Caught during the Rendering purge, where
`_moveSpeed` and `_zoomStep` were renamed and reverted before the commit.


## A field that must tell "old file" from "value 0" gets a sentinel initialiser

`JsonUtility` leaves a field at its initialiser when the JSON has no such key. So a new field
whose migration needs to know «was this file written before the field existed?» declares that
answer as its own initialiser — `edgeSuppressedMask = -1` — rather than adding a second key
(`edgeSuppressedMigrated`) to say it.

The signal costs nothing: no extra key in the JSON, no extra line in 27 snapshot baselines, and
no second thing that can disagree with the first. Files written since always carry a real value
(0..15 here) and pass the migration by. Pick a sentinel outside the field's legal range, and put
a test on the boundary — «a file with the key does NOT get migrated twice» is the failure that
silently rewrites a user's project on every load.

The sentinel belongs to records that arrived FROM a file — nothing else. `JsonUtility` writes a
nested object into the save even when the field is null, building it from the default
constructor, so `ProjectData.basePlate = null` quietly shipped `edgeSuppressedMask: -1` into 26
of 27 snapshot baselines: a record our own code constructed, wearing the «I am an old file»
flag. Every object of the format that our code builds must therefore start already migrated —
`ElementData.OfCurrentFormat()` — and a test should walk the format by reflection to say so,
rather than naming the one field somebody remembered.


**Проход после загрузки чинит СТЫК, а не РАЗМЕР.** `SceneRestorer.RepairAutoSeatedJointsAfterGridSnap`
звал у опоры полную автоподгонку — и каждое открытие проекта молча заменяло сохранённую высоту
своим расчётом, без команды и без отмены, а следующее сохранение уносило подмену в файл насовсем.
Пользователь видел «поставил 100, открыл — снова 95» и не мог найти этому следов в хронике.
Хозяин размера — пользователь и жест, который он сам сделал; загрузчик — никогда. Образец
разделения уже был у трубы: `PipeDocking.RepairAfterGridSnap` только двигает, `PipeDocking.Seat` —
жест, который ложится в undo. Единственное законное исключение — `MmGrid.Snap` на загрузке, и оно
меняет данные в пределах 0,5 мм с обоснованием.

Из того же куста: **молчаливый откат по нарушению тоже переписывает размер без следа.**
`ContextMenuUI` и `ResizeHandleManager.FinishDrag` при `BlockOnViolation` возвращают
`DimensionsMM = oldDims` без записи команды — в хронике не остаётся ничего, и «значение само
вернулось» становится необъяснимым.

## Fixing how something is CREATED does not fix what was already created

Placement, defaults and derived fields are applied at creation. A saved project carries the
numbers it was saved with, and the loader puts them back verbatim — that is the point of a save
file, and «чужие проекты не трогаем» is the rule.

So a fix to a factory or to a placement rule leaves every existing project exactly as broken as
it was, and the user will meet it as «you said you fixed it». Facades created before the
placement fix stay sunk into the floor by their bottom reveal and keep reporting a violation
until someone moves them by hand; assembled facades saved with no gaps stay with no gaps.

Two obligations follow, and both are cheap:

- **Say it in the report and in the release notes.** «New X is now correct; existing X keeps
  its saved value» is one sentence, and without it the fix reads as not working.
- **Decide deliberately whether a migration is wanted, and never write one by accident.** A
  loader that recomputes a saved value rewrites the user's work on open — silently, with no undo.
  If a migration IS wanted, it needs its own sentinel and its own test that it does not run
  twice (see «A field that must tell "old file" from "value 0"» above).
