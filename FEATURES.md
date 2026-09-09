# Feature → Code map

**Workflow:** For any task, call `codegraph_context("feature name")` FIRST to get focused context with entry points, related symbols, and key code.

## Elements

| Feature | Main classes | Tests | Path |
|---------|-------------|-------|------|
| Table | TableElement, GridManager | GridManagerTests, IsoScreenshotTests | Elements/, Rendering/ |
| Boards | KitchenElement, ElementMover, ElementFactory | KitchenElementTests, ElementFactoryTests | Elements/ |
| Facades | FacadeElement, FacadeDoor, AssembledFacadeElement | FacadeElementTests, FacadeDoorTests, AssembledFacadeTests | Elements/ |
| Drawers/GTV | DrawerElement, DrawerConstants, DrawerMesh, DrawerLinks, DrawerValidator | DrawerElementTests, DrawerRoundTripTests, DrawerFactoryTests | Elements/ |
| Drawers/Movento | MoventoDrawerMesh, DrawerConstants | MoventoDrawerTests, MoventoDrawerSceneTests | Elements/ |
| Walls | Wall, WallManager, WallCutaway | WallTests, WallManagerTests, WallCutawayTests | Elements/, Infrastructure/, Rendering/ |
| Radial shelves | RadialShelfElement, RadialShelfMesh | RadialShelfTests | Elements/ |
| Прикрепление деталей (нестандартный ящик) | AttachLinks (правила и дерево), AttachMove (перенос поддерева), AttachRider (езда за анимацией), KitchenElement.AttachedToName, SceneAnalyzer (ATT-01) | AttachLinksTests | Elements/, Analysis/ |
| Врезная техника (мойка, варочная) | SinkElement, CooktopElement, IPartCutout, KitchenElement.RegisterCutout, SetCooktopCutoutCommand | SinkElementTests, CooktopElementTests, SinkRealSceneTests | Elements/, Commands/ |
| Винтовая опора с футоркой | ScrewLegSpec (арифметика, округление стенки и порог захода), ScrewLegElement (InsertionIntoHostMM — заход измеряется), ScrewLegMesh + CylinderStackMesh, ScrewLegAutoFit (посадка на пол, IAutoSeated), ScrewLegHosting (вывод хозяина + глубина захода), ScrewLegCentring (LEG-01), ElementGeometry.CentresOnTarget (прилипание к середине) | ScrewLegSpecTests, ScrewLegAutoFitTests, ScrewLegCentringTests, ScrewLegHostingTests, SceneAnalyzerTests (LEG-02), SnapCoreScrewLegCentreTests | Pure/Elements/, Elements/, Geometry/, Analysis/ |

## Systems

