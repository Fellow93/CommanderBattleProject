@echo off
rem Double-click to link the campaign maps and write the server config; arguments go to Install-CampaignScenes.ps1
rem (e.g. -AdminPassword secret). Without arguments, an admin password already in the config is kept.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-CampaignScenes.ps1" %*
if "%~1"=="" pause
