@ECHO OFF

SET scriptpath=%~dp0

REM Try installing the COM0COM driver first
powershell.exe -ExecutionPolicy Bypass %scriptpath%\.\CheckAndInstallVirtualSerialPortDriver.ps1

REM Install the GPS Simulator
powershell.exe -ExecutionPolicy Bypass %scriptpath%\.\CheckAndInstallTFGpsSimulatorWindowsApp.ps1

ECHO "Press any key to exit..."
PAUSE>NUL
