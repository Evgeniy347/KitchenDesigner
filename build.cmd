@echo off
setlocal enabledelayedexpansion

set "FLAG_CLEAN="
set "FLAG_TESTS="
set "FLAG_PLAY="
set "FLAG_BUILD="

:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="-Clean" set "FLAG_CLEAN=1"
if /i "%~1"=="-RunTests" set "FLAG_TESTS=1"
if /i "%~1"=="-RunPlayMode" set "FLAG_PLAY=1"
if /i "%~1"=="-BuildOnly" set "FLAG_BUILD=1"
shift
goto :parse_args
:args_done

for %%I in ("%~dp0.") do set "root=%%~fI"

set "unity=C:\Program Files\Unity\Hub\Editor\6000.4.3f1\Editor\Unity.exe"
set "testResult=%root%\TestResults.xml"
set "log=%TEMP%\build-kitchen.log"

if not exist "%unity%" (
    echo [FAIL] Unity not found at %unity%
    exit /b 1
)

REM ---- Clean ----
if defined FLAG_CLEAN (
    echo === Clean ===
    call "%~dp0clean.cmd"
)

REM ---- Tests ----
set "testsOk=1"

if defined FLAG_TESTS if not defined FLAG_BUILD (
    echo === EditMode Tests ===
    "%unity%" -runTests -batchMode -projectPath "%root%" -testResults "%testResult%" -testPlatform EditMode -logFile "%log%"
    if exist "%testResult%" (
        powershell -NoProfile -Command "$x=[xml](Get-Content '%testResult%'); if ($x.'test-run' -and [int]$x.'test-run'.failed -gt 0) { Write-Host ('  Total: {0} | Passed: {1} | Failed: {2}' -f $x.'test-run'.total,$x.'test-run'.passed,$x.'test-run'.failed); exit 1 } else { Write-Host ('  Total: {0} | Passed: {1} | Failed: {2}' -f $x.'test-run'.total,$x.'test-run'.passed,$x.'test-run'.failed) }"
        if !errorlevel! neq 0 set "testsOk="
    )
)

if defined FLAG_PLAY if not defined FLAG_BUILD (
    echo === PlayMode Tests ===
    "%unity%" -runTests -batchMode -projectPath "%root%" -testResults "%testResult%" -testPlatform PlayMode -logFile "%log%"
    if exist "%testResult%" (
        powershell -NoProfile -Command "$x=[xml](Get-Content '%testResult%'); if ($x.'test-run' -and [int]$x.'test-run'.failed -gt 0) { Write-Host ('  Total: {0} | Passed: {1} | Failed: {2}' -f $x.'test-run'.total,$x.'test-run'.passed,$x.'test-run'.failed); exit 1 } else { Write-Host ('  Total: {0} | Passed: {1} | Failed: {2}' -f $x.'test-run'.total,$x.'test-run'.passed,$x.'test-run'.failed) }"
        if !errorlevel! neq 0 set "testsOk="
    )
)

if not defined testsOk if not defined FLAG_BUILD (
    echo [FAIL] Tests failed. Aborting build.
    exit /b 1
)

REM ---- Build ----
echo === Build ===
if exist "%root%\Build" rmdir /s /q "%root%\Build"

"%unity%" -quit -batchMode -projectPath "%root%" -executeMethod BuildProject.Build -logFile "%log%"
set "buildExit=!errorlevel!"

set "buildPath=%root%\Build\KitchenDesigner.exe"
if !buildExit! equ 0 if exist "!buildPath!" (
    for %%I in ("!buildPath!") do set "sizeBytes=%%~zI"
    set /a sizeMB=sizeBytes / 1048576
    if !sizeMB! equ 0 set "sizeMB=<1"
    echo.
    echo =================================
    echo  BUILD OK
    echo  Output: !buildPath!
    echo  Size:   !sizeMB! MB
    echo =================================
    exit /b 0
) else (
    echo.
    echo =================================
    echo  BUILD FAILED - exit code: !buildExit!
    echo =================================
    exit /b 1
)
