<#
.SYNOPSIS
    Installer update smoke: installs the BUILT setup silently into a SANDBOX directory,
    the way the in-app updater does, and fails on "Rolling back changes". Never touches
    the user's own installation, so it runs while the user's Kitchen Designer is open.

.DESCRIPTION
    Three passes, each with its own Inno /LOG under test-results\installer-smoke\:

      1) plain_install  - a silent first install into the sandbox.
      2) update_race    - the sandbox app is RUNNING; setup is started with the updater's
                          own switches (read from InstallerCommandLine.cs, so the script and
                          the app cannot drift apart) and the app is asked to close only
                          -AppExitDelaySec later - the updater's exact order: start setup,
                          then quit. Setup must wait for the old copy, install, and relaunch
                          the app (/RELAUNCH); the relaunched app is closed afterwards.
      3) update_cancel  - the sandbox app is RUNNING and nobody closes it. After
                          RelaunchAskAfterSec setup must ask "close the app and click OK";
                          OK while the app still runs must ask AGAIN; Cancel must end setup
                          with nothing installed, no rollback, the sandbox files and the
                          running app untouched.

    The bug this guards (v0.1906..v0.2037): the updater started setup while the Unity
    player was still exiting; Restart Manager could not close it ("Some applications
    could not be shut down"), /SUPPRESSMSGBOXES answered Abort, and setup logged
    "Rolling back changes" - the user saw a rollback, then both windows closed.

    THE SANDBOX. The setup is started with /SMOKE=1 /DIR=<temp>\kd-smoke-<guid>\app
    /MUTEX=<name unique to this run> /APPARGS=<app switches>; KitchenDesigner.iss then
    - installs only into that directory and refuses to start without /DIR and /MUTEX;
    - does not write the "Programs and Features" entry (same AppId as the user's install),
      the Start menu / desktop shortcuts, or the .kdproj association;
    - keeps its InstallLanguage copy in HKCU\Software\KitchenDesigner-Smoke, so the
      language contract (written on a first install, kept by a silent update) is still
      checked;
    - waits for the mutex <name>, not the one the user's running app holds. The sandbox app
      is started with -mutex <name> and holds the same one, so the sandbox setup and the
      sandbox app see only each other.
    The sandbox is uninstalled by its own uninstaller at the end and the temp directory is
    removed. Before and after, the script fingerprints everything of the USER'S that a
    careless run could touch - the installed files (SHA-256), the uninstall registry entry,
    the .kdproj association, HKCU\Software\KitchenDesigner (InstallLanguage), the Start menu
    folder and the desktop shortcut - and fails if one byte differs.

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

# BelowNormal priority: while agents run tests the user's foreground work must win. Set on
# this script BEFORE it starts anything - Windows gives a child the parent's class when the
# parent is Idle/BelowNormal, so the installer and the app under test inherit it. Keep it
# (agents/UNITY-GATEWAY.md).
try { [Diagnostics.Process]::GetCurrentProcess().PriorityClass = 'BelowNormal' } catch { }
$root = Split-Path -Parent $PSScriptRoot

$script:sandbox = $null
$script:sandboxApp = $null
$script:mutex = $null
$script:userBefore = $null

function Fail([string]$Message) {
    Write-Host "[FAIL] $Message" -ForegroundColor Red
    if ($script:sandbox) { Remove-Sandbox }
    if ($script:userBefore) { Write-UserDiff $script:userBefore (Get-UserFingerprint) }
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
$projectExt = [regex]::Match($iss, '#define\s+ProjectExt\s+"(?<e>[^"]+)"').Groups['e'].Value
$projectProgId = [regex]::Match($iss, '#define\s+ProjectProgId\s+"(?<p>[^"]+)"').Groups['p'].Value
$appName = [regex]::Match($iss, '#define\s+AppName\s+"(?<n>[^"]+)"').Groups['n'].Value
$smokeLanguageKey = [regex]::Match($iss, '#define\s+SmokeLanguageKey\s+"(?<k>[^"]+)"').Groups['k'].Value
if (-not ($projectExt -and $projectProgId -and $appName -and $smokeLanguageKey)) { Fail 'ProjectExt / ProjectProgId / AppName / SmokeLanguageKey not found in installer\KitchenDesigner.iss' }
$uninstSubKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\${appGuid}_is1"

$switchSource = Join-Path $root 'Assets\Scripts\Core\Pure\Update\InstallerCommandLine.cs'
$updaterSwitches = [regex]::Match((Get-Content -LiteralPath $switchSource -Raw), 'SilentRelaunchSwitches\s*=\s*"(?<s>[^"]+)"').Groups['s'].Value
if (-not $updaterSwitches) { Fail "SilentRelaunchSwitches not found in $switchSource" }
$plainSwitches = ($updaterSwitches -split '\s+' | Where-Object { $_ -ne '/RELAUNCH' }) -join ' '

$mutexSource = Join-Path $root 'Assets\Scripts\Core\Pure\Update\RunningInstanceMutex.cs'
$mutexArgument = [regex]::Match((Get-Content -LiteralPath $mutexSource -Raw), 'ArgumentName\s*=\s*"(?<a>[^"]+)"').Groups['a'].Value
if (-not $mutexArgument) { Fail "RunningInstanceMutex.ArgumentName not found in $mutexSource" }

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

# ---- what belongs to the USER and must come out of this run byte for byte ----

function Get-FileDigest([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
        try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $stream.Dispose() }
    }
    catch { return 'unreadable:' + $_.Exception.GetType().Name }
    finally { $sha.Dispose() }
}

function Get-TreeFingerprint([string]$Dir) {
    if (-not $Dir -or -not (Test-Path -LiteralPath $Dir)) { return 'absent' }
    $base = (Resolve-Path -LiteralPath $Dir).Path.TrimEnd('\')
    $lines = Get-ChildItem -LiteralPath $base -Recurse -File -ErrorAction SilentlyContinue |
        Sort-Object FullName |
        ForEach-Object { '{0}|{1}|{2}' -f $_.FullName.Substring($base.Length), $_.Length, (Get-FileDigest $_.FullName) }
    return ($lines -join "`n")
}

function Add-RegistryLines([string]$SubKey, [Collections.Generic.List[string]]$Lines) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($SubKey)
    if (-not $key) { return }
    try {
        foreach ($name in ($key.GetValueNames() | Sort-Object)) {
            $value = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            if ($value -is [byte[]]) { $value = [BitConverter]::ToString($value) }
            elseif ($value -is [string[]]) { $value = $value -join ';' }
            $Lines.Add("$SubKey|$name|$($key.GetValueKind($name))|$value")
        }
        foreach ($child in ($key.GetSubKeyNames() | Sort-Object)) { Add-RegistryLines "$SubKey\$child" $Lines }
    }
    finally { $key.Dispose() }
}

function Get-RegistryFingerprint([string]$SubKey) {
    $lines = New-Object 'Collections.Generic.List[string]'
    Add-RegistryLines $SubKey $lines
    if ($lines.Count -eq 0) { return 'absent' }
    return ($lines -join "`n")
}

function Get-UserInstallDir {
    $key = Get-ItemProperty -LiteralPath "HKCU:\$uninstSubKey" -ErrorAction SilentlyContinue
    if ($key) { return $key.'Inno Setup: App Path' }
    return $null
}

function Get-UserFingerprint {
    $startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) $appName
    $desktopLink = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) "$appName.lnk"
    $desktopDigest = if (Test-Path -LiteralPath $desktopLink) { Get-FileDigest $desktopLink } else { 'absent' }
    $association = (Get-RegistryFingerprint "Software\Classes\$projectExt") + "`n" + (Get-RegistryFingerprint "Software\Classes\$projectProgId")
    [ordered]@{
        'install files'                 = Get-TreeFingerprint (Get-UserInstallDir)
        'uninstall entry'               = Get-RegistryFingerprint $uninstSubKey
        'file association'              = $association
        'HKCU\Software\KitchenDesigner' = Get-RegistryFingerprint 'Software\KitchenDesigner'
        'Start menu folder'             = Get-TreeFingerprint $startMenu
        'desktop shortcut'              = $desktopDigest
    }
}

