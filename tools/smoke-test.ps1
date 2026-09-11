<#
.SYNOPSIS
    Дымовая проверка СОБРАННОГО плеера (post-build smoke test), встроенная в путь релиза.

.DESCRIPTION
    Пять тысяч тестов проекта гоняют редактор (EditMode/PlayMode) и никогда не запускают
    собранное приложение. Дважды это привело к тому, что все тесты были зелёные, а плеер
    не собирался или не поднимал MCP. Этот скрипт — последняя, самая дорогая ступень
    пирамиды: он запускает РЕАЛЬНЫЙ .exe и проверяет то, что видно только на нём.

    Проверяет по порядку:
      1) приложение запускается и отвечает по MCP (initialize -> serverInfo.name=unity-kitchen);
      2) создание элемента через MCP (create_elements);
      3) чтение его свойств через MCP (get_elements) и сверка с тем, что задавали;
      4) удаление элемента через MCP (delete_elements) и что он пропал из сцены;
      5) сохранение и загрузка проекта во временный файл и сверка результата;
      6) завершение приложения по команде и что процесс не остаётся висеть.

    Шаг 5 поднимает приложение с -mcpSaveDir <временный каталог>, которым скрипт владеет сам:
    он создаётся перед запуском и удаляется в конце прогона независимо от результата.

    Плеер запускается ещё и с -ephemeralSession: в этом режиме приложение НЕ пишет в
    PlayerPrefs (на Windows это реестр) ни «последний открытый проект», ни отметку
    первого запуска — см. EphemeralSessionArgument, LastProjectMemory и FirstRunMarker
    в Core. Раньше дымовой прогон подменял пользователю последний проект своим временным
    файлом, и следующий ОБЫЧНЫЙ запуск открывал kd-smoke-<хэш>\smoke-roundtrip.save.json.
    Чинится именно ЗАПИСЬ, а не подчистка после: прогон может упасть посередине, и тогда
    подчистка не выполнится, а ключ уже подменён.

    Плеер запускается БЕЗ видимого окна и БЕЗ звука — это прогон на машине пользователя,
    рядом с его собственной работой. -batchmode/-nographics не годятся: тест гоняет
    настоящий рендер (реальная сцена, create/get/delete через MCP). Окно прячет СЕБЯ
    плеер, аргументом -hideWindow (см. HideWindowArgument / DisplaySettings.ApplyWindowMode
    в Core) — решение живёт в приложении, а не в этом скрипте, поэтому оно переживает
    ЛЮБОЙ будущий вызов ApplyWindowMode (смена настроек, загрузка проекта), а не только
    вызов из Bootstrap при старте. Скрипт добавляет свой собственный ShowWindow(SW_HIDE)
    (Hide-PlayerWindow) только как страховку на первые секунды — окно может появиться
    раньше, чем движок дойдёт до Awake() и разберёт -hideWindow сам; как только окно
    один раз спряталось (или истёк короткий тайм-аут), скрипт эту страховку выключает и
    дальше полагается на приложение. Звук выключается аргументом -muteAudio (см.
    MuteAudioArgument / AudioOutputPolicy в Core/Audio) — он глушит вывод на время
    процесса и ничего не пишет в файл настроек пользователя.

    НЕ вызывается сам по себе ни из build.cmd, ни из обычного прогона тестов
    (agents/TESTS.md). Он часть пути релиза (installer/build-installer.cmd
    и installer/publish-github.cmd) и запускается только оттуда.

.PARAMETER ExePath
    Путь к собранному плееру. По умолчанию Build\KitchenDesigner.exe рядом с репозиторием.

.PARAMETER Port
    Порт MCP для ЭТОГО прогона. По умолчанию 19881 — НЕ 9337 (порт по умолчанию), чтобы не
    столкнуться с уже запущенным приложением пользователя на его порту по умолчанию.

.PARAMETER StartupTimeoutSec
    Сколько ждать, пока приложение поднимет MCP-сервер, прежде чем считать старт проваленным.

.EXAMPLE
    powershell -File tools\smoke-test.ps1
    powershell -File tools\smoke-test.ps1 -ExePath Build\KitchenDesigner.exe -Port 19881
