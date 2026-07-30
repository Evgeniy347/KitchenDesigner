<#
.SYNOPSIS
    Единая точка входа в Unity: ОДИН фоновый редактор, который живёт между вызовами.

.DESCRIPTION
    Правило одно для ВСЕХ скриптов проекта:

      редактор жив     → команда уходит в него через мост (Assets/Editor/EditorBridge.cs)
      редактора нет    → он поднимается ОДИН раз и остаётся жить дальше

    Холодного прогона по умолчанию больше нет. Он платил фиксированные 60–90
    секунд (лицензия, Asset Pipeline Refresh ~12 с, три domain reload) ДО первого
    теста, и платил их КАЖДЫЙ раз. Через мост платится только компиляция
    изменённых скриптов и сами тесты.

    Никакая команда не закрывает редактор сама. Закрыть его можно только явно:
    `stop` или `restart`.

    PlayMode тоже идёт через мост. Раньше он считался «навсегда холодным» —
    якобы виснет на входе в play mode. Виснул не play mode, а диагностика:
    `STATUS` исполнялся в главном потоке, занятом прогоном, клиент не получал
    ответа и убивал редактор сам. С неблокирующим `PING`/`STATUS` полный
    IntegrationPlayModeTests в живом демоне занимает 9.7 с против 3.5 минут
    холодным batch.

    ОДИН клиент за раз: команды берут файловый замок `Library\
    kd-unity-gateway.lock`. Второй клиент ЖДЁТ и печатает, кто держит очередь.
    Без замка два агента на одном проекте убивали редактор друг у друга и
    зацикливались на холодных стартах.

    Чем это оплачено: полный EditMode через мост чуть менее «свежий», чем в новом
    процессе (шаг SmoothDamp считается от Time.deltaTime, атлас шрифта переживает
    прогон). Если тест разошёлся именно на этом — перепроверьте его `-Cold`.

.PARAMETER Command
    tests   — прогон тестов (-Platform, -Filter)
    method  — статический метод редактора (-Method), например BuildProject.Build
    start   — поднять фоновый редактор и дождаться готовности
    stop    — попросить живой редактор закрыться
    restart — убить всё, что держит проект, и поднять чистого демона
    status  — жив ли редактор и чем занят

.EXAMPLE
    .\tools\unity.ps1 tests -Platform EditMode -Filter SnapCoreTests
    .\tools\unity.ps1 tests -Platform EditMode          # полный набор, тоже через мост
    .\tools\unity.ps1 tests -Platform PlayMode
    .\tools\unity.ps1 method -Method BuildProject.Build -LogSuffix win
    .\tools\unity.ps1 restart                            # если редактор завис
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('tests', 'method', 'start', 'stop', 'restart', 'status')]
    [string]$Command = 'status',

    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode',

    [string]$Filter = '',

    [string]$Method = '',
    [string]$ResultPath = '',
    [string]$LogSuffix = '',

    # Явный холодный batch: свой процесс Unity на один прогон. Нужен, только
    # когда тест разошёлся из-за несвежести живого редактора (шаг SmoothDamp от
    # Time.deltaTime, переживший прогон атлас шрифта). Демон после него
    # поднимается обратно.
    [switch]$Cold,

    # Совместимость со старыми вызовами: раньше -Live означал «полный набор через
    # мост». Теперь это поведение по умолчанию, флаг ничего не меняет.
    [switch]$Live,

    # Сколько ждать молчащий редактор, прежде чем счесть его зависшим и
    # перезапустить. Молчание на domain reload — секунды, а не минуты.
    [int]$SilenceMinutes = 3,

    # Тайм-аут ожидания результата, минут. Сорок минут, стоявшие здесь раньше,
    # сторожем не были: они молча вмещали двадцатидвухминутное зависание.
    # Бюджет прогона — минута; десять минут это уже авария, а не «долго».
    [int]$TimeoutMinutes = 10
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe'
$port = 9338
if (-not $ResultPath) { $ResultPath = Join-Path $repo 'test-results\tmp\TestResults.xml' }
$logBase = Join-Path $env:TEMP 'build-kitchen.log'
$log = if ($LogSuffix) { "$logBase.$LogSuffix.log" } else { $logBase }

