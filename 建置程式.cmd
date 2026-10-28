@echo off
setlocal
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:x64 /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:"PS鍵盤.exe" "PS鍵盤.cs"
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
echo Build complete.
pause
