<#
.SYNOPSIS
    Единая точка входа в Unity: живой редактор, если он открыт, иначе batch.

.DESCRIPTION
    Логика одна для ВСЕХ скриптов проекта:

      редактор открыт  → команда уходит в него через мост (Assets/Editor/EditorBridge.cs)
      редактор закрыт  → холодный `Unity.exe -batchMode`, как раньше

    Смысл в цене запуска. Холодный batch платит фиксированные 60–90 секунд
    (лицензия, Asset Pipeline Refresh ~12 с, три domain reload) ДО первого теста.
    Прогон одного тестового класса стоит из-за этого 2 минуты вместо 20 секунд.
    Через мост платится только компиляция изменённых скриптов и сами тесты.

    Побочно снимается вторая беда: проект держит эксклюзивный лок, и раньше
    открытый вручную редактор просто не давал batch-скриптам стартовать.

.PARAMETER Command
    tests   — прогон тестов (-Platform, -Filter)
    method  — статический метод редактора (-Method), например BuildProject.Build
    start   — поднять фоновый редактор (batch, без -quit) и дождаться готовности
    stop    — попросить живой редактор закрыться
    status  — где сейчас исполняются команды

.EXAMPLE
    .\tools\unity.ps1 tests -Platform EditMode -Filter SnapCoreTests
    .\tools\unity.ps1 tests -Platform PlayMode
    .\tools\unity.ps1 method -Method BuildProject.Build -LogSuffix win
    .\tools\unity.ps1 start
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('tests', 'method', 'start', 'stop', 'status')]
    [string]$Command = 'status',

    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode',

    [string]$Filter = '',
    [string]$Method = '',
    [string]$ResultPath = '',
    [string]$LogSuffix = '',

    # Не поднимать редактор ради одной команды — уйти в холодный batch.
    [switch]$NoDaemon,

    # Полный прогон через живой редактор: быстрее, но чуть менее верно (см.
    # Invoke-Tests). Для финальной проверки перед коммитом не использовать.
    [switch]$Live,

    # Тайм-аут ожидания результата, минут.
    [int]$TimeoutMinutes = 40
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe'
$port = 9338
if (-not $ResultPath) { $ResultPath = Join-Path $repo 'test-results\tmp\TestResults.xml' }
$logBase = Join-Path $env:TEMP 'build-kitchen.log'
$log = if ($LogSuffix) { "$logBase.$LogSuffix.log" } else { $logBase }

if (-not (Test-Path $unity)) { throw "Unity не найден: $unity" }

# ── Мост ────────────────────────────────────────────────────────────────

function Send-Bridge {
    param([string]$Line, [int]$TimeoutMs = 5000)

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.BeginConnect('127.0.0.1', $port, $null, $null)
        if (-not $connect.AsyncWaitHandle.WaitOne($TimeoutMs)) { return $null }
        $client.EndConnect($connect)

        $stream = $client.GetStream()
        $stream.ReadTimeout = [Math]::Max($TimeoutMs, 60000)
        $writer = New-Object System.IO.StreamWriter($stream)
        $writer.AutoFlush = $true
        $writer.WriteLine($Line)
        $reader = New-Object System.IO.StreamReader($stream)
        return $reader.ReadLine()
    } catch {
        return $null
    } finally {
        $client.Close()
    }
}

function Test-Bridge {
    # Короткий тайм-аут: «моста нет» — это нормальный, частый случай.
    $reply = Send-Bridge -Line 'PING' -TimeoutMs 1500
    return ($null -ne $reply -and $reply.StartsWith('OK'))
}

<#
    Редактор ДЕРЖИТ проект, даже если сокет молчит.

    Молчание — штатное состояние: слушатель снимается на время domain reload
    (`beforeAssemblyReload`), а тот случается при каждой перекомпиляции и на
    входе/выходе из PlayMode. Если решать по одному ping, скрипт в такие
    секунды поднимает ВТОРОЙ редактор на том же проекте — ровно это и
    случилось при первом прогоне PlayMode: два процесса Unity, конфликт за
    порт и за Library.

    Настоящий признак занятости — процесс Unity, чей проект совпадает с нашим.
    Lock-файл сам по себе не годится: после аварийного завершения он остаётся
    лежать.
#>
function Test-EditorRunning {
    foreach ($p in (Get-Process Unity -ErrorAction SilentlyContinue)) {
        try {
            $cmdline = (Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)").CommandLine
            if ($cmdline -and $cmdline -match [regex]::Escape($repo)) { return $true }
        } catch { }
    }
    return $false
}

