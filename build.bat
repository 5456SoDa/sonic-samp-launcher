@echo off
setlocal
echo === Sonic SAMP Launcher Build ===
taskkill /f /im SampLauncher.exe 2>nul

where cargo >nul 2>nul
if %ERRORLEVEL% neq 0 if exist "%USERPROFILE%\.cargo\bin\cargo.exe" set "PATH=%USERPROFILE%\.cargo\bin;%PATH%"

if not exist "samp_core\Cargo.toml" goto compile_cs
where cargo >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [WARN] Rust cargo not found - skipping samp_core.dll build.
    goto compile_cs
)
echo [1/3] Building samp_core.dll [Rust i686-pc-windows-msvc]...
pushd samp_core
cargo build --release
if errorlevel 1 (
    popd
    echo [FAILED] Rust samp_core build failed!
    exit /b 1
)
popd
copy /Y "samp_core\target\i686-pc-windows-msvc\release\samp_core.dll" "..\samp_core.dll" >nul
if errorlevel 1 (
    echo [FAILED] Could not copy samp_core.dll to game root!
    exit /b 1
)
echo [OK] samp_core.dll updated.

if not exist "sonic_guard\Cargo.toml" goto compile_cs
echo [2/3] Building SonicGuard.asi [Rust i686-pc-windows-msvc]...
pushd sonic_guard
cargo build --release
if errorlevel 1 (
    popd
    echo [FAILED] Rust sonic_guard build failed!
    exit /b 1
)
popd
if not exist "..\scripts" mkdir "..\scripts"
if exist "..\scripts\SonicGuard.asi.disabled" (
    copy /Y "sonic_guard\target\i686-pc-windows-msvc\release\sonic_guard.dll" "..\scripts\SonicGuard.asi.disabled" >nul
) else (
    copy /Y "sonic_guard\target\i686-pc-windows-msvc\release\sonic_guard.dll" "..\scripts\SonicGuard.asi" >nul
)
if errorlevel 1 (
    echo [FAILED] Could not copy SonicGuard.asi to scripts folder!
    exit /b 1
)
echo [OK] SonicGuard.asi updated.

:compile_cs

echo [3/3] Compiling SampLauncher.exe...
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
    exit /b %ERRORLEVEL%
)
