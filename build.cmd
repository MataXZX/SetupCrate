@echo off
setlocal

dotnet publish "%~dp0SetupCrate.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
if errorlevel 1 exit /b 1

echo.
echo Done: %~dp0bin\Release\net10.0-windows\win-x64\publish\SetupCrate.exe
endlocal
