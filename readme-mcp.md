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

## Где подробности

- Инструменты, единицы, рабочий цикл — `get_project_instructions` и `guide`
  прямо из агента; тексты живут в `Assets/Scripts/Core/MCP/Contract/McpGuideTexts.cs`.
- Список инструментов и их параметры — `Assets/Scripts/Core/MCP/Contract/McpToolRegistry.cs`.
- Разбор JSON-RPC — `Assets/Scripts/Core/MCP/McpRpcRouter.cs`, транспорт —
  `McpHttpBridge.cs`, проверки — `McpRequestGate.cs`.
- Реализация всех методов — `Assets/Scripts/Core/MCP/McpCommandHandler.cs`.