<#  Дождаться, пока редактор снова начнёт отвечать (после перекомпиляции). #>
function Wait-Bridge {
    param([int]$Minutes = 10)

    $deadline = (Get-Date).AddMinutes($Minutes)
    while ((Get-Date) -lt $deadline) {
        if (Test-Bridge) { return $true }
        if (-not (Test-EditorRunning)) { return $false }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Start-Daemon {
    Write-Host '=== Запуск фонового редактора ===' -ForegroundColor Cyan
    # Без -quit: редактор остаётся жить и обслуживать мост.
    Start-Process -FilePath $unity -ArgumentList @(
        '-batchMode', '-projectPath', $repo,
        '-executeMethod', 'EditorBridge.Daemon',
        '-logFile', "$logBase.daemon.log"
    ) | Out-Null

    $deadline = (Get-Date).AddMinutes(5)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        if (Test-Bridge) {
            Write-Host '  редактор готов' -ForegroundColor Green
            return $true
        }
    }
    throw "Редактор не поднялся за 5 минут, лог: $logBase.daemon.log"
}

function Wait-Job {
    param([string]$JobId)

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $reply = Send-Bridge -Line "STATUS $JobId"
        if (-not $reply) {
            # domain reload рвёт сокет — это норма, но только пока редактор жив.
            if (-not (Test-EditorRunning)) { throw 'Редактор умер во время прогона' }
            continue
        }
        if ($reply.StartsWith('ERR')) { throw $reply }

        $parts = $reply.Substring(3).Split('|')
        $state = $parts[0]
        if ($state -eq 'running') { continue }
        return $parts
    }
    throw "Задача $JobId не завершилась за $TimeoutMinutes мин"
}

# ── Команды ─────────────────────────────────────────────────────────────

function Resolve-Bridge {
    if ($NoDaemon) {
        if (Test-EditorRunning) { throw 'Редактор открыт — batch не стартует. Закройте Unity или уберите -NoDaemon' }
        return $false
    }
    if (Test-Bridge) { return $true }
    # Процесс жив, но молчит — идёт компиляция или domain reload. Ждём его, а
    # НЕ поднимаем второй редактор поверх того же проекта.
    if (Test-EditorRunning) { return (Wait-Bridge) }
    return (Start-Daemon)
}

<#
    Подхватить правки в коде ДО прогона.

    Фоновый редактор запущен в batch и за файловой системой не следит: без
    явного Refresh он гоняет тесты по коду, скомпилированному при старте. Это
    не «медленнее», это НЕВЕРНО — зелёный прогон на старом коде хуже красного.
    Ждём конца компиляции: она даёт domain reload, и сокет на это время молчит.
#>
function Sync-Editor {
    Send-Bridge -Line 'REFRESH' | Out-Null

    $deadline = (Get-Date).AddMinutes(10)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $reply = Send-Bridge -Line 'PING' -TimeoutMs 2000
        if (-not $reply) {
            if (-not (Test-EditorRunning)) { throw 'Редактор умер на компиляции' }
            continue    # идёт domain reload
        }
        if ($reply -notmatch 'compiling') { return }
    }
    throw 'Компиляция не закончилась за 10 минут'
}

<#
    Закрыть живой редактор и дождаться, пока он отпустит проект.
    Нужно перед холодным batch: проект держится эксклюзивно.
#>
function Get-EditorKind {
    foreach ($p in (Get-Process Unity -ErrorAction SilentlyContinue)) {
        try {
            $cmdline = (Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)").CommandLine
            if (-not $cmdline -or $cmdline -notmatch [regex]::Escape($repo)) { continue }
            return $(if ($cmdline -match 'EditorBridge\.Daemon') { 'daemon' } else { 'gui' })
        } catch { }
    }
    return 'none'
}

function Stop-Editor {
    $kind = Get-EditorKind
    if ($kind -eq 'none') { return $false }
    if ($kind -eq 'gui') {
        throw 'Открыт редактор Unity, а холодному прогону нужен эксклюзивный проект. Закройте его или добавьте -Live (быстрее, но чуть менее верно)'
    }

    Write-Host '  закрываю редактор (нужен холодный batch)' -ForegroundColor DarkGray
    Send-Bridge -Line 'QUIT' | Out-Null

    $deadline = (Get-Date).AddMinutes(2)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        if (-not (Test-EditorRunning)) { return $true }
    }
    throw 'Редактор не закрылся за 2 минуты'
}

<#
    Куда отправить прогон.

    Мост — для ИТЕРАЦИЙ: фильтр по классу отрабатывает за секунды вместо двух
    минут. Полный прогон по умолчанию идёт холодным batch, потому что живой
    редактор не даёт полной верности: пара тестов зависит от свежести процесса
    (шаг SmoothDamp считается от Time.deltaTime, атлас шрифта переживает
    прогон). Два полных прогона подряд в одной сессии разошлись на этих двух —
    цифра, на которой коммитят, должна получаться тем же способом, что и раньше.

    -Live заставляет гнать полный набор через мост: 2.5 минуты вместо 6-7,
    когда нужна скорость, а не финальная верность.

    PlayMode не идёт через мост никогда: в живом редакторе прогон виснет на
    входе в play mode, а эталоны скриншотов сняты холодным batch.
