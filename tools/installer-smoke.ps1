<#
.SYNOPSIS
    Installer update smoke: installs the BUILT setup silently over the installed copy, the
    way the in-app updater does, and fails on "Rolling back changes".

.DESCRIPTION
    Three passes, each with its own Inno /LOG under test-results\installer-smoke\:

      1) plain_install  - a silent install over whatever is installed (or a first install).
      2) update_race    - the installed app is RUNNING; setup is started with the updater's
                          own switches (read from InstallerCommandLine.cs, so the script and
                          the app cannot drift apart) and the app is asked to close only
                          -AppExitDelaySec later - the updater's exact order: start setup,
                          then quit. Setup must wait for the old copy, install, and relaunch
                          the app (/RELAUNCH); the relaunched app is closed afterwards.
      3) update_cancel  - the app is RUNNING and nobody closes it. After RelaunchAskAfterSec
                          setup must ask "close the app and click OK"; OK while the app still
                          runs must ask AGAIN; Cancel must end setup with nothing installed,
                          no rollback, the old version and the running app untouched.

    The bug this guards (v0.1906..v0.2037): the updater started setup while the Unity
    player was still exiting; Restart Manager could not close it ("Some applications
    could not be shut down"), /SUPPRESSMSGBOXES answered Abort, and setup logged
    "Rolling back changes" - the user saw a rollback, then both windows closed.

    It works on the user's REAL per-user install (same AppId, same HKCU uninstall key):
    only that install carries the previous version's files and uninstall log, which is
    what an update meets. It never uninstalls. It refuses to run while Kitchen Designer
    is open (pass 1 would roll back on the open files) and refuses to downgrade (the
    downgrade question in the .iss is a plain MsgBox that /SUPPRESSMSGBOXES does not
    answer - a silent run would hang on it).

    Part of the release path: installer\publish-github.cmd runs it after
    tools\smoke-test.ps1 and before tagging. Exit 0 = all three passes behaved.

.EXAMPLE
    powershell -File tools\installer-smoke.ps1
    powershell -File tools\installer-smoke.ps1 -SetupPath installer\output\KitchenDesigner-Setup-0.2040-x64.exe
#>
param(
    [string]$SetupPath,
    [int]$AppExitDelaySec = 4,
    [int]$AppStartSec = 10,
    [int]$SetupTimeoutSec = 300,
    [int]$AppCloseTimeoutSec = 60,
    [int]$Port = 19883
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$script:cleanupDir = $null

function Fail([string]$Message) {
    Write-Host "[FAIL] $Message" -ForegroundColor Red
    if ($script:cleanupDir) { Stop-Leftovers $script:cleanupDir }
    exit 1
}

if (-not $SetupPath) {
    $versionFile = Join-Path $root 'installer\output\version.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) { Fail "no -SetupPath and no $versionFile - build the installer first" }
    $ver = (Get-Content -LiteralPath $versionFile -Raw).Trim()
    $SetupPath = Join-Path $root "installer\output\KitchenDesigner-Setup-$ver-x64.exe"
}
if (-not (Test-Path -LiteralPath $SetupPath)) { Fail "setup not found: $SetupPath" }
$SetupPath = (Resolve-Path -LiteralPath $SetupPath).Path

$setupVersion = [regex]::Match([IO.Path]::GetFileName($SetupPath), '^KitchenDesigner-Setup-(?<v>[\d.]+)-x64\.exe$').Groups['v'].Value
if (-not $setupVersion) { Fail "cannot read the version from the setup file name: $SetupPath" }

$iss = Get-Content -LiteralPath (Join-Path $root 'installer\KitchenDesigner.iss') -Raw
$appGuid = [regex]::Match($iss, '#define\s+AppGuid\s+"(?<g>[^"]+)"').Groups['g'].Value
if (-not $appGuid) { Fail 'AppGuid not found in installer\KitchenDesigner.iss' }
$uninstKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\${appGuid}_is1"

$switchSource = Join-Path $root 'Assets\Scripts\Core\Pure\Update\InstallerCommandLine.cs'
$updaterSwitches = [regex]::Match((Get-Content -LiteralPath $switchSource -Raw), 'SilentRelaunchSwitches\s*=\s*"(?<s>[^"]+)"').Groups['s'].Value
if (-not $updaterSwitches) { Fail "SilentRelaunchSwitches not found in $switchSource" }
$plainSwitches = ($updaterSwitches -split '\s+' | Where-Object { $_ -ne '/RELAUNCH' }) -join ' '

$logDir = Join-Path $root 'test-results\installer-smoke'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

if (-not ([System.Management.Automation.PSTypeName]'InstallerSmoke.NativeMethods').Type) {
    Add-Type -Namespace InstallerSmoke -Name NativeMethods -MemberDefinition @'
[DllImport("user32.dll", SetLastError=true)] public static extern IntPtr FindWindowExW(IntPtr parent, IntPtr childAfter, IntPtr className, IntPtr windowName);
[DllImport("user32.dll", SetLastError=true)] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
[DllImport("user32.dll", SetLastError=true)] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
[DllImport("user32.dll", SetLastError=true, EntryPoint="FindWindowExW")] public static extern IntPtr FindDialogW(IntPtr parent, IntPtr childAfter, [MarshalAs(UnmanagedType.LPWStr)] string className, IntPtr windowName);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
'@
}

function Get-InstallDir {
    $key = Get-ItemProperty -LiteralPath $uninstKey -ErrorAction SilentlyContinue
    if ($key) { return $key.'Inno Setup: App Path' }
    return $null
}

function Get-AppProcesses([string]$Dir) {
    if (-not $Dir) { return @() }
    $prefix = $Dir.TrimEnd('\') + '\'
    @(Get-Process -Name KitchenDesigner, UnityCrashHandler64 -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) })
}

function Close-Gracefully($Process) {
    # WM_CLOSE to every top-level window of the process, hidden ones included - the same
    # graceful quit the player gets from the user (see Request-PlayerClose in smoke-test.ps1
    # for why CloseMainWindow() cannot reach a -hideWindow player).
    $handle = [IntPtr]::Zero
    while ($true) {
        $handle = [InstallerSmoke.NativeMethods]::FindWindowExW([IntPtr]::Zero, $handle, [IntPtr]::Zero, [IntPtr]::Zero)
        if ($handle -eq [IntPtr]::Zero) { break }
        $owner = 0
        [InstallerSmoke.NativeMethods]::GetWindowThreadProcessId($handle, [ref]$owner) | Out-Null
        if ($owner -eq $Process.Id) {
            [InstallerSmoke.NativeMethods]::PostMessage($handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        }
    }
}

function Stop-Leftovers([string]$Dir) {
    foreach ($p in Get-AppProcesses $Dir) {
        Close-Gracefully $p
        if (-not $p.WaitForExit($AppCloseTimeoutSec * 1000)) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    }
}

function Assert-SetupLog([string]$Pass, [string]$Log, [int]$ExitCode) {
    if (-not (Test-Path -LiteralPath $Log)) { Fail "${Pass}: setup wrote no log ($Log), exit $ExitCode" }
    $lines = Get-Content -LiteralPath $Log
    $rollback = ($lines | Select-String -SimpleMatch 'Rolling back changes' | Select-Object -First 1)
    if ($rollback) {
        $from = [Math]::Max(0, $rollback.LineNumber - 6)
        Write-Host ($lines[$from..($rollback.LineNumber - 1)] -join "`n")
        Fail "${Pass}: setup ROLLED BACK (log: $Log)"
    }
    if ($ExitCode -ne 0) { Fail "${Pass}: setup exit code $ExitCode (log: $Log)" }
    if (-not ($lines | Select-String -SimpleMatch 'Installation process succeeded')) {
        Fail "${Pass}: no 'Installation process succeeded' in $Log"
    }
    $wait = $lines | Select-String -SimpleMatch 'Update: ' | ForEach-Object { $_.Line.Substring(24).Trim() }
    Write-Host "[OK ] $Pass - installed, exit 0$(if ($wait) { ' - ' + ($wait -join '; ') })"
}

function Get-SetupDialog($SetupProcess) {
    $owners = @([uint32]$SetupProcess.Id) + @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($SetupProcess.Id)" | ForEach-Object { [uint32]$_.ProcessId })
    $handle = [IntPtr]::Zero
    while ($true) {
        $handle = [InstallerSmoke.NativeMethods]::FindDialogW([IntPtr]::Zero, $handle, '#32770', [IntPtr]::Zero)
        if ($handle -eq [IntPtr]::Zero) { return $null }
        $owner = 0
        [InstallerSmoke.NativeMethods]::GetWindowThreadProcessId($handle, [ref]$owner) | Out-Null
        if ($owners -contains $owner -and [InstallerSmoke.NativeMethods]::IsWindowVisible($handle)) { return $handle }
    }
}

function Wait-SetupDialog($SetupProcess, [int]$TimeoutSec) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline -and -not $SetupProcess.HasExited) {
        $dialog = Get-SetupDialog $SetupProcess
        if ($dialog) { return $dialog }
        Start-Sleep -Milliseconds 250
    }
    return $null
}

