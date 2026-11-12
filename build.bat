@echo off
setlocal
echo Compiling SampLauncher...
taskkill /f /im SampLauncher.exe 2>nul
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo Error: csc.exe not found at %CSC%
    exit /b 1
)

"%CSC%" /nologo /target:winexe /platform:x86 /unsafe /optimize+ /r:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll /win32icon:src\app.ico /out:..\SampLauncher.exe src\*.cs src\Properties\*.cs

if %ERRORLEVEL% equ 0 (
    echo [SUCCESS] SampLauncher.exe compiled successfully!
) else (
    echo [FAILED] Compilation failed with error code %ERRORLEVEL%
)
