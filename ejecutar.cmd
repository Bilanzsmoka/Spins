@echo off
REM Arranca PokerProOS sin necesidad de Visual Studio.
REM Doble clic sobre este archivo.

cd /d "%~dp0"

echo.
echo === PokerProOS ===
echo.

REM Los nodos de MSBuild cachean la evaluacion de los csproj. Si el TFM
REM de un proyecto cambio, siguen sirviendo la version vieja y la
REM restauracion parece no tener efecto. Apagarlos evita ese fallo.
echo Apagando servidores de compilacion...
dotnet build-server shutdown >nul 2>&1

echo Compilando...
dotnet build PokerProOS.slnx -v q --nologo
if errorlevel 1 (
    echo.
    echo La compilacion fallo. Si el error menciona project.assets.json
    echo y net10.0-windows, ejecuta limpiar.cmd y volve a intentar.
    echo.
    pause
    exit /b 1
)

echo.
echo Abriendo el navegador en http://localhost:5000
start "" http://localhost:5000

echo.
echo Corriendo. Cerra esta ventana o presiona Ctrl+C para detener.
echo.
dotnet run --project src/PokerProOS.Api --no-build --urls http://localhost:5000
