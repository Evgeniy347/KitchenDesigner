# Прогон тестов

## Build & run scripts

All in ``, Windows `.cmd`:

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
| `dotnet` (core + pure) | `.\tools\mutation-test.ps1 -TestsOnly` | 380 | **0,3 s** |
| EditMode | `build.cmd -RunTests` | 4237 | **~130 s** |
| PlayMode | `build.cmd -RunPlayMode` | 207 | **~180 s** |

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
> Under three minutes for the pair is normal — use a `-Filter` while iterating on one class.

## Four things the normal run does NOT include — ASK before running them

All four are `[Explicit]`, so NUnit skips them unless a filter names them. Each was
measured, not guessed, and each is excluded for the same reason: it dominated the
edit→check cycle while answering a question nobody was asking at that moment.

| What | Cost | Run it with |
|------|------|-------------|
| `SnapMutationTests` — brute-force sweep over every face pair | **79 s** (was 69% of all EditMode) | `unity.ps1 tests -Platform EditMode -Filter SnapMutationTests` |
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
plus an amend, keeping a copy of the working-tree version aside first. **Use `-Files`**, or
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

Two cheap checks that catch the swap before anyone reads it: the root's `testcasecount` (4334 for
EditMode, 213 for PlayMode right now) and `failed` — a copy claiming zero failures after a run
that printed twenty is the wrong file, every time.


**And `SaveValidationTests` can be red because the SCENE is wrong, not the code.** It demands
zero findings on the live file, so the moment the user's own project genuinely contains one —
a leg sunk into a carcass, an end only partly covered for edge banding — the test goes red and
stays red until they fix their kitchen. That is the test doing its job, not a regression.

Read the finding before diagnosing anything: the code says which rule fired and between which
parts. If it names elements the current change never touched, it is the scene. Do not suppress
it, do not edit the test to pass, and do not «fix» the file — say which finding appeared and
leave it to the owner. Twice this file has been red BY DESIGN: once after a rule was deliberately
added at the user's request so that their mistake would show up, and once when they moved a part
while the suite was running.

## Iterating on ONE test class

Use the gateway with a filter — **~11 s against ~73 s** for the whole suite. Filtering now
pays for itself: the fixed start is ~10 s, and a filtered run additionally turns Burst
compilation off (worth 2,3 s, and only there — on a full run it buys exactly nothing).
Still run the FULL suite before committing.

```powershell
.\tools\unity.ps1 tests -Platform EditMode -Filter "MyTests"
```

A filter that matches NOTHING is now an error, not a green run: `-Filter SnapCoreTests`
(no such class) used to print `Total: 0 | Failed: 0` and exit 0 — a typo that looked like
a passing check.

It prints the summary and the first failures itself; the full report stays in
`test-results/tmp/TestResults.xml` (parse with `[xml]`: `//test-case`,
`.failure.message.'#cdata-section'`).

Faster still: if the code under test lives in `Assets/Scripts/Core/Geometry/` or
`Assets/Scripts/Core/Pure/`, run it outside Unity — 354 tests in 0.25 s:

```powershell
.\tools\mutation-test.ps1 -TestsOnly
```
**Do not use `python`** — in this environment it is the Windows Store stub and silently
prints nothing.