function Write-UserDiff($Before, $After) {
    foreach ($name in $Before.Keys) {
        if ($Before[$name] -ne $After[$name]) {
            Write-Host "[FAIL] the user's '$name' CHANGED during the run" -ForegroundColor Red
            $left = @($Before[$name] -split "`n"); $right = @($After[$name] -split "`n")
            Compare-Object $left $right | Select-Object -First 10 | ForEach-Object { Write-Host "       $($_.SideIndicator) $($_.InputObject)" }
        }
    }
}

# ---- the sandbox ----

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

function Get-SandboxAppArguments {
    $arguments = @($mutexArgument, $script:mutex, '-mcpPort', "$Port", '-muteAudio', '-hideWindow', '-ephemeralSession', '-logFile', $script:playerLog)
    $arguments -join ' '
}

function Get-SandboxSwitches {
    "/SMOKE=1 /MUTEX=$($script:mutex) /DIR=`"$($script:sandboxApp)`" /NOICONS /TASKS=`"!desktopicon`" /APPARGS=`"$(Get-SandboxAppArguments)`""
}

function Start-SandboxApp {
    Start-Process -FilePath (Join-Path $script:sandboxApp 'KitchenDesigner.exe') -ArgumentList (Get-SandboxAppArguments) -PassThru
}

