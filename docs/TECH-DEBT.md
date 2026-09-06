# Реестр технического долга

Сводка аудита от 2026-09-07. Семь исполнителей просканировали репозиторий по областям;
находки приняты или отклонены координатором **после независимой перепроверки** — здесь
только то, что подтвердилось командой, а не то, что заявил исполнитель. Сырые отчёты
исполнителей: `tmp-scripts/scan-{A,B,D,E}-*.md` (вне git).

Порядок разделов — порядок работы. Пункт закрывается вычёркиванием строки и коммитом.

---

## Закрыто 2026-09-07 — приоритет 1 целиком

Пять задач раздано параллельно четырём субагентам Claude и одному opencode; guard-файлы и
прогон Unity — координатора. EditMode после кампании: **4381 тест, Failed: 0**.

| Было | Стало | Коммиты |
|---|---|---|
| 1.1 `DoorElement` и `WindowElement`: 414 совпадающих строк | общая база `WallOpeningElement`, у наследников — подгон по высоте, своя геометрия, свои `PerfMarkers` | `41bd2be0` |
| 1.2 `CooktopElement` и `SinkElement`: дублированная врезка | общая база `PartCutoutElement`, крючки `CutoutExtentsMM`/`RimHeightMM`/`AcceptsHost`; публичный API не менялся | `f891189f` |
| 1.3 десять мёртвых символов | удалены все; `InputCapture.cs` — последним, вместе со строкой в ratchet-таблице, которая его держала | `f0d2b826`, `f087ddb4`, `d14b719a`, `056a2ad4`, `ed716272`, `df2b8064`, `361e8fd3`, `def08692`, `4a5b806d` |
| 1.4 три клона секций меню с тремя копиями `Fingerprint` | база `ContextMenuListSection<T>`: хэш и раскрытие в базе, у секций — источник данных и рисование строки | `da574e9f` |
| 1.5 `SetGrooves`/`SetTextureOverlays`/`SetSwitchLights` | один `SetListCommand<T>` | `361e8fd3` |
| 1.6 лестница типов в `ConstraintValidator` без сторожа | вопросы ушли в `ValidationSnapshot`; заведён `ValidationLadderTests`, сканирующий `Core/Validation` | `1666e323`, `24456736` |
| 1.7 инструкции звали на `server/` и `deploy.cmd` с чужой ветки | вычищено из `agents/TESTS.md`, `agents/FLEET.md`, `LEAD-AGENT.md` | вне git |

Три урока, купленные этой кампанией:

- **Сторожа не знали про абстрактные базы.** `ElementTypeCatalog` считал типом элемента всякий
  класс с цепочкой до `KitchenElement`, поэтому каждый SRP-рефакторинг по образцу 1.1/1.2
  краснел одинаково: промежуточную базу «не зарегистрировали» в шести реестрах, хотя
  зарегистрировать её негде — регистрируют то, что можно создать. Теперь абстрактные
  исключены из ответа, но ОСТАЛИСЬ в цепочке наследования: убери их оттуда — и потомки
  перестанут доходить до базы, выпав из сторожа молча.
- **`git-commit.ps1` коммитил весь индекс.** `-Files` только добавлял пути, а `git commit`
  забирал всё застейдженное, включая чужое. При параллельной работе это значит чужой
  недописанный файл в твоём коммите. Починено pathspec-коммитом (`779cec4b`); подробности —
  `agents/FLEET.md` → «The git index is shared too».
- **Ratchet держит удаление файла.** Мёртвый `InputCapture.cs` нельзя было удалить, пока на
  него ссылалась строка бюджета в `ToleranceLiteralTests`: удаление файла ломает сторожа,
  проверяющего, что каждая строка бюджета называет существующий файл. Guard-файл и
  продакшен-файл уходят ОДНИМ коммитом.

---

## Приоритет 2 — чинить попутно, когда правишь рядом

### 2.1 Одно значение — несколько независимых констант

- **18 мм, толщина плиты:** `Geometry/AppConstants.cs:11` (`BOARD_THICKNESS_DEFAULT`),
  `MCP/ElementSpawners.cs:48` (`BOARD_DEFAULT_THICKNESS_MM`), `Elements/BasePlate.cs:8`
  (`PLATE_THICKNESS`). Причём `ElementSpawners.cs:67` уже читает первую — то есть файл
  одновременно пользуется общей константой и своей копией.
- **4 мм, стекло:** `AppConstants.cs:21` (`ASSEMBLED_GLASS_THICKNESS_MM`) и `:41`
  (`WINDOW_GLASS_THICKNESS_MM`).
- **50 мм, порог привязки:** мёртвая `AppConstants.SNAP_THRESHOLD` удалена (`df2b8064`), но
  значение по-прежнему живёт тремя литералами `50f` — `Pure/Infrastructure/KitchenSettings.cs:13`
  и `:304`, `Pure/Persistence/KitchenSettingsData.cs:9`. Имени у порога так и нет; заводить его
  придётся в `Pure`, потому что ссылка из `Pure` в `Geometry` ломает слои.
