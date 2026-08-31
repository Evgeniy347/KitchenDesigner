<#
.SYNOPSIS
    Единая точка входа в Unity: КАЖДЫЙ вызов — свой холодный batch-процесс.

.DESCRIPTION
    Правило одно для ВСЕХ скриптов проекта: команда к Unity запускает новый
    `Unity.exe -batchMode`, дожидается его выхода и разбирает отчёт. После
    команды процессов Unity не остаётся.

    Долгоживущий фоновый редактор («демон», мост на сокете) здесь БЫЛ и УБРАН.
    В теории он экономил фиксированные 60–90 секунд старта на каждом прогоне.
    На практике он постоянно отваливался: молчащий на domain reload сокет
    неотличим от зависшего, два клиента дрались за один редактор, зелёный
    прогон мог уехать по коду, скомпилированному до правки, а диагностика
    «жив ли он» стоила больше, чем сэкономленный старт. Холодный запуск
    медленнее, но воспроизводим: одно состояние вместо трёх, и никакого
    «почему-то в этот раз не так».

    ОДИН клиент за раз НА ВСЮ МАШИНУ: команды ждут именованный мьютекс
    `Local\kd-unity-gateway`, а потом берут файловый замок
    `Library\kd-unity-gateway.lock`. Проект Unity держит эксклюзивно, поэтому
    второй batch поверх первого просто не стартует — очередь честнее гонки.

.PARAMETER Command
    tests   — прогон тестов (-Platform, -Filter)
    method  — статический метод редактора (-Method), например BuildProject.Build
    status  — есть ли на проекте живой Unity и свободен ли шлюз
    stop    — снять Unity, зависший на этом проекте (аварийная кнопка)

.EXAMPLE
    .\tools\unity.ps1 tests -Platform EditMode -Filter SnapMutationTests
    .\tools\unity.ps1 tests -Platform EditMode          # полный набор
    .\tools\unity.ps1 tests -Platform PlayMode
    .\tools\unity.ps1 method -Method BuildProject.Build -LogSuffix win
    .\tools\unity.ps1 status
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('tests', 'method', 'status', 'stop')]
    [string]$Command = 'status',

    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode',

    [string]$Filter = '',

    [string]$Method = '',
    [string]$ResultPath = '',
    [string]$LogSuffix = '',

    # Сколько ждать МОЛЧАЩИЙ лог, прежде чем счесть batch зависшим. Работающий
    # Unity пишет строку на каждый шаг (импорт, компиляция, domain reload,
    # тест); молчание в течение минут — это зависание, а не «долго».
    [int]$SilenceMinutes = 5,

    # Общий потолок на прогон. Полный холодный EditMode — единицы минут,
    # PlayMode — минуты; полчаса это уже авария, а не «долго».
    [int]$TimeoutMinutes = 30,

    # Сколько ждать честного выхода ПОСЛЕ того, как Unity написал в лог
    # «Cleanup mono». К этому моменту работа сделана и отчёт лежит на диске;
    # процесс, не ушедший за это время, висит впустую. Замерено: из 24
    # прогонов 23 выходят за 0,6–1,7 с, а один простоял 2675 с.
    #
    # ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНО (0). Снятие живого Unity опирается на догадку,
    # что после «Cleanup mono» Library уже согласована, а это ещё не
    # доказано: если снятый процесс оставляет её грязной, следующий прогон
    # платит переимпортом. Включать числом секунд (например 20) — и только
    # после того, как замер «прогон после снятия против обычного» покажет,
    # что переимпорта нет.
    [int]$DoneGraceSeconds = 0
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe'
if (-not $ResultPath) { $ResultPath = Join-Path $repo 'test-results\tmp\TestResults.xml' }
$logBase = Join-Path $env:TEMP 'build-kitchen.log'
$log = if ($LogSuffix) { "$logBase.$LogSuffix.log" } else { $logBase }

if (-not (Test-Path $unity)) { throw "Unity не найден: $unity" }

# ── Очередь за проектом ─────────────────────────────────────────────────

