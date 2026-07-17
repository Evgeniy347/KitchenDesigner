@echo off
setlocal
REM Local debug: desktop (fastest iteration - Mono development build, incremental).
REM   run-desktop.cmd            - close previous debug instance, build Windows Debug, launch
REM   run-desktop.cmd -NoBuild   - just launch the existing Build_Debug\KitchenDesigner.exe

for %%I in ("%~dp0.") do set "root=%%~fI"

if /i "%~1"=="-NoBuild" goto :launch

REM Close only the previous DEBUG instance (from Builds\Win_Debug\) - it locks the output files.
powershell -NoProfile -Command "Get-Process KitchenDesigner -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*Builds\Win_Debug*' } | Stop-Process -Force" >nul 2>nul

echo === [1/2] Unity Windows Debug build ===
call "%root%\build.cmd" -WinDebug
if %errorlevel% neq 0 (
    echo [FAIL] Windows Debug build failed
    exit /b 1
)

:launch
if not exist "%root%\Builds\Win_Debug" mkdir "%root%\Builds\Win_Debug"
set "exe=%root%\Builds\Win_Debug\KitchenDesigner.exe"
if not exist "%exe%" (
    echo [FAIL] %exe% not found. Run without -NoBuild first.
    exit /b 1
)

echo === [2/2] Launch ===
start "" "%exe%"
echo Launched: %exe%
