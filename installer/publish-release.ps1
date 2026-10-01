# ---------------------------------------------------------------------------
#  Выкладывает релиз на GitHub так, чтобы обрыв посередине не оставлял мусор,
#  который выглядит как готовый релиз, и чтобы повторный запуск дожимал начатое.
#
#  Порядок (каждый шаг идемпотентен):
#    1. релиз есть? нет - создать ЧЕРНОВИКОМ без ассета (быстро); есть - обновить
#       заголовок и тело (черновик остаётся черновиком, опубликованный - опубликованным);
#    2. загрузить ассет: до -Retries попыток, у каждой свой тайм-аут, растущий с размером
#       файла (повисший gh убивается вместе с дочерними процессами); --clobber стирает
#       недогруженный остаток прошлой попытки;
#    3. ПРОВЕРИТЬ: ассет с таким именем есть, state=uploaded, размер равен локальному;
#    4. только после этого снять черновик (--draft=false --latest; для -PreRelease без --latest);
#    5. проверить, что релиз больше не черновик.
#
#  Раньше `gh release create <tag> <setup>` создавал черновик, грузил 60 МБ и только потом
#  публиковал: упал по тайм-ауту на загрузке - остался ПУСТОЙ черновик без объяснений.
#
#  Любой провал: код выхода 1 и блок «СОСТОЯНИЕ» - что есть на GitHub (черновик или нет,
#  какой ассет) и как продолжить. Повторный запуск той же команды безопасен.
#
#  Параметры:
#    -Tag -Version -Title -SetupPath -NotesFile -Repo   обязательные
#    -PreRelease      пометить предварительным (releases/latest его не увидит)
#    -VerifyTag       требовать, чтобы тег уже был на GitHub (cmd пушит его до этого шага)
#    -NoPublish       остановиться на проверенном черновике (для проб)
#    -DryRun          только показать план и текущее состояние на GitHub, ничего не менять
#    -SecondsPerMB    тайм-аут попытки = max(-MinTimeoutSec, -BaseSec + размер_МБ * это)  (10; -BaseSec 60)
#    -MinTimeoutSec   нижняя граница тайм-аута попытки (180)
#    -Retries         число попыток загрузки (3)
#    -GhExe           путь к gh (по умолчанию из PATH; подмена нужна тестам)
#
#  Сохранён в UTF-8 С BOM: Windows PowerShell 5.1 читает файл без BOM как ANSI.
# ---------------------------------------------------------------------------
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Title,
    [Parameter(Mandatory = $true)][string]$SetupPath,
    [Parameter(Mandatory = $true)][string]$NotesFile,
    [Parameter(Mandatory = $true)][string]$Repo,
    [switch]$PreRelease,
    [switch]$VerifyTag,
    [switch]$NoPublish,
    [switch]$DryRun,
    [double]$SecondsPerMB = 10,
    [int]$BaseSec = 60,
    [int]$MinTimeoutSec = 180,
    [int]$Retries = 3,
    [int]$CallTimeoutSec = 90,
    [int]$RetryPauseSec = 5,
    [string]$GhExe = ''
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

if (-not $GhExe) { $GhExe = (Get-Command gh -ErrorAction Stop).Source }
$assetName = Split-Path -Leaf $SetupPath
$script:published = $false

function Say([string]$text, [string]$color = 'Gray') {
    Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $text) -ForegroundColor $color
}

function Quote-Arg([string]$a) {
    if ($a -match '[\s"]' -or $a -eq '') { return '"' + ($a -replace '"', '\"') + '"' }
    return $a
}