<#
    ОДИН клиент за раз. Второй ЖДЁТ.

    Проект Unity держит эксклюзивно (`Library`), поэтому два batch-прогона
    одновременно невозможны физически: второй либо упадёт, либо будет долго
    ломиться в занятый Library. Замок превращает это в понятную очередь и
    печатает, кто её держит, — иначе чужой прогон выглядит как «у меня всё
    тормозит без причины».

    Замок — файл, открытый эксклюзивно НА ЗАПИСЬ, но доступный на ЧТЕНИЕ:
    держатель пишет туда, кто он и чем занят. Умер держатель — ОС отпускает
    дескриптор сама, залежавшихся замков не бывает.
#>
$lockPath = Join-Path $repo 'Library\kd-unity-gateway.lock'
$script:lockHandle = $null
$script:machineGate = $null

<#
    Очередь МАШИННАЯ, а не проектная.

    Файловый замок лежит внутри `Library` конкретного проекта, поэтому два
    Unity из РАЗНЫХ проектов замков друг друга не видят — и дерутся за то,
    что на машине одно. Клиент лицензирования (`Unity.Licensing.Client`,
    канал `LicenseClient-<user>`) один на пользователя: проигравший уходит в
    вечный цикл «connection lost → Timed-out after 60.01s» и не выходит сам.
    Процессор и диск тоже общие: прогон, попавший на соседний Unity, показал
    `CompileScripts: 93798 ms` вместо обычной секунды.

    Поэтому ждём именованный мьютекс, общий для всей машины, и только потом
    берём файловый замок. Порядок «сначала машинный, потом проектный» —
    единственный, при котором два клиента на разных проектах не могут
    заблокировать друг друга насмерть.

    Роли разные, поэтому нужны оба: мьютекс решает, КТО едет, файл отвечает
    на вопрос, КТО ДЕРЖИТ (мьютекс имени держателя не хранит).

    `Local\` вместо `Global\`: глобальное пространство имён нужно только для
    разных сеансов и служб и требует прав, а у нас несколько процессов одного
    пользователя.
#>
$machineGateName = 'Local\kd-unity-gateway'

function Read-GateHolder {
    try {
        $fs = [IO.File]::Open($lockPath, 'Open', 'Read', 'ReadWrite')
        try { return (New-Object IO.StreamReader($fs)).ReadToEnd().Trim() }
        finally { $fs.Dispose() }
    } catch { return '(кто — неизвестно)' }
}

function Enter-Gate {
    param([int]$WaitMinutes = 60)

    $script:machineGate = New-Object Threading.Mutex($false, $machineGateName)
    $got = $false
    try {
        $got = $script:machineGate.WaitOne([TimeSpan]::FromMinutes($WaitMinutes))
    }
    catch [Threading.AbandonedMutexException] {
        # Владелец умер, не отпустив очередь. Исключение при этом ОТДАЁТ
        # владение — работаем дальше как при обычном захвате.
        #
        # Сообщения отсюда не ждать. Замерено: убитый `Stop-Process -Force`
        # держатель в 4 прогонах из 4 не породил этого исключения вовсе —
        # следующий `WaitOne` просто вернул True. Ветка остаётся страховкой
        # на случай смерти ПОТОКА при живом процессе; расхождение с
        # документированным WAIT_ABANDONED не разобрано.
        $got = $true
    }
    if (-not $got) {
        throw "Не дождался очереди за $WaitMinutes мин: Unity занят другим прогоном на этой машине. Кто на этом проекте: $(Read-GateHolder)"
    }

    Enter-ProjectLock -WaitMinutes $WaitMinutes
}

function Enter-ProjectLock {
    param([int]$WaitMinutes = 60)

    $deadline = (Get-Date).AddMinutes($WaitMinutes)
    $announced = $false
    while ($true) {
        try {
            $fs = [IO.File]::Open($lockPath, 'Create', 'Write', 'Read')
            $who = "pid=$PID cmd=$Command $Platform $Filter начал=$(Get-Date -Format 'HH:mm:ss')"
            $bytes = [Text.Encoding]::UTF8.GetBytes($who)
            $fs.Write($bytes, 0, $bytes.Length)
            $fs.Flush()
            $script:lockHandle = $fs
            if ($announced) { Write-Host '  очередь подошла' -ForegroundColor Green }
            return
        }
        catch [IO.IOException] {
            if (-not $announced) {
                Write-Host "  проект занят другим клиентом: $(Read-GateHolder)" -ForegroundColor DarkYellow
                Write-Host '  жду очереди (Unity держит проект эксклюзивно)' -ForegroundColor DarkGray
                $announced = $true
            }
            if ((Get-Date) -gt $deadline) {
                throw "Не дождался очереди за $WaitMinutes мин. Держит: $(Read-GateHolder)"
            }
            Start-Sleep -Seconds 2
        }
    }
}

function Exit-Gate {
    if ($script:lockHandle) { $script:lockHandle.Dispose(); $script:lockHandle = $null }
    if ($script:machineGate) {
        # Мьютекс принадлежит ЗАХВАТИВШЕМУ ПОТОКУ, освобождать его можно
        # только оттуда же. Проверено: в этом скрипте `try` и `finally` идут
        # на одном потоке. А если процесс умрёт, не дойдя сюда, мьютекс
        # отпустит ОС — тоже проверено.
        try { $script:machineGate.ReleaseMutex() } catch { }
        $script:machineGate.Dispose()
        $script:machineGate = $null
    }
}

# ── Свободен ли проект ──────────────────────────────────────────────────

<#
    Холодному batch нужен проект целиком: Unity держит `Library` эксклюзивно.
    Поэтому перед стартом двор должен быть пуст.

    Различаем два случая. Редактор из Hub (GUI) закрывать за пользователя
    нельзя — там несохранённая сцена; говорим и останавливаемся. Осиротевший
    batch (остался от убитого скрипта) не отпустит Library сам и превращает
    каждую следующую команду в многоминутное ожидание — такой снимаем.
#>
<#
    Отбор идёт по НОРМАЛИЗОВАННОМУ пути, и это не косметика.

    Unity приводит путь проекта к прямым слэшам у себя внутри и в таком виде
    передаёт его дочерним процессам: `AssetImportWorker` получает
    `-projectPath F:/repos/.../kd-repose`, тогда как $repo здесь —
    `F:\repos\...\kd-repose`. Сравнение как есть не совпадает никогда,
    поэтому воркер для шлюза НЕВИДИМ: осиротевший импортёр не будет ни снят,
    ни показан в `status`, а `Library` он держит наравне с главным
    процессом — ровно тот случай многоминутных ожиданий, ради которого
    Stop-StrayUnity и написан.

    Воркер выделен отдельным Kind: снимать его вместе с сиротами правильно,
    но в `status` он должен называться своим именем, иначе «два batch на
    проекте» выглядит загадкой.
#>
function Get-UnityProcesses {
    $repoNorm = $repo -replace '\\', '/'
    $found = @()
    foreach ($p in (Get-Process Unity -ErrorAction SilentlyContinue)) {
        try {
            $cmdline = (Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)").CommandLine
            if (-not $cmdline) { continue }
            $norm = $cmdline -replace '\\', '/'
            if ($norm -notmatch [regex]::Escape($repoNorm)) { continue }
            $kind = if ($cmdline -match '-name\s+AssetImportWorker') { 'worker' }
                    elseif ($cmdline -match '-batchMode') { 'batch' }
                    else { 'gui' }
            $found += [pscustomobject]@{ Id = $p.Id; Kind = $kind }
        } catch { }
    }
    return $found
}

<#
    Открытый редактор из Hub — приговор прогону, и узнать об этом надо ДО
    очереди, а не после. Отстоять двадцать минут в очереди, чтобы услышать
    «закройте редактор», — худший из возможных порядков.

    Убивать здесь ничего нельзя: замок ещё не взят, и живой batch на этом
    проекте может быть ЧУЖИМ ИДУЩИМ прогоном, а не сиротой. Поэтому до
    очереди — только диагноз по GUI, а уборка сирот остаётся в
    Stop-StrayUnity, под замком, где чужих прогонов уже не бывает.
#>
function Assert-NoEditorGui {
    if (@(Get-UnityProcesses | Where-Object { $_.Kind -eq 'gui' }).Count -gt 0) {
        throw 'Открыт редактор Unity из Hub, а холодному прогону нужен эксклюзивный проект. Закройте его вручную'
    }
}

function Stop-StrayUnity {
    $procs = @(Get-UnityProcesses)
    if ($procs.Count -eq 0) { return }

    if (@($procs | Where-Object { $_.Kind -eq 'gui' }).Count -gt 0) {
        throw 'Открыт редактор Unity из Hub, а холодному прогону нужен эксклюзивный проект. Закройте его вручную'
    }

    foreach ($p in $procs) {
        Write-Host "  снимаю осиротевший batch Unity (pid $($p.Id))" -ForegroundColor DarkYellow
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    }
    # Дать ОС отпустить лок Library, иначе новый процесс упрётся в него.
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline -and (@(Get-UnityProcesses).Count -gt 0)) { Start-Sleep -Seconds 1 }
    if (@(Get-UnityProcesses).Count -gt 0) { throw 'Не удалось освободить проект от Unity' }
}

# ── Запуск batch ────────────────────────────────────────────────────────

<#
    Запустить Unity и дождаться его — но не «пока процесс жив».

    Так однажды ушло 22 минуты: тесты отработали за 228 секунд, а процесс завис
    ПОСЛЕ прогона (послепрогонный Undo плюс бесконечный 404 от лицензионного
    клиента). Живой процесс — не признак прогресса.

    Признак прогресса — РАСТУЩИЙ ЛОГ. Работающий Unity пишет строку на каждый
    шаг: импорт ассета, компиляция, domain reload, результат теста. Зависший не
    пишет ничего. Лог молчит $SilenceMinutes подряд → снимаем и говорим вслух,
    а не досиживаем до общего тайм-аута.

    Процесс снимается в finally: убитый скрипт не должен оставлять
    осиротевший Unity, держащий Library.
#>
<#
    Пульс лога: что в нём изменилось ОСМЫСЛЕННОГО.

    Размер файла как признак прогресса врёт. Потеряв клиент лицензирования,
    Unity пишет раз в минуту новую порцию ошибок — файл растёт, работа стоит,
    сторож по размеру считает это прогрессом и досиживает до общего тайм-аута.
    Так ушло больше 35 минут в одном замере.

    Поэтому смотрим на хвост (последние 32 КБ, а не файл целиком: он бывает
    мегабайтным, а опрос идёт раз в две секунды) и выбрасываем строки, которые
    Unity печатает не работая.

    `Beat` = число осмысленных строк в окне + последняя из них. Ловит молчание
    и повтор одной строки: окно фиксированное, поэтому счётчик упирается в
    потолок и замирает. НЕ ловит цикл из нескольких РАЗНЫХ строк — для лога он
    неотличим от работы. Против такого есть только -TimeoutMinutes, и это
    честный ответ: «зациклился» и «делает много мелкого» неразличимы в
    принципе.
#>
$script:LogNoise = '\[Licensing::|GetVirtualKey:'

function Get-LogPulse {
    param([string]$Path)

    try {
        $fs = [IO.File]::Open($Path, 'Open', 'Read', 'ReadWrite')
        try {
            if ($fs.Length -le 0) { return $null }
            $take = [int][Math]::Min($fs.Length, 32768)
            $fs.Seek(-$take, 'End') | Out-Null
            $buf = New-Object byte[] $take
            $null = $fs.Read($buf, 0, $take)
            $tail = [Text.Encoding]::UTF8.GetString($buf)
        }
        finally { $fs.Dispose() }
    }
    catch { return $null }

    $lines = @($tail -split "`n" | Where-Object { $_ -notmatch $script:LogNoise })
    [pscustomobject]@{
        Beat    = "$($lines.Count):$($lines[-1])"
        Done    = ($tail -match 'Cleanup mono')
        License = ($tail -match 'waiting for channel')
    }
}

