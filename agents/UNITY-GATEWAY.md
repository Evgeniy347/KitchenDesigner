# Шлюз Unity

`tools/unity.ps1` runs every Unity call as its **own cold `Unity.exe -batchMode`
process**, and every script goes through it. The process starts, does the job,
exits. Nothing survives between commands.

There used to be a long-lived background editor here (a "daemon" plus a socket
bridge in `Assets/Editor/EditorBridge.cs`). It was **removed**. On paper it saved
the fixed 60–90 s start on every run; in practice it kept falling over — a socket
silent during a domain reload is indistinguishable from a hung one, two clients
fought over one editor, a run could go green against code compiled before the
edit, and diagnosing "is it still alive" cost more than the start it saved. Cold
is slower and reproducible: one state instead of three, and no "for some reason
it's different this time".

Measured on this project, cold (re-measured 2026-09-04): one test class
**~11 s**, full EditMode (4744 tests) **~167 s**, full PlayMode (254 tests) **~113 s**,
WinDebug player ~3 min. (EditMode re-counted 2026-09-08; PlayMode fell from 220 s that day when
`FrameRateManager` stopped leaving the batch run at 10 fps — `agents/TESTS.md` → «Running all
tests».) The suites have roughly doubled since 2026-09-01, when the same
runs were 73 s / 91 s at 2860 / 92 tests — the per-test cost did not move, the count did.
The fixed start before the first test is **~10 s**, not the
60–90 s this file used to claim: licence and engine 2,4 s, two domain reloads, asset
refresh, and ~2,5 s of collecting the tests. An empty project of the same Unity version
has 8,1 s of that floor, so almost none of it is ours to remove: licence and engine 2,4 s,
two domain reloads, asset refresh, ~2,5 s collecting the tests. (The plan that measured this
was deleted once done — `893437de` — and its numbers live here now.)

```powershell
.\tools\unity.ps1 tests -Platform EditMode -Filter SnapshotTests   # targeted run
.\tools\unity.ps1 tests -Platform EditMode                         # full suite
.\tools\unity.ps1 tests -Platform PlayMode
.\tools\unity.ps1 method -Method BuildProject.Build                # player build
.\tools\unity.ps1 status                                           # is the project free
.\tools\unity.ps1 stop                                             # kill a Unity wedged on this project
```

Rules that matter:

- **ONE client at a time.** Unity holds the project (`Library`) exclusively, so two
  batch runs at once are impossible anyway — the second either dies or grinds against
  a locked `Library`. Every command takes the file lock `Library\kd-unity-gateway.lock`,
  which turns that into a visible queue and prints who holds it; otherwise someone
  else's run just looks like "everything is slow for no reason". `status` skips the
  lock on purpose (asking "is the project busy" must not queue) and reports the holder.
- **An open Unity from the Hub blocks the run, and the gateway will NOT close it** —
  there may be an unsaved scene in it. It says so and stops. An orphaned *batch*
  process (left by a killed script) is a different case: it never releases `Library`
  on its own and turns every later command into a multi-minute wait, so that one is
  force-killed.
- **A live process is NOT progress — a growing log is.** 22 minutes went once into a
  run whose tests finished in 228 s and which then wedged *after* the run (post-run
  Undo plus an endless 404 from the licensing client). The watchdog therefore watches
  `%TEMP%\build-kitchen.log`: a working Unity writes a line per import/compile/reload/test
  step, a wedged one writes nothing. Quiet longer than `-SilenceMinutes` (default 5)
  → killed and said out loud, rather than sitting until `-TimeoutMinutes` (default 30).
- **Каждый прогон печатает ДВЕ величины, а не одну**, плюс пять самых долгих классов: время
  тестов (`test-run/@duration` из отчёта NUnit) и накладные (стена минус это время). Порогов тоже
  два — тесты 170 с, накладные 45 с — и сообщение о превышении называет виновную половину.
  Замер 2026-09-11 на 5 498 тестах: 149 с NUnit + 33 с накладных; из накладных лицензия 2,4 с,
  два domain reload 5,1 с, **компиляция 14,3 с** (Runtime 5 с и сборка тестов 7 с последовательно),
  сбор тестов 2,5 с, выход процесса ~4 с. Пол пустого проекта — 8,1 с; всё сверх него это
  компиляция, и она платится в КАЖДОМ прогоне ворот, потому что ворота всегда идут после правки
  исходников. Правкой тестов она не сокращается. Один порог на сумму стоил полдня поиска секунд в
  тестах при том, что пятая часть бюджета лежала в компиляции. Цикл иначе деградирует тихо: минуту,
  которая вползла, замечают через неделю как «всё стало медленно».
- **`test-run/@duration` NUnit пишет в ТЕКУЩЕЙ культуре, а вложенные `test-suite/@duration` — в
  инвариантной.** В одном файле на русской машине лежат `0,2048422` и `0.204842`. Разбор корня по
  `InvariantCulture` молча съедает запятую и превращает 0,2 с в 2 048 422 с — а накладные при этом
  становятся отрицательными, что и есть единственный признак беды.
