@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo  SleepPrompt v1.2 - build and install to Desktop
echo  (standby S0 + no stale window / no UI-thread hang after wake)
echo ============================================================
echo.

set "FR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FR%\csc.exe" set "FR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FR%\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] .NET Framework C# compiler not found. Nothing was changed.
  goto :end
)

rem No Reference Assemblies on this machine: WPF dlls are in runtime's WPF\ subfolder.
set "WPFREF=%FR%\WPF"
if not exist "%WPFREF%\PresentationFramework.dll" (
  echo [ERROR] WPF assemblies not found at "%WPFREF%". Nothing was changed.
  goto :end
)

echo [1/3] Compiling SleepApp.cs ...
if exist SleepPrompt.exe del /f /q SleepPrompt.exe
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ ^
  /out:SleepPrompt.exe /win32icon:app.ico /win32manifest:app.manifest ^
  /lib:%FR%,%WPFREF% ^
  /r:System.dll /r:System.Xaml.dll ^
  /r:"%WPFREF%\WindowsBase.dll" ^
  /r:"%WPFREF%\PresentationCore.dll" ^
  /r:"%WPFREF%\PresentationFramework.dll" ^
  SleepApp.cs
if errorlevel 1 (
  echo.
  echo [ERROR] BUILD FAILED. Desktop file was NOT touched.
  echo   Compiler : %CSC%
  echo   RefDir   : %WPFREF%
  echo   Please paste this whole window back for diagnosis.
  goto :end
)
if not exist "SleepPrompt.exe" (
  echo [ERROR] Output SleepPrompt.exe missing. Desktop file was NOT touched.
  goto :end
)
echo       Build OK.

echo [2/3] Backing up the current Desktop exe ...
echo [3/3] Installing to Desktop ...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& { $d=[Environment]::GetFolderPath('Desktop'); $n=([char]0x8FDB+[char]0x5165+[char]0x7761+[char]0x7720)+'.exe'; $t=Join-Path $d $n; $p=(Get-Location).Path; $s=Join-Path $p 'SleepPrompt.exe'; $b=Join-Path $p 'desktop-backup'; if(-not (Test-Path -LiteralPath $b)){ New-Item -ItemType Directory -Path $b -Force | Out-Null }; if(Test-Path -LiteralPath $t){ Copy-Item -LiteralPath $t -Destination (Join-Path $b 'desktop-before-install.exe') -Force }; try { Copy-Item -LiteralPath $s -Destination $t -Force -ErrorAction Stop } catch { Write-Host ('[ERROR] Install failed: '+$_.Exception.Message); Write-Host 'The original Desktop file is untouched.'; exit 1 }; $h1=(Get-FileHash -LiteralPath $s -Algorithm SHA256).Hash; $h2=(Get-FileHash -LiteralPath $t -Algorithm SHA256).Hash; Write-Host ('  SRC  SHA256 '+$h1); Write-Host ('  DESK SHA256 '+$h2); if($h1 -eq $h2){ Write-Host '  INSTALL OK - Desktop exe replaced.' } else { Write-Host '  [ERROR] Hash mismatch, please check manually.' } }"
if errorlevel 1 (
  echo.
  echo [ERROR] Install step failed. See message above.
  goto :end
)

echo.
echo Done. Your Desktop shortcut keeps the same filename, so it now runs v1.2.
echo Verify: run "python test_flow.py" here; case4 must print PASS.
echo To roll back, copy desktop-backup\*.exe back over the Desktop file.

:end
echo.
pause
endlocal