function Invoke-Unity {
    param([string[]]$UnityArgs, [string]$LogPath, [switch]$KillAfterDone)

    # Лог Unity открывает на ПЕРЕзапись, поэтому стартовое «молчание» надо
    # мерить от несуществующего файла, а не от старого.
    Remove-Item $LogPath -Force -ErrorAction SilentlyContinue

    $proc = $null
    try {
        $proc = Start-Process -FilePath $unity -ArgumentList $UnityArgs -PassThru

        $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
        $lastProgress = Get-Date
        $lastBeat = ''
        $doneSince = $null
        $saidLicense = $false
        while (-not $proc.HasExited) {
            Start-Sleep -Seconds 2
            if ($proc.HasExited) { break }

            $pulse = Get-LogPulse -Path $LogPath
            if ($pulse) {
                if ($pulse.Beat -ne $lastBeat) {
                    $lastBeat = $pulse.Beat
                    $lastProgress = Get-Date
                }

                if ($pulse.License -and -not $saidLicense) {
                    $saidLicense = $true
                    Write-Host '  Unity потерял клиент лицензирования — на машине запущен второй Unity' -ForegroundColor Yellow
                    Write-Host '  что делать: закройте редактор Unity из Hub — либо ничего, прогон встанет в очередь сам' -ForegroundColor Yellow
                    Write-Host '  посмотреть, кто держит проект: .\tools\unity.ps1 status' -ForegroundColor Yellow
                }

                # «Cleanup mono» — работа кончилась: отчёт записан, Library
                # сброшена. Дальше процесс либо честно выйдет за секунду, либо
                # не выйдет вообще (замерено: 2675 с простоя на пустом
                # проекте). Ждать столько незачем.
                if ($KillAfterDone -and $DoneGraceSeconds -gt 0 -and -not $doneSince -and $pulse.Done) { $doneSince = Get-Date }
            }

            if ($doneSince -and ((Get-Date) - $doneSince).TotalSeconds -gt $DoneGraceSeconds) {
                Write-Host "  работа закончена, но процесс не вышел за $DoneGraceSeconds с — снимаю" -ForegroundColor DarkYellow
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                return 0
            }
            if (((Get-Date) - $lastProgress).TotalMinutes -gt $SilenceMinutes) {
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                throw "Лог не растёт $SilenceMinutes мин — Unity завис (снят). Лог: $LogPath"
            }
            if ((Get-Date) -gt $deadline) {
                Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                throw "Прогон не завершился за $TimeoutMinutes мин (снят). Лог: $LogPath"
            }
        }
        return $proc.ExitCode
    }
    finally {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

# ── Тесты ───────────────────────────────────────────────────────────────

<#
    Классы, которые не ПРОВЕРЯЮТ, а ЗАМЕРЯЮТ и РИСУЮТ.

    Тест сравнивает с эталоном и краснеет — он говорит «сломалось». Эти
    порождают файл: GIF анимации ящика, обзорный кадр, кадр зазоров, CSV
    профилировщика. Сломаться они не могут, могут только перезаписать docs/ —
    и перезаписывают каждый раз, отчего docs/*.png вечно грязные в git status.

    Цена — 160 секунд из 228 у всего набора PlayMode: две трети времени цикла
    «правка → проверка» уходило на рисование картинок, которые в этот момент
    никто не смотрит. И ускорить их нельзя, не обесценив: профиль на тридцати
    кадрах не профиль.

    Отсекает их сам NUnit: классы помечены `[Explicit]`, а такие тесты не едут,
    пока фильтр не назовёт их прямо. Регуляркой-исключением это НЕ решается —
    NUnit пускает всех потомков узла, прошедшего фильтр, поэтому
    `^(?!.*PerfProfileTests).*$` отсекает класс, но узел сборки проходит, и
    класс всё равно едет (проверено: генераторы отработали вместе с набором).

    Список ниже нужен только для СТРОКИ В ВЫВОДЕ — чтобы пропуск не был
    молчаливым — и должен совпадать с tools/artifacts.ps1.
#>
$script:GeneratorSuites = @(
    'PerfProfileTests',
    'DrawerAnimationGifTests',
    'OverviewScreenshotTests',
    'GapsScreenshotTests'
)

function Get-DefaultFilter {
    if ($Filter) { return $Filter }                       # спросили прицельно — отдаём как есть
    if ($Platform -ne 'PlayMode') { return '' }

    # Флага «и генераторы тоже» здесь нет намеренно: [Explicit] едет только по
    # прямому имени, поэтому «всё сразу» — это два разных прогона, а не один
    # фильтр. Второй прогон и есть tools\artifacts.ps1.
    Write-Host "  генераторы артефактов пропущены — tools\artifacts.ps1: $($script:GeneratorSuites -join ', ')" -ForegroundColor DarkGray
    return ''
}

<#
    Сколько это заняло — вслух, всегда.

    Цикл «правка → проверка» разъезжается незаметно: чужой клиент в очереди,
    разросшийся набор, лишний импорт — каждый добавляет минуты, и ни один себя
    не называет. Бюджет назван явно; всё, что дольше, печатается отдельной
    строкой, чтобы деградацию замечали сразу, а не через неделю «почему-то всё
    стало медленно».

    Бюджеты — ХОЛОДНЫЕ и ЗАМЕРЕННЫЕ: каждый прогон платит фиксированные 60–90 с
    старта (лицензия, Asset Pipeline Refresh, три domain reload) ДО первого
    теста, и они входят в цифры ниже. Замеры: прицельный класс 22 с, полный
    EditMode (2237 тестов) 65 с, полный PlayMode (75 тестов) 100 с. Бюджет —
    примерно полуторный запас к замеру: срабатывать он должен на деградации,
    а не на шуме.
#>
function Get-BudgetSeconds {
    if ($Command -eq 'method') { return 300 }             # сборка плеера
    if ($Filter) { return 60 }                            # прицельный прогон
    if ($Platform -eq 'PlayMode') { return 180 }
    return 120                                            # полный EditMode
}

function Report-Time {
    param([Diagnostics.Stopwatch]$Sw)
    $s = $Sw.Elapsed.TotalSeconds
    $budget = Get-BudgetSeconds
    if ($s -gt $budget) {
        Write-Host ("  прогон занял {0:N0} с — БОЛЬШЕ бюджета в {1} с" -f $s, $budget) -ForegroundColor Yellow
    } else {
        Write-Host ("  прогон занял {0:N1} с" -f $s) -ForegroundColor DarkGray
    }
}

function Invoke-Tests {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try { return (Invoke-TestsCore) } finally { Report-Time $sw }
}

function Invoke-TestsCore {
    Stop-StrayUnity

    $dir = Split-Path -Parent $ResultPath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Remove-Item $ResultPath -Force -ErrorAction SilentlyContinue

    $effectiveFilter = Get-DefaultFilter
    $unityArgs = @(
        '-runTests', '-batchMode', '-projectPath', $repo,
        '-testResults', $ResultPath, '-testPlatform', $Platform,
        '-logFile', $log
    )
    if ($effectiveFilter) { $unityArgs += @('-testFilter', $effectiveFilter) }

    Write-Host "=== $Platform (холодный batch) ===" -ForegroundColor Cyan
    Invoke-Unity -UnityArgs $unityArgs -LogPath $log -KillAfterDone | Out-Null

    if (-not (Test-Path $ResultPath)) { throw "Нет отчёта: $ResultPath (лог: $log)" }

    $x = [xml](Get-Content $ResultPath)
    $total = $x.'test-run'.total; $passed = $x.'test-run'.passed; $failed = $x.'test-run'.failed

    Write-Host ("  Total: {0} | Passed: {1} | Failed: {2}" -f $total, $passed, $failed)
    Show-Slowest
    Remove-TestSceneJunk

    # Ноль тестов — это НЕ успех.
    #
    # `-Filter SnapCoreTests` (класса с таким именем в проекте нет) давал
    # «Total: 0 | Failed: 0» и зелёный код возврата. То есть опечатка в фильтре
    # выглядела как прошедшая проверка — худший вид лжи, какой умеет набор
    # тестов.
    if ([int]$total -eq 0) {
        Write-Host "  ФИЛЬТР НЕ ПОЙМАЛ НИ ОДНОГО ТЕСТА: '$effectiveFilter'" -ForegroundColor Red
        return 1
    }

    if ([int]$failed -gt 0) {
        Show-Failures
        return 1
    }
    return 0
}

<#
    Куда ушло время прогона.

    Отчёт NUnit хранит длительность каждого узла; печатаем пять худших
    классов. Набор дешевеет не там, где кажется: обычно две-три сцены
    (кодирование GIF, профилирование) съедают больше, чем все остальные тесты
    вместе.
#>
function Show-Slowest {
    if (-not (Test-Path $ResultPath)) { return }
    $x = [xml](Get-Content $ResultPath)
    $rows = @($x.SelectNodes('//test-suite[@type="TestFixture"]') |
        ForEach-Object {
            [pscustomobject]@{
                Name    = $_.name
                Seconds = [double]::Parse($_.duration, [Globalization.CultureInfo]::InvariantCulture)
            }
        } |
        Sort-Object Seconds -Descending | Select-Object -First 5)
    if ($rows.Count -eq 0) { return }
    Write-Host '  дольше всех:' -ForegroundColor DarkGray
    foreach ($r in $rows) {
        Write-Host ("    {0,6:N1} с  {1}" -f $r.Seconds, $r.Name) -ForegroundColor DarkGray
    }
}

<#
    Тест-раннер PlayMode кладёт временную сцену прямо в Assets/
    (`InitTestScene<guid>.unity`) и убирает её сам — но только если прогон
    дошёл до конца. Оборванный прогон оставляет её лежать, а каждая такая
    сцена — лишний ассет, который следующий импорт обработает заново.
    Копятся они молча.
#>
function Remove-TestSceneJunk {
    Get-ChildItem (Join-Path $repo 'Assets') -Filter 'InitTestScene*.unity*' -ErrorAction SilentlyContinue |
        ForEach-Object {
            Write-Host "  убираю временную сцену $($_.Name)" -ForegroundColor DarkGray
            Remove-Item $_.FullName -ErrorAction SilentlyContinue
        }
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

# ── Метод редактора ─────────────────────────────────────────────────────

function Invoke-Method {
    if (-not $Method) { throw 'Нужен -Method (например BuildProject.Build)' }

    $sw = [Diagnostics.Stopwatch]::StartNew()
    try {
        Stop-StrayUnity
        Write-Host "=== $Method (холодный batch) ===" -ForegroundColor Cyan
        $code = Invoke-Unity -LogPath $log -UnityArgs @(
            '-quit', '-batchMode', '-projectPath', $repo,
            '-executeMethod', $Method, '-logFile', $log
        )
        if ($code -ne 0) { throw "Метод $Method упал (код $code), лог: $log" }
        return 0
    }
    finally { Report-Time $sw }
}

# ── Точка входа ─────────────────────────────────────────────────────────

# `status` намеренно БЕЗ замка: спросить «занят ли проект» должно быть можно и
# посреди чужого прогона — иначе диагностика встаёт в ту же очередь, которую и
# пришла разглядывать.
if ($Command -in @('tests', 'method')) { Assert-NoEditorGui }
if ($Command -ne 'status') { Enter-Gate }

try {
switch ($Command) {
    'tests'  { exit (Invoke-Tests) }
    'method' { exit (Invoke-Method) }
    'stop'   {
        $procs = @(Get-UnityProcesses)
        if ($procs.Count -eq 0) { Write-Host 'Unity на этом проекте не запущен'; exit 0 }
        foreach ($p in $procs) {
            Write-Host "  снимаю Unity (pid $($p.Id), $($p.Kind))" -ForegroundColor DarkYellow
            Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
        }
        exit 0
    }
    'status' {
        $procs = @(Get-UnityProcesses)
        if ($procs.Count -eq 0) {
            Write-Host 'проект свободен — каждая команда поднимает свой batch и закрывает его за собой'
        }
        else {
            foreach ($p in $procs) {
                Write-Host "Unity держит проект: pid $($p.Id) ($($p.Kind))" -ForegroundColor Yellow
            }
            Write-Host '  gui   — открытый редактор из Hub, закройте его вручную перед прогоном'
            Write-Host '  batch — идёт прогон, либо сирота от убитого скрипта: .\tools\unity.ps1 stop'
            Write-Host '  worker — AssetImportWorker, дочерний импортёр Unity; свой уходит сам, осиротевший держит Library'
        }

        # Кто сейчас в очереди за проектом. Без этой строки чужой прогон
        # выглядит как «у меня всё тормозит без причины».
        try {
            $probe = [IO.File]::Open($lockPath, 'OpenOrCreate', 'Write', 'Read')
            $probe.Dispose()
            Write-Host 'шлюз свободен'
        } catch {
            Write-Host "шлюз ЗАНЯТ: $(Read-GateHolder)" -ForegroundColor Yellow
        }
        exit 0
    }
}
}
finally { Exit-Gate }
