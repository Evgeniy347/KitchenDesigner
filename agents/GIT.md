# Git: коммиты, таймстемпы, временные файлы

- Repo root: `F:\repos\KitchenDesigner2\kd-repose\`
- No junk files, one logical change per commit
- **File deletions**: NEVER `rm -rf` / `Remove-Item -Force`. See CONVENTIONS.md → "CRITICAL: File operations"
- **NEVER revert a user's working-tree file** — `git checkout -- <path>`, `git restore`,
  `git stash` and friends destroy uncommitted work with no undo. This applies especially to
  `docs/example.save.json` (see below), and to anything else the user is editing. If a test
  needs a different input, give the TEST a frozen copy; never rewind the user's file.

## Commit message

English, format `<type>: <short desc>`. Types: `feat`, `fix`, `chore`, `test`, `refactor`

## CRITICAL: Commit timestamps

**NEVER build the timestamp by hand and NEVER call bare `git commit`.** Use the helper —
it applies every rule below and cannot produce a round or out-of-order timestamp:

A multi-line body works, but ONLY through a single-quoted here-string. A plain double-quoted
string with newlines is re-parsed by PowerShell and its words arrive as extra arguments, which
land in `-Files` and fail as `pathspec 'if' did not match any file(s)`. The closing `'@` must
sit at column 0:

```powershell
.\tools\git-commit.ps1 -Message @'
refactor: short subject line

Body paragraph, free to span lines and contain (parentheses), "quotes" and $signs.
'@ -Files Assets/Scripts/Core/Foo.cs, Assets/Scripts/Core/Foo.cs.meta
```

```powershell
.\tools\git-commit.ps1 -Message "feat: something" -Files Assets/Scripts/Core/Foo.cs, Assets/Scripts/Core/Foo.cs.meta
# -All            stage every tracked change instead of a file list
# -DryRun         print the timestamp it would use and stop
# -Amend          re-time the current HEAD commit (NOT in a shared tree — `agents/FLEET.md`
#                 → «Never `--amend` or `reset` in a shared tree»; a wrong commit gets a
#                 second commit on top, and history is rewritten by the coordinator alone)
# -Now <datetime> override "now" in the anchor calculation (scenario testing)
```

The rules it enforces (also the acceptance criteria for any history rewrite):

These rules CONFLICT when a day's window is exhausted, so their priority is explicit:

**strictly ascending  >  never later than the real clock  >  the evening window.**

When the window has no room left, it is the WINDOW that yields: the stamp is placed in
`(previous, now]` with a warning. A daytime stamp is cosmetically wrong; a future stamp is
factually wrong. `git log --date=relative` showing "in the future" is always a defect, never
intent — it happened once when Sunday filled up to 23:59:57 and the next commit, landing early
Monday, was normalised FORWARD to 19:00 that evening, taking 39 commits with it.

1. **Allowed window** — weekday (Mon–Fri): `19:00:00`–`23:59:59` only. Weekend (Sat, Sun): any
   time of day.
2. **Strictly ascending** — every commit is later than *all* of its parents. Never equal.
3. **Gap 1–10 min** — the offset from the previous commit is random in `[60, 600]` seconds,
   never a fixed step.
4. **No round times** — seconds are always `01`–`59`, so `19:00:00`, `20:15:00`, `21:00:00`
   and the like must never appear. A timestamp ending in `:00` is a bug.
5. **Overflow rolls forward** — if `previous + gap` lands before 19:00 on a weekday, it moves
   to `19:00` + random offset of that same day; past midnight it simply lands on the next day
   and is normalised there.
6. **Anchor to the real clock** — on a weekend, or on a weekday at/after 19:00, the stamp is the
   current moment (TODAY). On a weekday during the day (before 19:00) the stamp lands in
   YESTERDAY's evening session and the day is never later than yesterday. Only when history has
   already run past that anchor does the stamp roll forward (strict ordering outranks the anchor).

## Rewriting timestamps across history

`tools/git-fix-commit-times.ps1` re-times an entire branch under the same rules, walking the
DAG parents-first. It keeps each commit's original **date** and only replaces the **time**;
the date moves forward only when a commit's original date is earlier than a parent's (rule 2
wins over date preservation). Always `git branch backup/<name>` first — it rewrites hashes.

```powershell
.\tools\git-fix-commit-times.ps1 -Branch HEAD -DryRun   # report only
.\tools\git-fix-commit-times.ps1 -Branch HEAD           # rewrite
```

## CRITICAL: Push — NEVER push without explicit user request

**Do NOT push unless the user explicitly asks.** Commits stay local until the user says «запушь», «push», or similar. When user does ask: push ONLY weekdays 19:00–23:59 (Mon–Fri). Weekends — no restrictions. If time is outside window — tell user, explain the rule, ask explicit confirmation.

## Pre-commit checklist (MUST follow, in order)

1. **Edit files** (one logical change)
2. **Commit** via `.\tools\git-commit.ps1 -Message "<type>: <desc>" -Files <paths>` — it picks
   the timestamp itself; do not set `GIT_AUTHOR_DATE` / `GIT_COMMITTER_DATE` manually

## Temp files

- `tmp-scripts/` — scripts
- `test-results/` — test output