| Feature | Main classes | Tests | Path |
|---------|-------------|-------|------|
| Snap/Resize | SnapSystem, FaceContact, ResizeSnap, ResizeHandleManager, ResizeMath | SnapSystemTests, SnapScenarioTests, ResizeSnapTests | Snap/ |
| Посадка по устьям портов (труба ↔ фитинг: точкой в точку, а не гранью к грани) | SnapPort + SnapPortSeat (ЕДИНСТВЕННОЕ описание правила: пара устьев, ранг встречности, сдвиг; зовут SnapCore.TrySnap до граневых детентов, SnapNeighbourFacts → SnapSystem.Diagnose и ResizeSnap), ISnapPorts (устья объявляет сам элемент — PipeElement, PipeFittingElement), ElementGeometry.Ports, IMountsOnTarget (ось крепления тоже переехала из лестницы типов на элемент), PortTurn (Geometry/ — ЕДИНСТВЕННОЕ описание доворота устья в устье: TurnOnto/Rotate90/RotateWithTwist; SnapPortDock зовёт его, а не считает поворот заново), PipeFittingSeatChoice (Pure/Plumbing/ — правило «максимум связей»: какой порт своей детали и какой доворот на 90° вокруг оси стыка сажают её на трубу и заодно достают до свободного порта соседа, если это вообще геометрически возможно; тем же правилом, но с уже зафиксированным поворотом, пользуется PipeDocking.ReseatAfterRotation при повороте детали кнопками контекстного меню) | SnapPortSeatTests, SnapPortRuleSingleSourceTests (сторож на второе описание устья), PipeFittingPairSnapTests (100 пар фитинг↔фитинг: разомкнутых 0), PipeFittingSnapProbeTests, PipeFittingSnapTargetTests, PipeFittingSnapSceneProbeTests (сцена + snap_diagnose), PipeFittingSeatChoiceTests (дотнет, пара «может дотянуться» / «не может»), PipeEndFittingsMaximizeLinksTests, PipeFittingRotationLinksTests, ElementMoverSeatingOrderTests (сетка округляет ДО посадки устье-в-устье, а не после), PipeGapSensorTests (реальный зазор на замороженной копии сцены пользователя) | Geometry/, Elements/, Snap/, Pure/Plumbing/ |
| Ручки трансформации | ResizeHandleManager, TextureOverlayHandles, HandlePlacement + общий модуль Handles/ (HandleMetrics, HandleMeshes, HandleMaterials, HandleVisual, HandleScreenPick, HandleInput); рисуются поверх всего шейдером Hidden/KD/HandleOverlay, ловятся по экрану | ResizeHandleManagerTests, HandlePlacementTests, HandleMeshesTests, HandleScreenPickTests, TextureOverlayHandlesTests | Snap/, Snap/Handles/ |
| Align/Distribute | AlignDistributeTool | AlignDistributeToolTests | Snap/ |
| Validation | ValidationCore (правила), ValidationSnapshot + ConstraintValidator (адаптер сцены), FacadeValidator, DrawerValidator | ValidationCoreTests, ValidationInvariantTests, ConstraintValidatorTests, FacadeValidatorTests, DrawerValidatorTests | Geometry/, Validation/ |
| Groups/Modules | GroupManager, ModuleEditMode | GroupTests, ModuleSystemTests | Infrastructure/ |
| Specification | SpecificationManager | SpecificationManagerTests, SpecificationExportTests | Infrastructure/ |
| Строка состояния и история сообщений | StatusBarUI.ShowTransient (единственная дверь, она же пишет историю), StatusLevel, StatusBarSink (переходник автообновления), ConsoleLog (общий кольцевой буфер на 400 строк, свёртка повторов, отметка времени), ConsoleOverlay (лента по клавише «ё») | ConsoleLogTests, StatusBarSingleDoorTests, StatusBarUITests, StatusBarUIContractTests, StatusBarSinkTests, StatusBarLifetimeTests | Pure/UI/, UI/, Update/ |
| Edge banding | EdgeBanding (HasEdgeEffective — единственный читатель), EdgeStates (три состояния торца), SetEdgeBandingCommand, SceneAnalyzer (EDG-01) | EdgeBandingTests, EdgeStatesTests, EdgeStateSingleReaderTests | Geometry/, Elements/, Commands/, Analysis/ |
| Перенос состояний кромки при загрузке | EdgeStateMigration, SceneRestorer | EdgeStateMigrationTests | Persistence/ |
| Подложка торца (кромки нет → голая плита) | EdgeSubstrate, GrooveMesh.SubmeshLayout, KitchenElement.RefreshSubmeshMaterials | EdgeSubstrateTests | Materials/, Elements/ |
| Миниатюры элементов для каталога | ThumbnailRenderer (спавн через фабрику + изокамера на слое 31 → RenderTexture, без ReadPixels/PNG), IsoCameraRig (общая с IsoScreenshotTests математика ракурса 3/4), ElementFactorySandbox (флаг «песочницы»: ElementRoot.Publish не регистрирует элемент в PartRegistry — тем самым не трогает валидацию/спецификацию/автосохранение, все они читают PartRegistry) | ThumbnailRendererTests (снапшоты плиток в test-results/thumbnails, непустота по доле закрашенных пикселей), ElementFactorySandboxTests (противоположный вход: обычный спавн в реестре есть — песочный нет, без мёртвых записей), ElementFactorySandboxScopeTests (дотнет, вложенность/Dispose счётчика) | Rendering/, Pure/Elements/, Pure/Rendering/ |

## Persistence

