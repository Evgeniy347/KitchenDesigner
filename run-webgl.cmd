@echo off
setlocal
REM Local debug: WebGL in Docker.
REM   run-webgl.cmd            - build WebGL Debug, start docker stack, open browser
REM   run-webgl.cmd -NoBuild   - skip the Unity build (serve the existing Builds\WebGL_Debug)

for %%I in ("%~dp0.") do set "root=%%~fI"

if /i "%~1"=="-NoBuild" goto :docker

echo === [1/2] Unity WebGL Debug build ===
call "%root%\build.cmd" -WebGLDebug
if %errorlevel% neq 0 (
    echo [FAIL] WebGL Debug build failed
    exit /b 1
)

:docker
if not exist "%root%\Builds\WebGL_Debug\index.html" (
    echo [FAIL] Builds\WebGL_Debug not found. Run without -NoBuild first.
    exit /b 1
)

echo === [2/2] Docker stack (nginx + web + db) ===
docker compose -f "%root%\server\docker-compose.local.yml" up -d --build
if %errorlevel% neq 0 (
    echo [FAIL] docker compose up failed
    exit /b 1
)

echo.
echo === Waiting for server to be ready (checking for non-502 response) ===
set "retries=0"
:waitloop
set /a retries+=1
if %retries% gtr 20 goto :noready
powershell -NoProfile -Command "try { $r=Invoke-WebRequest -Uri 'http://localhost:8080' -TimeoutSec 2 -UseBasicParsing; exit ($r.StatusCode -eq 502) } catch { exit 1 }"
if %errorlevel% equ 0 (
    >nul timeout /t 1 /nobreak
    goto :waitloop
)
echo === Server responded with non-502 (attempt %retries%) ===
goto :open

:noready
echo [WARN] Server still returning 502 after 20 seconds, opening anyway...

:open
echo.
echo =================================
echo  LOCAL WEBGL UP
echo  URL: http://localhost:8080
echo  Rebuild loop: build.cmd -WebGLDebug, then refresh the browser
echo  Stop: docker compose -f server\docker-compose.local.yml down
echo =================================
start "" "http://localhost:8080"
