@echo off
setlocal enabledelayedexpansion

REM Release deploy: build the image locally, ship it, sync the WebGL Release build.
REM
REM The server holds NO source tree, so docker-compose.yml's `build: context: ..` cannot be
REM resolved there (it points outside %REMOTE_DIR%). Prod therefore runs a pre-built image
REM applied through docker-compose.prod.yml - the same mechanism as deploy-debug.cmd.
REM
REM Flags:
REM   -WebGLOnly   sync only the WebGL build. It is bind-mounted (./webgl:/app/webgl:ro),
REM                so the new client goes live without rebuilding or restarting anything.

set "FLAG_WEBGL_ONLY="

:parse_args
if "%~1"=="" goto :args_done
if /i "%~1"=="-WebGLOnly" set "FLAG_WEBGL_ONLY=1"
shift
goto :parse_args
:args_done

if not defined DEPLOY_SERVER set DEPLOY_SERVER=domovoy@192.168.0.189
if not defined DEPLOY_DIR set DEPLOY_DIR=/opt/kitchen-designer
set SERVER=%DEPLOY_SERVER%
set REMOTE_DIR=%DEPLOY_DIR%
set SCRIPT_DIR=%~dp0
set SSH_FLAGS=-o ConnectTimeout=10 -o StrictHostKeyChecking=no
set IMAGE_TAG=kitchen-server:release
set COMPOSE=-f docker-compose.yml -f docker-compose.prod.yml

if defined FLAG_WEBGL_ONLY (set "TOTAL=2") else (set "TOTAL=6")

echo.
echo ========================================
if defined FLAG_WEBGL_ONLY (
    echo  Kitchen Designer - Deploy WebGL only to %SERVER%
) else (
    echo  Kitchen Designer - Deploy to %SERVER%
)
echo ========================================
echo.

REM A release deploy without the client is a silent half-deploy - fail loudly instead.
if not exist "%SCRIPT_DIR%Builds\WebGL\index.html" (
    echo ERROR: Builds\WebGL not found. Run build.cmd -WebGL first.
    exit /b 1
)

if defined FLAG_WEBGL_ONLY goto :remote_dirs

echo [1/!TOTAL!] Building Docker image locally (%IMAGE_TAG%)...
REM Context is the repo root: KitchenServer.McpContract link-compiles the shared MCP
REM contract from Assets/Scripts/Core/MCP/Contract. See server/Dockerfile.
docker build -t %IMAGE_TAG% -f "%SCRIPT_DIR%server\Dockerfile" "%SCRIPT_DIR%."
if !errorlevel! neq 0 (
    echo ERROR: docker build failed
    exit /b 1
)
echo   Done.
echo.

:remote_dirs
if defined FLAG_WEBGL_ONLY (set "STEP=1") else (set "STEP=2")
echo [!STEP!/!TOTAL!] Preparing remote directories...
REM webgl/_trash keeps superseded builds instead of deleting them (see CONVENTIONS.md).
ssh %SSH_FLAGS% %SERVER% "mkdir -p %REMOTE_DIR%/webgl/_trash %REMOTE_DIR%/data %REMOTE_DIR%/server && find %REMOTE_DIR%/webgl -maxdepth 2 -name '_trash' -prune -o -name 'WebGL_Debug.*' -exec mv -t %REMOTE_DIR%/webgl/_trash {} +"
if !errorlevel! neq 0 (
    echo ERROR: failed to prepare remote directories
    exit /b 1
)
echo   Done.
echo.

if defined FLAG_WEBGL_ONLY (set "STEP=2") else (set "STEP=3")
echo [!STEP!/!TOTAL!] Copying WebGL build...
scp %SSH_FLAGS% -r "%SCRIPT_DIR%Builds\WebGL\*" %SERVER%:%REMOTE_DIR%/webgl/
if !errorlevel! neq 0 (
    echo ERROR: scp WebGL build failed
    exit /b 1
)
echo   Done.

if defined FLAG_WEBGL_ONLY goto :done_webgl_only

echo.
echo [4/!TOTAL!] Copying Docker config...
scp %SSH_FLAGS% "%SCRIPT_DIR%server\docker-compose.yml" %SERVER%:%REMOTE_DIR%/docker-compose.yml
if !errorlevel! neq 0 (echo ERROR: scp docker-compose.yml failed & exit /b 1)
scp %SSH_FLAGS% "%SCRIPT_DIR%server\docker-compose.prod.yml" %SERVER%:%REMOTE_DIR%/docker-compose.prod.yml
if !errorlevel! neq 0 (echo ERROR: scp docker-compose.prod.yml failed & exit /b 1)
REM nginx mounts ./server/nginx.conf - copying it to the root would be a no-op.
scp %SSH_FLAGS% "%SCRIPT_DIR%server\nginx.conf" %SERVER%:%REMOTE_DIR%/server/nginx.conf
if !errorlevel! neq 0 (echo ERROR: scp nginx.conf failed & exit /b 1)
echo   Done.

echo.
echo [5/!TOTAL!] Transferring Docker image to server (docker save ^| ssh docker load)...
docker save %IMAGE_TAG% | ssh %SSH_FLAGS% %SERVER% "docker load"
if !errorlevel! neq 0 (
    echo ERROR: docker save/load failed
    exit /b 1
)
echo   Done.

echo.
echo [6/!TOTAL!] Starting Docker services on server (pre-built image, no build)...
ssh %SSH_FLAGS% %SERVER% "cd %REMOTE_DIR% && docker compose %COMPOSE% up -d"
if !errorlevel! neq 0 (
    echo ERROR: docker compose up failed
    exit /b 1
)

REM nginx.conf is bind-mounted: `up -d` leaves the running container on the old config.
REM Validate before reloading so a broken config cannot take the site down.
ssh %SSH_FLAGS% %SERVER% "cd %REMOTE_DIR% && docker compose %COMPOSE% exec -T nginx nginx -t"
if !errorlevel! neq 0 (
    echo ERROR: nginx.conf is invalid - NOT reloading. The old config is still serving.
    exit /b 1
)
ssh %SSH_FLAGS% %SERVER% "cd %REMOTE_DIR% && docker compose %COMPOSE% exec -T nginx nginx -s reload"
if !errorlevel! neq 0 (
    echo ERROR: nginx reload failed
    exit /b 1
)
echo   Done.

echo.
echo Health check...
ssh %SSH_FLAGS% %SERVER% "cd %REMOTE_DIR% && docker compose %COMPOSE% exec -T web curl -fsS http://localhost:8080/health"
if !errorlevel! neq 0 (
    echo WARNING: /health did not answer yet. Check: ssh %SERVER% "cd %REMOTE_DIR% ^&^& docker compose %COMPOSE% logs --tail 50 web"
)

echo.
echo ========================================
echo  Deploy complete!
echo  HTTPS: https://kitchendesigner.duckdns.org/
echo  HTTP:  http://kitchendesigner.duckdns.org/ (redirects to HTTPS)
echo  MCP:   http://192.168.0.189:8081/hubs/mcp
echo ========================================
endlocal
exit /b 0

:done_webgl_only
echo.
echo ========================================
echo  WebGL deployed (bind mount - no restart needed)
echo  HTTPS: https://kitchendesigner.duckdns.org/unity/
echo ========================================
endlocal
exit /b 0
