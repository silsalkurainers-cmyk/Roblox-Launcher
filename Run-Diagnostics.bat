@echo off
setlocal EnableExtensions
set "LOG=%TEMP%\Roblox-Launcher-diagnostic.txt"
set "EXE=%~dp0Roblox-Launcher.exe"
> "%LOG%" echo ==== Roblox Launcher diagnostic ====
>> "%LOG%" echo Started: %DATE% %TIME%
>> "%LOG%" echo Script folder: "%~dp0"
>> "%LOG%" echo Files in script folder:
dir /a "%~dp0" >> "%LOG%" 2>&1
>> "%LOG%" echo Expected EXE: "%EXE%"

if not exist "%EXE%" (
  >> "%LOG%" echo ERROR: Roblox-Launcher.exe is missing beside this diagnostic script.
  echo Could not find Roblox-Launcher.exe beside this BAT file.
  echo Diagnostic saved to: "%LOG%"
  pause
  exit /b 2
)

for %%F in ("%EXE%") do >> "%LOG%" echo EXE size: %%~zF bytes
echo Found: "%EXE%"
echo Starting launcher...
start "" /wait "%EXE%"
set "EXITCODE=%ERRORLEVEL%"
>> "%LOG%" echo Process exit code: %EXITCODE%
>> "%LOG%" echo Finished: %DATE% %TIME%
echo.
echo Diagnostic saved to: "%LOG%"
pause
exit /b %EXITCODE%