function Invoke-Gh {
    param([string[]]$GhArgs, [int]$TimeoutSec)

    $outFile = [IO.Path]::GetTempFileName()
    $errFile = [IO.Path]::GetTempFileName()
    try {
        $argLine = ($GhArgs | ForEach-Object { Quote-Arg $_ }) -join ' '
        $p = Start-Process -FilePath $GhExe -ArgumentList $argLine -PassThru -NoNewWindow `
            -RedirectStandardOutput $outFile -RedirectStandardError $errFile
        $null = $p.Handle   # Windows PowerShell 5.1: without a cached handle ExitCode comes back empty
        $timedOut = -not $p.WaitForExit($TimeoutSec * 1000)
        if ($timedOut) {
            & taskkill.exe /T /F /PID $p.Id *> $null
            $p.WaitForExit(5000) | Out-Null
        }
        return [pscustomobject]@{
            Code     = if ($timedOut) { -1 } else { $p.ExitCode }
            TimedOut = $timedOut
            Out      = [string](Get-Content -LiteralPath $outFile -Raw -Encoding UTF8)
            Err      = [string](Get-Content -LiteralPath $errFile -Raw -Encoding UTF8)
        }
    }
    finally { Remove-Item -LiteralPath $outFile, $errFile -ErrorAction SilentlyContinue }
}

function Get-Release {
    for ($i = 1; $i -le 3; $i++) {
        $r = Invoke-Gh -GhArgs @('release', 'view', $Tag, '-R', $Repo, '--json', 'isDraft,isPrerelease,url,assets,tagName') -TimeoutSec $CallTimeoutSec
        if ($r.Code -eq 0) { return ($r.Out | ConvertFrom-Json) }
        if (-not $r.TimedOut -and $r.Err -match 'not found') { return $null }
        Say "gh release view: не получилось (попытка $i/3): $($r.Err.Trim())" 'DarkYellow'
        Start-Sleep -Seconds $RetryPauseSec
    }
    throw "Не удалось узнать состояние релиза $Tag (сеть или gh?)"
}

function Get-AssetState($release) {
    if ($null -eq $release) { return $null }
    return @($release.assets) | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
}

function Show-State {
    Write-Host ''
    Write-Host '---------------- СОСТОЯНИЕ НА GITHUB ----------------' -ForegroundColor Yellow
    try {
        $rel = Get-Release
        if ($null -eq $rel) {
            Write-Host "  релиза $Tag нет - на GitHub ничего не осталось" -ForegroundColor Yellow
        }
        else {
            $kind = if ($rel.isDraft) { 'ЧЕРНОВИК (пользователям не виден, не latest)' } else { 'ОПУБЛИКОВАН' }
            Write-Host "  релиз $Tag : $kind"
            Write-Host "  страница  : $($rel.url)"
            $a = Get-AssetState $rel
            if ($null -eq $a) { Write-Host "  ассет     : $assetName ОТСУТСТВУЕТ" -ForegroundColor Yellow }
            else { Write-Host "  ассет     : $($a.name)  state=$($a.state)  size=$($a.size)" }
        }
    }
    catch { Write-Host "  (состояние узнать не удалось: $($_.Exception.Message))" -ForegroundColor Yellow }
    Write-Host '  Продолжить: запустите ТУ ЖЕ команду публикации ещё раз - каждый шаг идемпотентен.'
    Write-Host "  Вручную: gh release upload $Tag `"$SetupPath`" -R $Repo --clobber"
    $latestFlag = if ($PreRelease) { '' } else { ' --latest' }
    Write-Host "           gh release edit $Tag -R $Repo --draft=false$latestFlag"
    Write-Host '-----------------------------------------------------' -ForegroundColor Yellow
}

function Fail([string]$message) {
    Say "ОШИБКА: $message" 'Red'
    Show-State
    exit 1
}

# ── предусловия ────────────────────────────────────────────────────────────
if (-not (Test-Path -LiteralPath $SetupPath)) { Fail "нет файла установщика: $SetupPath" }
if (-not (Test-Path -LiteralPath $NotesFile)) { Fail "нет файла с телом релиза: $NotesFile" }
$localSize = (Get-Item -LiteralPath $SetupPath).Length
$sizeMB = [math]::Round($localSize / 1MB, 1)
$timeoutSec = [int][math]::Max($MinTimeoutSec, $BaseSec + $sizeMB * $SecondsPerMB)

Say ("релиз {0} в {1}; ассет {2}, {3} МБ; тайм-аут попытки {4} с, попыток {5}" -f $Tag, $Repo, $assetName, $sizeMB, $timeoutSec, $Retries) 'Cyan'

try {
    $release = Get-Release
}
catch { Fail $_.Exception.Message }

if ($DryRun) {
    if ($null -eq $release) { Say '[dry] релиза нет: создам ЧЕРНОВИК без ассета' }
    else { Say "[dry] релиз есть (draft=$($release.isDraft)): обновлю заголовок и тело" }
    $existing = Get-AssetState $release
    if ($existing) { Say "[dry] ассет уже есть: state=$($existing.state) size=$($existing.size) (локальный $localSize)" }
    Say "[dry] загружу ассет (до $Retries попыток по $timeoutSec с), проверю state=uploaded и размер"
    if ($NoPublish) { Say '[dry] -NoPublish: остановлюсь на черновике' }
    else { Say ('[dry] сниму черновик (' + $(if ($PreRelease) { 'prerelease, не latest' } else { '--latest' }) + ')') }
    exit 0
}

# ── 1. релиз: создать черновиком или обновить ──────────────────────────────
if ($null -eq $release) {
    Say "создаю черновик $Tag (без ассета)" 'Cyan'
    $createArgs = @('release', 'create', $Tag, '-R', $Repo, '--draft', '--title', $Title, '--notes-file', $NotesFile)
    if ($PreRelease) { $createArgs += '--prerelease' }
    if ($VerifyTag) { $createArgs += '--verify-tag' }
    $r = Invoke-Gh -GhArgs $createArgs -TimeoutSec $CallTimeoutSec
    if ($r.Code -ne 0) {
        $release = Get-Release
        if ($null -eq $release) { Fail "gh release create не удался (код $($r.Code), тайм-аут=$($r.TimedOut)): $($r.Err.Trim())" }
        Say 'create вернул ошибку, но черновик на GitHub появился - продолжаю' 'DarkYellow'
    }
}
else {
    $kind = if ($release.isDraft) { 'черновик' } else { 'опубликованный' }
    Say "релиз $Tag уже есть ($kind) - обновляю заголовок и тело" 'Cyan'
    $editArgs = @('release', 'edit', $Tag, '-R', $Repo, '--title', $Title, '--notes-file', $NotesFile)
    if ($PreRelease) { $editArgs += '--prerelease' }
    $r = Invoke-Gh -GhArgs $editArgs -TimeoutSec $CallTimeoutSec
    if ($r.Code -ne 0) { Fail "gh release edit не удался (код $($r.Code), тайм-аут=$($r.TimedOut)): $($r.Err.Trim())" }
}

# ── 2-3. ассет: загрузить с повторами и проверить ──────────────────────────
function Test-AssetGood {
    $rel = Get-Release
    $a = Get-AssetState $rel
    if ($null -eq $a) { return @{ Ok = $false; Why = 'ассета нет' } }
    if ($a.state -ne 'uploaded') { return @{ Ok = $false; Why = "state=$($a.state)" } }
    if ([int64]$a.size -ne $localSize) { return @{ Ok = $false; Why = "размер $($a.size) вместо $localSize" } }
    return @{ Ok = $true; Why = '' }
}

$assetOk = $false
$pre = Test-AssetGood
if ($pre.Ok) {
    Say 'ассет уже на GitHub с тем же размером - пересылать нечего' 'Green'
    $assetOk = $true
}
for ($attempt = 1; -not $assetOk -and $attempt -le $Retries; $attempt++) {
    Say "загрузка ассета, попытка $attempt/$Retries (тайм-аут $timeoutSec с)" 'Cyan'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $r = Invoke-Gh -GhArgs @('release', 'upload', $Tag, $SetupPath, '-R', $Repo, '--clobber') -TimeoutSec $timeoutSec
    if ($r.TimedOut) { Say "попытка $attempt : тайм-аут $timeoutSec с, процесс снят" 'DarkYellow' }
    elseif ($r.Code -ne 0) { Say "попытка $attempt : gh вернул код $($r.Code): $($r.Err.Trim())" 'DarkYellow' }
    else { Say ("gh завершился за {0:N0} с, проверяю на стороне GitHub" -f $sw.Elapsed.TotalSeconds) }

    $chk = Test-AssetGood
    if ($chk.Ok) { $assetOk = $true; break }
    Say "проверка ассета: $($chk.Why)" 'DarkYellow'
    if ($attempt -lt $Retries) { Start-Sleep -Seconds $RetryPauseSec }
}
if (-not $assetOk) { Fail "ассет $assetName не загружен за $Retries попыток" }
Say "ассет проверен: $assetName, $localSize байт, state=uploaded" 'Green'

# ── 4-5. публикация только после проверенного ассета ───────────────────────
if ($NoPublish) {
    Say '-NoPublish: релиз оставлен ЧЕРНОВИКОМ' 'Yellow'
    exit 0
}

$release = Get-Release
if ($release.isDraft) {
    Say "снимаю черновик $Tag" 'Cyan'
    $pubArgs = @('release', 'edit', $Tag, '-R', $Repo, '--draft=false')
    if (-not $PreRelease) { $pubArgs += '--latest' }
    $r = Invoke-Gh -GhArgs $pubArgs -TimeoutSec $CallTimeoutSec
    if ($r.Code -ne 0) { Fail "не удалось опубликовать (код $($r.Code), тайм-аут=$($r.TimedOut)): $($r.Err.Trim())" }
}
else {
    Say 'релиз уже опубликован - снимать нечего' 'Gray'
}

$final = Get-Release
if ($null -eq $final -or $final.isDraft) { Fail 'после публикации релиз всё ещё черновик' }
Say "релиз $Tag опубликован: $($final.url)" 'Green'
exit 0
