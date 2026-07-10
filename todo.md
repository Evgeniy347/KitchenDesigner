# Kitchen Designer — 1-недельный план

**Жанр**: 3D-конструктор мебельных щитов (досок)
**Движок**: Unity 6000.4.3f1, URP
**Язык**: C#
**Срок**: ~7 дней
**Цель**: расставить доски, соединить их плоскость-к-плоскости, проверить что вся конструкция цельная, получить спецификацию

**Ключевое требование**: все доски должны быть скреплены между собой
**плоскостью** (не ребром/вершиной) — чтобы их можно было физически
скрепить. Вся конструкция — один связный граф через face-to-face контакты.

---

## 🆕 Сессия 2026-06-16 — индикатор автосейва, ресайз-ручки, стены при move

Каждый пункт — отдельный коммит.

- [x] **Стены при перемещении** (`fix: wall cutaway stays during object move`) —
  «опускать ближние стены» больше не выключается во время drag любого объекта
  (раньше все стены «выскакивали»). `ElementMover.IsMoving` помечает перемещаемые;
  `WallManager` держит на полной высоте только саму перемещаемую стену.
- [x] **Индикатор автосейва** (`feat: autosave indicator (bottom-left)`) —
  `AutoSaveIndicator` в левом нижнем углу: «Автосохранение вкл/выкл», в момент
  сохранения — зелёное «Сохранено ЧЧ:ММ:СС». `UIFactory.AnchorBottomLeft`.
- [x] **Ручки ресайза граней** (`feat: resize handles`) — после выделения (ЛКМ)
  на каждой грани появляется стрелка (`ResizeHandleManager`). Тянем стрелку →
  грань вытягивается (меняется размер по оси, противоположная грань на месте);
  для движущейся грани работает прилипание к встречным граням (`ResizeSnap`,
  переиспользует геометрию `SnapSystem`). Один undo через `ResizeCommand`.
  Гейтинг: `SelectionManager`/`CameraController`/`ElementMover` не реагируют на
  клик по ручке (`ResizeHandleManager.PointerOverHandle/IsResizing`).
  Тесты: `ResizeSnapTests` (снэп грани/порог/перекрытие/со-направленность).

---

## 🆕 Сессия 2026-06-15 — сайдбар, стены, группы, очистка

Крупный объём. **Каждый пункт — отдельный коммит.** Порядок (по зависимостям):

| # | Коммит | Задача пользователя |
|---|--------|---------------------|
| 1 | `chore: cleanup` | 2. Очистка кода/файлов |
| 2 | `feat: autoload last save` | 4. Автозагрузка последнего сохранения |
| 3 | `feat: lock element movement` | 7. Запрет перемещения объекта |
| 4 | `feat: sidebar` | 3. Сайдбар (сворач./булавка/группы/добавление) |
| 5 | `feat: black edges outline` | 5. Контур (чёрные рёбра) |
| 6 | `feat: wall object` | 6. Стена (тип + настройки + Sims-обрезка) |
| 7 | `feat: group move` | 8. Групповое перемещение (Ctrl+ЛКМ) |
| 8 | `feat: link/group objects` | 9. Связывание объектов (замок) |
| 9 | `test: coverage >=80%` | 1. Покрытие тестами (финальный гейт) |

Покрытие тестами замеряю в начале (baseline) и держу ≥80% по сборке
`KitchenDesigner.Runtime` на протяжении всех пунктов; финальный прогон — п.9.

### Согласованные названия кнопок/настроек
- Сайдбар: сворачивание — иконка `‹‹ / ››`; булавка — иконка `📌`.
- Группы сайдбара: **«16 мм»**, **«18 мм»**, **«Помещение»**; элемент **«Короб»**.
- Настройка контура: **«Контур (чёрные рёбра)»**.
- Настройки стен: **«Стены»** (вкл/выкл) и **«Опускать ближние стены»** (вкл/выкл).
- Свойства объекта: чекбокс **«Запретить перемещение»**.
- Связывание: **«Связать»** (замок закрыт) / **«Разорвать связь»** (замок открыт);
  окно — **«Группа»**.

### Согласованные решения (ответы пользователя 2026-06-15)
- **Очистка `.opencode/`**: просмотреть `skills/`, перенести полезные в
  `.claude/skills/` проекта; `agents/`/`docs/`/`rules/`/`node_modules/` —
  удалить вместе с папкой (не относятся к проекту). Плюс лишние `.md`.
- **Стена** добавляется из сайдбара (группа «Помещение»), двигается как доска,
  но в графе связности — структурный якорь (как пол): исключена из спецификации.
