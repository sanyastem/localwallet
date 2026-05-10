@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM ===========================================================================
REM  LocalWallet quick-run script
REM  - Starts the LocalWallet_Pixel6 AVD if no emulator is running
REM  - Waits for the emulator to finish booting
REM  - Builds the MAUI project and deploys + launches it on the emulator
REM ===========================================================================

set "SDK_DIR=%LOCALAPPDATA%\Android\Sdk"
set "EMU=%SDK_DIR%\emulator\emulator.exe"
set "ADB=%SDK_DIR%\platform-tools\adb.exe"
set "AVD=LocalWallet_Pixel6"
set "PROJECT=%~dp0src\LocalWallet\LocalWallet.csproj"

if not exist "%EMU%" (
    echo [error] emulator not found at "%EMU%"
    exit /b 1
)
if not exist "%ADB%" (
    echo [error] adb not found at "%ADB%"
    exit /b 1
)
if not exist "%PROJECT%" (
    echo [error] project not found at "%PROJECT%"
    exit /b 1
)

REM --- Is an emulator already running? ----------------------------------------
set "EMU_RUNNING="
for /f "tokens=1" %%d in ('"%ADB%" devices 2^>nul ^| findstr /b emulator-') do set "EMU_RUNNING=%%d"

if defined EMU_RUNNING (
    echo [info] emulator already online: !EMU_RUNNING!
) else (
    echo [info] starting AVD %AVD% ...
    start "" "%EMU%" -avd %AVD%

    echo [info] waiting for adb to see the device ...
    "%ADB%" wait-for-device
)

echo [info] waiting for boot to complete ...
:wait_boot
for /f "delims=" %%b in ('"%ADB%" shell getprop sys.boot_completed 2^>nul') do set "BOOT=%%b"
if not "!BOOT:~0,1!"=="1" (
    timeout /t 2 /nobreak >nul
    goto wait_boot
)
echo [info] emulator ready.

echo [info] building and deploying ...
dotnet build "%PROJECT%" -f net10.0-android -c Debug -t:Run
set "RC=%ERRORLEVEL%"

endlocal & exit /b %RC%
