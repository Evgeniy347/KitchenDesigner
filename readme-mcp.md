# Подключение к работающему приложению (MCP-мост)

## Для opencode / MCP-агента (удалённый сервер)

MCP-эндпоинт ASP.NET-сервера (`Streamable HTTP`, JSON-RPC):

```
http://kitchendesigner.duckdns.org:8081/mcp
```

Проброшен напрямую через Docker (`docker-compose.yml:30`), минуя nginx.
Доступен из интернета — порт 8081 открыт на роутере.

Проверить связь (PowerShell):
```powershell
$body = '{"jsonrpc":"2.0","id":1,"method":"ping"}'
curl.exe -s -X POST http://kitchendesigner.duckdns.org:8081/mcp -H "Content-Type: application/json" -d $body
```

Успешный ответ: `{"result":{},"id":1,"jsonrpc":"2.0"}`

**Важно:** этот эндпоинт подключён к Unity только когда десктопное приложение
активно и подключено к тому же серверу. Если приложение не запущено — MCP
команды будут возвращать ошибки.

## Для Unity (локальная разработка)

Приложение Kitchen Designer слушает TCP-порт **9337** на localhost.
Мост запускается вместе с приложением, поэтому оно должно быть ЗАПУЩЕНО
(`Build/KitchenDesigner.exe` либо проект в Unity) — иначе подключаться не к чему.

### Проверить связь

```powershell
.\tools\unity-bridge.ps1 ping
```

`"status": "ok"` — связь есть. «Не удалось подключиться» — приложение не запущено.

### Для нейросети (MCP)

MCP-сервер зарегистрирован в `.mcp.json` как **`unity-kitchen`** и сам
подключается к порту 9337. Достаточно, чтобы приложение было запущено.

## Где подробности

- Инструменты, единицы, рабочий цикл — `mcp-server/README.md`.
- Реализация всех методов — `Assets/Scripts/Core/MCP/McpCommandHandler.cs`.
- Серверный MCP — `server/KitchenServer.Web/Endpoints/McpEndpoints.cs`, `Program.cs:148`.
