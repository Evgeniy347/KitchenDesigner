# Реестр технического долга

Сводка аудита от 2026-09-07. Семь исполнителей просканировали репозиторий по областям;
находки приняты или отклонены координатором **после независимой перепроверки** — здесь
только то, что подтвердилось командой, а не то, что заявил исполнитель. Сырые отчёты
исполнителей: `tmp-scripts/scan-{A,B,D,E}-*.md` (вне git).

Порядок разделов — порядок работы. Пункт закрывается вычёркиванием строки и коммитом.

---

## Приоритет 1 — брать в работу

### 1.1 `DoorElement` и `WindowElement` — 414 строк дословной копипасты

`Assets/Scripts/Core/Elements/DoorElement.cs` (560 строк) и `WindowElement.cs` (627 строк).

Проверка: после нормализации `Window→Door`/`window→door` совпадают **414 непустых строк** —
74 % файла двери. Дублируются целиком `StepDoor`, `ApplyDoorPose`, `GetOpenBoxes`,
`SnapToWall`, `FindNearestWall`, `DistanceToWall`, `FindAttachedWall`, `UpdateCollider`,
конечный автомат `SetOpen/ToggleOpen/ForceClose/CycleOpenState`, `ApplyMaterialFrame`,
`PaintFrame`, приватный `DestroyGroup`. Расходится по существу только Y-подгон в `AlignToWall`.

Что делать: общий базовый класс «проём в стене, который открывается» — состояние, шаг позы,
поиск и регистрация стены, коллайдер; наследники дают только правило подгонки по высоте и
свои `PerfMarkers`.

Риск: класс живёт под `-nullable`, компилируется только через координатора (`Elements` не
входит в быстрый путь). Резать мелкими кусками, каждый — отдельный коммит.

### 1.2 `CooktopElement` и `SinkElement` — общая подсистема выреза продублирована

`CooktopElement.cs:79-485` и `SinkElement.cs:46-307`. Одинаковы ленивая настройка
`PartMount`, `SnapToPart`, `AttachToPart`, `FindCatchingPart`, `ClampOffsets`, `CutoutRectIn`,
`DescribeCatch`, `UpdateCollider`, `PrepareForDestruction`. Различаются только источник размера
выреза и отслеживание рыскания.

Плюс два одинаковых допуска в обоих файлах: `ALIGNED_ROTATION_EPSILON_DEG = 0.05f`
(`CooktopElement.cs:35`, `SinkElement.cs:26`).

Что делать: общий базовый тип «элемент, врезаемый в деталь», с крючками `YawDeg` и
`CutoutExtents` у наследников.

### 1.3 Мёртвый код — удалить

Каждая строка проверена: у символа ровно одно вхождение в репозитории — собственное
объявление, и ни одной ссылки ни из продакшена, ни из тестов, ни из сцен.

| Что | Где | Проверка |
|---|---|---|
| Весь файл: 5 структур событий `ElementCreatedEvent`, `ElementMovedEvent`, `ProjectLoadedEvent`, `SelectionChangedEvent`, `PartRemovedEvent` | `Core/Infrastructure/KitchenEvents.cs` | по 1 вхождению на каждую; никто не публикует и не подписывается |
| Класс отладочной клавиатуры `InputCapture` | `Core/Infrastructure/InputCapture.cs:5-70` | `MonoBehaviour`, но GUID `ecb5bc37…` не встречается ни в одной сцене, префабе или ассете; ссылок из кода нет |
| `SideHighlighter.ShowFaceWithBands` | `Core/Rendering/SideHighlighter.cs:74` | 1 вхождение |
| `ResizeHandleManager.AxisX/AxisY/AxisZ` | `Core/Snap/ResizeHandleManager.cs:37-39` | 3 внутренние константы, 0 ссылок (одноимённые символы в `HandleMaterials` и `ScrewLegCentring` — живые, это не путаница) |
| `ContextMenuUI.TexturePreviewActive` | `Core/UI/ContextMenuUI.cs:118` | 0 ссылок даже из тестов |
| `ContextMenuUI.FindDrawerForFacade` | `Core/UI/ContextMenuUI.cs:828` | 0 ссылок даже из тестов |
| `BuildInfo.FullVersion` | `Core/Infrastructure/BuildInfo.cs:5` | 1 вхождение |
| `ViewState.VisibilityHash` | `Core/Infrastructure/ViewState.cs:45` | 1 вхождение; рядом самопальная копия в `SceneVisibilityManager.cs:54` |
| `FrameRateManager.OnBrowserActivity` | `Core/Infrastructure/FrameRateManager.cs:50` | 1 вхождение; в `.jslib`/`.html` тоже не зовётся |
| `AppConstants.SNAP_THRESHOLD = 50f` | `Core/Geometry/AppConstants.cs:10` | 1 вхождение — см. 2.1, значение живёт тремя литералами |

