# ---------------------------------------------------------------------------
#  Печатает номер версии, вшитый в текущую сборку, - ровно одну строку.
#  Порядок: BuildInfo.Generated.cs -> (фолбэк) git rev-list --count HEAD.
#  Используется build-installer.cmd и publish-github.cmd, чтобы тег релиза
#  совпадал с версией внутри exe.
#
#  Параметры:
#    -RootPath <путь>   корень проекта kd-repose (по умолчанию - родитель скрипта)
#    -Force             вернуть git count, даже если BuildInfo существует
# ---------------------------------------------------------------------------
param(
    [string]$RootPath,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
if (-not $RootPath) {
    $RootPath = Split-Path -Parent $PSScriptRoot   # installer\ -> kd-repose\
}

$buildInfo = Join-Path $RootPath 'Assets\Scripts\Core\Infrastructure\BuildInfo.Generated.cs'

if (-not $Force -and (Test-Path -LiteralPath $buildInfo)) {
    $text = Get-Content -LiteralPath $buildInfo -Raw
    $m = [regex]::Match($text, 'Version\s*=\s*"([^"]+)"')
    if ($m.Success) {
        [Console]::Out.WriteLine($m.Groups[1].Value)
        return
    }
}

try {
    $count = (& git -C $RootPath rev-list --count HEAD) 2>$null
    if ($LASTEXITCODE -eq 0 -and $count) {
        [Console]::Out.WriteLine("0.$($count.Trim())")
        return
    }
} catch { }

throw "Cannot determine version: no BuildInfo.Generated.cs and git rev-list failed."
