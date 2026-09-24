<#
.SYNOPSIS
    Measurement tool: logs every new VISIBLE top-level window and every
    foreground-window change while it runs, so a flicker/focus-steal during a
    test or build run can be proven with numbers instead of "it flickered".

.DESCRIPTION
    Polls EnumWindows + GetForegroundWindow (user32) every -PollMs (default
    50 ms). A window is logged once, the first poll it is visible in. A
    foreground change is logged every time GetForegroundWindow() returns a
    different handle than the previous poll — this is what "steals keyboard
    focus while the user types" means in Win32 terms.

    Meant to bracket a single command: start the sensor, run the command
    under test, stop the sensor, read the CSV. Two ways to stop it:
      * -StopFile <path>   — sensor exits as soon as that file exists. Use
        this when the sensor runs as a background process (Start-Process)
        and the caller deletes the file first, runs the command, then
        creates the file and waits for the process to exit.
      * -DurationSeconds   — hard ceiling either way, so a forgotten sensor
        does not run forever.

.PARAMETER OutPath
    CSV log path (created fresh, overwritten if present).

.PARAMETER PollMs
    Poll interval in milliseconds. Default 50 — a flicker shorter than that
    would be invisible to a human anyway.

.PARAMETER StopFile
    Optional sentinel file. Deleted at startup if stale, then polled each
    cycle; the sensor exits the poll it appears in.

.PARAMETER DurationSeconds
    Safety ceiling. Default 600 s.

.EXAMPLE
    # bracket a command manually
    Remove-Item stop.flag -ErrorAction SilentlyContinue
    $p = Start-Process powershell -ArgumentList '-NoProfile -File tools\window-sensor.ps1 -OutPath run.csv -StopFile stop.flag' -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 1   # let the sensor's own first poll happen before the command starts
    & .\tools\unity.ps1 tests -Platform EditMode -Filter SceneLeakGuardReportTests
    New-Item stop.flag -ItemType File -Force | Out-Null
    $p.WaitForExit()
#>
param(
    [Parameter(Mandatory)] [string]$OutPath,
    [int]$PollMs = 50,
    [string]$StopFile = '',
    [int]$DurationSeconds = 600
)

$ErrorActionPreference = 'Stop'

if (-not ([System.Management.Automation.PSTypeName]'WindowSensor.NativeMethods').Type) {
    Add-Type -Namespace WindowSensor -Name NativeMethods -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
[DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
[DllImport("user32.dll", CharSet = CharSet.Auto)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
'@
}

if ($StopFile) { Remove-Item -LiteralPath $StopFile -Force -ErrorAction SilentlyContinue }

function Get-SensorWindowTitle {
    param([IntPtr]$Handle)
    $len = [WindowSensor.NativeMethods]::GetWindowTextLength($Handle)
    if ($len -le 0) { return '' }
    $sb = New-Object Text.StringBuilder ($len + 1)
    [WindowSensor.NativeMethods]::GetWindowText($Handle, $sb, $sb.Capacity) | Out-Null
    return $sb.ToString()
}

function Get-SensorProcInfo {
    param([IntPtr]$Handle)
    [uint32]$procId = 0
    [WindowSensor.NativeMethods]::GetWindowThreadProcessId($Handle, [ref]$procId) | Out-Null
    $pname = try { (Get-Process -Id $procId -ErrorAction Stop).ProcessName } catch { "pid$procId" }
    [pscustomobject]@{ Pid = $procId; Name = $pname }
}

function Write-SensorRow {
    param([IO.StreamWriter]$Writer, [long]$Ms, [string]$EventName, [uint32]$ProcId, [string]$ProcName, [string]$Title)
    $safeTitle = ($Title -replace '"', '''')
    $Writer.WriteLine("$Ms,$EventName,$ProcId,$ProcName,`"$safeTitle`"")
}

$writer = New-Object IO.StreamWriter($OutPath, $false, (New-Object Text.UTF8Encoding($false)))
$writer.AutoFlush = $true
$writer.WriteLine('TimestampMs,Event,Pid,Process,Title')

$seenWindows = New-Object 'System.Collections.Generic.HashSet[IntPtr]'
$lastForeground = [IntPtr]::Zero
$selfPid = $PID

# Seed with whatever is ALREADY on screen (the user's own desktop: browser,
# terminals, editors, ...) so the log only ever reports windows that appear
# AFTER the sensor starts watching — i.e. ones the bracketed command caused.
# Without this seed pass, every pre-existing window looks like a "new" one
# on the sensor's first poll and drowns the real signal.
$seedCallback = {
    param([IntPtr]$hWnd, [IntPtr]$lParam)
    if ([WindowSensor.NativeMethods]::IsWindowVisible($hWnd)) { [void]$seenWindows.Add($hWnd) }
    return $true
}
[WindowSensor.NativeMethods]::EnumWindows($seedCallback, [IntPtr]::Zero) | Out-Null
# Baseline foreground window is not logged either — only a CHANGE away from it is.
$lastForeground = [WindowSensor.NativeMethods]::GetForegroundWindow()

$sw = [Diagnostics.Stopwatch]::StartNew()

try {
    while ($true) {
        if ($StopFile -and (Test-Path -LiteralPath $StopFile)) { break }
        if ($sw.Elapsed.TotalSeconds -gt $DurationSeconds) { break }

        $current = New-Object 'System.Collections.Generic.List[IntPtr]'
        $callback = {
            param([IntPtr]$hWnd, [IntPtr]$lParam)
            if ([WindowSensor.NativeMethods]::IsWindowVisible($hWnd)) { $current.Add($hWnd) | Out-Null }
            return $true
        }
        [WindowSensor.NativeMethods]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null

        foreach ($h in $current) {
            if ($seenWindows.Contains($h)) { continue }
            $seenWindows.Add($h) | Out-Null
            $info = Get-SensorProcInfo -Handle $h
            if ($info.Pid -eq $selfPid) { continue }   # the sensor's own (hidden) host process
            $title = Get-SensorWindowTitle -Handle $h
            Write-SensorRow -Writer $writer -Ms $sw.ElapsedMilliseconds -EventName 'NEW_WINDOW' `
                -ProcId $info.Pid -ProcName $info.Name -Title $title
        }

        $fg = [WindowSensor.NativeMethods]::GetForegroundWindow()
        if ($fg -ne $lastForeground) {
            $info = Get-SensorProcInfo -Handle $fg
            $title = Get-SensorWindowTitle -Handle $fg
            Write-SensorRow -Writer $writer -Ms $sw.ElapsedMilliseconds -EventName 'FOREGROUND_CHANGE' `
                -ProcId $info.Pid -ProcName $info.Name -Title $title
            $lastForeground = $fg
        }

        Start-Sleep -Milliseconds $PollMs
    }
}
finally {
    $writer.Close()
}
