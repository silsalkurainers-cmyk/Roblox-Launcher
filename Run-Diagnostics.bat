@echo off
setlocal
set "LOG=%TEMP%\Roblox-Launcher-diagnostic.txt"
echo ==== Roblox Launcher diagnostic ==== > "%LOG%"
echo Started: %DATE% %TIME% >> "%LOG%"
echo EXE: "%~dp0Roblox-Launcher.exe" >> "%LOG%"
if not exist "%~dp0Roblox-Launcher.exe" (
  echo ERROR: EXE is missing. >> "%LOG%"
  echo Diagnostic log: "%LOG%"
  pause
  exit /b 2
)
start "" /wait "%~dp0Roblox-Launcher.exe"
echo Process exit code: %ERRORLEVEL% >> "%LOG%"
echo Finished: %DATE% %TIME% >> "%LOG%"
echo Diagnostic saved to: "%LOG%"
pause