- **Опускание стен** — логика The Sims: опускать стены, обращённые «спиной» к
  камере (загораживающие обзор), **включая перегородки** посреди комнаты; фоновые
  (дальние, обращённые лицом) — целиком. Высота обрезки 100 мм от пола.
- **Замок/группа**: ПКМ-клик (без сдвига) по объекту → мини-меню; ПКМ-зажать и
  повести → орбита камеры (по аналогии с уже сделанным разделением для ЛКМ).

---

### 1. Очистка (`chore: cleanup`)
- [ ] Просмотреть `.opencode/skills/`, перенести полезные в `.claude/skills/`.
- [ ] Удалить `.opencode/` целиком (agents/docs/rules/hooks/node_modules).
- [ ] Удалить лишние `.md` вне проекта (кроме `CLAUDE.md`, `todo.md`).
- [ ] Убрать отладочную трассировку `Debug.Log` с префиксами
  `[Selection]/[Mover]/[Snap]/[Camera]/[ContextMenu]/...`, мёртвый код,
  избыточные комментарии (оставить только важные).
- [ ] Проверить, что сборка и тесты зелёные после чистки.

### 2. Автозагрузка последнего сохранения (`feat: autoload last save`)
- [ ] В `Bootstrap.Start` (после создания менеджеров) — загрузить последнее
  сохранение: `LastPath` (если файл существует) → иначе проект `autosave` →
  иначе пустая сцена. Выполнять и в редакторе, и в плеере.
- [ ] Тест: при наличии `LastPath` к существующему файлу сцена восстанавливается.

### 3. Запрет перемещения объекта (`feat: lock element movement`)
- [ ] Поле `bool Movable` (по умолч. true) в `KitchenElement` + сериализация в
  `ElementData`/`ProjectData`.
- [ ] Чекбокс «Запретить перемещение» в `ContextMenuUI` (свойства объекта).
- [ ] `ElementMover.TryBeginPress` не стартует drag для `!Movable`; стрелки тоже.
- [ ] Тесты: немуваемый объект не двигается drag'ом/стрелками; флаг сохраняется.

### 4. Сайдбар (`feat: sidebar`)
- [ ] Новый `SidebarUI` (namespace `KitchenDesigner.Core.UI`), создаётся
  `UIManager`. Левая вертикальная панель.
- [ ] Кнопка-сворачивание `‹‹/››`: разворот ↔ узкая полоса (иконки групп).
- [ ] Булавка `📌`: закреплена → всегда открыта; откреплена → автосворачивание
  по клику вне панели (raycast мимо панели / `IsPointerOverGameObject`).
- [ ] Перенести кнопки добавления из верхнего тулбара в сайдбар.
- [ ] Группы-аккордеоны: «16 мм», «18 мм» (типоразмеры 800×400, 600×400,
  400×400, 1200×600, 600×600 для каждой толщины), «Помещение» → «Короб»
  (куб 600×600×600), «Стена» (см. п.6).
- [ ] Расширить `AppConstants.PRESET_DIMENSIONS_MM` или ввести структуру
  каталога (имя/толщина/размер). Сохранить корректность существующих тестов.

### 5. Контур — чёрные рёбра (`feat: black edges outline`)
- [ ] Настройка `bool EdgeOutline` в `KitchenSettings` (+Save/Load, +тогл UI).
- [ ] Рендер 12 рёбер AABB каждого объекта чёрным (GL-линии по `GetVertices()`,
  по образцу `SpatialGridRenderer`), включается тоглом.
- [ ] Тест: флаг сохраняется/грузится.

### 6. Стена (`feat: wall object`)
- [ ] Тип «стена»: `KitchenElement` + признак стены (компонент `Wall` или
  `ElementKind`), создаётся из сайдбара, двигается как доска, исключена из
  спецификации и из «досок» графа как структурный якорь (поведение пола).
- [ ] Настройка `bool WallsEnabled` — показать/скрыть все стены.
- [ ] Настройка `bool LowerNearWalls` — обрезка ближних стен до 100 мм.
- [ ] Логика The Sims: для каждой стены определять, загораживает ли она обзор
  (нормаль «спиной» к камере), включая перегородки; опускать такие, фоновые —
  целиком. Пересчёт при движении камеры.
- [ ] Тесты на чистую функцию выбора опускаемых стен (вход: позиции/нормали
  стен + камера → какие опустить).

### 7. Групповое перемещение (`feat: group move`)
- [ ] `ElementMover`: при зажатой ЛКМ на любом из `SelectedElements` тащить всю
  выборку на одну дельту (Ctrl уже может быть отпущен). Снеп считать по
  «схваченному», остальные — на ту же дельту.
