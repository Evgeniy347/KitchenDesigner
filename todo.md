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

---

### День 1 — Core (фундамент)

- [x] **Создать проект Unity 6000.4.3f1, URP**:
      — Build Settings: StandaloneWindows64, x86_64, Windowed
      — PlayerSettings: Managed Stripping Level = Low (сразу)
      — URP Asset: HDR=off, MSAA=off, Shadows=off (производительность)
- [x] **Структура папок**:
      `Scripts/Core/`, `Scripts/UI/`, `Scripts/Snap/`, `Resources/`, `Prefabs/`, `Tests/`
- [x] **Assembly Definitions**:
      — `KitchenDesigner.Runtime` (autoReferenced = false)
      — `KitchenDesigner.Tests.EditMode.asmdef` (ссылается на Runtime + TestFramework)
      — `KitchenDesigner.Tests.PlayMode.asmdef` (аналогично)
      — InternalsVisibleTo из Runtime → обе тестовые сборки
- [x] **AppConstants.cs**:
      — SAVE_FORMAT_VERSION
      — DEFAULT_GRID_STEP = 16
      — SNAP_THRESHOLD = 50 (мм)
      — BOARD_THICKNESS = 18 (толщина щита по умолчанию, мм)
      — PRESET_DIMENSIONS: список типоразмеров
      — PLATE_SIZE = 3000 (базовая плита)
- [x] **KitchenSettings.cs** (ScriptableObject, вместо GlobalSettings.cs):
      — gridStep: int = 16
      — gridEnabled: bool = true
      — snapEnabled: bool = true
      — snapThreshold: float = 50 (мм)
      — blockOnViolation: bool = false
      — autoSave: bool = false
      — autoSaveInterval: int = 60 (сек)
      — Save() / Load() через JsonUtility → PlayerPrefs или файл
- [x] **KitchenSettings.asset** (вместо GlobalSettings.asset) — в Resources/
- [x] **KitchenElement.cs** (MonoBehaviour):
      — boardName: string
      — DimensionsMM: Vector3Int (свойство, set → ApplyDimensions)
      — ApplyDimensions(): transform.localScale = dims * 0.001f
      — GetVertices() → Vector3[8] (8 вершин AABB в мировых координатах)
      — GetFaces() → Face[6] (6 плоскостей: центр + нормаль + размеры грани)
      — Rotate(Quaternion rotation) — поворот вокруг своего центра
- [x] **ElementFactory.cs** (static):
      — CreateBoard(Vector3Int dimsMM, string name, Vector3 position) → GameObject
      — CreateBoard presetName → GameObject
      — Duplicate(KitchenElement source) → GameObject (+ offset 100мм по X)
      — Пресеты: (800,400,18), (600,400,18), (400,400,18), (1200,600,18), (600,600,18)
      — Автоматически добавляет: BoxCollider, Rigidbody (isKinematic=true),
        KitchenElement, MeshRenderer (серый URP Lit material)
- [x] **BasePlate.cs**:
      — spawn при старте через Bootstrap (или в сцене)
      — Плита 3000×3000×18, тег "Floor", серый URP Lit
      — Тоже KitchenElement (участвует в валидации)
- [x] **Bootstrap.cs**:
      — Точка входа: Awake() → KitchenSettings.Load()
      — Start() → создать/найти BasePlate, создать CameraController,
        SelectionManager, ElementMover, GridManager (на пустом GameObject)
- [x] **Проверка**: EditMode-тесты — 13 шт (7 + 6):
      — CreateBoard возвращает не null
      — localScale = (0.8, 0.4, 0.018) для (800,400,18)
      — DimensionsMM clamp: (0, -5, 0) → (1, 1, 1)
      — GetVertices возвращает 8 элементов
      — GetFaces возвращает 6 элементов
      — KitchenSettings.Instance не null
      — KitchenSettings Save/Load сохраняет и восстанавливает поля
      — BasePlate создаётся с KitchenElement и корректными размерами (3000,3000,18)

### День 2 — Камера, Выбор, Перемещение

