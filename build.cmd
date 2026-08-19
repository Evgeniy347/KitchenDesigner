@echo off
setlocal enabledelayedexpansion

REM ---------------------------------------------------------------------------
REM  Единая логика для всех скриптов проекта: любая команда к Unity уходит через
REM  tools\unity.ps1, и каждая поднимает СВОЙ холодный Unity -batchMode. Фоновый
REM  редактор между вызовами больше не живёт: он экономил старт, но постоянно
REM  отваливался, и разбор его отказов стоил дороже сэкономленного.
REM
REM  ВАЖНО: -RunTests и -RunPlayMode БОЛЬШЕ НЕ СОБИРАЮТ плеер. Раньше сборка шла
REM  следом всегда и добавляла 7-8 минут к каждой проверке тестов. Нужен плеер —
REM  зовите build.cmd без флагов (release) или -WinDebug / -WebGL / -WebGLDebug.
REM ---------------------------------------------------------------------------

set "FLAG_CLEAN="
set "FLAG_TESTS="
set "FLAG_PLAY="
set "FLAG_BUILD="
set "FLAG_WEBGL="
set "FLAG_WEBGL_DEBUG="
set "FLAG_WIN_DEBUG="
set "FILTER="

:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="-Clean" set "FLAG_CLEAN=1"
if /i "%~1"=="-RunTests" set "FLAG_TESTS=1"
if /i "%~1"=="-RunPlayMode" set "FLAG_PLAY=1"
if /i "%~1"=="-BuildOnly" set "FLAG_BUILD=1"
if /i "%~1"=="-WebGL" set "FLAG_WEBGL=1"
if /i "%~1"=="-WebGLDebug" set "FLAG_WEBGL_DEBUG=1"
if /i "%~1"=="-WinDebug" set "FLAG_WIN_DEBUG=1"
REM Через goto, а не через if-блок: в скобках %~1 раскрывается ДО shift,
REM и значение фильтра теряется.
if /i "%~1"=="-Filter" goto :take_filter
shift
goto :parse_args

:take_filter
shift
set "FILTER=%~1"
shift
goto :parse_args

:args_done

for %%I in ("%~dp0.") do set "root=%%~fI"
set "gate=powershell -NoProfile -ExecutionPolicy Bypass -File "%root%\tools\unity.ps1""
set "log=%TEMP%\build-kitchen.log"

REM ---- Clean ----
if defined FLAG_CLEAN (
    echo === Clean ===
    call "%~dp0clean.cmd"
)

REM ---- Tests ----
if defined FLAG_TESTS (
    echo === EditMode Tests ===
    if defined FILTER (
        %gate% tests -Platform EditMode -Filter "!FILTER!"
    ) else (
        %gate% tests -Platform EditMode
    )
    if !errorlevel! neq 0 goto :tests_failed
)

if defined FLAG_PLAY (
    echo === PlayMode Tests ===
    if defined FILTER (
        %gate% tests -Platform PlayMode -Filter "!FILTER!"
    ) else (
        %gate% tests -Platform PlayMode
    )
    if !errorlevel! neq 0 goto :tests_failed
)

REM Тесты запрошены явно и сборка не запрошена — на этом всё.
if defined FLAG_TESTS goto :done_no_build
if defined FLAG_PLAY goto :done_no_build

REM ---- Builds ----
if defined FLAG_WEBGL goto :build_webgl
if defined FLAG_WEBGL_DEBUG goto :build_webgl_debug
if defined FLAG_WIN_DEBUG goto :build_win_debug

echo === Build Windows ===
if exist "%root%\Build" rmdir /s /q "%root%\Build"
%gate% method -Method BuildProject.Build -LogSuffix win
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

:done_no_build
exit /b 0

:tests_failed
echo [FAIL] Tests failed.
exit /b 1

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
if not exist "%root%\Build_Debug" mkdir "%root%\Build_Debug"
%gate% method -Method BuildProject.BuildWindowsDebug -LogSuffix winDebug
set "buildExit=!errorlevel!"

set "buildPath=%root%\Build_Debug\KitchenDesigner.exe"
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
mkdir "%root%\Builds\WebGL"
%gate% method -Method BuildProject.BuildWebGLRelease -LogSuffix webgl
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
mkdir "%root%\Builds\WebGL_Debug"
%gate% method -Method BuildProject.BuildWebGLDebug -LogSuffix webglDebug
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
