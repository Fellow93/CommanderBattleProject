@echo off
rem Double-click to build Commander Battle and install it into the game and the dedicated server.
rem A running server is stopped first: it locks its DLLs. Close the game too, for the client DLLs.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Server.ps1" -Stop
dotnet build "%~dp0..\CommanderBattle.sln" -c Release -v:minimal
if errorlevel 1 (echo. & echo BUILD FAILED. Is the game still open? It locks the client DLLs.) else (echo. & echo Built and installed.)
pause
