@echo off
setlocal
cd /d "%~dp0"
start "" "%~dp0GlassBoard.exe" --recover-widgets
