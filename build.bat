@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PROJECT=%SCRIPT_DIR%PremiumDock\PremiumDock.csproj
set OUTPUT=%SCRIPT_DIR%dist

if not exist "%OUTPUT%" mkdir "%OUTPUT%"

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT%"

if %errorlevel% neq 0 (
  echo Build failed.
  exit /b %errorlevel%
)

echo Build complete. Output: %OUTPUT%
endlocal
