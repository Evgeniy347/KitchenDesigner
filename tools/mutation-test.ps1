<#
.SYNOPSIS
    Тесты и мутационное тестирование геометрического ядра (KitchenDesigner.Geometry).

.DESCRIPTION
    Ядро собирается из тех же исходников, что компилирует Unity, но исполняется
    под обычным .NET — поэтому его тесты идут за десятки миллисекунд, а Stryker
    работает штатно, без форков и обвязки вокруг Unity.

    Порог (-ThresholdBreak, флаг Stryker --break-at) роняет прогон, если mutation score упал ниже.
    Снимайте baseline после каждого этапа и поднимайте порог следом.

    Обоснование и план: docs/GEOMETRY-EXTRACTION-PLAN.md

.PARAMETER ThresholdBreak
    Минимальный допустимый mutation score в процентах. 0 — не проверять.

.PARAMETER TestsOnly
    Только dotnet test, без мутационного прогона (быстрая проверка на каждый push).

.PARAMETER UnityManagedDir
    Каталог с UnityEngine.CoreModule.dll. По умолчанию берётся из csproj.
    Нужен на этапе СБОРКИ: ядро не вызывает движок, но типы Vector3/Mathf
    лежат в его сборке.

.EXAMPLE
    .\tools\mutation-test.ps1 -TestsOnly
    .\tools\mutation-test.ps1 -ThresholdBreak 45
#>
[CmdletBinding()]
param(
    [int]$ThresholdBreak = 0,
    [switch]$TestsOnly,
    [string]$UnityManagedDir = ''
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$testsDir = Join-Path $repo 'geometry\tests'
# --project у Stryker — ФИЛЬТР ПО ИМЕНИ проекта, а не путь к нему.
$coreProj = 'Geometry.csproj'

if (-not (Test-Path $testsDir)) {
    throw "Не найден $testsDir — ядро не выделено?"
}

if ($UnityManagedDir) {
    if (-not (Test-Path (Join-Path $UnityManagedDir 'UnityEngine.CoreModule.dll'))) {
        throw "В $UnityManagedDir нет UnityEngine.CoreModule.dll"
    }
    $env:UnityManagedDir = $UnityManagedDir.TrimEnd('\') + '\'
}

Push-Location $testsDir
try {
    Write-Host '=== Тесты ядра ===' -ForegroundColor Cyan
    dotnet test --nologo
    if ($LASTEXITCODE -ne 0) { throw "Тесты ядра упали (код $LASTEXITCODE)" }

    if ($TestsOnly) { return }

    Write-Host ''
    Write-Host '=== Мутационное тестирование ===' -ForegroundColor Cyan

    # Мутации идут ПОСЛЕ тестов: Stryker пересобирает проект под себя, и
    # порядок «сначала честный прогон, потом мутанты» экономит отладку.
    $strykerArgs = @('--project', $coreProj, '--reporter', 'html', '--reporter', 'json', '--reporter', 'cleartext')
    if ($ThresholdBreak -gt 0) { $strykerArgs += @('--break-at', "$ThresholdBreak") }

    dotnet-stryker @strykerArgs
    $strykerExit = $LASTEXITCODE

    if ($strykerExit -ne 0) {
        throw "Mutation score ниже порога $ThresholdBreak% (код $strykerExit)"
    }

    $report = Get-ChildItem -Path $testsDir -Recurse -Filter 'mutation-report.html' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($report) { Write-Host "Отчёт: $($report.FullName)" -ForegroundColor Green }
}
finally {
    Pop-Location
}
