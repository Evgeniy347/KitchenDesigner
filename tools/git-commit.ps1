<#
.SYNOPSIS
    Commits with a timestamp that satisfies the repository's timestamp rules.

.DESCRIPTION
    See AGENTS.md -> "CRITICAL: Commit timestamps". In short:
      * the stamp is anchored to the real moment: a weekend or a weekday at/after
        19:00 stamps TODAY with the current time; a weekday daytime run stamps
        YESTERDAY's evening session (the day is never later than yesterday)
      * if history already runs past that anchor, strict ordering wins and the
        stamp rolls forward from the previous commit instead
      * weekday (Mon-Fri) stamps live in 19:00:00-23:59:59; weekends are unrestricted
      * the time is a random 60-600 s after the previous commit, never a fixed step
      * seconds are always 01-59, so round times like 19:00:00 can never be produced
      * the result is always strictly later than HEAD

.EXAMPLE
    .\tools\git-commit.ps1 -Message "feat: add drawer sync" -Files Assets/Scripts/Core/Drawer.cs

.EXAMPLE
    .\tools\git-commit.ps1 -Message "chore: bump snapshots" -All -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Message,

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]] $Files,

    # Stage every tracked modification instead of an explicit file list.
    [switch] $All,

    # Print the timestamp that would be used and exit without committing.
    [switch] $DryRun,

    # Re-time the existing HEAD commit instead of creating a new one.
    [switch] $Amend,

    # Overrides "now" in the anchor calculation (scenario testing with -DryRun).
    [Nullable[datetime]] $Now
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Timezone the whole history is written in.
$Offset = '+0500'

$repoRoot = (git rev-parse --show-toplevel 2>$null)
if (-not $repoRoot) { throw 'Not inside a git repository.' }
Set-Location $repoRoot

# --------------------------------------------------------------------------------------
# Timestamp rules
# --------------------------------------------------------------------------------------

function Test-Weekend([datetime] $t) {
    $t.DayOfWeek -eq [DayOfWeek]::Saturday -or $t.DayOfWeek -eq [DayOfWeek]::Sunday
}

# Pulls a moment into the allowed window, rolling forward day by day if needed.
# Weekends pass through untouched; on a weekday anything before 19:00 jumps to
# 19:00 + a random offset of up to 40 minutes on that same day.
function Get-AllowedTime([datetime] $t) {
    for ($guard = 0; $guard -lt 14; $guard++) {
        if (Test-Weekend $t) { return $t }
        if ($t.Hour -ge 19) { return $t }
        return $t.Date.AddHours(19).AddSeconds((Get-Random -Minimum 7 -Maximum 2400))
    }
    throw "Could not normalise $t into the allowed window."
}

# Never leave a round timestamp behind: seconds are forced into 01..59.
function Set-NonRoundSeconds([datetime] $t) {
    if ($t.Second -eq 0) { return $t.AddSeconds((Get-Random -Minimum 1 -Maximum 60)) }
    return $t
}

# The public rule: next timestamp = previous + random 1..10 min, normalised, non-round,
# and strictly greater than the previous one.
function Get-NextCommitTime([datetime] $previous) {
    for ($attempt = 0; $attempt -lt 64; $attempt++) {
        $candidate = $previous.AddSeconds((Get-Random -Minimum 60 -Maximum 601))
        $candidate = Set-NonRoundSeconds (Get-AllowedTime $candidate)
        if ($candidate -gt $previous) { return $candidate }
        # Normalisation pulled it back (crossed into a weekday morning); retry from the
        # start of the next day's window.
        $previous = $previous.Date.AddDays(1)
    }
    throw 'Could not find a valid commit time.'
}

# When a day's evening session starts: weekdays 19:00, weekends 08:00.
function Get-WindowStart([datetime] $day) {
    if (Test-Weekend $day) { return $day.Date.AddHours(8) }
    return $day.Date.AddHours(19)
}

# --------------------------------------------------------------------------------------
# Pick the time
# --------------------------------------------------------------------------------------

$prev = $null
$headEpoch = (git log -1 --format=%at 2>$null)
if ($headEpoch) {
    $ref = if ($Amend) { 'HEAD~1' } else { 'HEAD' }
    $baseEpoch = (git log -1 --format=%at $ref 2>$null)
    if (-not $baseEpoch) { $baseEpoch = $headEpoch }
    $prev = [DateTimeOffset]::FromUnixTimeSeconds([int64] $baseEpoch).ToOffset([TimeSpan]::FromHours(5)).DateTime
}

$now = if ($null -ne $Now) { $Now } else { Get-Date }

if ($null -eq $prev) {
    # First commit in the repository - anchor on the current local time.
    $stamp = Set-NonRoundSeconds (Get-AllowedTime $now)
} elseif ((Test-Weekend $now) -or $now.Hour -ge 19) {
    # A weekend, or a weekday evening actually in progress: stamp the real moment.
    $stamp = $now
    if ($stamp -le $prev) { $stamp = $prev.AddSeconds((Get-Random -Minimum 60 -Maximum 601)) }
    $stamp = Set-NonRoundSeconds (Get-AllowedTime $stamp)
} else {
    # Weekday daytime: the day is at most yesterday's evening session.
    $capEnd = $now.Date.AddSeconds(-1)
    if ($prev -gt $capEnd) {
        Write-Warning 'History is already past yesterday - rolling the stamp forward.'
        $stamp = Get-NextCommitTime $prev
    } else {
        $continueGap = $prev.AddSeconds((Get-Random -Minimum 60 -Maximum 601))
        $sessionStart = (Get-WindowStart $now.Date.AddDays(-1)).AddSeconds((Get-Random -Minimum 1 -Maximum 2401))
        $stamp = if ($continueGap -gt $sessionStart) { $continueGap } else { $sessionStart }
        if ($stamp -gt $capEnd) {
            $room = [int]($capEnd - $prev).TotalSeconds
            if ($room -gt 60) {
                $stamp = $prev.AddSeconds((Get-Random -Minimum 60 -Maximum ([math]::Min($room, 601))))
            } else {
                Write-Warning 'Yesterday evening is full - rolling the stamp forward.'
                $stamp = Get-NextCommitTime $prev
            }
        }
        $stamp = Set-NonRoundSeconds $stamp
    }
}

$formatted = '{0} {1}' -f $stamp.ToString('yyyy-MM-ddTHH:mm:ss'), $Offset
Write-Host "timestamp: $formatted ($($stamp.DayOfWeek))"

if ($DryRun) {
    Write-Host 'dry run - nothing committed.'
    return
}

# --------------------------------------------------------------------------------------
# Stage and commit
# --------------------------------------------------------------------------------------

if ($All) {
    git add -A
    if ($LASTEXITCODE -ne 0) { throw 'git add -A failed.' }
} elseif ($Files -and $Files.Count -gt 0) {
    git add -- @Files
    if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }
} elseif (-not $Amend) {
    throw 'Nothing to stage: pass -Files <paths> or -All.'
}

$env:GIT_AUTHOR_DATE = $formatted
$env:GIT_COMMITTER_DATE = $formatted
try {
    if ($Amend) {
        git commit --amend -m $Message --date $formatted
    } else {
        git commit -m $Message
    }
    if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }
} finally {
    Remove-Item Env:GIT_AUTHOR_DATE, Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue
}

git log -1 --pretty=format:'%h %ad %s' --date=format:'%Y-%m-%d %H:%M:%S %a'
Write-Host ''
