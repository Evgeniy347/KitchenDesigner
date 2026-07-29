@echo off
setlocal
REM Local debug: desktop (fastest iteration - Mono development build, incremental).
REM   run-desktop.cmd            - close previous debug instance, build Windows Debug, launch
REM   run-desktop.cmd -NoBuild   - just launch the existing Build_Debug\KitchenDesigner.exe
REM   run-desktop.cmd -Profile   - launch with the native Unity profiler writing test-results\perf\capture.raw
REM                                (open it later via Window > Analysis > Profiler > Load; the
REM                                 in-app HUD/CSV on F9 / Shift+F9 needs no editor at all)
REM   Flags combine: run-desktop.cmd -NoBuild -Profile

set "nobuild="
set "profile="

:args
if /i "%~1"=="-NoBuild" set "nobuild=1" & shift & goto :args
if /i "%~1"=="-Profile" set "profile=1" & shift & goto :args
if not "%~1"=="" (
    echo [FAIL] Unknown argument: %~1
    pause
    exit /b 1
)

for %%I in ("%~dp0.") do set "root=%%~fI"

if defined nobuild goto :launch

REM Close only the previous DEBUG instance (from Build_Debug\) - it locks the output files.
powershell -NoProfile -Command "Get-Process KitchenDesigner -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '*Build_Debug*' } | Stop-Process -Force" >nul 2>nul

echo === [1/2] Unity Windows Debug build ===
call "%root%\build.cmd" -WinDebug
if %errorlevel% neq 0 (
    echo [FAIL] Windows Debug build failed
    pause
    exit /b 1
)

:launch
if not exist "%root%\Build_Debug" mkdir "%root%\Build_Debug"
set "exe=%root%\Build_Debug\KitchenDesigner.exe"
if not exist "%exe%" (
    echo [FAIL] %exe% not found. Run without -NoBuild first.
    pause
    exit /b 1
)

echo === [2/2] Launch ===
if defined profile (
    if not exist "%root%\test-results\perf" mkdir "%root%\test-results\perf"
    REM -profiler-maxusedmemory raises the profiler buffer (default 16 MB overflows fast
    REM on a scene with hundreds of elements and drops frames from the capture).
    start "" "%exe%" -profiler-enable -profiler-log-file "%root%\test-results\perf\capture.raw" -profiler-maxusedmemory 268435456
    echo Launched with profiler: %exe%
    echo Capture: %root%\test-results\perf\capture.raw
) else (
    start "" "%exe%"
    echo Launched: %exe%
)
