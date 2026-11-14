@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package_release.ps1"
exit /b %ERRORLEVEL%
