@echo off
REM Minimal build: compiles the DLL and drops it into GameData\KSPUtilMCPServer\.
REM Installing means copying that folder into the GameData of KSP -- this script never does it.
setlocal
cd /d "%~dp0"

if not defined KSPDIR (
    echo ERROR: KSPDIR is not set. Point it at your KSP install folder.
    exit /b 1
)

dotnet build KSPUtilMCPServer.csproj -p:KSP_DATA_DIR="%KSPDIR%\KSP_x64_Data"
if errorlevel 1 (
    echo ERROR: build failed
    exit /b 1
)

copy /y "Output\bin\KSPUtilMCPServer.dll" "GameData\KSPUtilMCPServer\" >nul
if errorlevel 1 (
    echo ERROR: could not copy the DLL into GameData
    exit /b 1
)

echo.
echo Built: GameData\KSPUtilMCPServer\KSPUtilMCPServer.dll
echo Copy GameData\KSPUtilMCPServer into the GameData of KSP to install it.
