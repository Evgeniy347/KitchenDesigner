# Подключение к работающему приложению (MCP)

Это заметка для разработчика. Пользовательская инструкция «как подключить
своего агента» — [Assets/StreamingAssets/MCP-CONNECT.md](Assets/StreamingAssets/MCP-CONNECT.md), и её же открывает
кнопка на вкладке **Настройки → MCP**.

## Локальная разработка

Приложение САМО говорит по MCP (транспорт Streamable HTTP) на
`http://127.0.0.1:9337/mcp`. Посредника нет: сервер поднимается вместе с
приложением, поэтому оно должно быть ЗАПУЩЕНО (`Build/KitchenDesigner.exe`
либо проект в Unity) — иначе подключаться не к чему.

Порт переопределяется переменной `UNITY_MCP_PORT` (в приоритете) или аргументом
`-mcpPort <номер>` — так на одной машине можно поднять второй экземпляр рядом с
уже запущенным, не деля с ним порт 9337. Мусор вместо числа или номер вне
диапазона 1..65535 не роняют приложение: оно откатывается на порт по умолчанию
и пишет об этом предупреждение в лог (`[MCP] -mcpPort …`). Разбор аргумента —
`KitchenDesigner.Core.MCP.McpPortArgument` (`Assets/Scripts/Core/Pure/MCP/`),
итоговое решение — `KitchenDesigner.Core.McpBridgeStatus.ResolvePort`, второго
места, где решается порт, нет. Если порт занят (второй экземпляр или чужая
программа), `McpHttpBridge` не падает — пишет `Cannot listen on port …` в лог и
продолжает работать без MCP; актуальный порт всегда написан в самой программе:
**Настройки → MCP**.

### Каталог, куда MCP разрешено сохранять