function Uninstall-Sandbox {
    $uninstaller = Join-Path $script:sandboxApp 'unins000.exe'
    if (-not (Test-Path -LiteralPath $uninstaller)) { return $true }
    $log = Join-Path $logDir "uninstall-$setupVersion.log"
    if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
    Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SMOKE=1 /MUTEX=$($script:mutex) /LOG=`"$log`"" | Out-Null
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-Path -LiteralPath $uninstaller) -and -not (Test-Path -LiteralPath (Join-Path $script:sandboxApp 'KitchenDesigner.exe'))) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Remove-Sandbox {
    if (-not $script:sandbox) { return }
    Stop-Leftovers $script:sandboxApp
    Uninstall-Sandbox | Out-Null
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $full = [IO.Path]::GetFullPath($script:sandbox)
    if ($full.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $full).StartsWith('kd-smoke-') -and (Test-Path -LiteralPath $full)) {
        Remove-Item -LiteralPath $full -Recurse -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath "HKCU:\$smokeLanguageKey") { Remove-Item -LiteralPath "HKCU:\$smokeLanguageKey" -Recurse -ErrorAction SilentlyContinue }
    $script:sandbox = $null
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
    Start-Process -FilePath $SetupPath -ArgumentList "$Switches $(Get-SandboxSwitches) /LOG=`"$Log`"" -PassThru
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

# ---- the user's installation: read-only from here to the end ----
$userDir = Get-UserInstallDir
$userRunning = @(Get-AppProcesses $userDir)
Write-Host "the user's installation: $(if ($userDir) { $userDir } else { 'none' }); running copies: $(if ($userRunning.Count) { 'pid ' + ($userRunning.Id -join ', ') + ' (left alone)' } else { 'none' })"
$script:userBefore = Get-UserFingerprint

$id = [guid]::NewGuid().ToString('N')
$script:sandbox = Join-Path ([IO.Path]::GetTempPath()) "kd-smoke-$id"
$script:sandboxApp = Join-Path $script:sandbox 'app'
New-Item -ItemType Directory -Force -Path $script:sandbox | Out-Null
$script:mutex = "KitchenDesigner.RunningInstance.smoke-$id"
$shortSandbox = (New-Object -ComObject Scripting.FileSystemObject).GetFolder($script:sandbox).ShortPath
$script:playerLog = Join-Path $shortSandbox 'player.log'
if (Test-Path -LiteralPath "HKCU:\$smokeLanguageKey") { Remove-Item -LiteralPath "HKCU:\$smokeLanguageKey" -Recurse }
Write-Host "sandbox: $($script:sandboxApp)  mutex: $($script:mutex)"

# ---- pass 1: plain silent install into the sandbox ----
$log1 = Join-Path $logDir "plain-install-$setupVersion.log"
$s1 = Start-Setup $plainSwitches $log1
Assert-SetupLog 'plain_install' $log1 (Wait-Setup $s1 'plain_install')

$appExe = Join-Path $script:sandboxApp 'KitchenDesigner.exe'
if (-not (Test-Path -LiteralPath $appExe)) { Fail "after plain_install there is no $appExe" }

# ---- the language contract: setup leaves a plain REG_SZ for the app, silent updates keep it ----
$langKey = "HKCU:\$smokeLanguageKey"
$langBefore = (Get-ItemProperty -LiteralPath $langKey -Name InstallLanguage -ErrorAction SilentlyContinue).InstallLanguage
if (-not $langBefore) { Fail "after plain_install there is no $langKey\InstallLanguage (the installer's language for the app, sandbox copy)" }
if ((Get-Item -LiteralPath $langKey).GetValueKind('InstallLanguage') -ne 'String') { Fail 'InstallLanguage must be a plain REG_SZ' }
Write-Host "[OK ] InstallLanguage = $langBefore (REG_SZ)"
Set-ItemProperty -LiteralPath $langKey -Name InstallLanguage -Value 'sentinel-keep-me'

# ---- pass 2: the auto-update race ----
$app = Start-SandboxApp
Start-Sleep -Seconds $AppStartSec
if ($app.HasExited) { Fail "update_race: the sandbox app exited by itself before the race (exit $($app.ExitCode))" }

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
$relaunchLine = (Get-CimInstance Win32_Process -Filter "ProcessId=$($relaunched.Id)").CommandLine
if ($relaunchLine -notmatch [regex]::Escape("$mutexArgument $($script:mutex)")) { Fail "update_race: the relaunched app did not get the sandbox mutex ($mutexArgument): $relaunchLine" }
$langAfter = (Get-ItemProperty -LiteralPath $langKey -Name InstallLanguage).InstallLanguage
Set-ItemProperty -LiteralPath $langKey -Name InstallLanguage -Value $langBefore
if ($langAfter -ne 'sentinel-keep-me') { Fail "update_race: the silent update overwrote InstallLanguage (sentinel -> '$langAfter')" }
Write-Host '[OK ] a silent update leaves InstallLanguage alone'
Start-Sleep -Seconds $AppStartSec
Stop-Leftovers $script:sandboxApp

# ---- pass 3: the app keeps running; the user is asked, clicks OK, is asked again, cancels ----
$askAfterSec = [int][regex]::Match($iss, '#define\s+RelaunchAskAfterSec\s+(?<s>\d+)').Groups['s'].Value
$recheckSec = [int][regex]::Match($iss, '#define\s+RelaunchRecheckSec\s+(?<s>\d+)').Groups['s'].Value
if ($askAfterSec -le 0 -or $recheckSec -le 0) { Fail 'RelaunchAskAfterSec / RelaunchRecheckSec not found in installer\KitchenDesigner.iss' }
$filesBefore = Get-TreeFingerprint $script:sandboxApp
$app3 = Start-SandboxApp
Start-Sleep -Seconds $AppStartSec
if ($app3.HasExited) { Fail "update_cancel: the sandbox app exited by itself (exit $($app3.ExitCode))" }

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
if ((Get-TreeFingerprint $script:sandboxApp) -ne $filesBefore) { Fail 'update_cancel: the installed files changed although the user cancelled' }
Write-Host "[OK ] update_cancel - asked $asked times, Cancel stopped setup (exit $code3), installed files untouched"
Close-Gracefully $app3
if (-not $app3.WaitForExit($AppCloseTimeoutSec * 1000)) { Fail "update_cancel: the app did not exit on WM_CLOSE in $AppCloseTimeoutSec s" }
Stop-Leftovers $script:sandboxApp

# ---- the sandbox goes away by its own uninstaller ----
if (-not (Uninstall-Sandbox)) { Fail 'the sandbox uninstaller did not remove the sandbox application in 120 s' }
if (Test-Path -LiteralPath "HKCU:\$smokeLanguageKey") { Fail "the sandbox uninstaller left $langKey behind" }
Write-Host '[OK ] the sandbox was removed by its own uninstaller'
Remove-Sandbox

# ---- and the user's installation is exactly as it was ----
$userAfter = Get-UserFingerprint
$changed = @($script:userBefore.Keys | Where-Object { $script:userBefore[$_] -ne $userAfter[$_] })
if ($changed.Count -gt 0) {
    Write-UserDiff $script:userBefore $userAfter
    $script:userBefore = $null
    Fail "the user's installation was modified: $($changed -join ', ')"
}
Write-Host "[OK ] the user's installation is byte-identical: $($script:userBefore.Keys -join ', ')"

Write-Host "INSTALLER SMOKE OK (logs: $logDir)"
exit 0