#>
param(
    [string]$ExePath,
    [int]$Port = 19881,
    [int]$StartupTimeoutSec = 30,
    [int]$PerTestBudgetMs = 10
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $root 'Build\KitchenDesigner.exe' }

if ($Port -eq 9337) {
    Write-Host "[FAIL] -Port 9337 is the application's OWN default port; a smoke run must use a different one (default 19881) so it never fights the user's own running instance." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    Write-Host "[FAIL] Player not found: $ExePath" -ForegroundColor Red
    exit 1
}

$mcpUrl = "http://127.0.0.1:$Port/mcp"
$results = New-Object System.Collections.Generic.List[object]
$overallOk = $true
$nextId = 1
$proc = $null

function Invoke-Rpc {
    param([string]$Method, [hashtable]$Params = @{})
    $script:nextId++
    $body = @{ jsonrpc = '2.0'; id = $script:nextId; method = $Method; params = $Params } | ConvertTo-Json -Depth 20 -Compress
    Invoke-RestMethod -Uri $mcpUrl -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15
}

function Invoke-Tool {
    param([string]$ToolName, [hashtable]$Arguments = @{})
    $resp = Invoke-Rpc -Method 'tools/call' -Params @{ name = $ToolName; arguments = $Arguments }
    if ($resp.error) {
        throw "MCP error calling ${ToolName}: $($resp.error.message)"
    }
    $text = $resp.result.content[0].text
    if ($resp.result.isError) {
        throw "Tool ${ToolName} reported isError: $text"
    }
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    return $text | ConvertFrom-Json
}

function Add-Result {
    param([string]$Name, [bool]$Passed, [double]$Ms, [string]$Detail = '', [bool]$Budgeted = $true)
    $script:results.Add([pscustomobject]@{ Name = $Name; Passed = $Passed; Ms = [math]::Round($Ms, 2); Detail = $Detail })
    if (-not $Passed) { $script:overallOk = $false }
    $status = if ($Passed) { 'OK ' } else { 'FAIL' }
    # A slow step never fails the run by itself - only a wrong result does (Passed above).
    # The budget is an orientir to flag noisily, not a gate; see agents/TESTS.md.
    $budgetNote = if (-not $Budgeted) { ' (cleanup, not a budgeted step)' } elseif ($Ms -gt $PerTestBudgetMs) { " (BUDGET EXCEEDED: >${PerTestBudgetMs}ms)" } else { '' }
    Write-Host ("[{0}] {1,-28} {2,7:N2} ms{3}{4}" -f $status, $Name, $Ms, $budgetNote, $(if ($Detail) { " - $Detail" } else { '' }))
}

if (-not ([System.Management.Automation.PSTypeName]'SmokeTest.NativeMethods').Type) {
    Add-Type -Namespace SmokeTest -Name NativeMethods -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
'@
}
$SW_HIDE = 0

function Hide-PlayerWindow {
    # Safety net only: the player hides ITSELF via -hideWindow (DisplaySettings.
    # ApplyWindowMode), which is the durable fix and survives every future call to
    # ApplyWindowMode, not just the one at boot. This function only covers the brief
    # window between Start-Process returning and the engine reaching Awake() and
    # acting on -hideWindow - see Wait-ForPlayerWindowToHide below, which polls this
    # a few times and then stops relying on it.
    param($Process)
    try {
        $Process.Refresh()
        $h = $Process.MainWindowHandle
        if ($h -ne [IntPtr]::Zero) {
            [SmokeTest.NativeMethods]::ShowWindow($h, $SW_HIDE) | Out-Null
            return $true
        }
    } catch { }
    return $false
}

function Wait-ForPlayerWindowToHide {
    # Bounded, separate from $StartupTimeoutSec: this only bridges the gap until the
    # player's own window shows up and gets hidden once. It is not how long we wait
    # for MCP - that loop below no longer touches the window at all.
    param($Process, [int]$TimeoutSec = 5)
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
        if ($Process.HasExited) { return }
        if (Hide-PlayerWindow -Process $Process) { return }
        Start-Sleep -Milliseconds 100
    }
}

function Stop-SmokeProcess {
    param($Process)
    if ($null -eq $Process) { return }
    try {
        if (-not $Process.HasExited) {
            $Process.CloseMainWindow() | Out-Null
            $Process.WaitForExit(3000) | Out-Null
        }
    } catch { }
    try {
        if (-not $Process.HasExited) {
            Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
            $Process.WaitForExit(3000) | Out-Null
        }
    } catch { }
}

