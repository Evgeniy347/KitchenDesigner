@echo off
setlocal enabledelayedexpansion

REM ---------------------------------------------------------------------------
REM  Build of the Kitchen Designer installer (Inno Setup).
REM    installer\build-installer.cmd              - build player (build.cmd) + setup.exe
REM    installer\build-installer.cmd -NoBuild     - package existing Build\ (no Unity build)
REM    installer\build-installer.cmd -Version X   - force version (else from BuildInfo)
REM    installer\build-installer.cmd -Sign        - STUB: code signing not configured yet
REM
REM  Version == what is baked into the exe (get-version.ps1); written to
REM  installer\output\version.txt so publish tags the exact exe version.
REM
REM  Comments are ASCII: a batch file is read in the OEM codepage. The ISCC
REM  locate + compile is delegated to compile-iss.ps1 (PowerShell handles the
REM  quoted paths that choke the batch parser).
REM ---------------------------------------------------------------------------

set "NOBUILD="
set "SIGN="
set "VER_OVERRIDE="

REM Script dir from the FULL path of %0, captured BEFORE arg parsing: `shift` in
REM this environment also rotates %0, so %~f0 after the loop is unreliable.
for %%I in ("%~f0") do set "scrdir=%%~dpI"
for %%I in ("%scrdir%..") do set "root=%%~fI"
set "installerDir=%scrdir%"
set "outDir=%installerDir%output"
set "getver=%installerDir%get-version.ps1"
set "compile=%installerDir%compile-iss.ps1"

:args
if "%~1"=="" goto :args_done
if /i "%~1"=="-NoBuild"   set "NOBUILD=1" & shift & goto :args
if /i "%~1"=="-Sign"      set "SIGN=1"    & shift & goto :args
if /i "%~1"=="-Version"   goto :take_version
echo [FAIL] Unknown argument: %~1 & exit /b 1
:take_version
REM %~1 expands BEFORE the shift on the same line, so shift and read are on separate lines.
shift
set "VER_OVERRIDE=%~1"
shift
goto :args
:args_done

if defined SIGN echo [WARN] -Sign: code signing not configured, building UNSIGNED installer.

if not exist "%outDir%" mkdir "%outDir%"

REM ---- 1) Player: build unless -NoBuild ----
if not defined NOBUILD (
    echo === [1/3] Unity Windows release build ===
    call "%root%\build.cmd"
    if errorlevel 1 ( echo [FAIL] build.cmd failed & exit /b 1 )
)

if not exist "%root%\Build\KitchenDesigner.exe" (
    echo [FAIL] Build\KitchenDesigner.exe not found. Run without -NoBuild first.
    exit /b 1
)

REM ---- 2) Version: override -> BuildInfo -> commit count ----
set "VER=%VER_OVERRIDE%"
if not defined VER (
    for /f "usebackq delims=" %%V in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%getver%" -RootPath "%root%"`) do set "VER=%%V"
)
if not defined VER ( echo [FAIL] cannot determine version & exit /b 1 )
> "%outDir%\version.txt" echo !VER!

REM ---- 3) Compile the installer ----
echo.
echo === [2/3] Compiling installer (version !VER!) ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%compile%" -Root "%root%" -Iss "%installerDir%KitchenDesigner.iss" -Version "!VER!"
if errorlevel 1 ( echo [FAIL] installer compile failed & exit /b 1 )

set "SETUP=%outDir%\KitchenDesigner-Setup-!VER!-x64.exe"
echo.
echo ========================================
echo  INSTALLER OK
echo  Output: !SETUP!
echo ========================================
endlocal
exit /b 0
