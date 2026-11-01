@echo off
setlocal
cd /d "%~dp0"
if not exist "Lumina.exe" (
  echo Lumina.exe is missing. Extract the entire ZIP first.
  pause
  exit /b 1
)
start "" "%~dp0Lumina.exe"
