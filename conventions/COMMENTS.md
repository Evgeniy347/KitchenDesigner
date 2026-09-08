# Комментарии живут в тестах

## Comments live in tests

Production source (`Assets/Scripts/**`, `Assets/Editor/**`, and any non-Unity project under
`geometry/` — `server/` and `mcp-server/` are gone from this branch)
contains NO comments. Not `//`, not `///`, not `/* */`.

**A ratchet's ceiling drops in the same commit as the cleanup — unless someone else owns the
ratchet.** When several agents share the tree, the guard files (`CommentRatchetTests`,
`ToleranceLiteralTests`, `TestQualityRatchetTests`, …) belong to ONE owner, and everyone else
reports numbers instead of editing them. The rule's purpose survives the change — a ceiling must
never outlive the debt it describes — but the synchronisation moves to the owner, and a red
"shrank" between commits is process, not breakage. Say so in the commit message.

The reason is not tidiness: two agents editing one file produced a commit that does not build on
its own, because each staged the whole file and took the other's half-finished half with it.

And take the numbers FROM THE GUARD, by running it — never by counting on the side. While agents
are working, an outside count is stale within minutes: a prediction that a ceiling "will be 14"
was already wrong by the time it was written, because a second name had been fixed in parallel
and the true value was 13.

Forbidden, without exception:

- XML doc (`/// <summary>`) on ANY member, public included
- comments on private fields, methods and constants
- separator comments (`// ── Кромки ──────────`)
- comments restating what the code does
- commented-out code
- TODO / FIXME / HACK — open a task, do not leave a note

Not comments, therefore allowed: `#pragma`, `#if`/`#endif`, `[NotUndoable("reason")]` and
other attribute strings, tool directives (`// ReSharper disable` and the like).

**Count them with a pattern that catches TRAILING comments.** An anchored `^\s*(//|///|/\*)`
does not see `int x = 5; // why`, and six such comments survived two separate purge passes
because of it. Use `Select-String -Pattern '(?<!:)//|/\*' -AllMatches`; the lookbehind keeps
`http://` inside string literals out of the count.

Where the explanation goes instead:

1. **Rename.** If a comment explains WHAT a method, field or constant is, the name is
   wrong. `EdgeRefreshFrames = 15` plus a comment about 60 Hz becomes
   `EdgeRecomputeEveryNFrames`. Extract the expression into a named local or a named
   constant until the line reads as the sentence the comment used to be.
2. **Test.** If a comment explains WHY — a bug it guards against, a measurement, a
   platform constraint — that reason belongs in a test that goes red when the reason is
   violated. The test NAME carries the behaviour, the `Assert` message carries the why.

### Deleting a comment is not free

Before removing any comment, one of these MUST be true, and the commit message states
which one:

- **(a)** a test already exists that fails if the described behaviour breaks;
- **(b)** you wrote that test in the same commit;
- **(c)** you renamed the code so the comment became a restatement of the name.

- **(d)** the comment went away together with the code it explained, because the behaviour
  moved to another class. Name the destination and the test that catches it there — do not
  stretch this case to fit (a).

Case (c) counts only when the new name carries the part of the comment that explained WHY. A
comment that stated both WHAT and WHY splits: the WHAT is absorbed by the name, the WHY still
needs (b). `FormatEdges` → `FormatBandedEdgesRecomputedFromScene` absorbed "these are computed,
not stored", but "therefore they answer for the CURRENT scene" still needed a test.

Never delete a `why` comment without (a), (b) or (c). Knowledge like «PointerExit по старой
полосе не придёт» or «O(n²) … давал на 259 элементах хич» was paid for with real debugging
sessions; it must survive the deletion as an executable test, not evaporate.