if (-not (Test-Path $unity)) { throw "Unity не найден: $unity" }

# ── Очередь за редактором ───────────────────────────────────────────────

<#
    ОДИН клиент за раз. Второй ЖДЁТ, а не убивает редактор.

    Так было до этого замка: два агента (или человек и агент) звали шлюз
    одновременно. Сосед компилирует — мост молчит; `Resolve-Bridge` считает
    молчание зависанием и СНИМАЕТ живой редактор посреди чужого прогона. Тот
    поднимает новый, и теперь уже он ловит молчание. Двое зацикливаются на
    холодных стартах по 60–90 с, и «прогон на 10 секунд» превращается в
    получасовое недоумение с обеих сторон.

    Замок — файл, открытый эксклюзивно НА ЗАПИСЬ, но доступный на ЧТЕНИЕ:
    держатель пишет туда, кто он и чем занят, а очередь это видит и печатает.
    Умер держатель — ОС отпускает дескриптор сама, залежавшихся замков не
    бывает.
#>
$lockPath = Join-Path $repo 'Library\kd-unity-gateway.lock'
$script:lockHandle = $null

function Read-GateHolder {
    try {
        $fs = [IO.File]::Open($lockPath, 'Open', 'Read', 'ReadWrite')
        try { return (New-Object IO.StreamReader($fs)).ReadToEnd().Trim() }
        finally { $fs.Dispose() }
    } catch { return '(кто — неизвестно)' }
}

function Enter-Gate {
    param([int]$WaitMinutes = 30)

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
                Write-Host "  редактор занят другим клиентом: $(Read-GateHolder)" -ForegroundColor DarkYellow
                Write-Host '  жду очереди (редактор НЕ трогаю — иначе мы убьём прогоны друг друга)' -ForegroundColor DarkGray
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
}

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
function Get-EditorProcesses {
    $found = @()
    foreach ($p in (Get-Process Unity -ErrorAction SilentlyContinue)) {
        try {
            $cmdline = (Get-CimInstance Win32_Process -Filter "ProcessId = $($p.Id)").CommandLine
            if ($cmdline -and $cmdline -match [regex]::Escape($repo)) { $found += $p }
        } catch { }
    }
    return $found
}

function Test-EditorRunning {
    return ((Get-EditorProcesses).Count -gt 0)
}

<#
    Убить ВСЁ, что держит этот проект.

    Нужно ровно для одного случая: процесс жив, но мост не отвечает дольше
    разумного — редактор завис или это осиротевший batch от убитого скрипта.
    Такой процесс не отпустит Library и превращает каждую следующую команду в
    многоминутное ожидание. Мягко его не закрыть: QUIT идёт через тот же мост,
    который и молчит.
#>
function Stop-EditorsForce {
    $procs = Get-EditorProcesses
    if ($procs.Count -eq 0) { return $false }
    foreach ($p in $procs) {
        Write-Host "  снимаю зависший Unity (pid $($p.Id))" -ForegroundColor DarkYellow
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    }
    # Дать ОС отпустить лок Library, иначе новый редактор упрётся в него.
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline -and (Test-EditorRunning)) { Start-Sleep -Seconds 1 }
    return $true
}

<#
    Дождаться, пока редактор снова начнёт отвечать (после перекомпиляции).

    Тайм-аут короткий НАМЕРЕННО. Молчание на domain reload длится секунды;
    молчание в течение минут означает не «занят», а «завис» — и ждать его
    десять минут (как было раньше) хуже, чем перезапустить.
#>
<#
    Признак жизни, когда мост ответить НЕ МОЖЕТ.

    На domain reload управляемого кода нет вообще: сокет закрыт, отвечать некому.
    Поэтому «нет ответа» само по себе не означает ни «работает», ни «завис» — а
    сторож по одному молчанию сокета ошибался в дорогую сторону. Один такой
    случай: правка тестового .cs, пересборка сборки на 2000 тестов идёт минуты,
    сокет молчит законно — сторож счёл это зависанием, снял редактор и заставил
    прогон начаться заново. 376 секунд вместо сорока.

    Работающий Unity ПИШЕТ В ЛОГ (импорт ассетов, компиляция, перезагрузка
    домена — каждый шаг оставляет строку). Зависший не пишет. Растущий лог и
    есть тот признак прогресса, которого не хватало.
