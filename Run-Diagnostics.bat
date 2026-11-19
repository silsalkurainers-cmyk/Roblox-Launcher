@echo off
setlocal EnableExtensions
set "LOG=%TEMP%\Roblox-Launcher-diagnostic.txt"
set "FOUND="
> "%LOG%" echo ==== Roblox Launcher diagnostic ====
>> "%LOG%" echo Started: %DATE% %TIME%
>> "%LOG%" echo Script folder: "%~dp0"
>> "%LOG%" echo Files in script folder:
dir /a "%~dp0" >> "%LOG%" 2>&1

for /r "%~dp0" %%F in (Roblox-Launcher.exe) do (
  if not defined FOUND set "FOUND=%%~fF"
)

if not defined FOUND (
  >> "%LOG%" echo ERROR: No file named Roblox-Launcher.exe found anywhere below the script folder.
  echo Could not find Roblox-Launcher.exe. See diagnostic log:
  echo "%LOG%"
  pause
  exit /b 2
)

>> "%LOG%" echo Found EXE: "%FOUND%"
for %%F in ("%FOUND%") do >> "%LOG%" echo EXE size: %%~zF bytes
echo Found: "%FOUND%"
echo Starting launcher...
start "" /wait "%FOUND%"
set "EXITCODE=%ERRORLEVEL%"
>> "%LOG%" echo Process exit code: %EXITCODE%
>> "%LOG%" echo Finished: %DATE% %TIME%
echo.
echo Diagnostic saved to: "%LOG%"
pause
exit /b %EXITCODE%