#>
function Invoke-Tests {
    $cold = ($Platform -eq 'PlayMode') -or (-not $Filter -and -not $Live)

    if ($cold) {
        # Демон после холодного прогона НЕ поднимаем: следующая команда, которой
        # он нужен, поднимет его сама. Иначе связка `-RunTests -RunPlayMode`
        # платила бы за два лишних старта редактора между прогонами.
        Stop-Editor | Out-Null
        return (Invoke-ColdTests)
    }

    $viaBridge = Resolve-Bridge

    if ($viaBridge) {
        Sync-Editor
        $reply = Send-Bridge -Line "RUN $Platform|$Filter|$ResultPath"
        if (-not $reply -or $reply.StartsWith('ERR')) { throw "Мост отказал: $reply" }
        $jobId = $reply.Substring(3).Trim()

        # STATUS отвечает плоско: state|total|passed|failed|error
        $parts = Wait-Job -JobId $jobId
        $state = $parts[0]
        $total = $parts[1]; $passed = $parts[2]; $failed = $parts[3]
    }
    else {
        return (Invoke-ColdTests)
    }

    Write-Host ("  Total: {0} | Passed: {1} | Failed: {2}" -f $total, $passed, $failed)
    if ($state -eq 'failed') {
        Show-Failures
        return 1
    }
    return 0
}

<#  Старый путь: свой процесс Unity на прогон. #>
function Invoke-ColdTests {
    $unityArgs = @(
        '-runTests', '-batchMode', '-projectPath', $repo,
        '-testResults', $ResultPath, '-testPlatform', $Platform,
        '-logFile', $log
    )
    if ($Filter) { $unityArgs += @('-testFilter', $Filter) }
    Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait | Out-Null
    if (-not (Test-Path $ResultPath)) { throw "Нет отчёта: $ResultPath (лог: $log)" }

    $x = [xml](Get-Content $ResultPath)
    $total = $x.'test-run'.total; $passed = $x.'test-run'.passed; $failed = $x.'test-run'.failed

    Write-Host ("  Total: {0} | Passed: {1} | Failed: {2}" -f $total, $passed, $failed)
    if ([int]$failed -gt 0) {
        Show-Failures
        return 1
    }
    return 0
}

function Show-Failures {
    if (-not (Test-Path $ResultPath)) { return }
    $x = [xml](Get-Content $ResultPath)
    $x.SelectNodes('//test-case') | Where-Object { $_.result -ne 'Passed' } | Select-Object -First 10 |
        ForEach-Object {
            Write-Host ("  [{0}] {1}" -f $_.result, $_.fullname) -ForegroundColor Red
            $msg = $_.failure.message.'#cdata-section'
            if ($msg) { ($msg -split "`n" | Select-Object -First 3) | ForEach-Object { Write-Host "      $_" } }
        }
}

function Invoke-Method {
    if (-not $Method) { throw 'Нужен -Method (например BuildProject.Build)' }

    if (Resolve-Bridge) {
        $reply = Send-Bridge -Line "METHOD $Method"
        if (-not $reply -or $reply.StartsWith('ERR')) { throw "Мост отказал: $reply" }
        $parts = Wait-Job -JobId $reply.Substring(3).Trim()
        if ($parts[0] -ne 'done') { throw "Метод $Method упал: $($parts[-1])" }
        return 0
    }

    $p = Start-Process -FilePath $unity -PassThru -Wait -ArgumentList @(
        '-quit', '-batchMode', '-projectPath', $repo,
        '-executeMethod', $Method, '-logFile', $log
    )
    if ($p.ExitCode -ne 0) { throw "Метод $Method упал (код $($p.ExitCode)), лог: $log" }
    return 0
}

switch ($Command) {
    'tests'  { exit (Invoke-Tests) }
    'method' { exit (Invoke-Method) }
    'start'  { if (Test-EditorRunning) { Write-Host 'редактор уже открыт'; Wait-Bridge | Out-Null } else { Start-Daemon | Out-Null }; exit 0 }
    'stop'   { if (Test-Bridge) { Send-Bridge -Line 'QUIT' | Out-Null; Write-Host 'редактор закрывается' } else { Write-Host 'редактор не открыт' }; exit 0 }
    'status' {
        $reply = Send-Bridge -Line 'PING' -TimeoutMs 1500
        if ($reply) { Write-Host "мост: $reply" } else { Write-Host 'мост не отвечает — команды пойдут холодным batch' }
        exit 0
    }
}