| Feature | Main classes | Tests | Path |
|---------|-------------|-------|------|
| Save/Load | ElementData, SaveLoadManager, SaveLoadManagerInstance | RoundTripTests, SnapshotTests, SaveLoadManagerTests | Persistence/ |
| Element conversion | ElementConverter | ElementConverterTests | Persistence/ |
| Auto-save | AutoSaveManager | AutoSaveQuitTests | Persistence/ |
| File dialogs | NativeFileDialog | — | Persistence/ |

## MCP / AI

| Feature | Main classes | Tests | Path |
|---------|-------------|-------|------|
| MCP commands | McpCommandHandler, McpModels | McpCommandHandlerTests | MCP/ |
| MCP HTTP endpoint (127.0.0.1:9337/mcp) | McpHttpBridge | McpHttpBridgeTests (PlayMode) | MCP/ |
| JSON-RPC / протокол MCP | McpRpcRouter, McpToolCall | McpRpcRouterTests | MCP/ |
| Защита эндпоинта (Host, Origin, метод, размер) | McpRequestGate | McpRequestGateTests | MCP/ |
| JSON Schema инструментов | McpJsonSchema | McpJsonSchemaTests (быстрый путь) | MCP/Contract/ |
| Contract | McpToolAttributes, McpToolParams, McpToolRegistry | — | MCP/Contract/ |
| Сверка MCP ↔ панель | ElementEditAppliers + *FieldsEditor | McpUiPropertyParityTests, McpUiCreationParityTests | MCP/, UI/ |
| Сверка MCP ↔ сайдбар | SidebarSpawnRouter, ElementSpawners | SidebarSpawnRouterTests | UI/, MCP/ |
| Сверка MCP ↔ настройки | SettingKeys, Settings*Tab | McpSettingsParityTests | MCP/, UI/ |

## UI

| Feature | Main classes | Tests | Path |
|---------|-------------|-------|------|
| Context menu | ContextMenuUI | ContextMenuLayoutTests, ContextMenuRefreshBugTests | UI/ |
| Main UI | UIManager, UIFactory | — | UI/ |
| Sidebar | SidebarUI, SidebarCatalog (данные: одна таблица Rows() + SidebarGroupKey), SidebarSpawnRouter, SidebarPresetResolution (Pure) | SidebarCatalogTests, SidebarSpawnRouterTests, SidebarPresetResolutionTests (Pure) | UI/, Pure/UI/ |
| Sidebar dock mode (раскрытый док / рейка иконок) | SidebarDockChoice (Pure), SidebarDockBudget.CollapsesAfterSpawn (Pure, «чей выбор главнее»: явный выбор > высота экрана), SidebarDockPreference (PlayerPrefs, не файл проекта) | SidebarDockBudgetTests (Pure), SidebarDockPreferenceTests, SidebarDockChoiceStaysOutOfTheProjectFileTests (Pure), SidebarPanelTests (Docked_/Rail_/DockModeButton_/UserChoice_…) | UI/, Pure/UI/ |
| Settings | SettingsPanelUI, KitchenSettings | SettingsPanelUITests, KitchenSettingsTests, McpSettingsParityTests | UI/, Infrastructure/ |
| Floor settings | FloorSettingsUI | FloorSettingsLayoutTests | UI/ |
| Help/Icons | HelpUI, IconFactory | — | UI/ |
| Музыка (плеер в правом верхнем углу) | MusicPanelUI, MusicPlayer, MusicOutput (переходник над AudioSource, на нём глушитель), AudioOutputPolicy (дверь «мы под прогоном»), MusicPlaylist, MusicState; трек и громкость едут в ProjectData | MusicPlaylistTests, MusicPersistenceTests, AudioOutputSingleDoorTests, AudioSilenceGuardTests (PlayMode) | UI/, Audio/, Pure/Audio/ |

## Rendering