- **0,4 с, длительность открывания:** после кампании осталось три места —
  `WallOpeningElement.cs:17` (дверь и окно слились), `FacadeElement.cs:90`, `DropDoor.cs:10`;
  плюс одинаковые строки `KeepAwake(OpenSeconds + 0.2f)`. `DrawerElement.cs:32` уже показывает
  правильный путь — ссылается на `DrawerConstants.DRAWER_ANIM_DURATION`.

### 2.2 Формулы без общего хелпера

- `0.5f * AppConstants.MM_TO_UNITS` — **16 вхождений в 12 файлах** (`CooktopElement`,
  `DrawerElement`, `DrawerLinks`, `PillarAutoFit`, `PlacementController`, `ScrewLegAutoFit`,
  `ScrewLegElement`, `UI/ElementSpawner`, `SinkElement`, `WallSeating`, `BasePlate`). Нужен один
  `AppConstants.HalfHeightUnits(mm)`.
- `Geometry/FaceRects.cs:7` и `Geometry/ResizeSnap.cs:96` считают одну и ту же проекцию грани
  на оси `u`/`v`.
- ~~`Geometry/ResizeSnap.cs:108` повторяет `Tolerance.IntervalsOverlap`~~ — **НЕ дубль, сводить
  нельзя.** Эпсилон направлен в противоположные стороны: `Overlap` даёт `left <= right +
  SnapEpsilon` (касание — это контакт), `IntervalsOverlap` даёт `min1 < max2 - margin`
  (касание — НЕ пересечение). `ResizeSnapBoundaryTests.MoveAndResize_BothTakeAnEdgeOnly
  FootprintTouch_AsContact` требует, чтобы стойка торцом у кромки панели прилипала; с общим
  примитивом он краснеет. Два похожих на вид решения с противоположным смыслом на границе.
- `Geometry/PinholeView.cs:67` и `Measure/MeasureGeometry.cs:57` — два `WorldSizeForPixels` с
  одинаковыми орто- и перспективной ветками.

### 2.3 Тринадцать проглоченных исключений

Пустые `catch {}` в продакшене: `Rendering/PhotoQualityController.cs:350,361,371`,
`Update/UnityWebRequestDownloader.cs:34,132`, `UI/SettingsMcpTab.cs:37-39` и другие — всего 13.
Каждый нужно либо сузить до конкретного типа с комментарием в тесте, либо дать логировать.

### 2.4 Дубли в тестах

СДЕЛАНО: `Assets/Tests/EditMode/McpTestFixture.cs` — общая база для файлов с MCP-обработчиком.
Снимает `MakeReq` (было в 14 файлах), `_handler = new McpCommandHandler()` + тирдаун
`_spawned`/`PartRegistry.Clear()` (было в 19 файлах, 17 сведены — 2 намеренно оставлены, см.
ниже) и тело `MakeElement` (было идентично в 6 файлах). Тирдаун — шаблонный метод: базовый
[SetUp]/[TearDown] чистит только то, что было общим БУКВАЛЬНО во всех копиях; каждый наследник,
которому нужен ещё один сброс (`GroupManager`, `CommandStack`, `ProjectRooms`, ...), объявляет
СВОЙ [SetUp]/[TearDown] с этой добавкой — NUnit гарантирует порядок база→наследник для SetUp и
наследник→база для TearDown. Три файла (`ApplianceRotationTests`, `DishwasherElementTests`,
`OvenElementTests`) сохранили свой полный TearDown как есть (меню/канва, ручной
`PartRegistry.Unregister`, `ElementFactory.ClearPools()`, `MaterialManager.ClearCache()`) —
это не подмножество общего шаблона, а другой сценарий, сливать его в общий метод означало бы
расширить то, что каждый конкретный тест чистит.

НЕ СЛИТО, намеренно:
- `McpConvertElementsReproTests.cs`, `McpSettingsParityTests.cs` — используют
  `_handler = new McpCommandHandler()`, но фикстура вокруг него другая (`EveryElementType` +
  `ProjectLoadStateGuard` в первом, снимок/восстановление `KitchenSettings` во втором); ни
  `PartRegistry.Clear()`, ни `_spawned` там нет. Заворачивать их в `McpTestFixture` означало бы
  дописывать поведение, которого раньше не было.
- `MakeFacade`/`MakeElement`, повторённые ЕЩЁ в ~19 файлах вне семьи MCP-обработчика
  (`FacadeElementTests`, `SceneTreeTests`, `GroupServiceTests`, `AttachLinksTests` и другие,
  плюс один в PlayMode) — разные базовые классы, разные тирдауны, не изучены в рамках этого
  прохода. Общий `ElementTestBase` для них — отдельная задача: проверить фикстуру каждого файла
  так же внимательно, как здесь, а не механически переименовать методы.
