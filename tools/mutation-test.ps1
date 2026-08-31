<#
.SYNOPSIS
    Тесты и мутационное тестирование быстрого пути (ядро + чистый слой).

.DESCRIPTION
    Обе сборки собираются из тех же исходников, что компилирует Unity, но
    исполняются под обычным .NET — поэтому их тесты идут за доли секунды, а
    Stryker работает штатно, без форков и обвязки вокруг Unity.

    Мутации идут ПОСЛЕ обычных тестов: Stryker пересобирает проект под себя, и
    порядок «сначала честный прогон, потом мутанты» экономит отладку.

    ВОРОТА. Порог на каждый проект лежит в geometry/mutation-baseline.txt —
    единственном месте, где живёт число. Скрипт передаёт его Стрейкеру ключом
    --break-at, и упавший ниже порога прогон возвращает ненулевой код. Сам файл
    стережёт MutationBaselineTests: порог, отставший от замера, стрелять
    перестаёт, а проект без строки не гейтится вовсе.

    Порогов ДВА, по одному на сборку, и это не оплошность — обоснование в шапке
    того же файла. Коротко: Stryker мутирует один проект за прогон, состав
    чистого слоя сейчас растёт, а общий процент прятал бы регресс ядра за ростом
    меньшего слоя.

    Подняли счёт — поднимите порог тем же коммитом, иначе храповик отдаст назад.

    Обоснование и план: docs/GEOMETRY-EXTRACTION-PLAN.md, docs/HARDENING-PLAN.md → C1.

.PARAMETER TestsOnly
    Только dotnet test, без мутационного прогона (быстрая проверка на каждый push).

.PARAMETER Only
    Мутировать один проект: core или pure. По умолчанию — все из базового уровня.

.PARAMETER ThresholdBreak
    Ручное переопределение порога для ВСЕХ проектов. 0 — брать из базового уровня.
    Нужен, чтобы снять новый замер (-ThresholdBreak 1) или проверить ворота.

.PARAMETER UnityManagedDir
    Каталог с UnityEngine.CoreModule.dll. По умолчанию берётся из csproj.
    Нужен на этапе СБОРКИ: ядро не вызывает движок, но типы Vector3/Mathf
    лежат в его сборке.

.EXAMPLE
    .\tools\mutation-test.ps1 -TestsOnly
    .\tools\mutation-test.ps1
    .\tools\mutation-test.ps1 -Only pure
#>
[CmdletBinding()]
param(
    [int]$ThresholdBreak = 0,
    [ValidateSet('core', 'pure')]
    [string]$Only = '',
    [switch]$TestsOnly,
    [string]$UnityManagedDir = ''
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$geometry = Join-Path $repo 'geometry'
$testsDir = Join-Path $geometry 'tests'
$pureTestsDir = Join-Path $geometry 'pure-tests'
$baselineFile = Join-Path $geometry 'mutation-baseline.txt'

if (-not (Test-Path $testsDir)) {
    throw "Не найден $testsDir — ядро не выделено?"
}

if ($UnityManagedDir) {
    if (-not (Test-Path (Join-Path $UnityManagedDir 'UnityEngine.CoreModule.dll'))) {
        throw "В $UnityManagedDir нет UnityEngine.CoreModule.dll"
    }
    $env:UnityManagedDir = $UnityManagedDir.TrimEnd('\') + '\'
}

function Get-Baseline {
    if (-not (Test-Path $baselineFile)) {
        throw "Не найден $baselineFile — ворота по mutation score без него открыты настежь"
    }

    Get-Content $baselineFile | ForEach-Object {
        $line = $_.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('#')) { return }

        $parts = $line -split '\s+'
        if ($parts.Count -ne 3) {
            throw "Строка базового уровня должна быть «<проект> <замер> <порог>»: $line"
        }

        [pscustomobject]@{
            Project   = $parts[0]
            Measured  = [double]::Parse($parts[1], [Globalization.CultureInfo]::InvariantCulture)
            Threshold = [int]$parts[2]
        }
    }
}

function Invoke-Stryker {
    param([string]$Project, [int]$Threshold)

    # Тестовый каталог выводится из имени проекта, а не из таблицы в скрипте:
    # новый проект быстрого пути попадает под ворота сам.
    $projectDir = Join-Path $geometry $Project
    $csproj = Get-ChildItem -Path $projectDir -Filter '*.csproj' | Select-Object -First 1
    if (-not $csproj) { throw "У проекта $Project нет *.csproj в $projectDir" }

    $projectTests = Join-Path $geometry "$Project-tests"
    if (-not (Test-Path $projectTests)) { $projectTests = $testsDir }

    Write-Host ''
    Write-Host "=== Мутационное тестирование: $Project (порог $Threshold%) ===" -ForegroundColor Cyan

    Push-Location $projectTests
    try {
        # --project у Stryker — ФИЛЬТР ПО ИМЕНИ проекта, а не путь к нему.
        $strykerArgs = @('--project', $csproj.Name,
            '--reporter', 'html', '--reporter', 'json', '--reporter', 'cleartext',
            '--break-at', "$Threshold")

        $started = Get-Date
        dotnet-stryker @strykerArgs | Tee-Object -Variable output
        $strykerExit = $LASTEXITCODE
        $spent = [int]((Get-Date) - $started).TotalSeconds

        $score = ($output | Select-String -Pattern 'final mutation score is ([\d\.,]+)' |
            Select-Object -Last 1).Matches.Groups[1].Value

        $report = Get-ChildItem -Path $projectTests -Recurse -Filter 'mutation-report.html' `
            -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($strykerExit -ne 0) {
            $where = if ($report) { $report.FullName } else { 'отчёт не создан' }
            throw ("Mutation score проекта {0} упал ниже порога {1}%: получено {2}% ({3} с).`n" +
                "Выжившие мутанты — места, где тест есть, а проверки нет; ищите их в отчёте:`n{4}`n" +
                "Если покрытие потеряно осознанно, порог опускается ОТДЕЛЬНЫМ коммитом " +
                "с причиной в geometry/mutation-baseline.txt.") -f `
                $Project, $Threshold, $score, $spent, $where
        }

        Write-Host "$Project`: mutation score $score% (порог $Threshold%), $spent с" -ForegroundColor Green
        if ($report) { Write-Host "Отчёт: $($report.FullName)" -ForegroundColor Green }
    }
    finally {
        Pop-Location
    }
}

Push-Location $testsDir
try {
    Write-Host '=== Тесты ядра ===' -ForegroundColor Cyan
    dotnet test --nologo
    if ($LASTEXITCODE -ne 0) { throw "Тесты ядра упали (код $LASTEXITCODE)" }

    Write-Host ''
    Write-Host '=== Тесты чистого слоя (Assets/Scripts/Core/Pure) ===' -ForegroundColor Cyan
    dotnet test $pureTestsDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "Тесты чистого слоя упали (код $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

if ($TestsOnly) { return }

$baseline = @(Get-Baseline)
if ($Only) { $baseline = @($baseline | Where-Object { $_.Project -eq $Only }) }
if ($baseline.Count -eq 0) { throw "В $baselineFile нет строк для прогона (-Only $Only)" }

foreach ($row in $baseline) {
    $threshold = if ($ThresholdBreak -gt 0) { $ThresholdBreak } else { $row.Threshold }
    Invoke-Stryker -Project $row.Project -Threshold $threshold
}