| Feature | Main classes | Tests | Path |
|---------|-------------|-------|------|
| Camera | CameraController | CameraControllerTests, CameraStateTests | Rendering/ |
| Grid | GridManager, SpatialGridRenderer | GridManagerTests | Rendering/ |
| Outlines | ElementOutline, ElementHighlighter, EdgeOutlineRenderer | ElementOutlineTests, FieldHighlightTests | Rendering/ |
| Highlight batching on bulk edits | HighlightBatch | HighlightBatchTests, ProjectLoadPerfTests | Rendering/ |
| Wall cutaway | WallCutaway | WallCutawayTests | Rendering/ |
| Scene visibility | SceneVisibility, SceneVisibilityManager | SceneVisibilityTests | Infrastructure/ |

## Other

| Feature | Main classes | Tests | Path |
|---------|-------------|-------|------|
| Прокладка труб — чистое ядро (без Unity) | PipeSpec/PipeSize (ГОСТ 3262-75), PipePort, PipeJoint, PipeNetwork, PipeSizes, PipeSurvey, PipeRules + PipeIssueCatalog (PIP-01…PIP-03), IPipeSceneSnapshot | PipeSpecTests, PipeJointTests, PipeNodePortsTests, PipeNetworkTests, PipeSurveyTests, PipeRulesTests | Pure/Plumbing/ |
| Труба как элемент сцены (ДУ + длина, сечение выводится) | PipeElement, PipeMesh, PipeElementSpec, PipeFieldsEditor, ScenePipeSnapshot (адаптер IPipeSceneSnapshot → SceneAnalyzer, PIP-01…PIP-03) | PipeElementSpecTests, ScenePipeSnapshotTests, ElementFieldsEditorTests (Pipe_*), RoundTripTests.Pipe_*, SnapshotTests.Snapshot_Pipe_*, IsoScreenshotTests.IsoPipe_Dn20_600 | Elements/, UI/, Analysis/ |
| Фитинги трассы (отвод, ПЕРЕХОДНАЯ муфта, тройник, заглушка, подача, обратка) — свойств своих нет, диаметры выводятся с трассы, и по ним же строится тело | PipeFittingSpec + PipeFittingNames (номинальная рама ног и портов + тело по выведенному проходу, имена типов), PipeFittingLayout (ЕДИНСТВЕННОЕ описание формы: список PipeSegment, из него и меш, и ступица, и габаритный ящик), PipeSizes.Widest, PipeFittingElement + шесть наследников (BoreSizeIds — выводимое поле), PipeFittingMesh (тонкая обёртка над PlumbingMesh/TubeMesh), HeatingMaterials (тёплый и холодный заводской цвет подачи и обратки), PipeFittingFieldsEditor, PipeFittingSizeLink (единственный писатель диаметров и перестройщик меша; повод — SceneChangeTracker.SettleDerivedLinks), ScenePipeSurvey, ScenePipeSnapshot.AddFitting | PipeFittingSpecTests (быстрый путь), PipeFittingBoreTests (тело растёт, устья стоят), PipeFittingMeshTests (меш совпадает с ящиком, торцы замкнуты, метки подачи и обратки), ScenePipeSnapshotTests (заглушка гасит PIP-01; PIP-02 срабатывает без переходной муфты и молчит с ней), PipeRulesTests.PipeRules_SizeMismatch_FiresWithoutATransitionCoupling_AndIsSilentWithOne, ElementFieldsEditorTests (Fitting_*), RoundTripTests.EveryFittingKind_*, SnapshotTests.Snapshot_PipeFittings_AllSixKinds, IsoScreenshotTests.IsoPipe{Elbow,Coupling,Tee,Cap,Supply,Return}_Dn20 | Pure/Plumbing/, Elements/, UI/, Analysis/ |
| Materials | MaterialCatalog, MaterialManager, TextureIndex, TextureLibrary | MaterialTests, TextureIndexTests | Materials/ |
| Commands/Undo | CommandStack, UndoHandler, CommandSerialization | CommandStackTests, CommandHistoryTests | Commands/ |
| EventBus | EventBus | EventBusTests | Infrastructure/ |
| Part registry | PartRegistry, PartData | PartRegistryTests, PartDataTests | Infrastructure/, Elements/ |
| Perf profiling (dev-only, F9) | PerfMonitor, ProfilerMarker в hot paths | — | Diagnostics/ |
