<#
.SYNOPSIS
    Очистка проекта Kitchen Designer от временных и сгенерированных файлов.
.DESCRIPTION
    Удаляет Library, Temp, Logs, Build, UserSettings, .vs, .opencode/node_modules,
    TestResults.xml и *.log из корня проекта.
#>

$root = Split-Path -Parent $PSScriptRoot
$items = @(
    "$root\Library",
    "$root\Temp",
    "$root\Logs",
    "$root\Build",
    "$root\Builds",
    "$root\.vs",
    "$root\UserSettings",
    "$root\.opencode\node_modules",
    "$root\TestResults.xml",
    "$root\TestResult.xml"
)

$removed = 0
$errors = 0

foreach ($item in $items) {
    if (Test-Path -LiteralPath $item) {
        try {
            if ((Get-Item -LiteralPath $item -ErrorAction Stop).PSIsContainer) {
                Remove-Item -LiteralPath $item -Recurse -Force -ErrorAction Stop
            } else {
                Remove-Item -LiteralPath $item -Force -ErrorAction Stop
            }
            Write-Host "  [DEL] $item"
            $removed++
        } catch {
            Write-Host "  [ERR] $item : $_"
            $errors++
        }
    }
}

Get-ChildItem -Path "$root" -Filter "*.log" -File | ForEach-Object {
    try {
        Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop
        Write-Host "  [DEL] $($_.Name)"
        $removed++
    } catch {
        Write-Host "  [ERR] $($_.Name) : $_"
        $errors++
    }
}

Write-Host ""
Write-Host "Clean complete: $removed items removed, $errors errors"
if ($errors -gt 0) { exit 1 } else { exit 0 }
