# Находит ISCC.exe (Inno Setup 6) и печатает полный путь к нему (или пусто).
# Порядок: PATH -> реестр деинсталляции -> типовые каталоги Program Files и
# %LOCALAPPDATA%\Programs. Держим в PowerShell, чтобы не спотыкаться о скобки
# в имени переменной %ProgramFiles(x86)% внутри batch.
$ErrorActionPreference = 'SilentlyContinue'

$cands = @()

$c = Get-Command iscc.exe
if ($c) { $cands += $c.Source }

foreach ($rk in 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
                 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
                 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*') {
    foreach ($key in (Get-ItemProperty $rk)) {
        if ($key.DisplayName -like 'Inno Setup*' -and $key.InstallLocation) {
            $cands += (Join-Path $key.InstallLocation 'ISCC.exe')
        }
    }
}

foreach ($dir in @(
    "$env:ProgramFiles\Inno Setup 6",
    "${env:ProgramFiles(x86)}\Inno Setup 6",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6")) {
    $cands += (Join-Path $dir 'ISCC.exe')
}

foreach ($p in $cands) {
    if ($p -and (Test-Path -LiteralPath $p)) { [Console]::Out.WriteLine($p); return }
}
