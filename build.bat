@echo off
setlocal
echo ========================================================
echo Building Dual Audio Mirror (Pure WASAPI Dual Headphone)
echo ========================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set WPF_LIB="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

%CSC% /nologo /t:winexe /optimize+ /platform:x64 /out:"DualAudioMirror.exe" ^
  /lib:%WPF_LIB% ^
  /r:PresentationCore.dll ^
  /r:PresentationFramework.dll ^
  /r:WindowsBase.dll ^
  /r:System.Xaml.dll ^
  /r:System.Windows.Forms.dll ^
  /r:System.Drawing.dll ^
  /r:System.dll ^
  /r:System.Core.dll ^
  src\AudioEngine.cs src\MainWindow.cs src\Program.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] DualAudioMirror.exe built successfully!
    echo Location: %~dp0DualAudioMirror.exe
) else (
    echo.
    echo [ERROR] Compilation failed.
)