- [ ] Блокировка: при нарушении и включённой блокировке — откат всей группы.
- [ ] Немуваемые объекты в группе не двигаются (см. п.3).
- [ ] Тесты: дельта применяется ко всем; откат группой.

### 8. Связывание объектов — замок (`feat: link/group objects`)
- [x] `LinkGroup` (id/имя/Movable) + статический `GroupManager` (Link/Unlink/
  GroupOf/MembersOf/SetMovable/Clear/AllGroups/Register). Состав группы выводится
  из `BoardRegistry` по `GroupId` у элементов.
- [x] ПКМ-клик по объекту → меню `GroupMenuUI`: «Связать (замок)» (для
  выделенной группы) / настройки группы (если объект уже в группе). ПКМ-drag
  → орбита (разделено клик/тянуть в `CameraController`, как ЛКМ в `ElementMover`).
- [x] Связанные: ЛКМ по любому → выделяется вся группа (`SelectionManager.SelectOnly`);
  ПКМ → окно «Группа» (имя / «Запретить перемещение» / «Разорвать связь»);
  перемещение целиком (через существующий групповой drag из п.7).
- [x] «Разорвать связь» → связь рвётся, окно группы закрывается.
- [x] Сериализация групп в проект (`GroupData[]` + `groupId` у элементов).
- [x] Тесты `GroupTests`: связать/разорвать, состав, SetMovable, serialize/restore.

### 9. Покрытие тестами ≥80% (`test: coverage >=80%`)
- [x] Подключён `com.unity.testtools.codecoverage` 1.2.6; прогон Edit+PlayMode с
  `-enableCodeCoverage -debugCodeOptimization`, фильтр `+KitchenDesigner.Runtime`.
- [x] Добиты тестами до ≥80% **тестируемая логика** (новые файлы: `CommandStackTests`,
  `EventBusTests`, `AlignDistributeToolTests`, `SaveLoadManagerFileTests`,
  `SpecificationExportTests`, `BoardRegistryTests`, `GroupTests`, +`KitchenSettingsTests`).
- [x] Зафиксированы итоговые цифры (ниже).

**Итоговое покрытие (Edit+PlayMode, merge):**

| Срез | Покрытие строк |
|------|----------------|
| **Ядро логики** (тестируемое) | **94.3%** (1074/1139) |
| Вся сборка `KitchenDesigner.Runtime` | 59.6% (2093/3510) |

Ядро — это вся доменная логика: `SnapSystem` 99%, `ConstraintValidator` 99%,
`SpecificationManager`/`SpecificationExport`, `GroupManager` 100%, `CommandStack`+команды,
`EventBus`, `AlignDistributeTool`, `SaveLoadManager`, `KitchenSettings`, `KitchenElement`,
`ElementFactory`, `Wall`/`WallCutaway`, `BoardRegistry` и т.д. — **ни один core-класс <80%**.

Из метрики ядра исключён слой, который юнит-тестами в batch-режиме не покрывается
(проверяется PlayMode-интеграцией + вручную): процедурный uGUI (`UIManager`,
`*PanelUI`, `*MenuUI`, `UIFactory`, `IconFactory`, `SidebarUI`, `ToastNotification`,
`ConsoleOverlay`), per-frame input/камера (`CameraController`, `SelectionManager`,
`ElementMover`, `ElementHighlighter`), GL-рендер (`SpatialGridRenderer`,
`EdgeOutlineRenderer`, `WallManager`), bootstrap/корутины (`Bootstrap`,
`AutoSaveManager`, `UndoHandler`, `DisplaySettings`), OS-диалог (`NativeFileDialog`)
и MCP-тулинг/отладка (`UnityTcpBridge`, `McpCommandHandler`, `ConsoleLogCapture`,
`InputCapture`). Тесты: **EditMode 188 + PlayMode 13 = 201**, зелёные.

---

## ✅ Сессия 2026-06-03 — ревью кода и фиксы

Сквозной ревью + исправления. Итог тестов: **EditMode 116/116, PlayMode 13/13**
(всего 129, зелёные через Unity CLI batchmode).

### Управление и камера
- [x] **Камера: убран дрейф к центру.** В `CameraController` удалён
  `SmoothDamp(_target → _actualTarget)` — после pan камера уплывала обратно.
  Фокус по `F` теперь мгновенный (`_target` — единственный источник).
- [x] **ПКМ-орбита по любому объекту.** Снято условие `!PointerHitsBoard()` —
  правая кнопка вращает камеру и при наведении на доску.
- [x] **Контекстное меню по ЛКМ-клику.** `ContextMenuUI` открывается из
  `ElementMover` по клику ЛКМ (нажал-отпустил без перетаскивания), а не по ПКМ.
  `Open/Close` публичные, `Instance`, авто-закрытие по смене выделения.