### 1.4 Секции контекстного меню — три клона одного скелета

`ContextMenuGrooveSection.cs` (194 строки), `ContextMenuTextureSection.cs` (323),
`ContextMenuLightLinkSection.cs` (210).

Три независимые приватные реализации `Fingerprint(...)` — `ContextMenuGrooveSection.cs:183`,
`ContextMenuTextureSection.cs:312`, `ContextMenuLightLinkSection.cs:200` — считающие один и тот
же хэш «изменился ли список». Плюс общий скелет `_expanded` / `Collapse()` / `Toggle()` /
`Count()` / `AfterChange()` / `Eligible()` / `Target => _host.Target`.

Что делать: базовый `ContextMenuListSection<T>` с хэшем списка и раскрытием; секции задают
только источник данных и рисование строки. Тесты секций (6 файлов с одинаковым `Setup()`)
поедут за базой.

### 1.5 `SetGroovesCommand` и `SetTextureOverlaysCommand` — одна команда, написанная дважды

`diff` двух файлов по 31 строке даёт различия **только** в имени класса, параметре `T` и
вызываемом сеттере. Рядом такой же `SetSwitchLightsCommand`.

Что делать: один `SetListCommand<T>(element, before, after, setter, description)`.

Смежное: `Core/Commands/UndoableProperties.cs:31` уже умеет отмену через рефлексию (атрибут
`[Undoable]`), а эти команды пишут before/after руками — две параллельные системы отмены для
свойств одного и того же элемента. Решить, какая из них главная, — часть этой же задачи.

### 1.6 Лестница типов в `ConstraintValidator` — нарушение правила без сторожа

`Core/Validation/ConstraintValidator.cs` — 8 прямых проверок вида `is XxxElement`
(строки 210, 211, 242, 280, 294, 343, 372, 395), при том что `agents/SUBSYSTEMS.md:18`
объявляет `Validation/ValidationSnapshot.cs` **единственным** местом, где такой вопрос
разрешён.

Ключевое: сторожа нет. `GeometryArchitectureTests` сканирует только `Core/Geometry` и
`Core/Pure`, `UiElementTypeLadderTests` — только слой UI. `Core/Validation` не сканирует никто,
поэтому правило нарушалось молча.

Что делать: перенести проверки в флаги `ValidationSnapshot` и **завести
`ValidationLadderTests`** по образцу двух существующих. Без теста починка протухнет заново —
`agents/TEST-DESIGN.md` → «дефект, о котором платформа не умеет доложить, невидим, пока не
построен сенсор».

### 1.7 Инструкции ссылаются на то, чего на ветке нет

Это долг в документах, а не в коде, но стоит он дороже: по этим строкам исполнители выбирают
команду проверки и получают ошибку вместо результата.

- `agents/TESTS.md:24`, `agents/FLEET.md:15`, `LEAD-AGENT.md:80` предписывают гонять
  `dotnet test "server\KitchenServer.Tests\KitchenServer.Tests.csproj"`. Каталога `server/` на
  ветке `develop` нет.
- `agents/TESTS.md:11-13` описывает `run-webgl.cmd`, `deploy.cmd`, `build-server.cmd`. В
  `kd-repose/` лежат только `build.cmd`, `clean.cmd`, `run-desktop.cmd`.
