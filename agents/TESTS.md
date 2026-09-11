# Прогон тестов

## Build & run scripts

All in the repository ROOT (`F:\repos\KitchenDesigner2\kd-repose\`), Windows `.cmd`:

| Script | Purpose |
|--------|---------|
| `build.cmd` | Tests and/or build via the gateway. Flags: `-Clean`, `-RunTests`, `-RunPlayMode`, `-Filter <name>`, `-BuildOnly`, `-WinDebug`. **`-RunTests`/`-RunPlayMode` no longer build the player** — that used to add 7–8 min to every test check; ask for a build explicitly |
| `run-desktop.cmd` | Windows Debug build → launch. `-NoBuild` skips build |

That is the whole list: `build.cmd`, `clean.cmd`, `run-desktop.cmd`. There is no other build
target here, and nothing on this branch is deployed anywhere — see `DEPLOY.md`.

## Running all tests

**When asked to «запусти тесты», «протестируй код» or similar — run BOTH gate suites:**

| Suite | Command | Tests | Time |
|-------|---------|-------|------|
| `dotnet` (core + pure) | `.\tools\mutation-test.ps1 -TestsOnly` | 1361 (867 + 494) | **~2 s** тестов, ~9 s стены |
| EditMode | `build.cmd -RunTests` | 5498 | **~149 s тестов + ~33 s накладных** |
| PlayMode | `build.cmd -RunPlayMode` | 254 | **~113 s** |

The `dotnet` row is not a fourth suite — those files are compiled twice, by Unity and by
`geometry/*.csproj`. It is the inner loop; the three below are the gate.

**The same script without `-TestsOnly` runs the mutation gate** — minutes, not seconds, and it
saturates a core, so ask before launching it when the machine is shared. The threshold per
project lives in `geometry/mutation-baseline.txt` and Stryker falls below it with a non-zero
exit; raise the number in the same commit that raises the score. `MutationBaselineTests` keeps
that file honest in milliseconds on every fast run.

**One-liner for both:**
```powershell
cd F:\repos\KitchenDesigner2\kd-repose
.\build.cmd -RunTests -RunPlayMode
```

> **Note:** EditMode runs first, then PlayMode; a failure stops the rest. No player is
> built — ask for that separately. Each suite is its own cold Unity, so each pays the
> fixed ~10 s start; the numbers above include it and were measured, not guessed.
> Under five minutes for the pair is normal — use a `-Filter` while iterating on one class.
>
> **PlayMode halved on 2026-09-08 (220 s → 113 s) by fixing a LEAKED GLOBAL, not by cutting tests.**
> `FrameRateManager` drops `Application.targetFrameRate` to its idle value (10) after 0,7 s without
> input and never restored it in `OnDestroy`; in batch there is never any input, so the first test
> that idled put the WHOLE remaining run at 10 fps and every later `yield return null` cost 100 ms.
> A suite that is mysteriously slow end-to-end is the signature: look for a component that writes a
> global engine setting and does not put it back.

## Four things the normal run does NOT include — ASK before running them

All four are `[Explicit]`, so NUnit skips them unless a filter names them. Each was
measured, not guessed, and each is excluded for the same reason: it dominated the
edit→check cycle while answering a question nobody was asking at that moment.

| What | Cost | Run it with |
|------|------|-------------|
| `SnapMutationTests` — brute-force sweep over every face pair | **335 s** (79 s before the port-seat rule; was 69% of all EditMode) | `unity.ps1 tests -Platform EditMode -Filter SnapMutationTests` |
| `PerfProfileTests` — 660 profiler frames | **72 s** | `tools\artifacts.ps1 -Only perf` |
| `ProjectLoadPerfTests` — разбивка времени открытия проекта | **12 s** | `unity.ps1 tests -Platform EditMode -Filter ProjectLoadPerfTests` |
| `DrawerAnimationGifTests`, `OverviewScreenshotTests`, `GapsScreenshotTests` — draw `docs/*.png`, `docs/*.gif` | **88 s** | `tools\artifacts.ps1` |

- **`SnapMutationTests` before any real change to `SnapSystem`/`ResizeSnap`.** The sweep
  finds holes no point test sees; skipping it there is how «растягивается, но не
  перетаскивается» ships.
- **The generators are not tests.** A test compares against a golden and goes red; these
  produce a file, and produce it on EVERY run — that is why `docs/*.png` was permanently
  dirty in `git status` and everyone stopped looking. `tools\artifacts.ps1` runs them in
  one play session and reports which files actually changed size.

## Smoke tests on the BUILT player — the tip of the pyramid, and it stays tiny

Twice in one night the suite was green at five thousand tests while the player did not compile,
and once that reached a release: the ordinary gate runs tests and never starts the built
application. So the release path now launches the player and checks it — but that check is the
TIP of the pyramid and must keep the shape of one.

**What belongs there:** only what is invisible below it — the app starts, MCP answers, a create /
read / delete round trip through MCP, a project save and load, the app exits leaving no process.
That is the whole list. Anything provable in an EditMode or a dotnet test is proved there
instead: those cost milliseconds and this costs a player launch.

**Budget: 10 ms per step is an orientir, not yet met** — first live run: `mcp_initialize` 2,85 мс
(HTTP itself is cheap), but `create/get/delete_elements` 280–410 мс each (scene ops, под
расследованием в `Core/MCP/*`, не здесь). A step over budget prints `BUDGET EXCEEDED` loudly but
never fails the run alone — only a wrong RESULT does; `shutdown_no_orphan` (~1,1 с) is player
teardown, not a budgeted step.

**Do not run them on their own.** They are part of publishing a release, not a command an agent
reaches for. Write them, wire them, leave them; a failed smoke check stops the release, which is
the only signal they owe anyone.

**The MCP port is an argument** (`-mcpPort`, default 9337), because the smoke run must not fight
the user's own running copy for the port.

**Плеер запускается скрытым и молчащим.** `-WindowStyle Hidden` сам по себе окно Unity-плеера
не прячет: плеер вызывает `ShowWindow` при старте и ещё раз при `Screen.SetResolution`. Окно
прячет СЕБЯ сам плеер — аргументом `-hideWindow` (`HideWindowArgument`, разобран так же, как
`-muteAudio`), применённым внутри `DisplaySettings.ApplyWindowMode`. Это решение приложения, а
не скрипта: оно переживает любой будущий вызов `ApplyWindowMode` (смену настроек, загрузку
проекта), а не только тот, что происходит при старте в `Bootstrap`. Скрипт `smoke-test.ps1`
держит собственный опрос `MainWindowHandle`/`user32!ShowWindow(SW_HIDE)` только как страховку на
первые секунды, пока движок не дошёл до `Awake()` — как только окно один раз спряталось, страховка
выключается. `-batchmode`/`-nographics` не годятся, дымовой проверке нужна живая сцена. Звук
глушится аргументом `-muteAudio` (`MuteAudioArgument` → `AudioOutputPolicy.Decide` в `Pure/`), а
не настройкой: файл настроек пользователя проверка не трогает. Пользователь работает за той же
машиной — всплывшее окно и заигравшая музыка это его рабочий день, а не косметика.

**Прогон на машине пользователя изолируется по ЗАПИСИ, а не по подчистке.** Дымовая проверка
подменяла пользователю «последний открытый проект»: обычный запуск после неё открывал
`kd-smoke-<хэш>/smoke-roundtrip.save.json`. Сохранить старое значение и вернуть в конце —
неверное лечение: прогон может упасть посередине, и тогда возвращать будет некому. Поэтому
`-ephemeralSession` (`EphemeralSessionArgument`, разобран как `-muteAudio`) означает «не
писать вовсе»: под ним живут `KitchenLastSavePath` и `KitchenFirstRunDone`. Настройки
сайдбара тоже под ним: `PlayerPrefs` трогает ровно ОДИН файл продакшена — `PreferenceStore`, — и любой новый ключ заводится через него; сторож `PlayerPrefsUnderEphemeralRuleTests` называет файл и ключ.

**Сборка плеера — отдельный редкий шаг, а не часть `-RunTests`.** Дважды за ночь тесты были
зелёными при несобирающемся приложении. Обычный прогон остаётся быстрым, но **в конце каждой
многоагентной сессии менеджер гоняет `build.cmd -BuildOnly`** — один раз на всех исполнителей.
Зелёный `dotnet` не компилирует ни UI, ни Elements, ни MCP; зелёный EditMode не доказывает, что
`.exe` собирается.

## `docs/example.save.json` — NEVER TOUCH IT

**This file belongs to the user. Do NOT modify it, do NOT revert it, do NOT
`git checkout --` it, do NOT let a test write to it. Ever.** Its working-tree state is
the user's current work, and a revert destroys it silently — there is no undo for
`git checkout --` on an uncommitted file.

**And do NOT raise an alarm about it — just ignore it.** Its dirty state in
`git status`, and commits that carry it (`chore: снимок рабочего проекта пользователя`
and the like), are the owner's own doing: they snapshot their working project themselves, and
have confirmed this explicitly. Do not report it as an anomaly, do not investigate who
committed it, do not ask another session about it, do not open a task for it. Note also
that provenance is not establishable here at all: the repo has ONE git identity, shared
by the user and by `tools/git-commit.ps1`, so the `Author` field cannot tell an agent's
commit from the owner's. "I checked the author" is not a check.

The hands-off rule above stays in force for us regardless: never modify, revert or
commit this file. Ignoring it means staying silent about it, not touching it.

It is a live project the user edits in the desktop app, and the app writes back into it:
`AutoSaveManager.cs:56` autosaves to `SaveLoadManager.LastPath` — the path of the OPEN
project — and only falls back to a file named `autosave` when nothing is open. So opening
`docs/example.save.json` in the app is enough for autosave to rewrite it. Nothing else in
the repo writes there; all other references only read.

**`git-commit.ps1 -All` sweeps it into your commit.** The file is dirty almost all the time,
so `-All` quietly adds 40 000 changed lines of the user's project to a refactoring commit —
this happened on 2026-08-31 and had to be undone with `git reset HEAD~1 -- docs/example.save.json`
plus an amend, keeping a copy of the working-tree version aside first. **That remedy is now
forbidden** — `agents/FLEET.md` → «Never `--amend` or `reset` in a shared tree»; a wrong commit
gets a second commit on top, and only the coordinator rewrites history. **Use `-Files`**, or
check `git status` before reaching for `-All`.

And a dozen tests read it as a fixture. That combination is the trap, and it has already
cost one full investigation: the user's version had `movable` cleared on 41 elements, they
entered the sweep, and `SnapMutationTests` produced 200 `NO-SNAP` errors. The snap code
was untouched — the committed version of the same file is green.

**So when a snap or snapshot test goes red, check `git status docs/example.save.json`
FIRST** — a fixture that moved explains it far more often than the code does. Then say so
and stop. Diagnosing it is right; "fixing" it by restoring the file is not.

The file can also change DURING a run, so "it failed, I re-ran it, now it fails differently"
is not a flaky test — it is the fixture moving underneath you. Two runs of unchanged code once
reported 1 and then 7 `COL-01` collisions. Compare counters only between runs whose `mtime` on
the file is identical.

The correct fix for a test is a FROZEN copy of the scene, never the live file —
`ValidationInvariantTests` already does exactly this and explains why in its summary.
Any test that needs a stable scene should follow it.


## The run's report survives exactly until the next run

`test-results/tmp/TestResults.xml` is overwritten by EVERY run, EditMode and PlayMode alike, and
the console summary prints only the FIRST failures — 8 of 20 in the run that taught us this. So a
coordinator who runs EditMode, then PlayMode, then copies "the report" hands the workers the
PLAYMODE file and does not notice: it is well-formed XML with `failed="0"`, and the worker
discovers the swap only after re-deriving half the diagnosis from source. That happened, and it
cost one worker its whole first pass.

Copy the XML aside the moment a run ends, before starting anything else, and name it after the
platform and the round:

```powershell
Copy-Item test-results\tmp\TestResults.xml test-results\round1\EditMode-round1.xml
```

Then hand the workers the FULL failure list from that copy, not the console tail:

```powershell
$x=[xml](Get-Content test-results\round1\EditMode-round1.xml -Raw)
$x.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { $_.fullname }
```

Two cheap checks that catch the swap before anyone reads it: the root's `testcasecount` (4744 for
EditMode, 254 for PlayMode on 2026-09-08 — the suites grow weekly, so compare against the run you
just did) and `failed` — a copy claiming zero failures after a run
that printed twenty is the wrong file, every time.


**`SaveValidationTests` no longer reads the live file at all (2026-09-09).** It used to demand
zero findings on `docs/example.save.json` directly, so the moment the user's own project
genuinely contained one — a leg sunk into a carcass, an end only partly covered for edge
banding, a couple of millimetres shy of a pipe joint — the test went red and stayed red until
they fixed their kitchen, indistinguishable from a real code regression. Its acceptance
assertions now run on a FROZEN fixture (`Fixtures/pipe-gap-scene.save.json`, a snapshot taken
at a point in time) against a numeric BASELINE (known error/warning/GAP-02 counts, known
sub-tolerance-joint count) — the same shape as `ValidationInvariantTests`, not a demand for
zero. `SaveValidationSensorTests` keeps watching the LIVE file, but only prints
(`TestContext.WriteLine`), never asserts — the same split `PipeGapSensorTests` already uses.
A rising baseline number is a real regression; read the finding (the code says which rule
fired and between which parts) before touching anything, and move the baseline down only after
confirming the scene, not the code, improved.

**The audit that followed (2026-09-09): NOTHING in the ordinary gate asserts on the live file
any more.** Twenty-seven files referenced it. Two more asserted about NAMED parts of the user's
kitchen — `EdgeSubstrateTests.RealScene_TopPanel_...` (`A12_upper_A_top`) and
`SinkRealSceneTests` (`Moyka` on `Countertop_B`) — and moved to the frozen fixture;
`PhotoModeScreenshotTests` was the last gate test loading the live file at all and moved too.
What still reads the live file is exactly two kinds, both harmless: SENSORS that only print
(`SaveValidationSensorTests`, the live half of `PipeGapSensorTests`) and `[Explicit]`
generators that are SUPPOSED to picture the real kitchen (`SnapMutationTests`,
`PerfProfileTests`, the `docs/*.png`/`*.gif` makers). The rule that came out of it: a test may
name a part of the user's scene only when the scene is frozen — on the live file, name
nothing and assert nothing.


**`Copy-Item` сохраняет время источника, и MSBuild считает восстановленный файл неизменившимся.**
После откатки мутации из бэкапа `dotnet test` собрал СТАРУЮ dll и показал те же красные, что и
мутация, — «откат не помог» читается как настоящий дефект и стоит целого ложного диагноза.
Восстанавливай исходник через `git show HEAD:path` либо обновляй время
(`$_.LastWriteTime = Get-Date`). Тот же капкан уже записан для снапшот-эталонов
(`Move-Item` тоже сохраняет время), но там цена — неверная дата файла, а здесь — неверный вывод
о продукте.

## Iterating on ONE test class

Use the gateway with a filter — **~11 s against ~149 s** for the whole suite. Filtering now
pays for itself: the fixed start is ~10 s, and a filtered run additionally turns Burst
compilation off (worth 2,3 s, and only there — on a full run it buys exactly nothing).
Still run the FULL suite before committing.

```powershell
.\tools\unity.ps1 tests -Platform EditMode -Filter "MyTests"
```

A filter that matches NOTHING is an error, not a green run — the reason and the rest of the
gateway's rules are in `agents/UNITY-GATEWAY.md`: `-Filter SnapCoreTests`
(no such class) used to print `Total: 0 | Failed: 0` and exit 0 — a typo that looked like
a passing check.

It prints the summary and the first failures itself; the full report stays in
`test-results/tmp/TestResults.xml` (parse with `[xml]`: `//test-case`,
`.failure.message.'#cdata-section'`).

Faster still: if the code under test lives in `Assets/Scripts/Core/Geometry/` or
`Assets/Scripts/Core/Pure/`, run it outside Unity — 1361 tests in ~2 s:

```powershell
.\tools\mutation-test.ps1 -TestsOnly
```
**Do not use `python`** — in this environment it is the Windows Store stub and silently
prints nothing.

## Быстрый путь: что уже опробовано и ОТКЛОНЕНО

Мерено 2026-09-08, шесть ядер, тёплая сборка. Стена `mutation-test.ps1 -TestsOnly` —
**8,7 с**, из них тесты только **2,3 с** (`Geometry.Tests` 867 за 2,0 с, `Pure.Tests`
494 за 0,28 с). Остальные 6,4 с — msbuild и хост vstest, а НЕ тесты. Это константа: она
не растёт с числом тестов и к цели «20 000 быстрых тестов» отношения не имеет. Три
попытки её срезать провалились — не повторять без нового аргумента:

- **Один хост vstest на обе сборки вместо двух `dotnet test`.** Сам хост действительно
  дешевле: `dotnet vstest Geometry.Tests.dll Pure.Tests.dll` = 4,26 с против 6,70 с у
  двух отдельных прогонов. Но два `dotnet build`, которые для этого нужны, съедают ровно
  этот выигрыш: стена стала **9,0–10,3 с против 8,7 с**. Хуже. Откачено.
- **`ParallelScope.All` вместо `ParallelScope.Fixtures`** в `geometry/tests`. Зелено
  (867/867, три прогона), но не быстрее: 2–3 с против 2 с. На шести ядрах фикстурный
  параллелизм уже даёт 4,3× (8,58 с CPU при 2,0 с стены) — потолок близко, и тесты
  внутри одной фикстуры делят её поля, так что риск есть, а выигрыша нет.
- **Общий кэш дерева исходников для архитектурных сторожей** (предложен в
  `test-results/fast-path-audit.md` §5 как «~2,5 с из 6»). Отклонено: в trx полного
  прогона `CoreTestSources_DoNotTouchTheEngine` показывает 0,98 с, но при запуске одного
  `GeometryArchitectureTests` все его 13 тестов вместе стоят **0,25 с**. Та секунда —
  разовый прогрев JIT и файлового кэша, списанный на первый попавшийся тест, а не цена
  сканирования. Кэш вернёт миллисекунды.

**Урок общий:** в trx и в `TestResults.xml` первый выполненный тест несёт на себе прогрев
всего процесса. Прежде чем чинить «самый медленный тест», прогони его класс отдельно —
половина таких «медленных» тестов при этом становится в десять раз дешевле.


**Покраснел брутфорс — сверяй ФИКСТУРУ раньше кода.** `SnapMutationTests` дал 6683 ошибки и
вырос с 335 до 602 с, и всё это выглядело регрессом вчерашних правок. Виноват был размер
сцены: `docs/example.save.json` — живой файл пользователя, и в нём стало 411 деталей против
305 на момент последнего зелёного прогона, причём обе новые семьи (24 винтовые опоры, шесть
цокольных ящиков) стоят в самом плотном углу, а свип квадратичен. Второй вопрос к красному
брутфорсу после «что я менял» — **сколько деталей в файле сегодня и сколько было при
последнем зелёном** (`git log -- docs/example.save.json`).

И наоборот: такой прогон окупается даже когда диагноз «не регресс». Тот же красный назвал
настоящий дефект продукта — винтовая опора прилипала к дну ящика, насаживаясь СКВОЗЬ него:
пара выглядела здоровой (встречные грани, зазор в пределах порога, полное перекрытие), и
никто не спрашивал, остаётся ли над гранью крепления толща цели.
