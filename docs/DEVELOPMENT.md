# Разработка

Всё, что нужно, чтобы собрать, запустить и проверить проект. Пользовательское
описание программы — в [README](../README.md).

## Открыть в редакторе

```
Assets/Scenes/TestScene.unity → Play
```

Сцена почти пустая: камера, направленный свет и `Bootstrap`. Всё остальное —
интерфейс, каталог, сетка, валидация — собирается кодом на старте.

## Сборка

| Скрипт | Назначение |
|--------|-----------|
| `build.cmd` | Сборка Windows (.exe) или WebGL. Флаги: `-Clean`, `-RunTests`, `-RunPlayMode`, `-BuildOnly`, `-WebGL`, `-WebGLDebug`, `-WinDebug` |
| `build-server.cmd` | Сборка ASP.NET-сервера |
| `clean.cmd` | Очистка временных файлов (Unity + сервер) |

## Запуск

**Локально (отладка):**

| Скрипт | Назначение |
|--------|-----------|
| `run-desktop.cmd` | Самая быстрая итерация: инкрементальная development-сборка Windows (Mono) → `Build_Debug/`, запускает exe. `-NoBuild` — только запуск |
| `run-webgl.cmd` | WebGL-отладка в Docker: собирает `Builds/WebGL_Debug` (без сжатия и стриппинга), поднимает nginx+web+db из `server/docker-compose.local.yml`, открывает http://localhost:8080. nginx монтирует папку сборки напрямую — пересобрал WebGL, обновил страницу, Docker не перезапускаешь. `-NoBuild` — пропустить сборку Unity |

**Релиз:**

| Скрипт | Назначение |
|--------|-----------|
| `build.cmd -WebGL` | Релизная сборка WebGL (gzip, high stripping) → `Builds/WebGL` |
| `deploy.cmd` | Публикация сервера + копирование WebGL-сборки и `server/docker-compose.yml` на прод-хост |

**Замеры сборки** (тёплый кеш Library; полное время включает ~15 с запуска
редактора Unity):

| Вариант | Полное время | Пайплайн Unity | Размер |
|---------|--------------|----------------|--------|
| Windows Debug, инкрементально (`-WinDebug`) | 19 с | 6 с | 157 МБ |
| Windows Debug, первый прогон | 71 с | 55 с | 157 МБ |
| WebGL Debug (`-WebGLDebug`) | 73 с | 56 с | 200 МБ |
| WebGL Release (`-WebGL`) | 9,5 мин | 549 с | 13,5 МБ |

## Тесты

Единая точка входа в Unity — `tools/unity.ps1`. Каждый вызов поднимает СВОЙ
холодный `Unity.exe -batchMode` и дожидается его выхода; долгоживущего фонового
редактора в проекте нет намеренно. Один клиент за раз на всю машину: команды
ждут именованный мьютекс, а затем берут файловый замок на `Library/`.

```powershell
.\tools\unity.ps1 tests -Platform EditMode                    # весь набор
.\tools\unity.ps1 tests -Platform EditMode -Filter SnapTests  # прицельно
.\tools\unity.ps1 tests -Platform PlayMode
.\tools\unity.ps1 status                                      # свободен ли шлюз
.\tools\unity.ps1 stop                                        # снять зависший batch
```

То же самое из сборки: `build.cmd -RunTests` (EditMode) и `-RunPlayMode`.

## Картинки для документации

Картинки в `docs/` рисуют PlayMode-генераторы. Это не тесты: тест СРАВНИВАЕТ с
эталоном и краснеет при расхождении, а генератор ПОРОЖДАЕТ файл и перезаписывает
его каждый раз, даже когда ничего не менялось. Поэтому они помечены `[Explicit]`,
в обычный прогон не попадают и запускаются отдельно:

```powershell
.\tools\artifacts.ps1                 # все генераторы
.\tools\artifacts.ps1 -Only photo     # только заглавный кадр в фоторежиме
.\tools\artifacts.ps1 -Only gif       # только GIF анимации ящика
```

| Генератор | Что рисует |
|-----------|-----------|
| `PhotoScreenshotTests` | `docs/photo.png` — заглавный кадр в фоторежиме |
| `OverviewScreenshotTests` | `docs/overview.png` — общий вид с интерфейсом |
| `GapsScreenshotTests` | `docs/gaps_overview.png`, `docs/facade-gaps.png` — контуры рёбер и зазоры |
| `SpecificationScreenshotTests` | `docs/specification.png` — окно спецификации |
| `DrawerAnimationGifTests` | `docs/drawer_animation.gif` — цикл открывания двойного ящика |
| `PerfProfileTests` | `test-results/perf/*.csv` — профиль камеры |

Общая обвязка живёт в `Assets/Tests/PlayMode/DocsArtifactFixture.cs`: загрузка
`docs/example.save.json`, окружение, орбита камеры, композиция UI поверх 3D и
запись PNG. Подводные камни зафиксированы там же комментариями, и каждый из них
однажды уже испортил картинку в `docs/`:

- **Плоскость UI-канвы.** В RenderTexture канва попадает только в режиме
  `ScreenSpaceCamera`, а там она соревнуется со сценой по глубине: далеко — пол
  выедает куски из панелей, слишком близко — не рисуется SDF-текст TextMeshPro.
- **Контуры рёбер.** `EdgeOutlineRenderer` рисует их через `GL.LINES` мимо
  буфера глубины и после канвы, поэтому в кадрах С интерфейсом они выключаются:
  иначе рёбра деталей, уходящих за левую панель, тянутся линиями поверх неё.
- **Камера.** Крупный план нельзя ставить `transform.LookAt` — `CameraController`
  каждый кадр пересчитывает позицию из орбиты, и ручная установка не доживает до
  съёмки. И фоторежим достраивает потолок: камера обязана остаться внутри
  комнаты, иначе в кадре изнанка перекрытия или наружная стена.

Все генераторы берут сцену из `docs/example.save.json` — это рабочий проект
владельца репозитория, снимки которого он коммитит сам. Файл не редактируют
руками и не откатывают.

## Стек

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
