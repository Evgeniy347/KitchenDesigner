# Работа флотом: один runner, много воркеров

## Running a fleet: ONE runner, many workers

**Whenever you spawn subagents that touch this repository, run Unity yourself and let them run
only `dotnet`.** This is the working model; it was used to purge 2800 comments across fifteen
directories with six agents at once.

It is a working model, not a ritual. Its point is that one Unity runs at a time and that nobody
waits on a neighbour — where following the letter would cost more than it saves, follow the point.
The parts that ARE strict are named as such below: freeze during a run, one owner per guard file,
and never make a foreign fix that could change behaviour.

- **Workers run `dotnet` freely — and nothing else.** `.\tools\mutation-test.ps1 -TestsOnly`
  needs no permission: seconds, no `Library` lock, no
  licensing client. Everything else — EditMode, PlayMode, `build.cmd`, `tools\unity.ps1`, player
  builds, the mutation gate without `-TestsOnly`, the `[Explicit]` sets — goes through the
  coordinator.
- **Better still: the coordinator does not wait to be asked.** The working shape is not «worker
  asks, coordinator permits, worker runs» but «coordinator runs the full suite and hands the raw
  failure list to each worker for its own area». One queue to Unity instead of several: nobody
  waits on a neighbour, nobody fights over `Library`, and nobody reads a neighbour's red as their
  own. Collect a few finished pieces, run Unity once for all of them.
- **The coordinator runs the suite and hands back the raw failure list** — no diagnosis. Each
  worker reads its own failures. They are better at it than the coordinator: they know what they
  just changed, and several of them named their expected reds BEFORE the run and were right.
- **FREEZE.** From the moment a worker asks for a run until it gets the answer, it edits nothing.
  The run reads the DISK, not the worker's idea of it. Five times in one session a half-written
  file blocked every agent at once — the run died on compilation in ten seconds without reaching
  a single test.
- **Better than freezing: rewrite each file in ONE operation.** Then no intermediate state exists
  on disk at all, for anybody. A worker invented this after breaking the build twice; it cures
  the cause, while freezing only treats the symptom.
- **A worker cannot check that Unity-only code compiles.** The fast path builds `Core/Geometry`
  and `Core/Pure` only, so anything in `UI`, `Rendering`, `MCP`, `Diagnostics`, `Elements` reaches
  the compiler exclusively through the coordinator. Workers must cut their portions smaller and
  commit sooner than feels necessary — and after every rename, grep for BOTH the old and the new
  name before moving on.
- **Guard files have ONE owner — the coordinator.** Ratchets (`CommentRatchetTests`,
  `ToleranceLiteralTests`, `TestQualityRatchetTests`, …) are edited by nobody else; workers report
  numbers. Two agents editing one file produced a commit that does not build on its own.
- **A blocked build is fixed by whoever notices it, not by whoever owns it.** This part of the
  model is deliberately NOT strict: a one-line, obvious repair in someone else's file — a missing
  `!` under `-nullable`, a verbatim-string prefix, a wrong overload — you just make, and then tell
  the owner what you changed and why. Bouncing a two-character fix back through a message costs
  everyone minutes and teaches nobody anything. Three such fixes happened in one session and none
  of them caused an argument.
  The line to hold is not "whose file" but **"can this change behaviour"**. When the minimal fix
  WOULD change it — turning a loud throw into a silent no-op, widening a guard, picking one of two
  plausible meanings — hand it back even though others are waiting. That came up once: the owner
  solved it a third way, by changing the signature so the bad state became unrepresentable, and
  neither of the two "obvious" fixes would have been right.
- **Say what you expect to be red before the run** — and what result would mean you were wrong;
  the whole rule is «Say what you expect to be red» below. And relay foreign reds by name — a
  worker who cannot tell its own failures from a neighbour's will "fix" the neighbour's code.
- Спавня субагентов, передавай им уроки, оплаченные сегодня, а не только задачу: список в этом
  файле и в `conventions/*.md` существует ровно для того, чтобы каждый следующий агент не покупал их
  заново.

