@echo off
rem Double-click to open the server manager window (state, setup checks, start, stop, build, maps, admin password).
start "" powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Server-Manager.ps1"
