@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PROJECT=%SCRIPT_DIR%PremiumDock\PremiumDock.csproj
set OUTPUT=%SCRIPT_DIR%dist

if not exist "%OUTPUT%" mkdir "%OUTPUT%"

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
  echo [ERROR] .NET SDK not found. Please install the official .NET SDK to build this project.
  echo         Download: https://dotnet.microsoft.com/download
  goto :finish
)

rem Framework-dependent publish to avoid bundling extra runtime libraries.
dotnet publish "%PROJECT%" -c Release -o "%OUTPUT%"
if %errorlevel% neq 0 (
  echo [ERROR] Build failed. See logs above.
  goto :finish
)

echo Build complete. Output: %OUTPUT%

:finish
echo.
echo Press any key to close this window...
pause >nul
endlocal
