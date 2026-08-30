<#
.SYNOPSIS
    Re-times every commit of a branch so the history satisfies the repository's timestamp
    rules (AGENTS.md -> "CRITICAL: Commit timestamps").

.DESCRIPTION
    Walks the commit DAG parents-first and assigns each commit a new author/committer time:

      * weekday (Mon-Fri) commits land in 19:00:00-23:59:59; weekends are unrestricted
        (08:00-23:59 is used as the working window so weekend commits do not sit at 03:00)
      * every commit is strictly later than all of its parents
      * the gap to the previous commit is random, 1-10 min, shrunk automatically when a day
        holds too many commits to fit the window
      * seconds are always 01-59, so round times like 19:00:00 never appear
      * no stamp is ever later than -NotAfter (the real clock by default); when a day's
        evening window has not happened yet, the day spills into a window starting at
        midnight instead of being pushed into the future

    Each commit keeps its original DATE. The date only moves forward when the original date
    is earlier than a parent's - ordering wins over date preservation.

    The rewrite changes every hash in the selected range. Make a backup branch first.
    -Since narrows the rewrite to <ref>..<branch>, which is what keeps already-pushed
    commits (and their hashes) untouched.

.EXAMPLE
    .\tools\git-fix-commit-times.ps1 -DryRun

.EXAMPLE
    .\tools\git-fix-commit-times.ps1 -Branch HEAD

.EXAMPLE
    .\tools\git-fix-commit-times.ps1 -Since origin/develop -Branch HEAD -DryRun