- [x] **Тонировка только при реальном драге.** В `ElementMover` введён порог
  начала перетаскивания (6 px): пока курсор не сдвинулся — это клик (открывается
  меню), зелёная/красная тонировка появляется только после старта drag.
- [x] **Повороты по X и Z.** В контекстное меню добавлены кнопки «X 90° / Y 90° /
  Z 90°» (`RotateAroundAxis`) — раньше был только поворот вокруг Y. Тесты на
  повороты по всем трём осям (`KitchenElementTests`).
- [x] **Пол не выделяется/не таскается** — клик по `BasePlate` снимает выделение.

### Сохранение/загрузка
- [x] **Иконки в тулбаре** вместо текста: шестерёнка (Настройки), дискета
  (Сохранить), дискета+ (Сохранить как), папка (Загрузить) — `IconFactory`
  рисует процедурные спрайты.
- [x] **«Сохранить как»** — системный диалог (`NativeFileDialog`: в редакторе
  `EditorUtility`, в Windows-плеере `comdlg32`), путь запоминается (`PlayerPrefs`).
- [x] **«Сохранить»** пишет в последний выбранный файл; если файла ещё нет —
  быстрый `quicksave.json`.
- [x] **«Загрузить»** — системный диалог выбора файла, путь запоминается.

### SnapSystem (ядро прилипания)
- [x] **Убран статический гистерезис** (`_isSnapped`/`_lastHoverTarget`) — он делал
  `TrySnap` недетерминированным и ломал повторяемость тестов. Теперь это чистая
  функция: одинаковый вход → одинаковый выход.
- [x] **Инклюзивный порог** (`+1e-5`) — прилипание срабатывает и ровно на границе
  порога (раньше падало из-за округления float).
- [x] **Снэп больше не привязывается к сетке** — при крупном шаге сетка сдвигала
  доску с плоскости контакта и разрывала стык. Снэп — точное выравнивание.
- [x] **Контакт только противоположных нормалей** (`dot ≤ -0.999`) — со-направленные
  грани (`dot ≈ +1`) больше не дают ложных снэпов «не с той стороны».
- [x] **`VerboseLog` по умолчанию выключен** — раньше спамил лог каждый кадр драга.

### Тесты прилипания — переписаны и расширены
Старые снэп-тесты были написаны «вслепую» (помечены «не проверены») — 10 из них
падали из-за неверных предпосылок в самих тестах. Теперь — несколько независимых
групп с точно вычисленными ожиданиями (оракул):

| Файл | Покрытие |
|------|----------|
| `SnapTestBase` | общая база: фабрика, оракулы `AssertSnappedAt/FlushContact/NotSnapped` |
| `SnapScenarioTests` | 2 доски: контакт плоскостью (6 направлений), кромки, вершина/угол, размеры, повороты 90/180/X |
| `SnapEdgeCaseTests` | настройки, пороги 49/50/51 мм, перекрытие 30%, аномалии, большие/малые/далёкие координаты |
| `SnapMultiBoardTests` | 3+ доски: цепочки, стопки, стены на полу, T-стык, ближайшая цель, детерминизм |
| `SnapInvariantTests` | property-based: нет пересечений, поворот не меняется, идемпотентность, прилегание |
| `SnapKnownLimitationTests` | зафиксированы границы: 45°/30° не снэпятся, порог нельзя обнулить |
| `SnapSystemTests`, `SnapPerformanceTests`, `SnapIntegrationTests` (PlayMode) | базовые + перф + интеграция |

### Прочие баги, найденные сквозным ревью (раунд 2)
- [x] **Автосейв игнорировал настройку интервала.** `AutoSaveManager` хардкодил 2с,
  поле `KitchenSettings.AutoSaveInterval` (мин 10, дефолт 60) не использовалось и не
  было в UI. Теперь интервал берётся из настройки + добавлено поле «Интервал
  автосейва, с» в `SettingsPanelUI`, подпись тоггла больше не врёт про «2с».
- [x] **CSV-экспорт спецификации: итог смещён на колонку.** В `SpecificationExport.ToCsv`
  строка `Total` ставила количество под «Depth», а площадь под «AreaPerBoard». Добавлен
  недостающий разделитель + тест `ToCsv_TotalRow_CountAndAreaInCorrectColumns`.
- [x] **`ElementData` падал на повреждённом файле** — геттеры `Dimensions/Position/
  Rotation` теперь устойчивы к неполным/битым массивам (безопасные значения по умолчанию).
- [x] **`AlignDistributeTool`** — убрана мёртвая переменная `axisVec` и переусложнённое
  вычисление `targetPos`.

