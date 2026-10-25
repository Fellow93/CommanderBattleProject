@echo off
rem Double-click to start the Commander Battle dedicated server; arguments go to Start-Server.ps1 (e.g. -Restart).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Server.ps1" %*
if "%~1"=="" pause