## Routine work goes to opencode, not to this session's context

A task that is mechanical, well understood, and whose result a COMMAND can check —
`opencode run -m opencode/big-pickle "<task>"` — is the cheap path, and reaching for it is
the DEFAULT, not a special case. `opencode/big-pickle` (and the other free models there) costs
nothing: no paid tokens, and it burns its own context instead of this session's. Two limited
resources are saved at once, and there is nothing to review afterwards that the check command
has not already answered.

So the question to ask before doing mechanical work here is not «could opencode do this?» but
«why am I doing it myself?». If the answer is only «it is quicker to type», you are spending the
scarce resource to save the free one. Reach for it OFTEN — the wrong reflex is doing a
twelve-file rename by hand while a free model idles.

```bash
opencode run -m opencode/big-pickle "In kd-repose, rename Foo to Bar everywhere in
Assets/Scripts and Assets/Tests, then run .\tools\mutation-test.ps1 -TestsOnly and report the
Total/Failed line. Do not commit."
```

What qualifies: a sweep across many files, a rename, boilerplate, regenerating an artefact,
mass-applying a pattern already agreed here. What does NOT: anything whose acceptance is a
judgement call — design, naming a new abstraction, deciding what a test should assert. Those
cost more to specify than to do.

Rules that make it work (they were three; the list grew as the free path was used):

- **The task text is self-contained.** opencode has not seen this conversation. Paths, the
  exact repo, what "done" looks like, and **the command that proves it** all go in the prompt.
  A task without a check command is not routine enough to delegate.
