# Agent instructions

**Language policy:** All internal reasoning and planning MUST be done in English. Final responses to the user in chat MUST be written in Russian.

## NEVER `git stash` — the working tree is shared

Up to five agents edit this ONE working tree at the same time. `git stash` takes their
uncommitted work too, not just yours, and `stash pop` over files that moved on disk in the
meantime restores a state nobody can verify. It has already happened twice in one night, the
second time by an agent whose own task text forbade it two paragraphs earlier — so the rule
lives here, at the top of the map, and not only in `agents/GIT.md`.

Same for `git reset --hard`, `git clean`, and `git checkout`/`restore` without a path. To
look at the committed version of a file, use `git show HEAD:path/file` — it writes wherever
you point it and touches nothing. A red test in a file that is not yours is not yours to
diagnose: name it in the report and move on.

## Output discipline (keep the context small)

A debugging session burns context through tool OUTPUT, not through source size — the rules
below are what saves context. This is NOT a licence to grow files. Class size and
decomposition are governed by CONVENTIONS.md → "Class responsibility (SRP)", and production
source carries no comments at all — see CONVENTIONS.md → "Comments live in tests".

- **Never poll a background task for PROGRESS** — `run_in_background` reports completion itself,
  and ~80 log checks cost ~15k tokens once. **LIVENESS is the exception**: a HUNG task never
  completes, so it never reports — one free-model scan sat twelve hours with an empty log. Every
  ~10 min check the log's mtime (an `ls`, not a read); empty or unchanged ⇒ kill and restart on
  another model. **Pick the right pulse for the kind of worker.** An `opencode` run streams to
  its log, so mtime IS its pulse. A Claude subagent's transcript file does not: one sat 13
  minutes without a write while committing steadily. Judge that one by what it produces —
  `git log --oneline -1` — and kill it only when the harness reports a stall.
- **Filter at the source, never dump raw.** Select the fields you need instead of
  `ConvertTo-Json` over a whole array; `grep` the Unity log by marker instead of printing a
  line range (its NUnit stack traces and `Serialization depth limit` spam are worthless).
  For sweeps print only rows where the value CHANGES.
- **Never search from the filesystem root.** `find / -iname …` on this machine walks network
  drives and system trees and does not finish: one such search sat five hours and was killed by
  the user, who noticed it before any agent did. Search the repository, and for engine or
  package sources `Library/PackageCache` — the agent that started that `find` had already
  found its file there minutes earlier.
- **Read code targeted.** `codegraph_context` → `Read` with `offset`/`limit`. Read a whole
  file only when you truly need all of it; four full reads of 500–700-line files cost ~30k.
- **Aggregate repeated failures.** A test that prints the same message for every millimetre
  should collapse it (`@ 195..200мм (6 шагов)`). Better for context AND for reading.
- **Write bulk findings to a file** under `test-results/`, then read back only the
  summary.


## Карта инструкций

Этот файл — карта, и больше ничего. Правила живут в файлах ниже; каждый читается целиком,
когда задача его касается. Ссылки вида «`AGENTS.md` → «Название раздела»», разбросанные по
коду, тестам и другим документам, теперь ведут в один из этих файлов — ищи раздел по имени.