#>
function Test-LogProgress {
    param([datetime]$Since)
    $daemonLog = "$logBase.daemon.log"
    if (-not (Test-Path $daemonLog)) { return $false }
    return ((Get-Item $daemonLog).LastWriteTime -gt $Since)
}

function Wait-Bridge {
    param([int]$Minutes = 0)
    if ($Minutes -le 0) { $Minutes = $SilenceMinutes }

    $deadline = (Get-Date).AddMinutes($Minutes)
    while ((Get-Date) -lt $deadline) {
        if (Test-Bridge) { return $true }
        if (-not (Test-EditorRunning)) { return $false }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Start-Daemon {
    # Второй Unity на том же проекте — это драка за Library, а не «ещё один
    # воркер». Перед стартом двор должен быть пуст.
    if (Test-EditorRunning) { Stop-EditorsForce | Out-Null }

    # Лог предыдущего демона сохраняем.
    #
    # Unity открывает -logFile на перезапись, поэтому лог УМЕРШЕГО демона
    # затирался логом следующего — а именно он и нужен, чтобы понять, почему
    # тот умер. Один раз это уже стоило целого прогона вслепую.
    $daemonLog = "$logBase.daemon.log"
    if (Test-Path $daemonLog) {
        $stamp = (Get-Item $daemonLog).LastWriteTime.ToString('yyyyMMdd-HHmmss')
        Move-Item $daemonLog "$logBase.daemon.$stamp.log" -Force -ErrorAction SilentlyContinue
        Get-ChildItem "$logBase.daemon.*.log" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -Skip 5 |
            Remove-Item -ErrorAction SilentlyContinue
    }

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

<#
    Ждать задачу — но не бесконечно и не «пока процесс жив».

    Так однажды ушло 22 минуты: тесты отработали за 228 секунд, а редактор
    завис ПОСЛЕ прогона (послепрогонный Undo плюс бесконечный 404 от
    лицензионного клиента). Процесс был жив, поэтому старая проверка «жив ли
    Unity» считала это работой, а единственный настоящий сторож стоял на
    -TimeoutMinutes 40. Живой процесс — не признак прогресса.

    Признак прогресса — ОТВЕТ моста. Молчит дольше -SilenceMinutes подряд →
    это зависание, снимаем и говорим вслух, а не досиживаем до сорока минут.
#>
function Wait-Job {
    param([string]$JobId)

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $lastReply = Get-Date
    $lastProgress = Get-Date
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $reply = Send-Bridge -Line "STATUS $JobId"
        if (-not $reply) {
            # domain reload рвёт сокет — это норма, но она длится секунды.
            if (-not (Test-EditorRunning)) { throw 'Редактор умер во время прогона' }
            if (((Get-Date) - $lastReply).TotalMinutes -gt $SilenceMinutes) {
                # Растущий лог = работает. См. Test-LogProgress.
                if (Test-LogProgress -Since $lastProgress) {
                    $lastProgress = (Get-Item "$logBase.daemon.log").LastWriteTime
                    $lastReply = Get-Date
                    continue
                }
                Stop-EditorsForce | Out-Null
                throw "Мост молчит $SilenceMinutes мин И лог не растёт — редактор завис (снят). Лог: $logBase.daemon.log"
            }
            continue
        }
        $lastReply = Get-Date
        if ($reply.StartsWith('ERR')) { throw $reply }

        $parts = $reply.Substring(3).Split('|')
        $state = $parts[0]
        if ($state -eq 'running') { continue }
        return $parts
    }
    throw "Задача $JobId не завершилась за $TimeoutMinutes мин"
}

# ── Команды ─────────────────────────────────────────────────────────────

<#
    Получить рабочий мост — любой ценой, но НИКОГДА не двумя редакторами сразу.

    Три состояния и три ответа:
      отвечает          → работаем;
      жив, но молчит    → ждём $SilenceMinutes (domain reload — это секунды);
                          не дождались — считаем зависшим, снимаем и поднимаем
                          заново, а не ждём вечно и не ставим второй поверх;
      процесса нет      → поднимаем демона, он останется жить после команды.
#>
function Resolve-Bridge {
    if (Test-Bridge) { return $true }

    if (Test-EditorRunning) {
        Write-Host '  редактор молчит (компиляция или domain reload) — жду' -ForegroundColor DarkGray
        if (Wait-Bridge) { return $true }
        Write-Host "  не ответил за $SilenceMinutes мин — перезапускаю" -ForegroundColor DarkYellow
        Stop-EditorsForce | Out-Null
    }

    return (Start-Daemon)
}

<#
    Подхватить правки в коде ДО прогона.

    Фоновый редактор запущен в batch и за файловой системой не следит: без
    явного Refresh он гоняет тесты по коду, скомпилированному при старте. Это
    не «медленнее», это НЕВЕРНО — зелёный прогон на старом коде хуже красного.
    Ждём конца компиляции: она даёт domain reload, и сокет на это время молчит.

    Меряем НЕПРЕРЫВНОЕ молчание, а не общее время. Компиляция сама по себе может
    идти минуты и при этом мост отвечает «compiling» — это работа. А вот сокет,
    молчащий $SilenceMinutes подряд, означает зависший Refresh: наблюдалось после
    подмены файла скрипта прямо во время обновления ассетов. Возвращаем $false,
    и вызывающий перезапускает редактора вместо десятиминутного ожидания.
#>
function Sync-Editor {
    Send-Bridge -Line 'REFRESH' | Out-Null

    $deadline = (Get-Date).AddMinutes(20)
    $lastReply = Get-Date
    $lastProgress = Get-Date
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $reply = Send-Bridge -Line 'PING' -TimeoutMs 2000
        if (-not $reply) {
            if (-not (Test-EditorRunning)) { return $false }
            if (((Get-Date) - $lastReply).TotalMinutes -gt $SilenceMinutes) {
                # Молчит долго — но пишет ли он в лог? Пишет — значит работает
                # (импорт, компиляция, domain reload), и убивать его нельзя.
                if (Test-LogProgress -Since $lastProgress) {
                    $lastProgress = (Get-Item "$logBase.daemon.log").LastWriteTime
                    $lastReply = Get-Date
                    Write-Host '  мост молчит, но лог растёт — идёт обновление ассетов, жду' -ForegroundColor DarkGray
                    continue
                }
                Write-Host "  мост молчит $SilenceMinutes мин И лог не растёт — это зависание" -ForegroundColor DarkYellow
                return $false
            }
            continue    # идёт domain reload
        }
        $lastReply = Get-Date
        if ($reply -notmatch 'compiling') { return $true }
    }
    Write-Host '  компиляция не закончилась за 20 минут' -ForegroundColor DarkYellow
    return $false
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
        throw 'Открыт редактор Unity из Hub, а холодному прогону нужен эксклюзивный проект. Закройте его вручную'
    }

    Write-Host '  закрываю редактор (нужен холодный batch)' -ForegroundColor DarkGray
    Send-Bridge -Line 'QUIT' | Out-Null

    $deadline = (Get-Date).AddMinutes(2)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        if (-not (Test-EditorRunning)) { return $true }
    }

    # QUIT идёт по мосту, а зависший (или осиротевший) редактор по мосту не
    # отвечает — просить его бесполезно, снимаем.
    Write-Host '  не закрылся по-хорошему' -ForegroundColor DarkYellow
    Stop-EditorsForce | Out-Null
    if (Test-EditorRunning) { throw 'Не удалось освободить проект от Unity' }
    return $true
}