- [x] **GridManager.cs** — SnapToGrid, UseGrid
- [x] **CameraController.cs**:
      — Alt+ЛКМ: орбита
      — Alt+СКМ: pan
      — Scroll: zoom
      — Клавиши: 1 = Front, 2 = Side (Right), 3 = Top
      — F = центрировать на выбранном
- [x] **SelectionManager.cs** (Singleton):
      — Raycast по ЛКМ
      — OnSelectionChanged событие
      — Подсветка emission (жёлтый)
      — Deselect по пустому месту
- [x] **Подсветка выбора**:
      — Жёлтая emission подсветка
      — Восстанавливает оригинальный материал при deselect
- [x] **ElementMover.cs**:
      — Захват по ЛКМ на выбранном
      — Drag XZ с GridManager.SnapToGrid
      — Shift+drag: перемещение по Y
      — Escape: отмена движения
      — Mouse up: финализация
- [x] **Проверка**:
      — EditMode: SnapToGrid 4 кейса (17→16, 9→16, 0→0, disabled)
      — Ручная: Alt+scroll zoom, Alt+ЛКМ orbit, Alt+СКМ pan, клавиши 1/2/3, F

### День 3 — Snap System (прилегание плоскостью)

- [ ] **SnapSystem.cs**:
      — SnapResult: { snapped: bool, position: Vector3, targetName: string, faceIndex: int }
      — TrySnap(KitchenElement moved, List<KitchenElement> others) → SnapResult
      — **Алгоритм**:
          1. Если !snapEnabled → вернуть not snapped
          2. Пройти по всем others (включая BasePlate)
          3. Для каждой пары граней (6×6 = 36 сравнений на пару):
             a. Проверить параллельность: dot(normalA, normalB) > 0.999
             b. Проверить расстояние: dist(planeA, planeB) < snapThreshold
             c. Проверить перекрытие: проекции граней пересекаются (overlap по 2 осям)
             d. Если все 3 условия — snap candidate
          4. Выбрать ближайший candidate
          5. Вычислить новую позицию: moved的中心 смещается так, чтобы грань A совпала с гранью B
          6. Применить GridManager.SnapToGrid к результату
      — Возврат: { snapped: true, позиция, имя цели }
- [ ] **SnapVisualizer.cs** (LineRenderer на отдельном объекте):
      — При proximity (distance < threshold): жёлтый пунктир от moved к target
      — При snap (действие): зелёная линия на 0.5 сек
      — Не рисовать для BasePlate (чтоб не засорять)
- [ ] **Edge-кейсы снэппинга**:
      — Доска к полу (BasePlate): нижняя грань доски к верхней грани пола
      — Доска к доске встык: любые параллельные грани
      — Не снэпать, если перекрытие граней < 30% площади (чтобы не "за угол")
      — Не снэпать, если доски пересекаются (overlap по всем 3 осям)
- [ ] **Проверка**:
      — EditMode: два элемента с зазором 30 мм — snapped = true
      — EditMode: зазор 60 мм (> threshold 50) — snapped = false
      — EditMode: элемент над BasePlate (Y=40) — snap к полу
      — EditMode: доски пересекаются — не снэпать
      — EditMode: snap отключён — false
      — PlayMode: перетащить к другому → позиция совпала

### День 4 — Constraint Validation (плоскость-к-плоскости + цельность)

- [ ] **FaceContact.cs** (struct):
      — elementA, elementB: KitchenElement
      — faceA, faceB: int (0-5)
      — contactArea: float (площадь перекрытия граней)
      — isFaceToFace: bool (true если перекрытие > 50% минимальной грани)
- [ ] **ConstraintValidator.cs** (static):
      — Validate(List<KitchenElement> all) → ValidationResult
      — **Проверка 1: Face-to-face контакты**
          * Для каждой пары элементов найти FaceContact
          * Если две грани параллельны, расстояние < 0.5 мм, и перекрытие > 50% → contact
      — **Проверка 2: Связность графа (все доски — один компонент)**
          * Построить граф: вершины = элементы, рёбра = face-to-face контакты
          * BFS от первого элемента (или от BasePlate)
          * Если после BFS есть непосещённые вершины → violation: "отсоединённая группа"
      — **ValidationResult**:
          ```csharp
          class ValidationResult {
              List<FaceContact> contacts;
              List<KitchenElement> violations;  // элементы без контактов
              List<List<KitchenElement>> isolatedGroups;  // отдельные компоненты
              bool isValid;  // все в одном компоненте, каждый с контактом
          }
          ```