#>
[CmdletBinding()]
param(
    [string] $Branch = 'HEAD',

    # Rewrite only <ref>..<branch>. The commits at and below <ref> keep their timestamps and
    # act as the base, so already-pushed history is left untouched.
    [string] $Since,

    # No stamp may be later than this moment. Defaults to the real clock, which is what stops
    # the rewrite from re-creating the future timestamps it was called to remove.
    [Nullable[datetime]] $NotAfter,

    # Report the planned timestamps without touching the repository.
    [switch] $DryRun,

    # Skip the interactive confirmation before rewriting.
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Offset = '+0500'
$WeekdayStart = 19 * 3600            # 19:00:00
$WeekdayEnd = 24 * 3600 - 1          # 23:59:59
$WeekendStart = 8 * 3600             # 08:00:00
$WeekendEnd = 24 * 3600 - 1          # 23:59:59
if ($null -eq $NotAfter) { $NotAfter = Get-Date }

$repoRoot = (git rev-parse --show-toplevel 2>$null)
if (-not $repoRoot) { throw 'Not inside a git repository.' }
Set-Location $repoRoot

# filter-branch updates the refs named in its rev-list arguments. A literal HEAD would
# update whichever worktree it runs in, so resolve it to the branch name once, up front.
if ($Branch -eq 'HEAD') {
    $named = (git symbolic-ref --quiet --short HEAD 2>$null)
    if ($named) { $Branch = $named }
}
$Range = if ($Since) { "$Since..$Branch" } else { $Branch }

function Test-Weekend([datetime] $t) {
    $t.DayOfWeek -eq [DayOfWeek]::Saturday -or $t.DayOfWeek -eq [DayOfWeek]::Sunday
}

# The day's window, never reaching past the real clock. When the evening window of the
# current day has not happened yet, the day spills to a window that starts at midnight:
# a daytime stamp breaks the cosmetic 19:00 rule, a future stamp breaks a fact.
function Get-WindowEnd([datetime] $day) {
    $end = if (Test-Weekend $day) { $day.Date.AddSeconds($WeekendEnd) } else { $day.Date.AddSeconds($WeekdayEnd) }
    if ($end -gt $NotAfter) { return $NotAfter }
    return $end
}

function Get-WindowStart([datetime] $day) {
    $start = if (Test-Weekend $day) { $day.Date.AddSeconds($WeekendStart) } else { $day.Date.AddSeconds($WeekdayStart) }
    if ($start -gt (Get-WindowEnd $day)) { return $day.Date.AddSeconds(1) }
    return $start
}

function Set-NonRoundSeconds([datetime] $t) {
    if ($t.Second -eq 0) { return $t.AddSeconds((Get-Random -Minimum 1 -Maximum 60)) }
    return $t
}

# ---------------------------------------------------------------------------------------
# Read the DAG
# ---------------------------------------------------------------------------------------

$order = @(git rev-list --reverse --topo-order $Range)
if ($order.Count -eq 0) { throw "No commits selected by $Range." }

$parents = @{}
$origDate = @{}
foreach ($line in git rev-list $Range --pretty=format:'%H%x09%P%x09%ad' --date=format:'%Y-%m-%d') {
    if ($line -like 'commit *') { continue }
    $f = $line -split "`t"
    $parents[$f[0]] = if ($f[1]) { $f[1].Trim() -split '\s+' } else { @() }
    $origDate[$f[0]] = [datetime]::ParseExact($f[2], 'yyyy-MM-dd', $null)
}

# How many commits share each original date - drives the gap budget so a busy day still
# fits inside its window.
$perDay = @{}
foreach ($sha in $order) {
    $key = $origDate[$sha].ToString('yyyy-MM-dd')
    if ($perDay.ContainsKey($key)) { $perDay[$key]++ } else { $perDay[$key] = 1 }
}

$maxGapForDay = @{}
foreach ($key in $perDay.Keys) {
    $day = [datetime]::ParseExact($key, 'yyyy-MM-dd', $null)
    $span = ((Get-WindowEnd $day) - (Get-WindowStart $day)).TotalSeconds
    $budget = [int][math]::Floor($span / ($perDay[$key] + 1))
    $maxGapForDay[$key] = [math]::Max(20, [math]::Min(600, $budget))
}

# ---------------------------------------------------------------------------------------
# Assign timestamps
# ---------------------------------------------------------------------------------------

$assigned = @{}
$dateMoved = 0

# A parent outside the selected range keeps its real timestamp and becomes the base the
# first rewritten commit has to beat.
foreach ($sha in $order) {
    foreach ($p in $parents[$sha]) {
        if ($origDate.ContainsKey($p) -or $assigned.ContainsKey($p)) { continue }
        $epoch = (git log -1 --format=%at $p 2>$null)
        if (-not $epoch) { continue }
        $assigned[$p] = [DateTimeOffset]::FromUnixTimeSeconds([int64] $epoch).ToOffset([TimeSpan]::FromHours(5)).DateTime
    }
}

foreach ($sha in $order) {
    $day = $origDate[$sha]
    $key = $day.ToString('yyyy-MM-dd')
    $maxGap = $maxGapForDay[$key]
    $minGap = [math]::Max(1, [math]::Min(60, $maxGap - 1))

    $base = $null
    foreach ($p in $parents[$sha]) {
        if ($assigned.ContainsKey($p) -and (($null -eq $base) -or ($assigned[$p] -gt $base))) {
            $base = $assigned[$p]
        }
    }

    if ($null -eq $base) {
        # Root commit: open its day's window.
        $t = (Get-WindowStart $day).AddSeconds((Get-Random -Minimum 1 -Maximum ($maxGap + 1)))
    } elseif ($base.Date -lt $day.Date) {
        # First commit of a later day: restart at that day's window.
        $t = (Get-WindowStart $day).AddSeconds((Get-Random -Minimum 1 -Maximum ($maxGap + 1)))
    } else {
        $t = $base.AddSeconds((Get-Random -Minimum $minGap -Maximum ($maxGap + 1)))
        # Outside the window (past midnight, or a weekday morning) -> next day's window.
        for ($guard = 0; $guard -lt 30; $guard++) {
            $lo = Get-WindowStart $t
            $hi = Get-WindowEnd $t
            if ($t -ge $lo -and $t -le $hi) { break }
            if ($t -lt $lo) { $t = $lo.AddSeconds((Get-Random -Minimum 1 -Maximum 900)) }
            else { $t = (Get-WindowStart $t.Date.AddDays(1)).AddSeconds((Get-Random -Minimum 1 -Maximum 900)) }
        }
    }

    $t = Set-NonRoundSeconds $t
    if ($null -ne $base -and $t -le $base) { $t = Set-NonRoundSeconds $base.AddSeconds($minGap) }
    if ($t.Date -ne $day.Date) { $dateMoved++ }

    $assigned[$sha] = $t
}

# ---------------------------------------------------------------------------------------
# Verify the plan before touching anything
# ---------------------------------------------------------------------------------------

$problems = @()
foreach ($sha in $order) {
    $t = $assigned[$sha]
    if ($t.Second -eq 0) { $problems += "$($sha.Substring(0,8)): round seconds $t" }
    if ($t -gt $NotAfter) { $problems += "$($sha.Substring(0,8)): $t is later than the clock ($NotAfter)" }
    if ($t -lt (Get-WindowStart $t) -or $t -gt (Get-WindowEnd $t)) {
        $problems += "$($sha.Substring(0,8)): $t outside its day's window"
    }
    foreach ($p in $parents[$sha]) {
        if ($assigned.ContainsKey($p) -and $assigned[$p] -ge $t) {
            $problems += "$($sha.Substring(0,8)): $t not after parent $($p.Substring(0,8)) $($assigned[$p])"
        }
    }
}

Write-Host "commits: $($order.Count); dates moved forward: $dateMoved; rule violations: $($problems.Count)"
if ($problems.Count -gt 0) {
    $problems | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
    throw 'Planned timestamps violate the rules - aborting.'
}

if ($DryRun) {
    foreach ($sha in $order) {
        '{0} {1:yyyy-MM-dd HH:mm:ss ddd} <- {2:yyyy-MM-dd}' -f $sha.Substring(0, 8), $assigned[$sha], $origDate[$sha]
    }
    Write-Host 'dry run - nothing rewritten.'
    return
}

# ---------------------------------------------------------------------------------------
# Rewrite
# ---------------------------------------------------------------------------------------

if (-not $Force) {
    $answer = Read-Host "Rewrite $($order.Count) commits on $Branch? every hash changes [y/N]"
    if ($answer -ne 'y') { Write-Host 'aborted.'; return }
}

$mapPath = Join-Path ([System.IO.Path]::GetTempPath()) 'git-time-map.txt'
$sb = [System.Text.StringBuilder]::new()
foreach ($sha in $order) {
    [void] $sb.Append($sha).Append(' ').Append($assigned[$sha].ToString('yyyy-MM-ddTHH:mm:ss')).Append(' ').Append($Offset).Append("`n")
}
[System.IO.File]::WriteAllText($mapPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

$mapUnix = ($mapPath -replace '\\', '/') -replace '^([A-Za-z]):', '/$1'
$envFilter = @"
d=`$(grep -m1 "^`$GIT_COMMIT " '$mapUnix' | cut -d' ' -f2-)
if [ -n "`$d" ]; then
    export GIT_AUTHOR_DATE="`$d"
    export GIT_COMMITTER_DATE="`$d"
fi
"@

$env:FILTER_BRANCH_SQUELCH_WARNING = '1'

# filter-branch refuses to run while the working tree is dirty, and this repository is
# permanently dirty by design (docs/example.save.json is the user's live project and must
# never be reverted or stashed). A throw-away detached worktree gives filter-branch the
# clean tree it insists on; refs and objects are shared, so the rewrite still lands on the
# real branch, and the main worktree keeps its uncommitted file.
$dirty = @(git status --porcelain)
$workRoot = $repoRoot
$scratch = $null
if ($dirty.Count -gt 0) {
    $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ("kd-retime-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
    Write-Host "working tree is dirty - rewriting from a scratch worktree: $scratch"
    git worktree add --detach $scratch $Branch | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'git worktree add failed.' }
    $workRoot = $scratch
}

try {
    Push-Location $workRoot
    git filter-branch -f --env-filter $envFilter --tag-name-filter cat -- $Range
    $filterExit = $LASTEXITCODE
    Pop-Location
    if ($filterExit -ne 0) { throw 'git filter-branch failed.' }
} finally {
    if ($scratch) {
        git worktree remove --force $scratch 2>&1 | Out-Null
        git worktree prune | Out-Null
    }
}

Write-Host ''
git log -15 --pretty=format:'%h %ad %s' --date=format:'%Y-%m-%d %H:%M:%S %a'
Write-Host ''
