@echo off
setlocal

if "%~1"=="" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-from-source.ps1" -Action Build -OpenOutput
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-from-source.ps1" %*
)

set "DISKSCOPE_BUILD_EXIT=%ERRORLEVEL%"
if not "%DISKSCOPE_BUILD_EXIT%"=="0" (
  echo.
  echo DiskScope could not be built from source. Review the error above.
  if "%CI%"=="" pause
)

exit /b %DISKSCOPE_BUILD_EXIT%
