@echo off
if not exist "%~dp0dist\win-x64\ZIP-Select.exe" (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Build.ps1" -Action Publish -Runtime win-x64
    if errorlevel 1 (
        pause
        exit /b 1
    )
)
start "" "%~dp0dist\win-x64\ZIP-Select.exe"
