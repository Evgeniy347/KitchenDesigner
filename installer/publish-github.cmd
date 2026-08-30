@echo off
setlocal enabledelayedexpansion

REM ---------------------------------------------------------------------------
REM  Публикация установщика Kitchen Designer как релиза на GitHub.
REM    installer\publish-github.cmd              - собрать (если надо) и выложить релиз
REM    installer\publish-github.cmd -DryRun      - показать что будет, ничего не трогать
REM    installer\publish-github.cmd -NoBuild     - не собирать; setup\ уже должен быть в output\
REM    installer\publish-github.cmd -Version X   - явная версия (иначе из output\version.txt / BuildInfo)
REM    installer\publish-github.cmd -NotesFile F - файл с русским перечнем изменений (автор — агент;
REM                                                см. installer\PUBLISH.md и commits-since-release.ps1).
REM                                                Без него в «Что нового» попадёт заглушка + предупреждение.
REM
REM  Делает: тег v<вер> -> push origin -> gh release create/upload ассета
REM  -> печатает стабильную ссылку downloads/latest.
REM ---------------------------------------------------------------------------

set "DRYRUN="
set "NOBUILD="
set "VER_OVERRIDE="
set "NOTESFILE="

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

REM ---- gh доступен? ----
where gh >nul 2>nul
if errorlevel 1 ( echo [FAIL] gh CLI not found on PATH & exit /b 1 )

REM ---- версия ----
set "VER=%VER_OVERRIDE%"
if not defined VER if exist "%outDir%\version.txt" set /p "VER="<"%outDir%\version.txt"
if not defined VER for /f "usebackq delims=" %%V in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%getver%" -RootPath "%root%"`) do set "VER=%%V"
if not defined VER ( echo [FAIL] cannot determine version & exit /b 1 )
REM съесть возможные пробелы/CRLF
for /f "tokens=1" %%V in ("!VER!") do set "VER=%%V"

set "SETUP=%outDir%\KitchenDesigner-Setup-!VER!-x64.exe"
set "TAG=v!VER!"

echo.
echo ========================================
echo  Kitchen Designer -^> GitHub Release
echo  Version : !VER!
echo  Tag     : !TAG!
echo  Setup   : !SETUP!
if defined DRYRUN echo  MODE    : DRY RUN
echo ========================================
echo.

if defined DRYRUN goto :dry_plan

REM ---- собрать установщик, если его нет ----
if not exist "!SETUP!" (
    if defined NOBUILD ( echo [FAIL] -NoBuild, но !SETUP! отсутствует & exit /b 1 )
    echo === [1/4] Building installer ===
    call "%installerDir%build-installer.cmd"
    if errorlevel 1 ( echo [FAIL] build-installer failed & exit /b 1 )
    if not exist "!SETUP!" ( echo [FAIL] still no setup after build & exit /b 1 )
)

REM ---- slug репозитория из origin (gh определяет репо по текущему каталогу) ----
pushd "%root%"
for /f "usebackq delims=" %%S in (`gh repo view --json nameWithOwner -q ".nameWithOwner"`) do set "SLUG=%%S"
popd
if not defined SLUG ( echo [FAIL] cannot resolve repo slug (gh repo view) & exit /b 1 )
echo repo: !SLUG!

REM ---- тело релиза: фикс. шаблон + «Что нового» из NOTESFILE (UTF-8 через PS) ----
set "NOTES=%TEMP%\kd-release-notes.!TAG!.md"
powershell -NoProfile -ExecutionPolicy Bypass -File "%installerDir%make-release-notes.ps1" -Version "!VER!" -SetupName "KitchenDesigner-Setup-!VER!-x64.exe" -ChangelogFile "!NOTESFILE!" -OutFile "!NOTES!"
if errorlevel 1 ( echo [FAIL] make-release-notes failed & exit /b 1 )

REM ---- тег ----
git -C "%root%" rev-parse -q --verify "refs/tags/!TAG!" >nul
if errorlevel 1 (
    echo === [2/4] Creating tag !TAG! ===
    git -C "%root%" tag !TAG!
    if errorlevel 1 ( echo [FAIL] git tag failed & exit /b 1 )
    git -C "%root%" push origin !TAG!
    if errorlevel 1 ( echo [FAIL] git push tag failed & exit /b 1 )
) else (
    echo tag !TAG! exists, pushing if needed
    git -C "%root%" push origin !TAG! >nul 2>nul
)

REM ---- релиз (create или upload, идемпотентно) ----
gh release view "!TAG!" -R "!SLUG!" >nul 2>nul
if errorlevel 1 (
    echo === [3/4] Creating release !TAG! ===
    gh release create "!TAG!" "!SETUP!" -R "!SLUG!" --title "Kitchen Designer !VER!" --notes-file "!NOTES!"
    if errorlevel 1 ( echo [FAIL] gh release create failed & exit /b 1 )
) else (
    echo === [3/4] Uploading asset to existing release !TAG! ===
    gh release upload "!TAG!" "!SETUP!" -R "!SLUG!" --clobber
    if errorlevel 1 ( echo [FAIL] gh release upload failed & exit /b 1 )
)
del "!NOTES!" >nul 2>nul

echo === [4/4] Done ===
echo.
echo Download (stable link):
echo   https://github.com/!SLUG!/releases/latest/download/KitchenDesigner-Setup-!VER!-x64.exe
echo Release page:
echo   https://github.com/!SLUG!/releases/tag/!TAG!
endlocal
exit /b 0

:dry_plan
echo [dry] would build/installer if missing: "!SETUP!"
if defined NOTESFILE (echo [dry] changelog from: !NOTESFILE!) else (echo [dry] NO -NotesFile: «Что нового» будет заглушкой!)
echo [dry] tag !TAG! + push origin
echo [dry] gh release create !TAG! "!SETUP!" --title "Kitchen Designer !VER!"  (upload --clobber if exists)
echo [dry] download would be: https://github.com/.../releases/latest/download/KitchenDesigner-Setup-!VER!-x64.exe
endlocal
exit /b 0
