# Деплой, релиз, кодировки

## There is nothing to deploy from this branch

`develop` is the desktop branch. There is no `server/`, no ASP.NET host, no Docker image and no
deploy script here; a host and a URL an agent remembers from somewhere else are not served from
this tree, and a "deploy step" described from here is always wrong.

The MCP surface is part of that: the app speaks MCP itself on `http://127.0.0.1:9337/mcp`,
loopback only, no authentication, and there is no generator that emits the contract from a
schema. So a change to the MCP contract is **self-contained** — edit the handler and the guide
texts (`Core/MCP/Contract/McpGuideTexts.cs`) and you are done; nothing has to be regenerated,
re-validated elsewhere or redeployed. If a remote validator ever silently STRIPS unknown fields,
that validator is not on this branch and not this branch's problem to keep in sync.

## Desktop release (installer → GitHub)

- Пользователь просит выложить релиз/инсталлятор — ОБЯЗАТЕЛЬНО прочитай **installer/PUBLISH.md** и делай по нему.

## `.cmd` files: ASCII and CRLF, no exceptions

`cmd.exe` seeks inside a batch file by BYTE OFFSET. Cyrillic breaks the parse under
`chcp 65001` — which is exactly what a PowerShell host gives you — and the interpreter starts
executing the tails of its own comments (`'he' is not recognized`, `do was unexpected at this
time`). A bare LF breaks `goto` and glues lines together. Together they made
`build-installer.cmd` unable to build the installer at all from that shell.

Russian text belongs in the `.ps1` helpers, never in a `.cmd`. After touching any `*.cmd`,
verify both properties:

```powershell
([regex]::Matches([IO.File]::ReadAllText($p), "(?<!`r)`n")).Count   # must be 0
```
```bash
grep -P "[^\x00-\x7F]" file.cmd    # must be empty
```

`sed -i` from git-bash silently rewrites CRLF as LF — re-check after using it.

**And that goes for every CRLF/BOM file, not just `.cmd`.** One `sed -i` on a `.csproj` ate the
BOM and rewrote the whole file as LF, turning a one-line edit into a 53-line diff. After any
`sed -i`, check `git diff --stat`: if more lines changed than you edited, the encoding went with
them — restore it.
