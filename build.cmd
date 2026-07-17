@echo off
setlocal enabledelayedexpansion

set "FLAG_CLEAN="
set "FLAG_TESTS="
set "FLAG_PLAY="
set "FLAG_BUILD="
set "FLAG_WEBGL="
set "FLAG_WEBGL_DEBUG="
set "FLAG_WIN_DEBUG="

:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="-Clean" set "FLAG_CLEAN=1"
if /i "%~1"=="-RunTests" set "FLAG_TESTS=1"
if /i "%~1"=="-RunPlayMode" set "FLAG_PLAY=1"
if /i "%~1"=="-BuildOnly" set "FLAG_BUILD=1"
if /i "%~1"=="-WebGL" set "FLAG_WEBGL=1"
if /i "%~1"=="-WebGLDebug" set "FLAG_WEBGL_DEBUG=1"
if /i "%~1"=="-WinDebug" set "FLAG_WIN_DEBUG=1"
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

REM ---- Tests (skip for WebGL builds) ----
set "testsOk=1"

if defined FLAG_WEBGL goto :build_webgl
if defined FLAG_WEBGL_DEBUG goto :build_webgl_debug
if defined FLAG_WIN_DEBUG goto :build_win_debug

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

REM ---- Build Windows ----
echo === Build Windows ===
if exist "%root%\Build" rmdir /s /q "%root%\Build"

"%unity%" -quit -batchMode -projectPath "%root%" -executeMethod BuildProject.Build -logFile "%log%.win.log"
set "buildExit=!errorlevel!"

set "buildPath=%root%\Build\KitchenDesigner.exe"
if !buildExit! neq 0 goto :fail_win
if not exist "!buildPath!" goto :fail_win
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

:fail_win
echo.
echo =================================
echo  BUILD FAILED - exit code: !buildExit!
echo  Log: %log%.win.log
echo =================================
exit /b 1

:build_win_debug
echo === Build Windows Debug (fast, incremental) ===
REM No rmdir: keeping the output folder lets Unity reuse artifacts between runs.
REM Output goes to Build_Debug\ so a running exe from Build\ never locks the build.

"%unity%" -quit -batchMode -projectPath "%root%" -executeMethod BuildProject.BuildWindowsDebug -logFile "%log%.winDebug.log"
set "buildExit=!errorlevel!"

set "buildPath=%root%\Builds\Win_Debug\KitchenDesigner.exe"
if !buildExit! neq 0 goto :fail_win_debug
if not exist "!buildPath!" goto :fail_win_debug
echo.
echo =================================
echo  WINDOWS DEBUG BUILD OK
echo  Output: !buildPath!
echo =================================
exit /b 0

:fail_win_debug
echo.
echo =================================
echo  WINDOWS DEBUG BUILD FAILED - exit code: !buildExit!
echo  Log: %log%.winDebug.log
echo =================================
exit /b 1

:build_webgl
echo === Build WebGL Release ===
if exist "%root%\Builds\WebGL" rmdir /s /q "%root%\Builds\WebGL"

"%unity%" -quit -batchMode -projectPath "%root%" -executeMethod BuildProject.BuildWebGLRelease -logFile "%log%.webgl.log"
set "buildExit=!errorlevel!"

if !buildExit! neq 0 goto :fail_webgl
if not exist "%root%\Builds\WebGL\index.html" goto :fail_webgl
echo.
echo =================================
echo  WEBGL RELEASE BUILD OK
echo  Output: %root%\Builds\WebGL\
echo =================================
exit /b 0

:fail_webgl
echo.
echo =================================
echo  WEBGL BUILD FAILED - exit code: !buildExit!
echo  Log: %log%.webgl.log
echo =================================
exit /b 1

:build_webgl_debug
echo === Build WebGL Debug (fast) ===
if exist "%root%\Builds\WebGL_Debug" rmdir /s /q "%root%\Builds\WebGL_Debug"

"%unity%" -quit -batchMode -projectPath "%root%" -executeMethod BuildProject.BuildWebGLDebug -logFile "%log%.webglDebug.log"
set "buildExit=!errorlevel!"

if !buildExit! neq 0 goto :fail_webgl_debug
if not exist "%root%\Builds\WebGL_Debug\index.html" goto :fail_webgl_debug
echo.
echo =================================
echo  WEBGL DEBUG BUILD OK
echo  Output: %root%\Builds\WebGL_Debug\
echo =================================
exit /b 0

:fail_webgl_debug
echo.
echo =================================
echo  WEBGL DEBUG BUILD FAILED - exit code: !buildExit!
echo  Log: %log%.webglDebug.log
echo =================================
exit /b 1
