@echo off
chcp 65001 >nul 2>&1
title Limpeza de arquivos .meta - pixreward-blitz

echo ============================================
echo   LIMPEZA DE ARQUIVOS .META
echo   Pasta: pixreward-blitz
echo ============================================
echo.

set "PASTA=%~dp0pixreward-blitz"

if not exist "%PASTA%" (
    echo [ERRO] Pasta nao encontrada: %PASTA%
    echo Verifique se este .bat esta dentro de StreamingAssets.
    pause
    exit /b 1
)

echo Contando arquivos .meta...
set COUNT=0
for /r "%PASTA%" %%f in (*.meta) do set /a COUNT+=1

if %COUNT%==0 (
    echo.
    echo [OK] Nenhum arquivo .meta encontrado. Pasta ja esta limpa!
    echo.
    pause
    exit /b 0
)

echo Encontrados: %COUNT% arquivo(s) .meta
echo.
echo Removendo...

set DELETED=0
for /r "%PASTA%" %%f in (*.meta) do (
    del /f /q "%%f" >nul 2>&1
    set /a DELETED+=1
)

echo.
echo ============================================
echo   CONCLUIDO!
echo   %DELETED% arquivo(s) .meta removido(s).
echo ============================================
echo.
pause