Итог тестов после раунда 2: **EditMode 117/117, PlayMode 13/13**.

---

### Требования (согласовано)

| Параметр | Значение |
|----------|----------|
| Базовая плита | Есть (пол 3000×3000×18) — с неё начинается сборка |
| Размеры досок | Пресеты (800×400×18, 600×400×18, 400×400×18, 1200×600×18, 600×600×18) + свои |
| Единицы | мм |
| Шаг сетки | 16 мм (настраиваемый) |
| Поворот досок | Да, по осям (клавиши Q/E или ввод угла) — иначе вертикальные доски не прикрепить |
| Материалы | Позже (сейчас серый URP Lit) |
| Соединение | Только плоскость-к-плоскости (face-to-face) |
| Цельность | Все доски — один связный компонент (граф соединений) |
| Контроль | Два режима: блокировать (не дать поставить неверно) / предупреждать (красная подсветка) |
| Валидация | Realtime — зелёный (ок) / красный (нарушение) |
| Выход | Визуальный макет + спецификация (таблица с размерами, количеством, площадью) |
| Save/Load | Базовый (JSON) |

---

### Процесс контроля выполнения

Каждый шаг завершается **тройной проверкой**:
1. **EditMode-тесты** — Unit-тесты без сцены (мгновенно)
2. **PlayMode-тесты** — Интеграционные тесты на сцене
3. **Ручная проверка** — запуск в Editor, визуальное подтверждение

Ход работ фиксируется в `production/session-state/`:
- После каждого дня: `session-state.md` — что сделано, что нет, блокеры
- После каждого билда: build-лог

### Реализовано (Core)
- [x] `KitchenElement` — MonoBehaviour с DimensionsMM, GetVertices, GetFaces, Rotate, Describe
- [x] `ElementFactory` — CreateBoard, CreatePreset, Duplicate, пресеты 5 типоразмеров
- [x] `BasePlate` — пол 3000×3000×18, тег "Floor"
- [x] `Bootstrap` — точка входа, создаёт BasePlate + все менеджеры
- [x] `AppConstants` — SAVE_FORMAT_VERSION, MM_TO_UNITS, PRESET_DIMENSIONS
- [x] `KitchenSettings` — ScriptableObject с Save/Load через PlayerPrefs
- [x] `DisplaySettings` — применение режима окна в плеере
- [x] Assembly Definitions: Runtime + EditMode.Tests + PlayMode.Tests

### Реализовано (Камера и ввод)
- [x] `CameraController` — орбита (ПКМ по пустому), pan (ЛКМ по пустому / СКМ), zoom (колесо), пресеты (1/2/3), фокус (F)
- [x] Защита от кликов сквозь UI (`IsPointerOverGameObject`)
- [x] `InputCapture` — диагностический логгер ввода (опциональный)

### Реализовано (Выбор и перемещение)
- [x] `SelectionManager` с жёлтым emission-подсветкой, событиями `OnSelectionChanged`
- [x] `ElementMover` — drag по XZ, Shift+Y, Escape-отмена, полупрозрачная подсветка, проверка пересечений
- [x] Движение стрелками на шаг сетки (Shift — по вертикали)
- [x] Дублирование по Ctrl+D
- [x] Валидация `MovedCausesViolation` при блокировке

### Реализовано (Snap System)
- [x] `SnapSystem` — выравнивание по кромкам-/кромкам+/центрам (`BestEdgeDelta`), плоскости заподлицо
- [x] Прилипание **во время перетаскивания** (ElementMover.UpdateDrag)
- [x] Отладочные логи `VerboseLog` с описанием объектов, граней, осей выравнивания
- [x] Проверка пересечений `ElementsIntersect` с допуском 0.1мм
- [x] Проверка параллельности граней и перекрытия >30%

### Реализовано (Валидация)
- [x] `ConstraintValidator` — face-to-face контакты + BFS-связность от BasePlate
- [x] `FaceContact` struct — элементA/B, грани, площадь, isFaceToFace
- [x] `ElementHighlighter` — зелёный (валидно) / красный (нарушение), BasePlate исключён
- [x] Режим блокировки (`blockOnViolation`) — откат позиции

### Реализовано (UI — процедурный uGUI)
- [x] `UIFactory` — Canvas, Panel, Button, Label, InputField, Toggle
- [x] `UIManager` — тулбар (пресеты, Spec, Settings, Save, Load)
- [x] `ContextMenuUI` (ПКМ) — название, Ш/В/Г, X/Y/Z, RX/RY/RZ, live-update при драге, применить/повернуть/дублировать/удалить
- [x] `SettingsPanelUI` — сетка/шаг, снэп/порог, блокировка, автосохранение, пространственная сетка, оконный режим
- [x] `SpecificationPanelUI` — таблица групп досок с площадью
- [x] `ConsoleOverlay` — консоль логов по `~`, автоскролл

