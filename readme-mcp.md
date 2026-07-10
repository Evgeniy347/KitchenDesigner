# Как подключиться к работающему приложению (MCP-мост)

Приложение Kitchen Designer (`Build/KitchenDesigner.exe`) слушает TCP-порт
**9337** на localhost. Приложение должно быть ЗАПУЩЕНО, иначе подключаться не к чему.

## Самый простой способ — готовый скрипт

Выполни в PowerShell из корня репозитория:

```powershell
.\tools\unity-bridge.ps1 ping
```

Если ответ `"status": "ok"` — связь есть. Дальше вызывай любые команды:

```powershell
.\tools\unity-bridge.ps1 get_all_elements                        # все доски: имя, размеры, позиция, поворот
.\tools\unity-bridge.ps1 get_console_logs '{"count":50}'         # последние логи приложения
.\tools\unity-bridge.ps1 get_settings                            # настройки снэпа/сетки/автосейва
.\tools\unity-bridge.ps1 snap_diagnose '{"name":"Board_1"}'      # почему доска (не) прилипает
.\tools\unity-bridge.ps1 get_element_info '{"name":"Board_1"}'   # одна доска по имени
.\tools\unity-bridge.ps1 move_element '{"name":"Board_1","x":0,"y":0.2,"z":1}'
.\tools\unity-bridge.ps1 take_screenshot                         # вернёт путь к PNG
```

Первый аргумент — имя метода. Второй (необязательный) — параметры,
**JSON одной строкой в одинарных кавычках**.

## Если скрипт недоступен — сырой протокол

Открой TCP-соединение на `127.0.0.1:9337` и отправь ОДНУ строку JSON + `\n`:

```json
{"id":"1","method":"get_all_elements","parameters":"{}"}
```

Правила:
- `parameters` — это СТРОКА с JSON внутри (не объект!). Без параметров — `"{}"`.
- В конце запроса обязателен перевод строки `\n`.
- Ответ придёт одной строкой JSON: `{"id":"1","type":"result","data":...}`
  или `{"type":"error", ...}`.

## Частые ошибки

- «Не удалось подключиться» → приложение не запущено. Запусти
  `Build\KitchenDesigner.exe` и подожди пару секунд.
- **Соединение есть, но ответ не приходит (таймаут чтения)** → окно приложения
  потеряло фокус, и плеер встал на паузу (в старых сборках runInBackground
  выключен). Обход: запусти команду и сразу кликни по окну KitchenDesigner —
  ответ придёт. В новых сборках исправлено (приложение работает в фоне).
- `Unknown method` → запущена старая сборка без этого метода. Пересобери проект.
- Отправил `params` объектом вместо строки `parameters` → параметры молча
  потеряются (новые сборки понимают оба варианта, старые — только `parameters`).

## Модули (готовые узлы из досок)

Модуль — именованная группа досок (например «Тумба с ящиками»). Команды:

```powershell
.\tools\unity-bridge.ps1 get_modules                              # все модули с составом
.\tools\unity-bridge.ps1 module_info '{"module":"Тумба"}'         # один модуль: детали + габариты
.\tools\unity-bridge.ps1 create_module '{"name":"Тумба","members":["Бок левый","Бок правый","Дно"]}'
.\tools\unity-bridge.ps1 add_to_module '{"module":"Тумба","name":"Полка"}'
.\tools\unity-bridge.ps1 remove_from_module '{"name":"Полка"}'
.\tools\unity-bridge.ps1 enter_module_edit '{"module":"Тумба"}'   # режим редактирования модуля
.\tools\unity-bridge.ps1 exit_module_edit
.\tools\unity-bridge.ps1 dissolve_module '{"module":"Тумба"}'     # распустить (детали остаются)
```

В `get_all_elements` у каждой детали есть `moduleId`/`moduleName` — по ним видно,
из чего состоит модуль. В режиме редактирования модуля (enter_module_edit)
в приложении детали модуля редактируются поштучно, остальная сцена
заблокирована и затемнена; выход — Esc, кнопка «Готово» или exit_module_edit.

## Полный список методов

`ping`, `get_status`, `get_scene_hierarchy`, `find_objects`, `get_object_info`,
`set_object_active`, `delete_object`, `set_position`, `set_rotation`, `set_scale`,
`get_all_elements`, `get_element_info`, `move_element`, `resize_element`,
`rotate_element`, `create_element`, `delete_element`, `undo`, `redo`,
`get_specification`, `export_specification_csv`, `select_element`,
`get_undo_stack_info`, `get_console_logs`, `get_settings`, `set_snap_verbose`,
`snap_diagnose`, `get_modules`, `module_info`, `create_module`, `dissolve_module`,
`add_to_module`, `remove_from_module`, `enter_module_edit`, `exit_module_edit`,
`take_screenshot`.

Реализация всех методов: `Assets/Scripts/Core/MCP/McpCommandHandler.cs`.
