@echo off
echo.
echo === Build WebGL Debug + Deploy Debug ===
echo.

call "%~dp0build.cmd" -WebGLDebug
if %ERRORLEVEL% neq 0 (
    echo [FAIL] WebGL Debug build failed. Aborting deploy.
    exit /b 1
)

call "%~dp0deploy-debug.cmd"
if %ERRORLEVEL% neq 0 (
    echo [FAIL] Deploy failed.
    exit /b 1
)

echo.
echo === Build + Deploy Debug complete! ===
