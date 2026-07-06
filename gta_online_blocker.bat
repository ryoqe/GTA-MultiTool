@echo off
chcp 65001 >nul
:: GTA Multi-Tool Launcher
:: This script requests Administrator privileges and then launches the main PowerShell script.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Запрашиваем права администратора...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set "BATCH_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0gta_multitool.ps1"
exit /b