`save_project` пишет на диск, и это делает его опасным по умолчанию: агент на той
стороне соединения может назвать любой путь. Единственная защита — параметр запуска
`-mcpSaveDir <каталог>`: без него `save_project` отказывает всегда (`Refused: no MCP save
directory is configured…`), а с ним — отказывает для любого пути, который после
разрешения (`..`, абсолютный путь, символическая ссылка, разный регистр, `/` вместо `\`)
не оказался ВНУТРИ этого каталога. Сравнение идёт по нормализованным полным путям, а не
по строкам — символическая ссылка внутри разрешённого каталога, указывающая наружу, не
помогает обойти проверку. `load_project` ничего не пишет, поэтому им не ограничен —
отказ там прежний: файла нет или он битый.

Разбор аргумента — `KitchenDesigner.Core.McpSaveDirectoryArgument`, сама проверка —
`KitchenDesigner.Core.McpSaveDirectoryGuard` (обе в `Assets/Scripts/Core/Pure/Infrastructure/`),
итоговое состояние — `KitchenDesigner.Core.MCP.McpSaveDirectoryStatus.AllowedDirectory`,
второго места, где решается разрешённый каталог, нет. `Bootstrap` пишет в лог при
старте, настроен каталог или нет (`[MCP] Каталог, разрешённый для save_project: …` либо
предупреждение, что параметр не задан).

### Проверить связь

```powershell
curl -s -X POST http://127.0.0.1:9337/mcp -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"ping\",\"arguments\":{}}}"
```

`"status": "ok"` — связь есть. `Failed to connect` — приложение не запущено.

### Для нейросети (MCP)

Сервер зарегистрирован в `.mcp.json` как **`unity-kitchen`** по адресу выше.
Достаточно, чтобы приложение было запущено. Любому другому агенту хватит той же
строки:

```
claude mcp add --transport http unity-kitchen http://127.0.0.1:9337/mcp
```

Эндпоинт слушает только loopback и отбивает чужие `Host` и `Origin`.

### Положение детали, ответ мутации и отмена (для слабых моделей)

Слабая модель путалась в трёх системах координат (вход — минимальный угол, выход — центр,
`get` без Y) и в ответах на 75 полей. Теперь контракт такой, тексты — в `McpGuideTexts`:

- **`ref`** — параметр запроса: какая ТОЧКА мирового бокса детали (после поворота) имеется в
  виду. Слова через `-`: `left|right` (X), `bottom|top` (Y), `back|front` (Z), `center` —
  середина неназванных осей; по умолчанию `left-bottom-back` — прежний минимальный угол.
  Разбор — `McpReference` (`Pure/MCP`, 27 значений, каноническое имя разбирается обратно в ту
  же точку), применение — `McpAnchor.RefOffsetAfter`/`PlaceRefPointAt`. Ответ называет
  канонический `ref` и даёт позиции в нём, поэтому «прочитал → записал» ничего не двигает.
- **Ответ мутации** — всегда `MutationReply` (`Pure/MCP`): `placements` (на деталь: `posMm`,
  мировой `footprintMm` x,y,z, `on`, `touches`, `gaps`, `room`, `level`, `issues`) и
  `sceneViolationDelta {added, removed}` — только то, что сломал или починил ЭТОТ вызов.
  Полный `ElementInfo` отдаёт только `get_elements` (и устаревший `get_all_elements`).
  Отношения считает `PlacementRelations` (чистая функция над боксами в мм), собирает
  `McpPlacementBuilder`, строки `issues` — `McpPlacementIssues`, «до/после» — `McpMutationReport`.
- **`place`** `{items:[{name, type/size, on, against, align}]}` - позиция считается сервером из слов
  (`McpPlacer` + чистый `PlaceSolver`), отказ называет недостающую ось, спорящие ограничения, близкие
  имена или перекрытого соседа с гранью, у которой встать. `dry_run` есть у `place`, `create_elements`,
  `clone_elements`, `align_elements`, `edit_elements`; подсказки отказов — `McpNameHints`; планировочные
  инструменты отвечают `PlanReply` с `sceneViolationDelta`.
- **`undo` / `redo`** `{steps}` — поверх `CommandStack`; шаг = один мутирующий вызов.
- JSON на проводе компактный (без отступов), целые миллиметры без `.0`: сенсор бюджета —
  `McpPlacementReplyTests.CreateElements_ThreeCabinetsOnTheWire_FitTheByteBudget_…`.

### Замер: разбивка одного вызова по этапам

Каждый ответ пишет в лог одну строку `[MCP][Timing] method=… total=…ms
accepted->queued=…ms queued->started=…ms started->executed=…ms executed->responded=…ms
validateRecomputes=… sceneScans=… stages=[…]`. Стадии — принят (вход в `Serve`),
поставлен в очередь (перед `_mainThreadActions.Enqueue`), начал выполняться (внутри
очереди, перед `McpCommandHandler.Handle`), выполнен (сразу после), ответ отправлен
(после записи байт). `validateRecomputes` — сколько раз за этот вызов реально пересчиталась
валидация всей сцены (`McpValidationCache`, `Assets/Scripts/Core/MCP/McpValidationCache.cs`)
— счётчик работы, а не миллисекунды: он не плавает от машины к машине. Для батча из
нескольких `tools/call` в одном HTTP-теле строка описывает ПОСЛЕДНИЙ вызов батча
(упрощение, батчи в реальном использовании агентом — редкость).

`sceneScans` — сколько раз за этот вызов обошли всю сцену (`PartRegistry.GetAll`,
счётчик `Assets/Scripts/Core/Pure/Diagnostics/SceneScanCounter.cs`). Один обход на
сцене в 400 деталей стоит 0,7–2,2 МБ мусора и десятки миллисекунд, поэтому это число
важнее миллисекунд. Счётчик монотонный и чтением не сбрасывается — покадровый
`SceneScanLog`, который забирает `PerfMonitor`, продолжает работать рядом и не теряет
свой кадр. Следом в квадратных скобках идут ИМЕНА обходивших, повтор одного места —
`×N`: `sceneScans=4 [McpCommandHandler.HandleCreateElements, ElementHighlighter.RefreshHighlights ×2]`.

`stages=[…]` — разбивка самого обработчика по именованным участкам
(`Assets/Scripts/Core/Pure/MCP/McpCallStages.cs`): у `create_elements` это `parseJson`, `acceptItems`,
`spawn`, `commandStack`, `snapOpenings`, `settle`, `describe`, у `load_project` —
`rebuildScene` (досадка связей и перекраска живут внутри загрузчика, не отдельным
участком), у `save_project` — `writeFile`. Повтор одного участка
схлопывается в `×N`. Участки размечает обработчик; у неразмеченного метода скобок нет.

Вызов, который не идёт на главный поток (`initialize`, `tools/list`), печатает
`total=…ms answeredWithoutTheScene` вместо этапов: его отметки очереди никто не ставит,
и раньше разбивка выводила из нулей минус длиной в пять секунд.

Чистая арифметика разбивки (тики → мс, без Unity) — `Assets/Scripts/Core/Pure/MCP/
McpCallTimingBreakdown.cs`, проверяется `dotnet` в `Assets/Tests/EditMode/Pure/
McpCallTimingBreakdownTests.cs`; сами приборы — `McpCallStagesTests.cs` и
`SceneScanCounterTests.cs` там же.

### Прогрев на старте моста

`McpHttpBridge.StartBridge` зовёт `McpWarmup.RunOnce()` ДО того, как поднимется
слушающий поток, — значит к первому запросу агента прогрев уже состоялся.
Разбивка дымового прогона 12.09 назвала холод поимённо: первый `create_elements`
сессии стоил 130,27 мс, второй — 1,96 мс, и разница почти вся в двух этапах —
`accept` 57,61 мс против 0,05 мс и `exec->resp` 46,40 мс против 0,38 мс. Это
Newtonsoft, впервые строящий контракты на `ParamsCreateElements` с его
`CreateItem[]` и на `ElementInfo`.

Прогрев резолвит контракт каждого типа поверхности (`McpWireTypes` выводит список
рефлексией от `CreateItem`, а не перечислением руками) и один раз прогоняет обе
трубы целиком — разбор запроса и запись ответа. Резолвер у MCP теперь ОДИН и общий,
`McpJson.Resolver`; он же считает построенные контракты, и это счётчик РАБОТЫ, на
котором стоят сенсоры `Assets/Tests/EditMode/McpWarmupTests.cs`: после прогрева
разбор `create_elements` и запись `ElementInfo` строят НОЛЬ контрактов. Честный
остаток холода — анонимная обёртка ответа: её тип свой у каждого места вызова,
заранее прогреть его нельзя, и это один контракт на четыре скалярных поля.

## Где подробности

- Инструменты, единицы, рабочий цикл — `get_project_instructions` и `guide`
  прямо из агента; тексты живут в `Assets/Scripts/Core/MCP/Contract/McpGuideTexts.cs`.
- Список инструментов и их параметры — `Assets/Scripts/Core/MCP/Contract/McpToolRegistry.cs`.
- Разбор JSON-RPC — `Assets/Scripts/Core/MCP/McpRpcRouter.cs`, транспорт —
  `McpHttpBridge.cs`, проверки — `McpRequestGate.cs`.
- Реализация всех методов — `Assets/Scripts/Core/MCP/McpCommandHandler.cs`.
