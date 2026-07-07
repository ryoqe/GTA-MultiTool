@echo off
chcp 65001 >nul

if "%~1"=="ELEVATED" goto :run_payload

fsutil dirty query %systemdrive% >nul 2>&1
if %errorLevel% neq 0 (
    echo ??????????? ????? ??????????????...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -ArgumentList 'ELEVATED' -Verb RunAs"
    exit /b
)

:run_payload
set "BATCH_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0gta_multitool.ps1"
exit /b