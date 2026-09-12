# Git: коммиты, push, временные файлы

- Repo root: `F:\repos\KitchenDesigner2\kd-repose\`
- No junk files, one logical change per commit
- **File deletions**: NEVER `rm -rf` / `Remove-Item -Force`. See CONVENTIONS.md → "CRITICAL: File operations"
- **NEVER revert a user's working-tree file** — `git checkout -- <path>`, `git restore`,
  `git stash` and friends destroy uncommitted work with no undo. This applies especially to
  `docs/example.save.json` (the whole rule is in `agents/TESTS.md` → «`docs/example.save.json` —
  NEVER TOUCH IT»), and to anything else the user is editing. If a test
  needs a different input, give the TEST a frozen copy; never rewind the user's file.

## Commit message

English, format `<type>: <short desc>`. Types: `feat`, `fix`, `chore`, `test`, `refactor`

## CRITICAL: Push — NEVER push without explicit user request

**Do NOT push unless the user explicitly asks.** Commits stay local until the user says «запушь», «push», or similar. When user does ask: push ONLY weekdays 19:00–23:59 (Mon–Fri). Weekends — no restrictions. If time is outside window — tell user, explain the rule, ask explicit confirmation.

## Pre-commit checklist (MUST follow, in order)

1. **Edit files** (one logical change)
2. **Commit** — the timestamp is not yours to build: never call bare `git commit`, and never set
   `GIT_AUTHOR_DATE` / `GIT_COMMITTER_DATE` by hand.
3. **`git status --short | grep '^??'` — и добавь `.meta` каждого нового файла.** За одну сессию
   девяти агентов потерялось **двадцать пять** `.meta`; каждый из девяти считал, что закоммитил
   свою работу целиком. Unity перегенерирует GUID на чистом клоне, и любая сериализованная
   ссылка на такой файл рвётся — а заметит это не автор, а следующий, у кого «почему-то отвалилась
   сцена». Правило простое: `.cs` и `.cs.meta` едут одним коммитом, всегда.

## CRITICAL: never `git stash` — the tree is shared

Several agents edit the SAME working tree at the same time. `git stash` takes everyone's
uncommitted work, not yours: on 2026-09-10 one agent stashed to look at an unrelated red
test and silently reverted files two other agents were writing at that moment. It restored
them by hand afterwards and believed nothing was lost — but «believed» is the whole problem,
because a stash pop over files that moved on disk in the meantime cannot be verified by the
one who did it.

The same goes for anything else that rewrites the tree wholesale: `git checkout -- .`,
`git reset --hard`, `git clean`, `git restore` without a path. If you need a clean state to
compare against, read the committed version instead — `git show HEAD:path/file` writes it
anywhere you like without touching the tree.

And a red test in a file that is not yours is not yours to diagnose: say so in the report and
move on. The manager knows who owns what.

## Temp files

- `tmp-scripts/` — scripts
- `test-results/` — test output