<#
    Куда отправить прогон.

    Через мост идёт ВСЁ, PlayMode в том числе: холодный старт стоит 60–90 секунд
    на каждый вызов, и платить их за каждую проверку — дороже, чем та неполная
    свежесть, которую даёт живой процесс.

    PlayMode раньше считался «навсегда холодным»: в живом редакторе прогон
    якобы виснет на входе в play mode. Это оказалось иллюзией. Виснул не play
    mode, а ДИАГНОСТИКА: мост исполнял `STATUS` в главном потоке, а тот занят
    прогоном, — клиент не получал ответа и через -SilenceMinutes убивал
    редактор. Когда `PING`/`STATUS` стали отвечать из потока сокета, картина
    оказалась ровной: `playing` через 5 с, весь IntegrationPlayModeTests за
    9.7 с против 3.5 минут холодным batch.

    Холодным остался только -Cold: явная перепроверка теста, который разошёлся
    из-за несвежести живого редактора (шаг SmoothDamp от Time.deltaTime,
    переживший прогон атлас шрифта). Демона он возвращает в фон после себя.
#>
function Invoke-Tests {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try { return (Invoke-TestsCore) } finally { Report-Time $sw }
}

<#
    Сколько это заняло — вслух, всегда.

    Цикл «правка → проверка» разъезжается незаметно: холодный старт, чужой
    клиент в очереди, застрявший Refresh — каждый добавляет минуты, и ни один
    себя не называет. Бюджет на прогон — минута; всё, что дольше, печатается
    отдельной строкой, чтобы деградацию замечали сразу, а не через неделю
    «почему-то всё стало медленно».
