@echo off
setlocal
echo ========================================================
echo  NetworkSharing.exe - Windows Native Build Script
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist %CSC% goto :nocsc

echo Compiling NetworkSharing.exe...
powershell -NoProfile -Command "& '%CSC%' /nologo /optimize+ /target:winexe /out:NetworkSharing.exe /reference:System.Windows.Forms.dll,System.Drawing.dll,System.DirectoryServices.dll,System.Net.dll,System.Core.dll,System.Security.dll,System.dll (Get-ChildItem -Path src -Filter *.cs -Recurse | Select-Object -ExpandProperty FullName)"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo  BUILD SUCCESSFUL: NetworkSharing.exe
    echo ========================================================
    powershell -NoProfile -Command "Get-Item NetworkSharing.exe | Select-Object Name, Length, LastWriteTime"
    exit /b 0
) else (
    echo.
    echo BUILD FAILED with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

:nocsc
echo Error: C# Compiler (csc.exe) not found at %CSC%
exit /b 1
