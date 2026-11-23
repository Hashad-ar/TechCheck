@echo off
setlocal
set "techcheck_uninstaller=%TEMP%\Uninstall-TechCheck-%RANDOM%-%RANDOM%.ps1"
copy /y "%~dp0Uninstall-TechCheck.ps1" "%techcheck_uninstaller%" >nul
if errorlevel 1 (
  echo Unable to prepare the TechCheck uninstaller.
  pause
  exit /b 1
)
pushd "%TEMP%" >nul
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%techcheck_uninstaller%" -KeepCommandLauncher
set "techcheck_exit_code=%ERRORLEVEL%"
del /q "%techcheck_uninstaller%" >nul 2>&1
echo.
if not "%techcheck_exit_code%"=="0" (
  echo Uninstallation failed. Review the message above.
  pause
  exit /b %techcheck_exit_code%
)
echo TechCheck uninstallation completed.
(goto) 2>nul & del /q "%LOCALAPPDATA%\TechCheck\Uninstall-TechCheck.cmd" >nul 2>&1 & rmdir "%LOCALAPPDATA%\TechCheck" >nul 2>&1 & exit /b 0