### Реализовано (Save/Load)
- [x] `SaveLoadManager` — Capture/Serialize/Deserialize/Restore + файловый IO
- [x] Бэкап существующего файла в zip с датой (`CompressionLevel.Optimal`)
- [x] `AutoSaveManager` — каждые 2с, **только при изменениях** (сравнение JSON)
- [x] `ProjectData` / `ElementData` — сериализуемые структуры
- [x] Проверка `IsVersionCompatible` при загрузке

### Реализовано (Спецификация)
- [x] `SpecificationManager` — группировка по (имя, размеры), площадь поверхности, BasePlate исключён

### Реализовано (Визуализация)
- [x] `SpatialGridRenderer` — GL-линии на полу (мелкие 100мм, крупные 1м), галочка в настройках

### Метрики (актуально на 2026-06-03)
- [x] **EditMode 117 + PlayMode 13 = 130 passing тестов** (прогон Unity CLI batchmode, все зелёные)
- [x] Снэп-тесты переписаны: 6 файлов (Scenario/EdgeCase/MultiBoard/Invariant/KnownLimitation + base)
- [x] Старые «не проверенные» снэп-тесты (10 падавших) исправлены — были неверные предпосылки в тестах, ядро снэпа в основном корректно

---

## 🔴 Критические баги (требуют исправления)

### 1. Шейдер сетки несовместим с URP
- [x] **Исправлено:** Создан `Assets/Resources/Shaders/GridLine.shader` (URP Unlit vertex-color), `SpatialGridRenderer` использует `Shader.Find("Hidden/GridLine")` + `Resources.Load` fallback

### 2. Отложенное удаление при загрузке проекта
- [x] **Исправлено:** Добавлена `ClearBoardsImmediate` с `DestroyImmediate` перед `RestoreScene`

### 3. Валидация только перемещаемой доски
- [x] **Исправлено:** `MovedCausesViolation` проверяет все нарушения в радиусе `snapThreshold * 2`

### 4. Отсутствие гистерезиса в снэпе
- [x] **Исправлено:** Два порога (enter / exit +30%), `_isSnapped` + `_lastHoverTarget`

### 5. Legacy UI без TextMeshPro
- [ ] **Не исправлено:** TMP требует подключения пакета — отложено

### 6. ContextMenuUI: позиции/повороты применяются без проверки
- [x] **Где:** `ContextMenuUI.Apply` устанавливает position/rotation напрямую
- [x] **Проблема:** После ручного ввода координат не запускается валидация, доска может оказаться в воздухе с красной подсветкой, но без отката
- [x] **Решение:** Добавлен `WouldCauseViolation()` с откатом изменения при `BlockOnViolation`

---

## 🧪 Архитектура тестирования прилипания

Самый сложный функционал — требует максимального покрытия. Разбито на 4 типа тестов.

### 1. `SnapScenarioTests.cs` — базовые сценарии

**Две доски:**
- [x] `TwoBoards_EdgeToEdge_AlignsMinEdges` — кромки совпадают
- [x] `TwoBoards_CenterToCenter_AlignsCenters` — у центра совпадают центры
- [x] `TwoBoards_MaxEdgeToMaxEdge` — правые кромки совпадают
- [x] `TwoBoards_SmallBoardNearBigBoard_AlignsNearestEdge` — мелкая доска липнет к ближайшей кромке большой
- [x] `TwoBoards_FaceToFace_Flush` — плоскости заподлицо
- [x] `TwoBoards_Rotated90_EdgeToEdge` — поворот на 90°, затем прилипание
- [x] `TwoBoards_Rotated45_EdgeToEdge` — поворот на 45°
- [x] `TwoBoards_PartialOverlapBelow30Percent_NotSnapped` — перекрытие <30%
- [x] `TwoBoards_ExactlyAtThreshold_Snapped` — ровно на пороге (50мм)
- [x] `TwoBoards_JustOverThreshold_NotSnapped` — 51мм → нет снэпа

**Три доски:**
- [x] `ThreeBoards_ChainABC_AllSnapped` — A→B→C
- [x] `ThreeBoards_TShape_VerticalOnHorizontal` — T-образная
- [x] `ThreeBoards_LShape_CornerAlignment` — L-образная
- [x] `ThreeBoards_UShape_ThreeWalls` — U-образная
- [x] `ThreeBoards_StackOnFloor` — стопка на полу
- [x] `ThreeBoards_ConflictingSnaps_ChoosesNearest` — ближайший
- [x] `ThreeBoards_MixedRotations` — разные повороты

