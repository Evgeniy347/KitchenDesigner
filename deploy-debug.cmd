@echo off
setlocal enabledelayedexpansion

if not defined DEPLOY_SERVER set DEPLOY_SERVER=domovoy@192.168.0.189
if not defined DEPLOY_DIR set DEPLOY_DIR=/opt/kitchen-designer
set SERVER=%DEPLOY_SERVER%
set REMOTE_DIR=%DEPLOY_DIR%
set SCRIPT_DIR=%~dp0
set SSH_FLAGS=-o ConnectTimeout=10 -o StrictHostKeyChecking=no
set IMAGE_TAG=kitchen-server:debug

echo.
echo ========================================
echo  Kitchen Designer DEBUG - Deploy to %SERVER%
echo ========================================
echo.

echo [1/6] Building Docker image locally (%IMAGE_TAG%)...
docker build -t %IMAGE_TAG% -f "%SCRIPT_DIR%server\Dockerfile" "%SCRIPT_DIR%."
if %ERRORLEVEL% neq 0 (
    echo ERROR: docker build failed
    exit /b 1
)
echo   Done.

echo.
echo [2/6] Creating remote directories...
ssh %SSH_FLAGS% %SERVER% "mkdir -p %REMOTE_DIR%/webgl %REMOTE_DIR%/data %REMOTE_DIR%/server"
if %ERRORLEVEL% neq 0 (
    echo ERROR: failed to create remote directories
    exit /b 1
)
echo   Done.

echo.
echo [3/6] Copying WebGL Debug build...
if exist "%SCRIPT_DIR%Builds\WebGL_Debug\" (
    scp %SSH_FLAGS% -r "%SCRIPT_DIR%Builds\WebGL_Debug\*" %SERVER%:%REMOTE_DIR%/webgl/
    if %ERRORLEVEL% neq 0 (
        echo WARNING: scp WebGL Debug build failed - continuing anyway
    ) else (
        echo   Done.
    )
) else (
    echo   ERROR: Builds\WebGL_Debug not found. Run build.cmd -WebGLDebug first.
    exit /b 1
)

echo.
echo [4/6] Copying Docker compose + nginx config...
scp %SSH_FLAGS% "%SCRIPT_DIR%server\docker-compose.yml" %SERVER%:%REMOTE_DIR%/docker-compose.yml
if %ERRORLEVEL% neq 0 (echo ERROR: scp docker-compose.yml failed & exit /b 1)
scp %SSH_FLAGS% "%SCRIPT_DIR%server\docker-compose.debug.yml" %SERVER%:%REMOTE_DIR%/docker-compose.debug.yml
if %ERRORLEVEL% neq 0 (echo ERROR: scp docker-compose.debug.yml failed & exit /b 1)
scp %SSH_FLAGS% "%SCRIPT_DIR%server\nginx.conf" %SERVER%:%REMOTE_DIR%/server/nginx.conf
if %ERRORLEVEL% neq 0 (echo ERROR: scp nginx.conf failed & exit /b 1)
echo   Done.

echo.
echo [5/6] Transferring Docker image to server (docker save ^| ssh docker load)...
docker save %IMAGE_TAG% | ssh %SSH_FLAGS% %SERVER% "docker load"
if %ERRORLEVEL% neq 0 (
    echo ERROR: docker save/load failed
    exit /b 1
)
echo   Done.

echo.
echo [6/6] Starting Docker services on server (no build, using pre-built image)...
ssh %SSH_FLAGS% %SERVER% "cd %REMOTE_DIR% && docker compose -f docker-compose.yml -f docker-compose.debug.yml up -d"
if %ERRORLEVEL% neq 0 (
    echo ERROR: docker compose up failed
    exit /b 1
)

echo.
echo ========================================
echo  Deploy DEBUG complete!
echo  HTTPS: https://kitchendesigner.duckdns.org/
echo  HTTP:  http://kitchendesigner.duckdns.org/ (redirects to HTTPS)
echo  MCP:   http://192.168.0.189:8081/hubs/mcp
echo ========================================

endlocal