- [ ] **ElementHighlighter.cs**:
      — Материал-замена или цвет emission:
          * Зелёный — всё ок (есть face-to-face контакт, входит в главный компонент)
          * Красный — нарушение (нет контакта или в изолированной группе)
      — Обновляется при каждом перемещении (в ElementMover.OnDragEnd, OnObjectCreated)
      — При selected: рамка поверх подсветки валидации
- [ ] **Режим блокировки (GlobalSettings.blockOnViolation)**:
      — true: ElementMover не финализирует позицию при mouse up,
          если позиция приводит к violation
      — Показывает красную вспышку + доска возвращается на исходную
      — false: доска ставится куда угодно, но подсвечивается красным
- [ ] **Проверка**:
      — EditMode: две доски face-to-face → isValid = true
      — EditMode: одна доска в воздухе → 1 violation
      — EditMode: доска на BasePlate → isValid = true
      — EditMode: доска касается ребром → violation (не face-to-face)
      — EditMode: три доски цепочкой A-B-C → isValid = true
      — EditMode: две изолированных группы A-B и C-D → violation: 2 компонента
      — EditMode: пустая сцена (только BasePlate) → isValid = true
      — PlayMode: blockOnViolation = true → попытка поставить в воздухе отменяется
      — PlayMode: blockOnViolation = false → доска ставится, подсвечена красным
      — Ручная: переключение режимов, цвета правильные

### День 5 — UI (Toolbar, Context Menu, Settings)

- [ ] **Canvas** (Screen Space Overlay):
      — EventSystem
      — Два слоя: Toolbar (верх) + ContextMenu (по месту)
- [ ] **TopToolbar.cs** (верхняя панель):
      — Кнопка «+ Доска ▼»:
          * Выпадающий список пресетов (5 шт)
          * Пункт «Свой размер...» → диалог ввода Ш/В/Г (int, >0)
          * После выбора/ввода → ElementFactory.CreateBoard с offset от камеры
      — Кнопка «Спецификация» → открыть SpecificationPanel
      — Кнопка «⚙» → открыть GlobalSettingsPanel
      — Кнопка «💾 Сохранить» → SaveLoadManager.SaveProject
      — Кнопка «📂 Загрузить» → диалог выбора файла
- [ ] **ContextMenu.cs** (по ПКМ на KitchenElement):
      — Открывается у курсора
      — Поля Ш / В / Г (InputField, int, валидация > 0)
      — Позиция X / Y / Z (InputField, float)
      — Поле «Название» (InputField)
      — Кнопка «Применить» → ApplyDimensions()
      — Кнопка «Повернуть 90°» → rotate 90° вокруг оси Y
      — Кнопка «Дублировать» → ElementFactory.Duplicate
      — Кнопка «Удалить» → Destroy
      — Закрытие: Escape или клик вне
- [ ] **GlobalSettingsPanel.cs**:
      — Toggle «Сетка» ↔ gridEnabled
      — InputField «Шаг сетки, мм» ↔ gridStep (interactable = gridEnabled)
      — Toggle «Снэппинг» ↔ snapEnabled
      — InputField «Порог снэпа, мм» ↔ snapThreshold
      — Toggle «Блокировать ошибки» ↔ blockOnViolation
- [ ] **Проверка**:
      — PlayMode: ContextMenu — изменить размер, проверить что scale обновился
      — PlayMode: ContextMenu — дублировать, проверить +1 элемент
      — PlayMode: ContextMenu — удалить, проверить что уничтожен
      — PlayMode: зажать 0 в поле Ш → clamp до 1
      — Ручная: меню не выходит за экран, пресеты в выпадающем списке

### День 6 — Specification + Save/Load

- [ ] **ElementData.cs** (Serializable):
      — name, dimensionsMM (int[3]), position (float[3]), rotation (float[4])
- [ ] **ProjectData.cs** (Serializable):
      — version: int (AppConstants.SAVE_FORMAT_VERSION)
      — elements: ElementData[]
