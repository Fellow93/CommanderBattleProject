@echo off
rem Double-click to start the Commander Battle dedicated server, replacing one that is already running.
rem With arguments, they go to Start-Server.ps1 as given (e.g. -Stop).
if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Server.ps1" -Restart
    pause
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Server.ps1" %*
)
