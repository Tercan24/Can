@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -Test -RenderPreviews
if errorlevel 1 (
  echo.
  echo Derleme basarisiz oldu.
  pause
  exit /b 1
)
echo.
echo tercan.exe hazir.
pause
