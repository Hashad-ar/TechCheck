@echo off
setlocal
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Install-TechCheck.ps1"
set "techcheck_exit_code=%ERRORLEVEL%"
echo.
if not "%techcheck_exit_code%"=="0" echo Installation failed. Review the message above.
pause
exit /b %techcheck_exit_code%
