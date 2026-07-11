@echo off
setlocal enabledelayedexpansion

for %%I in ("%~dp0.") do set "root=%%~fI"

set "removed=0"
set "errors=0"

for %%I in (
    "%root%\Library"
    "%root%\Temp"
    "%root%\Logs"
    "%root%\Build"
    "%root%\Builds"
    "%root%\.vs"
    "%root%\UserSettings"
    "%root%\TestResults.xml"
    "%root%\TestResult.xml"
) do (
    if exist "%%~I" (
        dir /a:d "%%~I" >nul 2>nul
        if !errorlevel! equ 0 (
            rmdir /s /q "%%~I"
        ) else (
            del /f /q "%%~I"
        )
        if !errorlevel! equ 0 (
            echo   [DEL] %%~I
            set /a removed+=1
        ) else (
            echo   [ERR] %%~I
            set /a errors+=1
        )
    )
)

REM ---- Server build artifacts ----
for %%I in (
    "%root%\server\KitchenServer.AppHost\bin"
    "%root%\server\KitchenServer.AppHost\obj"
    "%root%\server\KitchenServer.Web\bin"
    "%root%\server\KitchenServer.Web\obj"
    "%root%\server\KitchenServer.ServiceDefaults\bin"
    "%root%\server\KitchenServer.ServiceDefaults\obj"
    "%root%\server\publish"
) do (
    if exist "%%~I" (
        rmdir /s /q "%%~I"
        if !errorlevel! equ 0 (
            echo   [DEL] %%~I
            set /a removed+=1
        ) else (
            echo   [ERR] %%~I
            set /a errors+=1
        )
    )
)

for /f "delims=" %%I in ('dir /a:-d /b "%root%\*.log" 2^>nul') do (
    del /f /q "%root%\%%I" 2>nul
    if !errorlevel! equ 0 (
        echo   [DEL] %%I
        set /a removed+=1
    ) else (
        echo   [ERR] %%I
        set /a errors+=1
    )
)

echo.
echo Clean complete: %removed% items removed, %errors% errors
if %errors% gtr 0 exit /b 1 else exit /b 0