#>
$script:BudgetSeconds = 60

function Report-Time {
    param([Diagnostics.Stopwatch]$Sw)
    $s = $Sw.Elapsed.TotalSeconds
    if ($s -gt $script:BudgetSeconds) {
        Write-Host ("  прогон занял {0:N0} с — БОЛЬШЕ бюджета в {1} с" -f $s, $script:BudgetSeconds) -ForegroundColor Yellow
    } else {
        Write-Host ("  прогон занял {0:N1} с" -f $s) -ForegroundColor DarkGray
    }
}

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

function Invoke-TestsCore {
    if ($Cold) { return (Invoke-ColdTests) }

    # Одна повторная попытка: зависший на обновлении ассетов редактор чинится
    # перезапуском, а не ожиданием. Второй отказ — уже настоящая проблема.
    foreach ($attempt in 1..2) {
        if (-not (Resolve-Bridge)) { throw 'Не удалось получить рабочий редактор' }
        if (Sync-Editor) { break }
        if ($attempt -eq 2) { throw 'Редактор не отдаёт обновлённые ассеты даже после перезапуска' }
        Write-Host '  перезапускаю редактора и пробую ещё раз' -ForegroundColor DarkYellow
        Stop-EditorsForce | Out-Null
    }

    # Поля через табуляцию: фильтр — регулярка, и '|' в ней разрезал бы строку.
    $reply = Send-Bridge -Line "RUN $Platform`t$(Get-DefaultFilter)`t$ResultPath"
    if (-not $reply -or $reply.StartsWith('ERR')) { throw "Мост отказал: $reply" }
    $jobId = $reply.Substring(3).Trim()

    # STATUS отвечает плоско: state|total|passed|failed|error
    $parts = Wait-Job -JobId $jobId
    $state = $parts[0]
    $total = $parts[1]; $passed = $parts[2]; $failed = $parts[3]

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
        Write-Host "  ФИЛЬТР НЕ ПОЙМАЛ НИ ОДНОГО ТЕСТА: '$(Get-DefaultFilter)'" -ForegroundColor Red
        return 1
    }

    if ($state -eq 'failed') {
        Show-Failures
        return 1
    }
    return 0
}

<#
    Свой процесс Unity на один прогон — для PlayMode и -Cold.

    Две обязанности сверх самого прогона:
      • освободить проект (Stop-Editor) — batch не стартует поверх живого;
      • вернуть демона в фон, чем бы прогон ни кончился. Иначе связка
        `-RunTests -RunPlayMode` оставляла бы после себя пустой двор, и
        следующая команда снова платила бы за холодный старт.

    Процесс запускается с -PassThru и снимается в finally: убитый скрипт не
    должен оставлять осиротевший Unity, держащий Library (именно такой сирота
    превращал каждую следующую команду в многоминутное ожидание).
