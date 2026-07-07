@echo off
chcp 65001 >nul
echo Locating C# compiler csc.exe...

set "CSC="
if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) else if exist "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not "%CSC%"=="" goto :compile
echo [ERROR] C# compiler csc.exe for .NET Framework 4.0 was not found.
pause
exit /b 1

:compile
echo Compiling multi-file project to gta_multitool.exe...

"%CSC%" ^
  /target:winexe ^
  /out:gta_multitool.exe ^
  /optimize+ ^
  /debug- ^
  /platform:anycpu ^
  /win32manifest:gta_multitool.manifest ^
  /reference:System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Management.dll,System.Xml.dll ^
  I18n.cs ^
  Program.cs ^
  Theme.cs ^
  ConfigManager.cs ^
  NativeMethods.cs ^
  CustomControls.cs ^
  ProcessManager.cs ^
  FirewallManager.cs ^
  GameController.cs ^
  SystemAnalyzer.cs ^
  NetworkTools.cs ^
  RegistryTools.cs ^
  OverlayForm.cs ^
  MainForm.cs

if %errorlevel% equ 0 (
    echo [SUCCESS] File gta_multitool.exe successfully created!
) else (
    echo [ERROR] Compilation failed.
)