function Wait-DialogGone($SetupProcess, [int]$TimeoutSec) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline -and (Get-SetupDialog $SetupProcess)) { Start-Sleep -Milliseconds 100 }
}

function Press-DialogButton($Dialog, [int]$ButtonId) {
    [InstallerSmoke.NativeMethods]::PostMessage($Dialog, 0x0111, [IntPtr]$ButtonId, [IntPtr]::Zero) | Out-Null
}

function Start-Setup([string]$Switches, [string]$Log) {
    if (Test-Path -LiteralPath $Log) { Remove-Item -LiteralPath $Log }
    Start-Process -FilePath $SetupPath -ArgumentList "$Switches /LOG=`"$Log`"" -PassThru
}

function Wait-Setup($Process, [string]$Pass) {
    if (-not $Process.WaitForExit($SetupTimeoutSec * 1000)) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        Fail "${Pass}: setup did not finish in $SetupTimeoutSec s"
    }
    return $Process.ExitCode
}

Write-Host "=== Installer update smoke: $SetupPath ==="
Write-Host "updater switches: $updaterSwitches"

$installDir = Get-InstallDir
if ($installDir) {
    $running = Get-AppProcesses $installDir
    if ($running.Count -gt 0) {
        Fail "Kitchen Designer is running from $installDir (pid $($running.Id -join ', ')). Close it and re-run: installing over open files is exactly the rollback this smoke guards."
    }
    $installed = (Get-ItemProperty -LiteralPath $uninstKey).DisplayVersion
    if ($installed -and ([version]$installed -gt [version]$setupVersion)) {
        Fail "installed $installed is newer than the setup $setupVersion. The smoke never downgrades: the .iss asks with a plain MsgBox that a silent run cannot answer."
    }
}

# ---- pass 1: plain silent install over the installed copy ----
$log1 = Join-Path $logDir "plain-install-$setupVersion.log"
$s1 = Start-Setup $plainSwitches $log1
Assert-SetupLog 'plain_install' $log1 (Wait-Setup $s1 'plain_install')

$installDir = Get-InstallDir
if (-not $installDir) { Fail "after plain_install there is no uninstall key $uninstKey" }
$appExe = Join-Path $installDir 'KitchenDesigner.exe'
if (-not (Test-Path -LiteralPath $appExe)) { Fail "after plain_install there is no $appExe" }

# ---- pass 2: the auto-update race ----
$script:cleanupDir = $installDir
$app = Start-Process -FilePath $appExe -ArgumentList @('-mcpPort', "$Port", '-muteAudio', '-hideWindow', '-ephemeralSession') -PassThru
Start-Sleep -Seconds $AppStartSec
if ($app.HasExited) { Fail "update_race: the installed app exited by itself before the race (exit $($app.ExitCode))" }

$log2 = Join-Path $logDir "update-race-$setupVersion.log"
$raceStart = Get-Date
$s2 = Start-Setup $updaterSwitches $log2
Start-Sleep -Seconds $AppExitDelaySec
Close-Gracefully $app
$code2 = Wait-Setup $s2 'update_race'
Assert-SetupLog 'update_race' $log2 $code2
if (-not $app.WaitForExit($AppCloseTimeoutSec * 1000)) {
    Fail "update_race: the old app did not exit on WM_CLOSE in $AppCloseTimeoutSec s"
}

$deadline = (Get-Date).AddSeconds(30)
$relaunched = $null
while (-not $relaunched -and (Get-Date) -lt $deadline) {
    $relaunched = Get-Process -Name KitchenDesigner -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $appExe -and $_.StartTime -gt $raceStart } | Select-Object -First 1
    if (-not $relaunched) { Start-Sleep -Milliseconds 500 }
}
if (-not $relaunched) { Fail 'update_race: setup installed but did not relaunch the app (/RELAUNCH)' }
Write-Host "[OK ] update_race - app relaunched (pid $($relaunched.Id)), closing it"
Start-Sleep -Seconds $AppStartSec
Stop-Leftovers $installDir

# ---- pass 3: the app keeps running; the user is asked, clicks OK, is asked again, cancels ----
$askAfterSec = [int][regex]::Match($iss, '#define\s+RelaunchAskAfterSec\s+(?<s>\d+)').Groups['s'].Value
$recheckSec = [int][regex]::Match($iss, '#define\s+RelaunchRecheckSec\s+(?<s>\d+)').Groups['s'].Value
if ($askAfterSec -le 0 -or $recheckSec -le 0) { Fail 'RelaunchAskAfterSec / RelaunchRecheckSec not found in installer\KitchenDesigner.iss' }
$versionBefore = (Get-ItemProperty -LiteralPath $uninstKey).DisplayVersion
$app3 = Start-Process -FilePath $appExe -ArgumentList @('-mcpPort', "$Port", '-muteAudio', '-hideWindow', '-ephemeralSession') -PassThru
Start-Sleep -Seconds $AppStartSec
if ($app3.HasExited) { Fail "update_cancel: the installed app exited by itself (exit $($app3.ExitCode))" }

$log3 = Join-Path $logDir "update-cancel-$setupVersion.log"
$s3 = Start-Setup $updaterSwitches $log3
$ask1 = Wait-SetupDialog $s3 ($askAfterSec + 20)
if (-not $ask1) { Fail "update_cancel: no 'close the app and click OK' question within $($askAfterSec + 20) s while the app kept running" }
Press-DialogButton $ask1 1
Wait-DialogGone $s3 10
$ask2 = Wait-SetupDialog $s3 ($recheckSec + 20)
if (-not $ask2) { Fail 'update_cancel: OK while the app still runs must ask AGAIN, but setup went on without asking' }
Press-DialogButton $ask2 2
$code3 = Wait-Setup $s3 'update_cancel'

$lines3 = Get-Content -LiteralPath $log3
if ($lines3 | Select-String -SimpleMatch 'Rolling back changes') { Fail "update_cancel: Cancel ROLLED BACK instead of stopping before the copy (log: $log3)" }
if ($lines3 | Select-String -SimpleMatch 'Installation process succeeded') { Fail "update_cancel: setup installed although the user cancelled (log: $log3)" }
if (-not ($lines3 | Select-String -SimpleMatch 'cancelled by the user')) { Fail "update_cancel: no 'cancelled by the user' in $log3" }
$asked = @($lines3 | Select-String -SimpleMatch 'asking the user').Count
if ($asked -lt 2) { Fail "update_cancel: asked $asked time(s), expected 2 (OK, then again)" }
if ($code3 -eq 0) { Fail 'update_cancel: setup reported success (exit 0) after Cancel' }
if ($app3.HasExited) { Fail 'update_cancel: Cancel must leave the running app alone, but it is gone' }
$versionAfter = (Get-ItemProperty -LiteralPath $uninstKey).DisplayVersion
if ($versionAfter -ne $versionBefore) { Fail "update_cancel: installed version changed $versionBefore -> $versionAfter" }
Write-Host "[OK ] update_cancel - asked $asked times, Cancel stopped setup (exit $code3), $versionBefore untouched"
Close-Gracefully $app3
if (-not $app3.WaitForExit($AppCloseTimeoutSec * 1000)) { Fail "update_cancel: the app did not exit on WM_CLOSE in $AppCloseTimeoutSec s" }

Write-Host "INSTALLER SMOKE OK (logs: $logDir)"
exit 0
