@echo off
setlocal enabledelayedexpansion

if not defined DEPLOY_SERVER set DEPLOY_SERVER=domovoy@192.168.0.189
if not defined DEPLOY_DIR set DEPLOY_DIR=/opt/kitchen-designer
set SERVER=%DEPLOY_SERVER%
set REMOTE_DIR=%DEPLOY_DIR%
set SCRIPT_DIR=%~dp0
set SSH_FLAGS=-o ConnectTimeout=10 -o StrictHostKeyChecking=no

echo.
echo ========================================
echo  Kitchen Designer - Deploy to %SERVER%
echo ========================================
echo.

echo [1/5] Building ASP.NET server...
cd /d "%SCRIPT_DIR%server"
dotnet publish KitchenServer.Web\KitchenServer.Web.csproj -c Release -o publish
if %ERRORLEVEL% neq 0 (
    echo ERROR: dotnet publish failed
    exit /b 1
)
echo   Done.

echo.
echo [2/5] Creating remote directories...
ssh %SSH_FLAGS% %SERVER% "mkdir -p %REMOTE_DIR%/webgl %REMOTE_DIR%/data"
if %ERRORLEVEL% neq 0 (
    echo ERROR: failed to create remote directories
    exit /b 1
)
echo   Done.

echo.
echo [3/5] Copying WebGL build...
if exist "%SCRIPT_DIR%Builds\WebGL\" (
    scp %SSH_FLAGS% -r "%SCRIPT_DIR%Builds\WebGL\*" %SERVER%:%REMOTE_DIR%/webgl/
    if %ERRORLEVEL% neq 0 (
        echo WARNING: scp WebGL build failed - continuing anyway
    ) else (
        echo   Done.
    )
) else (
    echo   WARNING: Builds\WebGL not found - skipping. Build Unity WebGL first.
)

echo.
echo [4/5] Copying Docker config...
scp %SSH_FLAGS% "%SCRIPT_DIR%server\Dockerfile" %SERVER%:%REMOTE_DIR%/
if %ERRORLEVEL% neq 0 (echo ERROR: scp Dockerfile failed & exit /b 1)
scp %SSH_FLAGS% "%SCRIPT_DIR%server\docker-compose.yml" %SERVER%:%REMOTE_DIR%/
if %ERRORLEVEL% neq 0 (echo ERROR: scp docker-compose.yml failed & exit /b 1)
scp %SSH_FLAGS% "%SCRIPT_DIR%server\nginx.conf" %SERVER%:%REMOTE_DIR%/
if %ERRORLEVEL% neq 0 (echo ERROR: scp nginx.conf failed & exit /b 1)
echo   Done.

echo.
echo [5/5] Starting Docker services on server...
ssh %SSH_FLAGS% %SERVER% "cd %REMOTE_DIR% && docker compose up -d --build"
if %ERRORLEVEL% neq 0 (
    echo ERROR: docker compose up failed
    exit /b 1
)

echo.
echo ========================================
echo  Deploy complete!
echo  WebGL:  http://192.168.0.189/
echo  Server: http://192.168.0.189/api/
echo ========================================

endlocal
