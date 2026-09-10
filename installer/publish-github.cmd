@echo off
setlocal enabledelayedexpansion

REM ---------------------------------------------------------------------------
REM  Publishes the Kitchen Designer installer as a GitHub release.
REM    installer\publish-github.cmd              - build if needed and publish
REM    installer\publish-github.cmd -DryRun      - show the plan and the release body
REM    installer\publish-github.cmd -NoBuild     - do not build; setup must be in output\
REM    installer\publish-github.cmd -PreRelease  - mark the release as a prerelease
REM    installer\publish-github.cmd -Version X   - explicit version (else BuildInfo)
REM    installer\publish-github.cmd -NotesFile F - file with the Russian changelog,
REM                                                written by the agent; see PUBLISH.md
REM                                                and commits-since-release.ps1.
REM
REM  Does: tag v<version> -> push origin -> gh release create/edit + asset upload
REM  -> prints the download link.
REM
REM  ASCII ONLY, deliberately: cmd.exe seeks inside a batch file by byte offset and
REM  mis-parses multibyte characters when the console runs at code page 65001. The
REM  same script then splits lines mid-word and reports things like "he is not
REM  recognized". Russian text belongs in the .ps1 helpers, never here.
REM ---------------------------------------------------------------------------

set "DRYRUN="
set "NOBUILD="
set "VER_OVERRIDE="
set "NOTESFILE="
set "PRERELEASE="

REM Script dir from the FULL path of %0, captured BEFORE arg parsing: `shift`
REM in this environment also rotates %0, so %~f0 after the loop is unreliable.
for %%I in ("%~f0") do set "scrdir=%%~dpI"
for %%I in ("%scrdir%..") do set "root=%%~fI"
set "installerDir=%scrdir%"
set "outDir=%installerDir%output"
set "getver=%installerDir%get-version.ps1"

:args
if "%~1"=="" goto :args_done
if /i "%~1"=="-DryRun"    set "DRYRUN=1"  & shift & goto :args
if /i "%~1"=="-NoBuild"   set "NOBUILD=1" & shift & goto :args
if /i "%~1"=="-PreRelease" set "PRERELEASE=1" & shift & goto :args
if /i "%~1"=="-Version"   goto :take_version
if /i "%~1"=="-NotesFile" goto :take_notesfile
echo [FAIL] Unknown argument: %~1 & exit /b 1
:take_version
shift
set "VER_OVERRIDE=%~1"
shift
goto :args
:take_notesfile
shift
set "NOTESFILE=%~1"
shift
goto :args
:args_done

REM ---- is gh available? ----
where gh >nul 2>nul
if errorlevel 1 ( echo [FAIL] gh CLI not found on PATH & exit /b 1 )

REM A typo in the changelog path used to pass silently: make-release-notes only warned
REM and the release went out with the "changelog not filled in" placeholder.
if defined NOTESFILE if not exist "%NOTESFILE%" ( echo [FAIL] -NotesFile not found: %NOTESFILE% & exit /b 1 )

REM ---- version ----
REM The authority is BuildInfo (what is actually baked into the built player), NOT
REM output\version.txt: that file survives every rebuild and stays behind from the
REM previous installer. It used to be read first, so a publish -NoBuild after a fresh
REM build quietly took the old number, found the old setup next to it and uploaded it
REM into the ALREADY published release.
set "VER=%VER_OVERRIDE%"
if not defined VER for /f "usebackq delims=" %%V in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%getver%" -RootPath "%root%"`) do set "VER=%%V"
if not defined VER if exist "%outDir%\version.txt" set /p "VER="<"%outDir%\version.txt"
if not defined VER ( echo [FAIL] cannot determine version & exit /b 1 )
REM eat any stray spaces / CRLF
for /f "tokens=1" %%V in ("!VER!") do set "VER=%%V"

REM ---- is the packaged installer behind the player? ----
if not defined VER_OVERRIDE if exist "%outDir%\version.txt" (
    set "PKGVER="
    set /p "PKGVER="<"%outDir%\version.txt"
    for /f "tokens=1" %%V in ("!PKGVER!") do set "PKGVER=%%V"
    if not "!PKGVER!"=="!VER!" (
        echo [FAIL] output\version.txt = !PKGVER! but the build carries !VER! - the installer is stale.
        echo        Rebuild it: installer\build-installer.cmd   ^(or pass -Version explicitly^)
        exit /b 1
    )
)

set "SETUP=%outDir%\KitchenDesigner-Setup-!VER!-x64.exe"
set "TAG=v!VER!"

echo.
echo ========================================
echo  Kitchen Designer -^> GitHub Release
echo  Version : !VER!
echo  Tag     : !TAG!
echo  Setup   : !SETUP!
if defined PRERELEASE echo  Channel : PRERELEASE ^(releases/latest will NOT point at it^)
if defined DRYRUN echo  MODE    : DRY RUN
echo ========================================
echo.

if defined DRYRUN goto :dry_plan

REM ---- build the installer if it is missing ----
if not exist "!SETUP!" (
    if defined NOBUILD ( echo [FAIL] -NoBuild, but !SETUP! is missing & exit /b 1 )
    echo === [1/5] Building installer ===
    call "%installerDir%build-installer.cmd"
    if errorlevel 1 ( echo [FAIL] build-installer failed & exit /b 1 )
    if not exist "!SETUP!" ( echo [FAIL] still no setup after build & exit /b 1 )
)