| Файл | О чём | Читать когда |
|---|---|---|
| **`LEAD-AGENT.md`** | роль менеджера: кто что запускает, кто правит инструкции, как раздавать работу | **как только тебя назвали главным агентом / менеджером / координатором** |
| `agents/GIT.md` | коммиты, push, `.meta`, запрет `stash`, временные файлы | перед любым коммитом |
| `agents/UNITY-GATEWAY.md` | `tools/unity.ps1`, холодный batch, замок, watchdog, что уже измерено и опровергнуто, «только один Unity на машине» | перед любым запуском Unity |
| `agents/FLEET.md` | один runner и много воркеров, opencode для рутины, общие реестры, общий git-индекс, как судить модель по кадру | как только в задаче участвует больше одного агента |
| `agents/TESTS.md` | какие наборы есть и сколько стоят, `build.cmd`, что НЕ входит в обычный прогон, `docs/example.save.json`, итерация по одному классу | перед прогоном тестов |
| `agents/TEST-DESIGN.md` | тест, который может упасть; противоположные входы; `!= null` как несущая проверка; построить недостающий сенсор; приёмка снапшот-эталонов | перед тем как ПИСАТЬ тест |
| `agents/SUBSYSTEMS.md` | снэп (две реализации, одна геометрия), валидация в ядре, скриншоты через PlayMode, MCP-мост, чек-лист нового свойства | при работе с этими подсистемами |
| `agents/DEPLOY.md` | деплой, релиз инсталлятора, ASCII+CRLF в `.cmd` | при выкладке и при правке `.cmd` |
| `CONVENTIONS.md` | **тоже карта** — правила кода лежат в `conventions/*.md`: `STRUCTURE` (SRP, быстрый путь, проверки типа), `COMMENTS`, `SERIALIZATION`, `TEST-NAMING`, `SHAPE-AND-SCREENSHOTS`, `UNITS-AND-FILES`, `CORRECTNESS` | перед правкой любого класса — как минимум `STRUCTURE` и `COMMENTS` |
| `FEATURES.md` | карта фича → классы → тесты | в начале задачи по фиче |
| `docs/TEXTURES.md` | текстуры декоров: физический размер, бесшовность, импорт | перед любой правкой текстур |
| `docs/APPLIANCES-BRIEF.md` | бриф встраиваемой техники Bosch: габариты, вырезы, ниши | при работе с варочной, духовкой, посудомойкой |
| `docs/UI-GUIDELINES.md` | правила UI, `UIStyle`, §13 подсказки «i» и тексты в `HintText`, §12 гизмо, §9 выделение и декор | перед правкой панелей, окон и гизмо |
| `docs/todo_evolution.md` | карта развития: критика UI, переделка сайдбара, путь от кухни к симулятору постройки дома, вопросы к пользователю | когда планируешь крупную работу по UI, каталогу или новым разделам продукта |
| `docs/DEVELOPMENT.md` | сборка и окружение | при проблемах со сборкой |
| `installer/PUBLISH.md` | публикация релиза/инсталлятора | когда просят выложить релиз |
| `readme-mcp.md` | MCP-поверхность приложения | при работе с MCP |

**Новый файл инструкций обязан попасть в эту таблицу тем же изменением, которое его создаёт.**
Файл, выросший за ~200 строк, дробится: карта дешевле длинного свитка, который перестают
дочитывать.

## Navigation (USE FIRST for any task)

1. `codegraph_context("feature name")` — focused context with entry points, symbols, code
2. `codegraph_search("ClassName")` — find class location
3. `codegraph_search("ClassNameTests")` — find tests
4. **FEATURES.md** — карта фича → классы → тесты
5. **CONVENTIONS.md** → «Class responsibility (SRP)» и «Comments live in tests» — обе обязательны
   перед правкой ЛЮБОГО класса: они решают, можно ли вообще класть изменение в существующий
   файл, и запрещают любой комментарий в продакшен-коде.
6. Всё остальное — по таблице выше.

## Architecture (quick reference)

```
Assets/Scripts/Core/            (сокращённо; полный список — `ls Assets/Scripts/Core`)
├── Geometry/      ← ValidationCore, снэп-геометрия — БЕЗ Unity, второй сборкой `geometry/core`
├── Pure/          ← слой без сцены — второй сборкой `geometry/pure`; быстрый путь живёт здесь
├── Elements/      ← KitchenElement, FacadeElement, Wall, Drawer, Table…
├── Snap/          ← SnapSystem, FaceContact, ResizeSnap
├── Commands/      ← CommandStack, UndoHandler
├── Persistence/   ← ElementData, SaveLoadManager
├── MCP/           ← McpCommandHandler, McpHttpBridge, McpRpcRouter, McpRequestGate
├── UI/            ← ContextMenuUI, UIManager, SidebarUI
├── Rendering/     ← GridManager, CameraController, WallCutaway
├── Materials/     ← MaterialCatalog, MaterialManager
├── Infrastructure/← Bootstrap, EventBus, GroupManager, GameContext
├── Diagnostics/   ← PerfMonitor (dev-only, F9)
└── Validation/    ← ConstraintValidator, FacadeValidator
```