Write-Host "=== Smoke test: $ExePath (port $Port) ===" -ForegroundColor Cyan

# ---- 0) Own temp directory for -mcpSaveDir, torn down when the run ends ----
$tempSaveDir = Join-Path ([System.IO.Path]::GetTempPath()) "kd-smoke-$([guid]::NewGuid().ToString('N').Substring(0,8))"
New-Item -ItemType Directory -Path $tempSaveDir -Force | Out-Null

# ---- 1) Launch ----
# -muteAudio: the smoke test must never play sound on the machine it runs on (see
# MuteAudioArgument / AudioOutputPolicy in Core/Audio) - same reasoning as -mcpPort not
# fighting the user's own instance for a port. -hideWindow: the player hides its own
# window (see HideWindowArgument / DisplaySettings.ApplyWindowMode in Core) - always
# passed, same as -muteAudio.
# -ephemeralSession: nothing from this run outlives it - neither the last opened project
# nor the first-run marker (LastProjectMemory / FirstRunMarker in Core).
try {
    $proc = Start-Process -FilePath $ExePath `
        -ArgumentList @('-mcpPort', "$Port", '-mcpSaveDir', "$tempSaveDir", '-muteAudio', '-hideWindow', '-ephemeralSession') `
        -WindowStyle Hidden -PassThru
} catch {
    Write-Host "[FAIL] Could not start player: $_" -ForegroundColor Red
    Remove-Item -LiteralPath $tempSaveDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}
Wait-ForPlayerWindowToHide -Process $proc

# ---- Wait for MCP to come up (start cost is NOT charged to any single test) ----
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$up = $false
while ($sw.Elapsed.TotalSeconds -lt $StartupTimeoutSec) {
    if ($proc.HasExited) {
        Write-Host "[FAIL] Player process exited during startup (exit code $($proc.ExitCode))." -ForegroundColor Red
        Remove-Item -LiteralPath $tempSaveDir -Recurse -Force -ErrorAction SilentlyContinue
        exit 1
    }
    try {
        $r = Invoke-Rpc -Method 'initialize' -Params @{}
        if ($r.result.serverInfo.name) { $up = $true; break }
    } catch { Start-Sleep -Milliseconds 300 }
}
if (-not $up) {
    Write-Host "[FAIL] MCP did not answer within ${StartupTimeoutSec}s - app did not start or the bridge failed to bind." -ForegroundColor Red
    Stop-SmokeProcess -Process $proc
    Remove-Item -LiteralPath $tempSaveDir -Recurse -Force -ErrorAction SilentlyContinue
    exit 1
}

$testName = "SmokeTestBoard_$($proc.Id)"