- `SnapTestBase` (EditMode, сценовый) и `Geometry/SnapCoreTestBase` (снимки `ElementGeometry`,
  компилируется и Unity, и `dotnet` — см. `agents/TEST-DESIGN.md` → «A test under
  `Assets/Tests/EditMode/Geometry/` is a CORE test, not a scene test»). Разделены границей
  сборки: `SnapCoreTestBase` не имеет права видеть `GameObject`/`KitchenSettings`, иначе
  `tools/mutation-test.ps1 -TestsOnly` и Stryker перестанут её компилировать. Порог `50f`
  продублирован (`SnapTestBase` берёт его из `KitchenSettings.SnapThreshold`, `SnapCoreTestBase`
  задаёт как явную константу `Threshold`, с комментарием о причине) — это единственное, что
  МОЖНО было бы вынести, но общего слоя, который видят обе сборки и куда стоило бы класть тестовую
  константу (а не производственную), сейчас нет; заводить его ради одного числа — накладнее, чем
  два раза написать `50f`. Оставлено как есть.

Что делать дальше: `ElementTestBase` для оставшихся ~19 файлов. Работа механическая и
проверяется командой — кандидат в opencode.

### 2.5 Мелочи UI

- Привязка «Скругление» (`CornerRadiusMM`) продублирована в `ChairFieldsEditor.cs:15`,
  `SofaFieldsEditor.cs:15`, `StoolFieldsEditor.cs:11`, `PouffeFieldsEditor.cs:25`.
- Высота полосы перетаскивания окна — литерал `40f` в семи местах (плюс `44f` в
  `ProjectInstructionsPanelUI.cs:23`, и неясно, опечатка это или намерение).
- Цвета мимо `UIStyle`: `(0.38, 0.40, 0.46)` в `IssueTableView.cs:26`,
  `HierarchyPanelUI.cs:151`, `SpecificationPanelUI.cs:116`.
- `ExpressionParser.IsValidDimensionChar` продублирован трижды.
- `new LegSet(transform, "Leg")` — четыре одинаковые ленивые инициализации.

---

## Приоритет 3 — принято, не чинить сейчас

- **God-классы.** `KitchenElement` (581 строка: пазы, вырезы, кромка, зазоры, текстуры,
  подвес), `ContextMenuUI` (900 строк, 25 редакторов в конструкторе), `ElementMover`
  (602 строки: перетаскивание + призрачный меш + подкраска + дублирование/удаление).
  Формально это нарушение `conventions/STRUCTURE.md`. Разбирать их отдельным заходом дорого и
  рискованно: половина не компилируется без Unity. Правило — **резать по кусочку, когда
  правишь этот файл по другому поводу**, а не заводить «рефакторинг ради рефакторинга».
- **Test-only API.** `MaterialPreviewActive`, `ToggleTextures`, `FindDishwasherForFacade` в
  `ContextMenuUI` живут только ради тестов. Это слабее мёртвого кода: удалять нельзя, но и
  считать их частью поверхности не стоит.
- **`SidebarCatalog`** — 30 булевых свойств `is*` вместо таблицы данных
  (`SidebarCatalog.cs:50-109`).
- **Бойлерплейт `*FieldsEditor`** — константы `Node`/`Label` плюс `Bind` на каждый элемент.
  Скучно, но прозрачно; трогать только вместе с генерацией панелей.

---

## Отклонено (проверено, находкой не является)

- **`WallManager.RoomMode` мёртв** — нет: используется трижды в собственном файле
  (`WallManager.cs:38,71,78`) и покрыт десятком тестов.
- **Удалить `CLAUDE.md` как дубль `AGENTS.md`** — нет: это точка входа агента, её читает
  харнесс.
- **`TODO` в `Tests/PlayMode/SimpleGif.cs`** — чужой вендоренный GIF-кодировщик, не наш долг.
- **Дыра в «Карте инструкций» по `mcp-v2/PLAN.md`** — файл на месте, строка в таблице есть.

Отдельно: `TODO`/`FIXME`/`HACK` и закомментированного кода в продакшене **ноль**. Кампания
вычистки комментариев держится, эту категорию искать больше не нужно.

---

## Вне кода: свод правил не под версионным контролем

Корень git — `kd-repose/`. Всё, что выше: `AGENTS.md`, `LEAD-AGENT.md`, `agents/*.md`,
`CONVENTIONS.md`, `conventions/*.md`, `FEATURES.md`, `tasks/` — **вне репозитория**. У свода,
на который опирается каждый агент, нет ни истории, ни отката, ни бэкапа: неудачная правка
инструкции невосстановима, а `LEAD-AGENT.md` §2 при этом требует, чтобы общие правила менял
только координатор — проверить, кто и что изменил, нечем.

Решение за пользователем; вариантов два — второй репозиторий на уровне
`F:\repos\KitchenDesigner2`, либо перенос свода внутрь `kd-repose/`.

Заодно наверху лежит мусор, ничем не упоминаемый: `kitchen-plan.md`, `dns-induction-p1.md`
(3439 строк дампа DOM), `tmp-scripts/scrape_lmdf.ps1`, и байт-в-байт дубль
`GTV AXIS PRO.md` = `GTV/GTV AXIS PRO.md`.
