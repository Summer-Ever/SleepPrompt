@echo off
setlocal
cd /d "%~dp0"

set "FR=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FR%\csc.exe" set "FR=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FR%\csc.exe"
if not exist "%CSC%" (
  echo ERROR: .NET Framework C# compiler not found.
  exit /b 1
)

rem This machine has no "Reference Assemblies" installed. The WPF assemblies
rem live in the runtime's WPF\ subfolder, which csc does not search by default.
set "WPFREF=%FR%\WPF"
if not exist "%WPFREF%\PresentationFramework.dll" (
  echo ERROR: WPF assemblies not found at "%WPFREF%".
  exit /b 1
)

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
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK -^> SleepPrompt.exe
endlocal
