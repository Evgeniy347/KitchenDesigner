<p align="center">
  <h1 align="center">Kitchen Designer</h1>
  <p align="center">
    3D Furniture Board Constructor / 3D-конструктор мебельных щитов<br>
    Unity 6000 • C# • URP • ASP.NET Core 10 • PostgreSQL
  </p>
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
</p>

<p align="center">
  <img src="docs/overview.png" alt="Общий вид программы" width="700">
</p>

---

## English

### Overview

A visual 3D furniture board constructor for laying out cabinets, kitchens, and other case furniture.

### Features

- **Create boards** of arbitrary size or from presets
- **Face-to-face snapping** — boards automatically snap to each other's faces (configurable threshold)
- **Grid snapping** — configurable grid step (default 1 mm)
- **Spatial grid** — optional 3D grid rendered on the floor
- **Facade gaps** — per-side gap control (left, right, top, bottom; default 2 mm each)

  <img src="docs/facade-gaps.png" alt="Facade gaps" width="500">

- **Validation system** — automatic intersection and connectivity checking (BFS from walls/floor); violations highlighted in red
- **Block on violation** — optionally prevent moves that make the structure invalid
- **Groups (modules)** — group boards into named modules, move them as one
- **Module edit mode** — edit one module while others are locked and dimmed
- **Specification** — bill of materials with dimensions and area, export to CSV

  <img src="docs/export-csv.png" alt="Export to CSV" width="500">

- **Save/Load** — projects saved as JSON. Load a ready-made configuration from file (see `docs/example.save.json`)
- **Auto-save** — configurable interval, only when changes detected. **Backup** — on manual save, the old file is archived to `saves/backups/`
- **Undo/Redo** — command stack of up to 20 operations
- **Camera controls** — orbit (RMB), pan (MMB), zoom (scroll), WASD movement
- **Resize handles** — interactive resize via handles on selected boards
- **Material catalog** — apply textures/decor to boards
- **Wall cutaway** (Sims-like) — walls lower to 100 mm when near for visibility
- **Edge outlines** — optional board edge highlighting
- **Align/Distribute** — align and evenly distribute boards

### MCP Bridge (AI-driven modeling)

Kitchen Designer is available via **MCP (Model Context Protocol)** — an AI agent can model the construction autonomously.

Any MCP-compatible AI agent (Claude Code, opencode, etc.) can read the scene, create/move/resize boards, check violations, control the camera, and export specifications.

To connect an agent, simply ask it to read the [`readme-mcp.md`](readme-mcp.md) file and follow the workflow described there.

### Quick Start

```bash
# Open in Unity Editor
# Open Assets/Scenes/TestScene.unity
# Hit Play
```

### Scripts

| Script | Purpose |
|--------|---------|
| `build.cmd` | Build Windows (.exe) or WebGL. Flags: `-Clean`, `-RunTests`, `-RunPlayMode`, `-BuildOnly`, `-WebGL` |
| `build-server.cmd` | Build ASP.NET server |
| `clean.cmd` | Clean temporary files (Unity + server) |

### Stack

| Component | Version |
|-----------|---------|
| Unity | 6000.4.3f1 |
| Render Pipeline | URP 17.4.0 |
| Language (front) | C# |
| Backend | ASP.NET Core 10 + Blazor Server |
| Orchestration | .NET Aspire 13 |
| Auth | ASP.NET Identity |
| Database | PostgreSQL |
| Tests | Unity Test Framework (NUnit) |

---

## Русский

### Что это

Визуальный 3D-конструктор для раскладки мебельных щитов (досок). Позволяет проектировать кухни, шкафы и другую корпусную мебель.

### Возможности

- **Создание досок** произвольных размеров или из пресетов
- **Face-to-face snapping** — доски автоматически прилипают друг к другу гранями (порог настраивается)
- **Привязка к сетке** — шаг сетки настраивается (по умолчанию 1 мм)
- **Пространственная сетка** — опциональная 3D-сетка на полу
- **Зазоры фасадов** — у фасадных элементов можно выставить зазор слева, справа, сверху и снизу (по умолчанию 2 мм с каждой стороны)

  <img src="docs/facade-gaps.png" alt="Зазоры фасадов" width="500">

- **Система проверки** — автоматический контроль пересечений и связности конструкции (BFS от пола/стен); нарушения подсвечиваются красным
- **Блокировка при нарушениях** — опционально запрещает перемещение, если конструкция становится невалидной
- **Группы (модули)** — объединение досок в именованные группы, перемещение целиком
- **Режим редактирования модуля** — редактирование одного модуля, остальные блокируются и затемняются
- **Спецификация** — таблица всех деталей с размерами и площадью, экспорт в CSV

  <img src="docs/export-csv.png" alt="Экспорт в CSV" width="500">

- **Сохранение/загрузка** — проекты сохраняются в JSON. Можно загрузить готовую конфигурацию из файла (см. `docs/example.save.json`)
- **Автосохранение** — с настраиваемым интервалом, только при наличии изменений. **Бекап** — при ручном сохранении старый файл архивируется в `saves/backups/`
- **Undo/Redo** — стек команд до 20 операций
- **Управление камерой** — орбита (ПКМ), панорама (СММ), зум (колесо), WASD
- **Ручки изменения размера** — интерактивное изменение через ручки на выделенной доске
- **Каталог материалов** — применение текстур/декора к доскам
- **Обрезка стен** (Sims-like) — при наведении стены опускаются до 100 мм для обзора
- **Edge Outline** — опциональная подсветка рёбер досок
- **Align/Распределение** — выравнивание и равномерное распределение досок

### Управление через MCP (нейросеть)

Kitchen Designer доступен по **MCP (Model Context Protocol)** — ИИ-агент может моделировать конструкцию самостоятельно.

Любой MCP-совместимый ИИ-агент (вроде Claude Code, opencode и т.п.) может читать сцену, создавать/перемещать/изменять доски, проверять нарушения, управлять камерой и экспортировать спецификацию.

Для подключения агента достаточно попросить его прочитать файл [`readme-mcp.md`](readme-mcp.md) и следовать описанному там рабочему циклу.

### Быстрый старт

```bash
# Открыть проект в Unity Editor
# Открыть Assets/Scenes/TestScene.unity
# Нажать Play
```

### Скрипты

| Скрипт | Назначение |
|--------|-----------|
| `build.cmd` | Сборка Windows (.exe). Флаги: `-Clean`, `-RunTests`, `-RunPlayMode`, `-BuildOnly`, `-WebGL` |
| `build-server.cmd` | Сборка ASP.NET сервера |
| `clean.cmd` | Очистка временных файлов (Unity + сервер) |

### Стек

| Компонент | Версия |
|-----------|--------|
| Unity | 6000.4.3f1 |
| Render Pipeline | URP 17.4.0 |
| Язык (front) | C# |
| Бэкенд | ASP.NET Core 10 + Blazor Server |
| Оркестрация | .NET Aspire 13 |
| Аутентификация | ASP.NET Identity |
| База данных | PostgreSQL |
| Тесты | Unity Test Framework (NUnit) |

---

## Лицензия / License

MIT License. See [LICENSE](LICENSE) for details.