- `LEAD-AGENT.md:29` приводит `server/OPERATIONS.md` как пример узкой инструкции.
- При этом `agents/DEPLOY.md:10` прямо пишет, что ничего этого нет. То есть два документа
  свода противоречат друг другу, и побеждает тот, который агент прочитал первым.

Что делать: пометить эти строки как «только ветка `webgl/develop`» либо убрать. Правит
координатор — файлы общие (`LEAD-AGENT.md` §2).

---

## Приоритет 2 — чинить попутно, когда правишь рядом

### 2.1 Одно значение — несколько независимых констант

- **18 мм, толщина плиты:** `Geometry/AppConstants.cs:11` (`BOARD_THICKNESS_DEFAULT`),
  `MCP/ElementSpawners.cs:48` (`BOARD_DEFAULT_THICKNESS_MM`), `Elements/BasePlate.cs:8`
  (`PLATE_THICKNESS`). Причём `ElementSpawners.cs:67` уже читает первую — то есть файл
  одновременно пользуется общей константой и своей копией.
- **4 мм, стекло:** `AppConstants.cs:21` (`ASSEMBLED_GLASS_THICKNESS_MM`) и `:41`
  (`WINDOW_GLASS_THICKNESS_MM`).
- **50 мм, порог привязки:** именованная `AppConstants.SNAP_THRESHOLD` мертва, а реальное
  значение — три литерала `50f` в `Pure/Infrastructure/KitchenSettings.cs:13` и `:304` и
  `Pure/Persistence/KitchenSettingsData.cs:9`.
- **0,4 с, длительность открывания:** `DoorElement.cs:22`, `FacadeElement.cs:90`,
  `WindowElement.cs:21`, `DropDoor.cs:10`; плюс четыре одинаковые строки
  `KeepAwake(OpenSeconds + 0.2f)`. `DrawerElement.cs:32` уже показывает правильный путь —
  ссылается на `DrawerConstants.DRAWER_ANIM_DURATION`.

### 2.2 Формулы без общего хелпера

- `0.5f * AppConstants.MM_TO_UNITS` — **16 вхождений в 12 файлах** (`CooktopElement`,
  `DrawerElement`, `DrawerLinks`, `PillarAutoFit`, `PlacementController`, `ScrewLegAutoFit`,
  `ScrewLegElement`, `UI/ElementSpawner`, `SinkElement`, `WallSeating`, `BasePlate`). Нужен один
  `AppConstants.HalfHeightUnits(mm)`.
- `Geometry/FaceRects.cs:7` и `Geometry/ResizeSnap.cs:96` считают одну и ту же проекцию грани
  на оси `u`/`v`.
- `Geometry/ResizeSnap.cs:108` вручную повторяет `Tolerance.IntervalsOverlap`, которым уже
  пользуются шесть других мест.
- `Geometry/PinholeView.cs:67` и `Measure/MeasureGeometry.cs:57` — два `WorldSizeForPixels` с
  одинаковыми орто- и перспективной ветками.

### 2.3 Тринадцать проглоченных исключений

Пустые `catch {}` в продакшене: `Rendering/PhotoQualityController.cs:350,361,371`,
`Update/UnityWebRequestDownloader.cs:34,132`, `UI/SettingsMcpTab.cs:37-39` и другие — всего 13.
Каждый нужно либо сузить до конкретного типа с комментарием в тесте, либо дать логировать.

### 2.4 Дубли в тестах

- `MakeReq` — построитель MCP-запроса, скопированный в **14 файлов**.
- Тирдаун `_spawned` + `PartRegistry.Clear()` — 30+ повторов.
- `_handler = new McpCommandHandler()` — 19 файлов.
- `MakeElement`/`MakeFacade` — по 4–5 идентичных тел.
- Две параллельные базы: `SnapTestBase` и `Geometry/SnapCoreTestBase`, включая одинаковый
  порог `50f`.

Что делать: один `McpTestFixture` и один общий `ElementTestBase`. Работа механическая и
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
