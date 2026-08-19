# ---------------------------------------------------------------------------
#  Locates ISCC.exe and compiles KitchenDesigner.iss with /DVersion (+ /DHasRu
#  when the Russian translation ships). Kept in PowerShell because the batch
#  parser trips over the quoted ISCC path / parentheses.
#
#  Exit code 0 = compiled & produced the expected setup exe; non-zero = failed.
#
#  Params: -Root <kd-repose> -Iss <path.iss> -Version 0.N
# ---------------------------------------------------------------------------
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [Parameter(Mandatory=$true)][string]$Iss,
    [Parameter(Mandatory=$true)][string]$Version
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$find = Join-Path $here 'find-iscc.ps1'
$iscc = (& powershell -NoProfile -ExecutionPolicy Bypass -File "$find") | Select-Object -First 1
if (-not $iscc) {
    Write-Error 'Inno Setup 6 (ISCC.exe) not found. Install: winget install -e --id JRSoftware.InnoSetup'
    exit 1
}
$iscc = $iscc.Trim()

$hasRu = Test-Path -LiteralPath (Join-Path (Split-Path -Parent $iscc) 'Languages\Russian.isl')

Write-Host "ISCC    : $iscc"
Write-Host "Russian : $hasRu"

$issArgs = @("/DVersion=$Version")
if ($hasRu) { $issArgs += '/DHasRu=1' }
$issArgs += $Iss

& $iscc @issArgs
if ($LASTEXITCODE -ne 0) { Write-Error "ISCC failed with code $LASTEXITCODE"; exit 1 }

$out = Join-Path (Join-Path $here 'output') "KitchenDesigner-Setup-$Version-x64.exe"
if (-not (Test-Path -LiteralPath $out)) { Write-Error "setup not produced: $out"; exit 1 }

$mb = [math]::Round((Get-Item -LiteralPath $out).Length / 1MB, 1)
Write-Host "INSTALLER OK: $out ($mb MB, version $Version)"
exit 0