- [ ] **SaveLoadManager.cs**:
      — SaveProject(string name) → JSON в `persistentDataPath/saves/name.json`
      — LoadProject(string name) → очистка сцены (кроме BasePlate) + создание через Factory
      — GetSaveFiles() → список файлов
      — При загрузке: проверка version, если не совпадает — предупреждение
- [ ] **AutoSaveManager.cs**:
      — Coroutine каждые autoSaveInterval секунд, если autoSave = true
      — Toast «💾 Автосохранение» в левом нижнем углу (2 сек)
- [ ] **SpecificationManager.cs**:
      — Группировка по (dimensionsMM, name)
      — Площадь поверхности доски: 2*(Ш*В + Ш*Г + В*Г) в м² (2 знака)
      — Итог: общее количество, суммарная площадь
- [ ] **SpecificationPanel.cs** (TMP ScrollView):
      — Таблица: № | Название | Ш×В×Г (мм) | Кол-во | Площадь (м²)
      — Итоговая строка: Всего: N досок | Площадь: X.XX м²
      — Кнопка «Закрыть»
- [ ] **Проверка**:
      — EditMode: группировка — 2 одинаковых + 1 другой → 2 группы
      — EditMode: площадь доски 800×400×18 = 2*(0.8*0.4 + 0.8*0.018 + 0.4*0.018) = 0.6832 м²
      — EditMode: пустой список → 0 досок, 0.00 м²
      — PlayMode: save 3 доски → load → сцена совпадает
      — PlayMode: загрузка несуществующего файла → Debug.LogError, сцена не сломана
      — Ручная: сохранение → закрыть .exe → запустить → загрузить → доски на месте

### День 7 — Build, финальные тесты, чистка

- [ ] **EditMode-тесты: все Passed**
      — ElementFactory: CreateBoard, scale, clamp, vertices (8), faces (6)
      — GlobalSettings: Instance, Save/Load, поля
      — GridManager: SnapToGrid (17→16, 9→16, 0→0, disabled)
      — SnapSystem: proximity (30mm→true, 60mm→false), floor snap, overlap, disabled
      — ConstraintValidator: face-to-face ok, solo board violation,
        edge touch = violation, chain = ok, isolated groups = violation,
        empty scene = ok, BasePlate counts
      — Specification: grouping, area calc (800×400×18 = 0.68 м²), empty list
- [ ] **PlayMode-тесты: все Passed**
      — SelectionManager: raycast find element
      — ElementMover: Escape cancel position
      — SnapSystem: drag to snap position match
      — ConstraintValidator Blocking: blockOnViolation prevents invalid, warn allows
      — ContextMenu: edit dimension → scale updates, duplicate → +1, delete → null
      — Settings: toggles affect behaviour
      — SaveLoad: save 3 → load → verify dimensions, unique IDs, missing file
- [ ] **Build**:
      — `build.ps1`: -executeMethod BuildProject.Build
      — Managed Stripping Level = Low, Strip Engine Code = true
      — BuildOptions.CompressWithLz4
      — < 30 секунд, < 100 MB
- [ ] **Финальная ручная проверка .exe**:
      — Запуск: окно открывается, пол виден
      — Создание доски: из пресета и свой размер
      — Камера: Alt+ЛКМ, scroll, Alt+СКМ, 1/2/3 presets, F
      — Выбор/перемещение: ЛКМ, drag по XZ, Shift+Y, Escape
      — Снэппинг: доска прилипает к полу и к другим доскам
      — Валидация: зелёная вместе, красная отдельно
      — Блокировка: включить — не даёт поставить в воздухе
      — ContextMenu: ПКМ → изменить размер, повернуть, дублировать, удалить
      — Спецификация: открыть, проверить таблицу
      — Save/Load: сохранить → перезапустить → загрузить → всё на месте

---

### Out of scope (сознательно)

- Материалы/текстуры/цвета — позже
- Undo/redo — не нужно для личного инструмента
- Multi-selection — одна доска за раз
- Физика (gravity, collisions) — всё isKinematic
- Импорт/экспорт CAD форматов — только базовый JSON
- Сетка стен (только пол как BasePlate)
- Измерение расстояний между досками
