<#
.SYNOPSIS
    Сборка Kitchen Designer: .exe + тесты.
.DESCRIPTION
    Параметры:
      -Clean        : выполнить clean.ps1 перед сборкой
      -RunTests     : запустить EditMode тесты
      -RunPlayMode  : запустить PlayMode тесты
      -BuildOnly    : только сборка, без тестов

    Примеры:
      .\build.ps1 -Clean -RunTests
      .\build.ps1 -BuildOnly
#>

param(
    [switch]$Clean,
    [switch]$RunTests,
    [switch]$RunPlayMode,
    [switch]$BuildOnly
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe"
$testResult = "$root\TestResults.xml"
$log = Join-Path $env:TEMP "build-kitchen.log"

if (-not (Test-Path $unity)) {
    Write-Host "[FAIL] Unity not found at $unity"
    exit 1
}

# ---- Clean ----
if ($Clean) {
    Write-Host "=== Clean ==="
    & "$root\clean.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[WARN] Clean had errors, continuing..."
    }
}

# ---- Tests ----
$testsOk = $true

if ($RunTests -and -not $BuildOnly) {
    Write-Host "=== EditMode Tests ==="
    & $unity -runTests -batchMode -projectPath "$root" -testResults "$testResult" -testPlatform EditMode -logFile "$log"
    if ($LASTEXITCODE -ne 0 -or (Test-Path $testResult)) {
        [xml]$xml = Get-Content $testResult -ErrorAction SilentlyContinue
        if ($xml -and $xml.'test-run') {
            $total = $xml.'test-run'.total
            $passed = $xml.'test-run'.passed
            $failed = $xml.'test-run'.failed
            Write-Host "  Total: $total | Passed: $passed | Failed: $failed"
            if ($failed -gt 0) { $testsOk = $false }
        }
    }
}

if ($RunPlayMode -and -not $BuildOnly) {
    Write-Host "=== PlayMode Tests ==="
    & $unity -runTests -batchMode -projectPath "$root" -testResults "$testResult" -testPlatform PlayMode -logFile "$log"
    if ($LASTEXITCODE -ne 0 -or (Test-Path $testResult)) {
        [xml]$xml = Get-Content $testResult -ErrorAction SilentlyContinue
        if ($xml -and $xml.'test-run') {
            $total = $xml.'test-run'.total
            $passed = $xml.'test-run'.passed
            $failed = $xml.'test-run'.failed
            Write-Host "  Total: $total | Passed: $passed | Failed: $failed"
            if ($failed -gt 0) { $testsOk = $false }
        }
    }
}

if (-not $testsOk -and -not $BuildOnly) {
    Write-Host "[FAIL] Tests failed. Aborting build."
    exit 1
}

# ---- Build ----
Write-Host "=== Build ==="
Remove-Item -LiteralPath "$root\Build" -Recurse -Force -ErrorAction SilentlyContinue

& $unity -quit -batchMode -projectPath "$root" -executeMethod BuildProject.Build -logFile "$log"
$buildExit = $LASTEXITCODE

$buildPath = "$root\Build\KitchenDesigner.exe"
if ($buildExit -eq 0 -and (Test-Path $buildPath)) {
    $size = (Get-Item $buildPath).Length / 1MB
    Write-Host ""
    Write-Host "================================="
    Write-Host " BUILD OK"
    Write-Host " Output: $buildPath"
    Write-Host " Size:   $('{0:F1}' -f $size) MB"
    Write-Host "================================="
    exit 0
} else {
    Write-Host ""
    Write-Host "================================="
    Write-Host " BUILD FAILED (exit code: $buildExit)"
    Write-Host "================================="
    exit 1
}