- **Never let it near Unity while you are.** opencode calls the same
  `tools/unity.ps1`, and the licensing client is machine-wide (see "Only ONE Unity per
  machine"). A second agent starting Unity behind your back is exactly what "the editor
  restarted for no reason" looks like. Say so in the prompt: *Unity runs are the
  coordinator's; use `tools/mutation-test.ps1 -TestsOnly`.*
- **This applies to subagents too, not only to the coordinator.** A worker with a mechanical
  sub-part of its own task delegates it the same way instead of grinding through it — a sweep
  inside a sweep is still a sweep. Same two conditions: self-contained text, a check command,
  and no Unity.
- **Watch it for liveness every ~10 minutes** (`AGENTS.md` → "Output discipline"). A free model
  can hang without exiting, so nothing notifies you. Compare the log's mtime; empty or unchanged
  for 10 minutes ⇒ kill and restart. Give it a hard budget in the prompt — "at most 25 shell
  commands, then print the report" — and have it print the report to stdout instead of writing a
  file: a model that spends its whole budget reading produces nothing at all.
- **opencode cannot read ABOVE `kd-repose`, and it exits 0 when it gives up.** Its permission
  covers the repository only. That used to hide the whole corpus from it: `AGENTS.md`,
  `agents/*.md` and `conventions/*.md` lived one level UP, outside the repo, and a run told to
  "read `AGENTS.md` first" auto-denied those reads and returned **exit 0 having changed
  nothing** — the cheapest possible lie, a green code over an empty commit. The corpus moved
  INSIDE the repository on 2026-09-08, so a pointer at a rule file now resolves; naming the
  exact file and section is worth more than naming the map. Three things outlive the move.
  Keep the whole task inside the repo — anything above `kd-repose/` is still unreadable and
  still fails silently. Inline the few lines the task actually turns on rather than making a
  free model read a 250-line file to find them. And judge the result by `git log --oneline` or
  a `grep` for what should have changed — never by the exit code.
- **Two failures on the same area end the free path — escalate to a Claude subagent.** Cost
  order still holds, but a model that has burned its budget twice on one area will burn it a
  third time. The free models handle a directory whose debt is visible locally — the same
  constant twice, two neighbouring classes copy-pasted. They fall apart where the answer needs
  the call graph held in mind: three separate runs died on `Core/Geometry`+`Core/Pure`
  (136 files), and one Sonnet subagent using `codegraph_*` closed it in a single pass. Judge the
  area by that, not by file count.

## Judging a MODEL: give the worker the picture, not a verdict

A worker cannot run Unity, so it cannot see what it built. Describing the render to it in words
and asking for a fix is how a whole round of edits goes into the bin: three times in one session
the defect was NOT where it looked.

- «the spout is invisible» → the iso camera stands on -Z, wall fittings seat their back face on
  z = 0 and grow into +Z. The camera was photographing the fitting from behind. The spout, the
  lever and the hand shower were all fine.
- «the rain head reads as a flat lozenge» → `TubeMesh` tessellated every tube with 16 segments.
  On the Ø32 riser that is 6 mm facets and invisible; on the Ø250 head it is 48 mm facets, and a
  disc cannot look round. Thickening the band — the literal reading of the note — would have
  produced a thicker faceted lozenge.
- «the close-up is aimed past the thermostat» → `CreateIsoCamera` clamps distance to 0,5 m (it
  must: without it a screw leg fills the frame). A 148 mm close-up silently became 414 mm.

So the loop is: the worker writes the iso frames, the COORDINATOR runs PlayMode and hands back
the PNG PATHS, and the worker opens them with Read and judges its own work. Rounds are cheap;
a round spent fixing the wrong thing is not. State what each frame must show BEFORE the run —
a frame named after a part must contain that part, and «I cannot see X» then separates
«X is badly modelled» from «X is not in shot».

Two habits that paid for themselves here. Pin the viewpoint with a test:
`IsoCamera_FacesTheWallSideOfAFitting` guards both the sign of the camera axis AND that the
back face sits on z = 0, so flipping either end goes red. And name a rendered file as the
quality bar — «read like `iso_toilet_360x790x660.png`» told the worker more than any adjective.

## Two workers must never edit the shared registries at once

Adding an element type touches ~15 registry files (factory, restorers, duplicators,
`ElementSelector`, `ElementTypeConverter`, `ElementFacets`, `MaterialSlot`, the MCP surface,
`ElementCapture`/`ElementData`, `ContextMenuUI`, the sidebar, the screenshot tests). Two agents
writing those at the same time lose work SILENTLY — last write wins and no test notices, because
each half compiles.

So when two new types are wanted at once, phase them: one agent takes the registries, the other
does everything that lives only in its own files (the element class, its geometry, its tests) and
**stops** at the first registry edit, saying so. The coordinator releases the registries when the
first lands. Do not let "run them in parallel" mean "edit the same file in parallel".

The same rule, smaller: **register a UI field editor in BOTH halves** — the `_editors` list AND
`Build()`. A stool once shipped with a row present in one half only, so the field never appeared
in the panel and nothing failed.

## Say what you expect to be red — and what would mean you were wrong

Naming the expected failures before the run costs a line and saves the coordinator a diagnosis.
Go one step further and name the result that would DISPROVE you: "if this test stays green, my
rewrite was not enough and I will say so", "if these three go red too, the break touched more than
I aimed at". Agents that did this were right about eight reds out of eight, and the one who wrote
"if it is green, that is a finding, not luck" was the one who caught his own broken bench.

A result that is only interpreted after the fact can always be read as success. State the
interpretation first, then run.

## When several agents share the tree

- `Total:` stops being a constant — others are adding tests. Judge by `Failed: 0`, and sort the
  failure list by directory: what falls outside your own is not yours. Re-run once, then report
  it and leave it alone.
- Compilation is shared. Never step away for a test run with a half-written file on disk; the
  next agent to build will collect your errors and waste a cycle on them.
- **The identity is shared too — commit provenance cannot be established from git.** Every commit
  in this repository carries the same author, because `git-commit.ps1` uses the identity configured
  in the repo and agents have none of their own. `e85cd8b5` (an agent, refactoring Snap) and
  `274e517c` (a snapshot of the user's own working file) are indistinguishable by author, and the
  timestamps do not separate them either — a daytime stamp is exactly what the helper produces when
  the evening window is exhausted. So "I checked the author" is NOT a check. Only the message and
  the contents distinguish us; if you need to know who made a commit, ask the user.
- **The git index is shared too.** Leaving anything in `git add` between your own commits means
  the next agent's `git-commit.ps1` sweeps it into an unrelated commit — this has already
  happened once with a staged test rename. Stage nothing you are not committing right now.
  The cause was in the helper, not only in the discipline: `-Files` staged those paths but the
  commit that followed took the WHOLE index. Since `779cec4b` it commits with a pathspec, so
  `-Files` now means what it says and a neighbour's staged file stays staged. Discipline still
  matters — `-All` sweeps everything by design — but a second agent's work no longer lands in
  your commit by accident. If you ever see files you did not touch in your own `git show --stat`,
  that is this failure returning, not a mystery.
  Обратная сторона того же: **твой файл может уехать в чужой коммит**, пока ты пишешь сообщение.
  Ищи свои строки в `git log -p -- <файл>`, а не в `git diff`, и не переделывай работу, которую
  сосед уже закоммитил под своим именем — содержимое на месте, чужое только авторство.
- Commit small and often: the window in which neighbours can see your intermediate state is
  exactly the gap between your commits.
- **Every `.ps1` that contains Cyrillic is UTF-8 WITH BOM** — not just `tools/unity.ps1`.
  Windows PowerShell reads a BOM-less file as ANSI. In comments the damage is invisible; inside
  a string literal it breaks the quoting and the script falls apart in ways that look like logic
  errors. This bit a throwaway benchmark script written during this campaign, by an agent who
  had already read the rule about `unity.ps1` and did not generalise it. Applies to scratch
  scripts too — those are exactly the ones written without care.


## A worker's abandoned background process is nobody's job to notice

The liveness rule above watches the free model, because a hung `opencode` never exits and so never
reports. There is a quieter version of the same waste: a subagent starts something in the
background, finds the answer another way, and walks off. One did — `find / -maxdepth 6`, looking
for a package it then located directly in `Library/PackageCache`; the disk walk ran for three
hours after its task had finished and been committed. Nothing was broken, nothing was reported,
and it surfaced only because the USER noticed a stale entry in the task list.

So: a worker that backgrounds a command owns it until it ends — kill it when the answer arrives
from elsewhere, and say in the report that you did. And when you start a broad filesystem search
on Windows, reach for the narrow path first: `Library/PackageCache` and the package manifest
answer «which version of this package is here» in milliseconds, while `find /` answers it in
hours.

## Removing a member: your files and its callers are two different sets

A worker's boundaries are written in terms of files it may EDIT. The callers of a member it
deletes live outside those boundaries by definition, and it will not look there. Two agents
crossed this way in one session: thirty `is*` flags left `SidebarCatalog`, and a test in another
agent's territory still read three of them — the build stopped for everyone until someone
noticed.

The compiler caught that one. It would not have caught a member read through reflection, a name
in a serialized asset, or a string in a guide text — and those are exactly where this project
keeps its cross-checks. So: **before deleting a public or internal member, grep the WHOLE tree
for it**, including files you are forbidden to edit, and put what you found in the report. You
are not being asked to fix the neighbour's file; you are being asked not to leave a hole nobody
knows about.

## Never `--amend` or `reset` in a shared tree

Two incidents in one session, same root. A worker ran `git-commit.ps1 -Amend` with `-Files` and
the helper committed the WHOLE index anyway, sweeping in a neighbour's unfinished files — the
`-Amend` branch had no pathspec, the gap that `779cec4b` had already closed for the normal path.
Another worker went further: `git reset HEAD~1` plus `commit --amend` on the shared index moved a
NEIGHBOUR'S COMMIT off the branch entirely. Its changes survived in the working tree and were
re-committed, but only because the owner noticed.

The helper now passes a pathspec on `-Amend` too, and warns when `-Amend` is used without
`-Files`. The discipline stands regardless: **in a shared tree you only ever add commits.**
Rewriting history — `--amend`, `reset`, `rebase` — is the coordinator's, and even then not while
others are working. A commit that is wrong gets a second commit on top; that is what history is
for. If you think you need `--amend`, you need a new commit.
