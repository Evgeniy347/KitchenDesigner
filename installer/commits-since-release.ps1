# ---------------------------------------------------------------------------
#  only-history helper: печатает список коммитов с ПРОШЛОГО релиза до HEAD,
#  чтобы агент мог оформить «Что нового». НИЧЕГО не меняет (только git log/show).
#
#  Для каждого коммита даёт: короткий хэш, тему (message) и набор «областей» по
#  изменённым путям — это подсказка, чтобы отбросить не-десктоп (tests/server/
#  webgl/docs) и понять, к какой фиче относятся багфиксы (их в релиз НЕ берут,
#  оставляют одну строку «добавлена фича»). Окончательный отбор/перевод/краткость
#  делает агент — см. installer\PUBLISH.md.
#
#  Параметры:
#    -RepoPath   корень git (по умолчанию родитель папки скрипта)
#    -TargetTag  тег, который СЕЙЧАС публикуется (исключается из «прошлого релиза»)
#    -SinceRef   явная база (например v0.632). Без неё = последний тег до HEAD.
#    -MaxCount   сколько коммитов максимум показать, если тегов ещё нет (def 60)
# ---------------------------------------------------------------------------
param(
    [string]$RepoPath,
    [string]$TargetTag,
    [string]$SinceRef,
    [int]$MaxCount = 60
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
if (-not $RepoPath) { $RepoPath = Split-Path -Parent $PSScriptRoot }

function G([string[]]$a) { (& git -C $RepoPath -c core.quotepath=false @a) }

# ── Определяем базу отсчёта ─────────────────────────────────────────────────
$base = $SinceRef
if (-not $base) {
    $tags = G @('tag','--sort=-creatordate') | Where-Object { $_ }
    if ($TargetTag) { $tags = $tags | Where-Object { $_ -ne $TargetTag } }
    if ($tags) { $base = ($tags | Select-Object -First 1) }
}

if ($base) {
    $range = "$base..HEAD"
    Write-Host "# изменения после релиза $base"
} else {
    $range = "-n $MaxCount"
    Write-Host "# прошлых тегов нет — показываю последние $MaxCount коммитов"
}
Write-Host ''

# ── Коммиты + области изменений ─────────────────────────────────────────────
$rows = G (@('log','--no-merges','--pretty=format:%h%x09%s') + @($range))
if (-not $rows) { Write-Host '(пусто — с прошлого релиза новых коммитов нет)'; return }

foreach ($line in $rows) {
    if (-not $line.Trim()) { continue }
    $parts = $line -split "`t", 2
    $h = $parts[0]
    $subj = if ($parts.Count -gt 1) { $parts[1] } else { '' }

    $files = G @('show','--pretty=format:','--name-only','--root', $h) |
        Where-Object { $_.Trim() }
    $areas = New-Object System.Collections.Generic.SortedSet[string]
    foreach ($f in $files) {
        $p = ($f -split '/')[0]
        if ($p -eq 'Assets') {
            $parts = $f -split '/'
            $areas.Add(($( if ($parts.Count -ge 3) { $parts[0..2] -join '/' } else { 'Assets' }))) | Out-Null
        } else {
            $areas.Add($p) | Out-Null
        }
    }
    Write-Host ("{0}`t{1}" -f $h, $subj)
    Write-Host ("      · области: " + (($areas | Select-Object -First 6) -join ', '))
}

Write-Host ''
Write-Host '# Ориентир фильтра: в релиз — только DESKTOP Unity (Assets/Scripts/Core, Assets/Editor, installer).'
Write-Host '# Игнор: Assets/Tests, server/, Assets/WebGLTemplates, docs/, Packages/.*'
Write-Host '# `fix:`/`refactor:` правки уже вошедшей в этот релиз `feat:` — НЕ отдельным пунктом, а частью строки фичи.'