#>
function Invoke-ColdTests {
    Stop-Editor | Out-Null

    $unityArgs = @(
        '-runTests', '-batchMode', '-projectPath', $repo,
        '-testResults', $ResultPath, '-testPlatform', $Platform,
        '-logFile', $log
    )
    if ($Filter) { $unityArgs += @('-testFilter', $Filter) }

    $proc = $null
    try {
        $proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
        $proc.WaitForExit()
    }
    finally {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
        Start-Daemon | Out-Null
    }

    if (-not (Test-Path $ResultPath)) { throw "Нет отчёта: $ResultPath (лог: $log)" }

    $x = [xml](Get-Content $ResultPath)
    $total = $x.'test-run'.total; $passed = $x.'test-run'.passed; $failed = $x.'test-run'.failed

    Write-Host ("  Total: {0} | Passed: {1} | Failed: {2}" -f $total, $passed, $failed)
    Remove-TestSceneJunk
    if ([int]$total -eq 0) {
        Write-Host "  ФИЛЬТР НЕ ПОЙМАЛ НИ ОДНОГО ТЕСТА: '$Filter'" -ForegroundColor Red
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

    Отчёт хранит длительность каждого класса; печатаем пять худших. Набор
    дешевеет не там, где кажется: обычно две-три сцены (кодирование GIF,
    профилирование) съедают больше, чем все остальные тесты вместе.
#>
function Show-Slowest {
    if (-not (Test-Path $ResultPath)) { return }
    $x = [xml](Get-Content $ResultPath)
    $rows = $x.SelectNodes('//slowest/suite')
    if (-not $rows -or $rows.Count -eq 0) { return }
    Write-Host '  дольше всех:' -ForegroundColor DarkGray
    foreach ($r in $rows) {
        Write-Host ("    {0,6:N1} с  {1}" -f [double]$r.seconds, $r.name) -ForegroundColor DarkGray
    }
}

<#
    Тест-раннер PlayMode кладёт временную сцену прямо в Assets/
    (`InitTestScene<guid>.unity`) и убирает её сам — но только если прогон
    дошёл до конца. Оборванный прогон оставляет её лежать, а каждая такая
    сцена — лишний ассет, который следующий Refresh импортирует заново.
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

# `status` намеренно БЕЗ замка: спросить «чем занят редактор» должно быть можно
# и посреди чужого прогона — иначе диагностика встаёт в ту же очередь, которую
# и пришла разглядывать.
if ($Command -ne 'status') { Enter-Gate }

try {
switch ($Command) {
    'tests'  { exit (Invoke-Tests) }
    'method' { exit (Invoke-Method) }
    'start'  { Resolve-Bridge | Out-Null; Write-Host 'редактор готов' -ForegroundColor Green; exit 0 }
    'stop'   {
        if (Test-EditorRunning) { Stop-Editor | Out-Null; Write-Host 'редактор закрыт' }
        else { Write-Host 'редактор не открыт' }
        exit 0
    }
    'restart' { Stop-EditorsForce | Out-Null; Start-Daemon | Out-Null; exit 0 }
    'status' {
        $procs = Get-EditorProcesses
        $reply = Send-Bridge -Line 'PING' -TimeoutMs 1500
        if ($reply) {
            Write-Host "редактор жив (pid $($procs.Id -join ', ')), мост: $reply" -ForegroundColor Green
        }
        elseif ($procs.Count -gt 0) {
            Write-Host "процесс есть (pid $($procs.Id -join ', ')), но мост молчит — компиляция, domain reload или зависание" -ForegroundColor Yellow
            Write-Host '  если это надолго: .\tools\unity.ps1 restart'
        }
        else {
            Write-Host 'редактора нет — первая же команда поднимет его и оставит в фоне'
        }
        if ($procs.Count -gt 1) {
            Write-Host "  ВНИМАНИЕ: процессов $($procs.Count) — они дерутся за Library, нужен restart" -ForegroundColor Red
        }

        # Кто сейчас в очереди за редактором. Без этой строки чужой прогон
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