- **`dotnet` FIRST, Unity LAST.** The core AND the scene-free layer compile a second time under
  plain `dotnet` (`Assets/Scripts/Core/Geometry` + `Assets/Scripts/Core/Pure`, with their test
  directories): `.\tools\mutation-test.ps1 -TestsOnly` runs 1361 tests in **~2 s**. Anything you
  extract that does not need a scene belongs there — CONVENTIONS.md → "A class without a scene
  lives on the fast path". A cold Unity batch still costs ~10 s of fixed overhead — licence,
  engine, two domain reloads, asset refresh, test collection — *before the first test runs*,
  so a one-class `-Filter` costs ~11 s. Iterate on `dotnet`; run Unity when the change
  touches scene behaviour or you are about to commit.
- **The cold start has been dissected; do NOT re-measure it.** Of ~11 s for a filtered run:
  licensing **2,4–2,8 s** (fully blocking — the engine starts only after it), engine 0,2 s,
  domain reload #1 1,0 s, asset refresh ~3,0 s (reload #2 1,7 s inside it), «project loaded»
  0,5 s, TestRunner start and collecting the suite (3400 tests at the time of the measurement)
  1,6 s, the tests themselves 0,9 s. An empty project of the same Unity version has an 8,1 s
  floor, so almost none of this is ours to remove.
  Licensing is the largest single hole and nothing known touches it.
- **Already measured and REFUTED — spending the machine on these again is waste:**
  `indexOnEditorStartup=false` (indexing starts after the run and lives on a worker), `-nographics`,
  `-disable-assembly-updater`, `m_RefreshImportMode: 1`, removing `codecoverage` (0,5 s and it is
  used), Windows Defender exclusions in every form tried (project, `Library`, `Temp`, `Unity.exe`,
  the editor install and `%LOCALAPPDATA%\Unity`), a warm licensing client with
  `--disable-auto-shutdown` (0,06 s — `connect` goes to zero and `handshake` grows by exactly as
  much), `-assemblyNames` (inside the control's own spread), floating licences (a paid product,
  not for Personal). `-noUpm` does not just fail to help — it BREAKS the run: no `TestResults.xml`
  and one domain reload instead of two.
- **What DID work, and is already applied:** dropping `com.unity.ai.assistant` (−2,4 s, and it
  cut the asset tree by a third), polling the process every 250 ms instead of every 2 s (~1 s, and
  it removed the mysterious 16,3/18,4 bimodality — that was our own `Start-Sleep`, not the editor),
  and `UNITY_BURST_DISABLE_COMPILATION` on FILTERED runs only (−2,3 s there, exactly zero on a
  full run, where the work returns inside the suite).
- **A parallel workload invalidates any Unity timing.** One busy core costs a cold filtered run
  THREE times its duration (13 s → 44,3 s), and a bench that averages load across six cores does
  not see it: a single-threaded hog lifts the average by ~17 points, which drowns in Unity's own
  noise. Measure per-core, and name the processes.
- **PlayMode before a release and after any panel-layout change.** It went unrun for an entire
  refactoring campaign — the largest blind spot in it.
- **A filter that matches nothing is an error, not a green run.** `-Filter SnapCoreTests`
  (no such class) used to print `Total: 0 | Failed: 0` and exit 0 — a typo that looked
  like a passing check.
- **One class per `-Filter`.** A `|` alternation through `build.cmd` is mangled by `cmd.exe`
  and the run dies with a bare `[FAIL]` and nothing to read. For several classes, either run
  them one at a time or just run the full suite.
- **Never pipe a run through `Select-Object -Last N`.** It cuts off the `Total:` line and the
  head of the failure list, which is exactly what you needed. Write the output to a file under
  `test-results/` and grep that.

## Only ONE Unity per machine, whatever the project

The licensing client is machine-wide. Start a second Unity — even from a different project, even
from a copy made for benchmarking — and the two fight over `LicenseClient-<user>`; the loser
loops forever:

```
[Licensing::Module] Error: The connection with the Unity Licensing Client has been lost.
[Licensing::IpcConnector] Channel LicenseClient-<user> doesn't exist
[Licensing::Module] Timed-out after 60.01s, waiting for channel: "LicenseClient-<user>"
```

A measured run sat in that loop for over 35 minutes. **The silence watchdog does not catch it**,
because the log keeps GROWING — a fresh batch of errors every 60 seconds looks exactly like
progress.

The gateway lock does not protect you here: it lives in `Library/` of one project, so two Unitys
from two projects never see each other's lock. Until that lock is machine-wide, treat "no second
Unity anywhere on this machine" as the rule, and check `Get-Process Unity` before starting one.

There is a second, unrelated hang: roughly 4% of batch runs finish all their work and then sit on
`Cleanup mono` — one measured case idled 2682 s after 6.8 s of work. Independent of run length or
`-Filter`. If a run goes quiet after the report is already on disk, it is this; the report is
valid and the process can be killed.


## «Только один разок, маленьким фильтром» — это тоже второй Unity

The rule «Unity runs belong to the coordinator» reads to a worker like a queueing convention, so
one of them ran `unity.ps1 tests -Filter PipeFitting` «just to check myself» while the coordinator
was running mutation checks. It survived — the gateway lock serialised them — but the lock lives
in one project's `Library/`, and the licensing client is MACHINE-wide: the very failure this rule
exists to prevent is invisible to that lock. A filtered run is not a smaller version of a run,
it is a second Unity.

So the wording in a task is «do not run Unity, not even a filtered one», and a worker that wants
a targeted run asks for it — naming the filter and what must be green. That costs one line in a
report and saves the 35-minute licensing loop that no watchdog catches, because the log keeps
growing while it fails.
