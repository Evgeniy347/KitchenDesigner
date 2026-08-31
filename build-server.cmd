@echo off
setlocal

set "root=%~dp0server"

echo === Build Server ===
dotnet build "%root%\KitchenServer.slnx" -c Release
if %errorlevel% neq 0 exit /b 1

echo === Publish Web ===
dotnet publish "%root%\KitchenServer.Web\KitchenServer.Web.csproj" -c Release -o "%root%\publish\web"
if %errorlevel% neq 0 exit /b 1

echo.
echo =====================================
echo  SERVER BUILD OK
echo  Web publish: %root%\publish\web
echo =====================================
