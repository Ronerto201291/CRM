@echo off
REM ========================================
REM FASE 1 - EJECUCION COMPLETA
REM ========================================

cd /d C:\CRM

echo.
echo ===================================
echo 1. COMPILAR BACKEND
echo ===================================
dotnet build backend\Erp.Api\Erp.Api.csproj --nologo

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Compilacion fallida
    pause
    exit /b 1
)

echo.
echo ===================================
echo 2. MIGRACIONES AUTO - Iniciando API
echo ===================================
echo Las migraciones se aplicaran automaticamente al iniciar...

dotnet run --project backend\Erp.Api

echo.
echo ===================================
echo 3. LISTO PARA FRONTEND
echo ===================================
echo En otra terminal, ejecutar:
echo cd frontend
echo npm run dev

pause
