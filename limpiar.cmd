@echo off
REM Repara el error "project.assets.json no tiene un destino para net10.0-windows".
REM Cerra Visual Studio ANTES de ejecutar esto.

cd /d "%~dp0"

echo.
echo === Limpieza de compilacion ===
echo.

tasklist /FI "IMAGENAME eq devenv.exe" 2>nul | find /I "devenv.exe" >nul
if not errorlevel 1 (
    echo.
    echo   Visual Studio esta abierto. Cerralo primero y volve a ejecutar
    echo   este archivo, o su cache va a reescribir lo que limpiemos.
    echo.
    pause
    exit /b 1
)

echo Apagando servidores de compilacion...
dotnet build-server shutdown >nul 2>&1

echo Borrando cache de Visual Studio...
if exist ".vs" rmdir /s /q ".vs"

echo Borrando obj y bin...
for /d %%p in (src\*) do (
    if exist "%%p\obj" rmdir /s /q "%%p\obj"
    if exist "%%p\bin" rmdir /s /q "%%p\bin"
)
for /d %%p in (tests\*) do (
    if exist "%%p\obj" rmdir /s /q "%%p\obj"
    if exist "%%p\bin" rmdir /s /q "%%p\bin"
)

echo Restaurando paquetes...
dotnet restore PokerProOS.slnx --nologo -v q
if errorlevel 1 (
    echo.
    echo La restauracion fallo.
    pause
    exit /b 1
)

echo.
echo Listo. Ya podes ejecutar ejecutar.cmd, o abrir PokerProOS.slnx en
echo Visual Studio (el .slnx, no un .sln).
echo.
pause