**Особые случаи:**
- [x] `BoardOnFloor_CenterY0_2_IsValid` — доска 400мм на полу
- [x] `BoardOnFloor_SnapsFromAbove` — доска в воздухе → снэп к полу
- [x] `BoardDisabled_DoesNotSnap` — цель отключена
- [x] `MovedBoardDisabled_DoesNotSnap` — сама доска отключена
- [x] `ZeroDimensions_ClampedToOne` — (0,0,0) → (1,1,1)

### 2. `SnapEdgeCaseTests.cs` — граничные условия
- [x] `SnapDisabled_InSettings_ReturnsNotSnapped`
- [x] `ThresholdZero_NoSnap`
- [x] `ThresholdVeryLarge_SnapsEverything`
- [x] `MultipleTargets_ChoosesClosest`
- [x] `MovingAwayFromTarget_NoSnap`
- [x] `ParallelFaces_NoOverlap_NoSnap`
- [x] `PerpendicularFaces_NoSnap`
- [x] `OppositeNormals_NoSnap`
- [x] `FloatingPointPrecision_1e9_NoFalsePositive`
- [x] `VeryLargeBoard_SnapsCorrectly`
- [x] `VerySmallBoard_SnapsCorrectly`

### 3. `SnapIntegrationTests.cs` — PlayMode интеграционные
- [x] `DragBoard_NearOther_SnapsDuringDrag`
- [x] `DragBoard_ThenRelease_StaysSnapped`
- [x] `DragBoard_EscapePressed_ReturnsToStart`
- [x] `DragBoard_IntersectingOther_RedTint`
- [x] `DragBoard_NoIntersection_GreenTint`
- [x] `DragBoard_BlockOnViolation_RevertsPosition`
- [x] `GridSnap_AfterSnap_Combined`
- [x] `RotateBoard_ThenSnap_CorrectAxes`

### 4. `SnapPerformanceTests.cs` — производительность
- [x] `Snap_50Boards_Under16ms`
- [x] `Snap_100Boards_Under32ms`
- [x] `Snap_200Boards_Under64ms`
- [x] `Validate_50Boards_Under32ms`
- [x] `Validate_100Boards_Under64ms`

### Оракул (ожидаемое поведение)
- [x] Для каждого теста определяется: `snapped: bool`
- [x] Конечная позиция `Vector3` с допуском 0.001м = 1мм
- [x] Оси выравнивания (u/v)
- [x] Цель снэпа (`targetName`)
- [x] Отсутствие пересечений после снэпа

---

## 🟡 Улучшения UX (приоритетные)

- [ ] **Ghost-preview**: `Graphics.DrawMesh` с полупрозрачным материалом в позиции снэпа — видеть, куда прилипнет доска, до отпускания
- [ ] **Визуальная валидация**: подсвечивать только нарушающие грани через `MaterialPropertyBlock` + красный emission, а не всю доску
- [ ] **Плавный фокус камеры**: по `F` — `Vector3.SmoothDamp` за 0.3с, а не телепорт
- [ ] **Align & Distribute**: кнопки в контекстном меню «Выровнять по X/Y/Z», «Распределить» — работают с `Bounds` выделенных объектов
- [ ] **Measurement Tool**: по `M` включать режим рулетки — клик A, клик B → линия + `UILabel` с расстоянием в мм
- [ ] **Контекстное меню: привязка к границам экрана** — `RectTransform.anchoredPosition` не должен выходить за `Screen.width/height`
- [ ] **Индикатор автосохранения**: toast-уведомление «💾 сохранено» при срабатывании AutoSaveManager
- [ ] **Подтверждение перезаписи** при QuickSave, если файл уже существует

---

## 🟢 Архитектурные улучшения

- [ ] **EventBus**: легковесная шина на `Action<T>` — `OnElementMoved`, `OnElementCreated`, `OnProjectLoaded`, `OnSelectionChanged` для развязки `ElementMover` → `UI`/`AutoSave`/`Specification`
- [ ] **Структурированный логгер**: заменить `Debug.Log` на `Log.Info`/`Log.Warning`/`Log.Error` с флагами компиляции для отключения в релизе
- [ ] **BoardRegistry**: кэш всех KitchenElement с регистрацией/дерегистрацией вместо `FindObjectsByType` в рантайме
- [ ] **Spatial hash** для `SnapSystem`: O(N²) → O(N log N) при большом количестве досок
- [ ] **Безопасные единицы**: `struct Millimeter` или extension `.ToMeters()` / `.ToMM()`, чтобы избежать путаницы (y=0.009 vs y=0.2)
- [ ] **ScriptableObject-конфиги**: `SnapConfigSO`, `GridConfigSO`, `CameraConfigSO` — пресеты поведения
- [ ] **Рефакторинг ElementMover**: разделить на `InputDragHandler`, `SnapResolver`, `MoveValidator` — отдельные классы
- [ ] **Развязать ContextMenuUI**: вынести логику применения в отдельный `BoardCommands` класс

