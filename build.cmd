@echo off
setlocal
rem Build SetupCrate. Uses local SDK from ..\tools\dotnet when present.
set "ROOT=%~dp0.."
set "DOTNET=%ROOT%\tools\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"

set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_HOME=%ROOT%\tools\dotnet-home"
set "NUGET_PACKAGES=%ROOT%\tools\nuget"
set "TMP=%ROOT%\tools\tmp"
set "TEMP=%ROOT%\tools\tmp"
set "APPDATA=%ROOT%\tools\appdata"
set "LOCALAPPDATA=%ROOT%\tools\localappdata"
if not exist "%TMP%" mkdir "%TMP%"

"%DOTNET%" publish "%~dp0SetupCrate.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)

echo.
echo Done: %~dp0bin\Release\net10.0-windows\win-x64\publish\SetupCrate.exe
endlocal