REM ---- post-build smoke test: 5000+ tests can be green while the PLAYER itself is
REM broken (fails to start, MCP never comes up) - that reached a release before this
REM step existed. tools\smoke-test.ps1 launches the actual Build\KitchenDesigner.exe on
REM its OWN mcp port (never 9337, so it never fights an already-running user instance)
REM and drives it over MCP. A failure here stops the release; see tools\smoke-test.ps1
REM and installer\PUBLISH.md step 7 for what it checks.
set "playerExe=%root%\Build\KitchenDesigner.exe"
if not exist "!playerExe!" ( echo [FAIL] !playerExe! not found - cannot smoke-test a player that was not built & exit /b 1 )
echo === [2/5] Smoke-testing the built player ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%root%\tools\smoke-test.ps1" -ExePath "!playerExe!"
if errorlevel 1 ( echo [FAIL] Smoke test failed - release NOT published & exit /b 1 )

REM ---- repo slug from origin (gh resolves the repo from the current directory) ----
pushd "%root%"
for /f "usebackq delims=" %%S in (`gh repo view --json nameWithOwner -q ".nameWithOwner"`) do set "SLUG=%%S"
popd
if not defined SLUG ( echo [FAIL] cannot resolve repo slug (gh repo view) & exit /b 1 )
echo repo: !SLUG!

REM ---- release body: fixed template + the changelog from NOTESFILE (UTF-8 via PS) ----
set "NOTES=%TEMP%\kd-release-notes.!TAG!.md"
powershell -NoProfile -ExecutionPolicy Bypass -File "%installerDir%make-release-notes.ps1" -Version "!VER!" -SetupName "KitchenDesigner-Setup-!VER!-x64.exe" -ChangelogFile "!NOTESFILE!" -OutFile "!NOTES!"
if errorlevel 1 ( echo [FAIL] make-release-notes failed & exit /b 1 )

REM ---- tag ----
git -C "%root%" rev-parse -q --verify "refs/tags/!TAG!" >nul
if errorlevel 1 (
    echo === [3/5] Creating tag !TAG! ===
    git -C "%root%" tag !TAG!
    if errorlevel 1 ( echo [FAIL] git tag failed & exit /b 1 )
    git -C "%root%" push origin !TAG!
    if errorlevel 1 ( echo [FAIL] git push tag failed & exit /b 1 )
) else (
    echo tag !TAG! exists, pushing if needed
    git -C "%root%" push origin !TAG! >nul 2>nul
)

set "GHFLAGS="
if defined PRERELEASE set "GHFLAGS=--prerelease"

REM ---- release (create, or edit + upload; idempotent) ----
gh release view "!TAG!" -R "!SLUG!" >nul 2>nul
if errorlevel 1 (
    echo === [4/5] Creating release !TAG! ===
    gh release create "!TAG!" "!SETUP!" -R "!SLUG!" --title "Kitchen Designer !VER!" --notes-file "!NOTES!" !GHFLAGS!
    if errorlevel 1 ( echo [FAIL] gh release create failed & exit /b 1 )
) else (
    echo === [4/5] Updating existing release !TAG! ===
    REM A second run used to upload the asset only and leave the body from the first
    REM run, so a corrected changelog went nowhere.
    gh release edit "!TAG!" -R "!SLUG!" --title "Kitchen Designer !VER!" --notes-file "!NOTES!" !GHFLAGS!
    if errorlevel 1 ( echo [FAIL] gh release edit failed & exit /b 1 )
    gh release upload "!TAG!" "!SETUP!" -R "!SLUG!" --clobber
    if errorlevel 1 ( echo [FAIL] gh release upload failed & exit /b 1 )
)
del "!NOTES!" >nul 2>nul

echo === [5/5] Done ===
echo.
if defined PRERELEASE (
    echo Download ^(prerelease - by tag only^):
    echo   https://github.com/!SLUG!/releases/download/!TAG!/KitchenDesigner-Setup-!VER!-x64.exe
    echo   NOTE: releases/latest and the in-app updater do NOT see a prerelease.
) else (
    echo Download ^(stable link^):
    echo   https://github.com/!SLUG!/releases/latest/download/KitchenDesigner-Setup-!VER!-x64.exe
)
echo Release page:
echo   https://github.com/!SLUG!/releases/tag/!TAG!
endlocal
exit /b 0

:dry_plan
echo [dry] would build/installer if missing: "!SETUP!"
if defined NOTESFILE (echo [dry] changelog from: !NOTESFILE!) else (echo [dry] NO -NotesFile: the changelog will be a placeholder)
echo [dry] tag !TAG! + push origin
if defined PRERELEASE (
    echo [dry] gh release create !TAG! "!SETUP!" --title "Kitchen Designer !VER!" --prerelease  ^(edit+upload if exists^)
    echo [dry] download would be: https://github.com/.../releases/download/!TAG!/KitchenDesigner-Setup-!VER!-x64.exe
    echo [dry] releases/latest and the in-app updater will NOT see it.
) else (
    echo [dry] gh release create !TAG! "!SETUP!" --title "Kitchen Designer !VER!"  ^(edit+upload if exists^)
    echo [dry] download would be: https://github.com/.../releases/latest/download/KitchenDesigner-Setup-!VER!-x64.exe
)
REM PUBLISH.md promises that -DryRun shows the release body; it never rendered one.
set "NOTES=%TEMP%\kd-release-notes.!TAG!.dryrun.md"
powershell -NoProfile -ExecutionPolicy Bypass -File "%installerDir%make-release-notes.ps1" -Version "!VER!" -SetupName "KitchenDesigner-Setup-!VER!-x64.exe" -ChangelogFile "!NOTESFILE!" -OutFile "!NOTES!"
if errorlevel 1 ( echo [FAIL] make-release-notes failed & exit /b 1 )
echo.
echo ---- release body ^(!NOTES!^) ----
powershell -NoProfile -Command "Get-Content -LiteralPath $env:NOTES -Encoding UTF8"
echo ---- end of body ----
endlocal
exit /b 0