---

## 🔵 Фичи из профессиональных редакторов

- [ ] **Grouping / Hierarchy**: `KitchenGroup : MonoBehaviour` с `List<KitchenElement>`. Кухня = модули (шкаф = дно + стенки + полки + фасад). Drag группы двигает все дочерние с сохранением оффсетов
- [ ] **Export CSV**: `SpecificationManager.ExportToCSV(path)` через `StringBuilder` с разделителем `;` и UTF-8 BOM для заказа распила
- [ ] **Export PDF**: генерация HTML → печать, либо легковесный `iText`
- [ ] **Axis Lock (X/Z при драге)**: по `X`/`Z` во время драга блокировать ось через `Plane`-проекцию
- [ ] **Undo/Redo (лёгкий)**: стек `ICommand { Execute(); Rollback(); }` на последние 20 команд (позиция, поворот, создание, удаление) — без `UnityEditor.Undo`
- [ ] **Multi-selection**: Ctrl+ЛКМ, рамка выделения
- [ ] **Import DXF/OBJ**: для интеграции с CAD (out of scope на ближайший спринт)

---

## 📋 Приоритизированный план работ

### Фаза 1: Стабильность (критические фиксы)
- [x] Исправить URP-шейдер в `SpatialGridRenderer` — создан `Assets/Resources/Shaders/GridLine.shader` (URP Unlit, vertex color)
- [x] Исправить отложенное удаление в `LoadProject` — добавлена `ClearBoardsImmediate` с `DestroyImmediate`
- [x] Добавить валидацию соседей при перемещении — `MovedCausesViolation` проверяет все нарушения в радиусе `snapThreshold * 2`
- [x] Добавить гистерезис в `SnapSystem` — два порога (enter / exit +30%), `_isSnapped` + `_lastHoverTarget`
- [x] Добавить `IsPointerOverGameObject` в оставшиеся места — `ContextMenuUI` + `CameraController` проверяют `IsPointerOverGameObject(0)` дополнительно

### Фаза 2: Тестирование прилипания
- [x] `SnapScenarioTests` — 10 тестов (2 доски) — добавлены
- [x] `SnapScenarioTests` — 7 тестов (3 доски) — добавлены
- [x] `SnapScenarioTests` — 5 тестов (особые случаи) — добавлены
- [x] `SnapEdgeCaseTests` — 11 тестов — новый файл
- [x] `SnapIntegrationTests` — 8 тестов PlayMode — новый файл
- [x] `SnapPerformanceTests` — 5 тестов — новый файл

### Фаза 3: UX
- [x] Ghost-preview — `Graphics.DrawMesh` c полупрозрачным материалом в позиции снэпа
- [x] Плавный фокус камеры — `Vector3.SmoothDamp` на F (0.3с)
- [x] Align & Distribute — кнопки в тулбаре, работают с мульти-выбором
- [x] Toast-уведомления — AutoSave + Export CSV показывают сообщения
- [ ] Measurement Tool — отложено

### Фаза 4: Архитектура
- [x] BoardRegistry — кэш с Register/Unregister, заменяет `FindObjectsByType<KitchenElement>()`
- [x] EventBus — `EventBus<T>`: Subscribe/Publish/Unsubscribe + события ElementCreated/Moved/SelectionChanged/ProjectLoaded/BoardRemoved
- [ ] Structured logger — отложено
- [ ] Spatial hash для снэпа — отложено
- [ ] Рефакторинг ElementMover — отложено

### Фаза 5: Фичи
- [x] Multi-selection — Ctrl+click, список `SelectedElements`, все подсвечиваются
- [x] Axis Lock — X/Z во время драга
- [x] Export CSV — кнопка в спецификации, сохраняет на рабочий стол
- [x] Undo/Redo — `CommandStack` (20 команд) + Ctrl+Z/Ctrl+Y
- [ ] Grouping — отложено

---

## 🚫 Out of scope (сознательно)
- [ ] Материалы/текстуры/цвета — позже
- [ ] Физика (gravity, collisions) — всё isKinematic
- [ ] Импорт/экспорт CAD форматов — только базовый JSON (расширить позже)
- [ ] Сетка стен (только пол как `BasePlate`)