try {
    # ---- 2) initialize contract ----
    $t = [System.Diagnostics.Stopwatch]::StartNew()
    $resp = Invoke-Rpc -Method 'initialize' -Params @{}
    $t.Stop()
    $okName = $resp.result.serverInfo.name -eq 'unity-kitchen'
    Add-Result -Name 'mcp_initialize' -Passed $okName -Ms $t.Elapsed.TotalMilliseconds -Detail "serverInfo.name=$($resp.result.serverInfo.name)"

    # ---- 3) create element ----
    $t = [System.Diagnostics.Stopwatch]::StartNew()
    $created = Invoke-Tool -ToolName 'create_elements' -Arguments @{
        items = @(@{
            name       = $testName
            type       = 'board'
            anchor_x_mm = 9000
            anchor_y_mm = 0
            anchor_z_mm = 9000
            width      = 321
            height     = 22
            depth      = 333
        })
    }
    $t.Stop()
    $createOk = $created -and $created.ok -eq $true -and ($created.created -contains $testName)
    Add-Result -Name 'create_elements' -Passed ([bool]$createOk) -Ms $t.Elapsed.TotalMilliseconds

    # ---- 4) read it back ----
    $t = [System.Diagnostics.Stopwatch]::StartNew()
    $fetched = Invoke-Tool -ToolName 'get_elements' -Arguments @{ names = @($testName); summary = $true }
    $t.Stop()
    $elem = $fetched.elements | Where-Object { $_.name -eq $testName } | Select-Object -First 1
    $eps = 0.5
    $sizeOk = $elem -and
        ([math]::Abs([double]$elem.dimXMm - 321) -le $eps) -and
        ([math]::Abs([double]$elem.dimYMm - 22)  -le $eps) -and
        ([math]::Abs([double]$elem.dimZMm - 333) -le $eps)
    Add-Result -Name 'get_elements' -Passed ([bool]$sizeOk) -Ms $t.Elapsed.TotalMilliseconds -Detail "found=$([bool]$elem)"

    # ---- 5) delete it ----
    $t = [System.Diagnostics.Stopwatch]::StartNew()
    Invoke-Tool -ToolName 'delete_elements' -Arguments @{ names = @($testName) } | Out-Null
    $t.Stop()
    $afterDelete = Invoke-Tool -ToolName 'get_elements' -Arguments @{ names = @($testName); summary = $true }
    $gone = ($afterDelete.missing -contains $testName) -or -not ($afterDelete.elements | Where-Object { $_.name -eq $testName })
    Add-Result -Name 'delete_elements' -Passed ([bool]$gone) -Ms $t.Elapsed.TotalMilliseconds

    # ---- 6) save + load a project round trip ----
    $saveTestName = "SmokeTestSave_$($proc.Id)"
    Invoke-Tool -ToolName 'create_elements' -Arguments @{
        items = @(@{
            name       = $saveTestName
            type       = 'board'
            anchor_x_mm = 9000
            anchor_y_mm = 0
            anchor_z_mm = 9500
            width      = 444
            height     = 55
            depth      = 222
        })
    } | Out-Null

    $savePath = Join-Path $tempSaveDir 'smoke-roundtrip.save.json'

    $t = [System.Diagnostics.Stopwatch]::StartNew()
    $saveResp = Invoke-Tool -ToolName 'save_project' -Arguments @{ path = $savePath }
    Invoke-Tool -ToolName 'delete_elements' -Arguments @{ names = @($saveTestName) } | Out-Null
    $loadResp = Invoke-Tool -ToolName 'load_project' -Arguments @{ path = $savePath }
    $t.Stop()

    $afterLoad = Invoke-Tool -ToolName 'get_elements' -Arguments @{ names = @($saveTestName); summary = $true }
    $restored = $afterLoad.elements | Where-Object { $_.name -eq $saveTestName } | Select-Object -First 1
    $roundTripOk = $saveResp -and $saveResp.ok -eq $true -and
        $loadResp -and $loadResp.ok -eq $true -and
        $restored -and
        ([math]::Abs([double]$restored.dimXMm - 444) -le $eps) -and
        ([math]::Abs([double]$restored.dimYMm - 55)  -le $eps) -and
        ([math]::Abs([double]$restored.dimZMm - 222) -le $eps)
    Add-Result -Name 'save_load_roundtrip' -Passed ([bool]$roundTripOk) -Ms $t.Elapsed.TotalMilliseconds -Detail "restored=$([bool]$restored)"
}
catch {
    # An exception here means a check aborted mid-way (unreachable MCP, unexpected
    # response shape, ...). Record it as a failure instead of letting it fall through
    # silently - $overallOk must never stay true just because a later Add-Result never ran.
    Add-Result -Name 'unexpected_exception' -Passed $false -Ms 0 -Detail "$_"
}
finally {
    # ---- 7) shutdown: must exit and leave no process ----
    $t = [System.Diagnostics.Stopwatch]::StartNew()
    Stop-SmokeProcess -Process $proc
    Start-Sleep -Milliseconds 200
    $stillThere = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    $t.Stop()
    Add-Result -Name 'shutdown_no_orphan' -Passed ($null -eq $stillThere) -Ms $t.Elapsed.TotalMilliseconds -Budgeted $false

    # ---- own temp -mcpSaveDir: clean up regardless of outcome ----
    Remove-Item -LiteralPath $tempSaveDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ''
Write-Host '=== Summary ===' -ForegroundColor Cyan
$results | Format-Table -AutoSize | Out-String | Write-Host

if ($overallOk) {
    Write-Host '[OK] Smoke test passed.' -ForegroundColor Green
    exit 0
} else {
    Write-Host '[FAIL] Smoke test failed - release must NOT be published.' -ForegroundColor Red
    exit 1
}
