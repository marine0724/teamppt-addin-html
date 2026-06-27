@echo off
setlocal
chcp 65001 >nul

echo ========================================
echo  TEAMPPT Add-in 제거
echo ========================================

set APP=%LOCALAPPDATA%\TeampptAddin\app
set DLL_PATH=%APP%\TeampptAddin.dll

rem 구버전(스크립트 폴더에 직접 설치) 호환: 고정경로에 없으면 스크립트 폴더 시도
if not exist "%DLL_PATH%" (
    set DLL_PATH=%~dp0bin\Release\TeampptAddin.dll
    if not exist "%~dp0bin\Release\TeampptAddin.dll" set DLL_PATH=%~dp0bin\Debug\TeampptAddin.dll
)

echo.
echo [1/3] COM 등록 해제 중...
if exist "%DLL_PATH%" (
    %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe "%DLL_PATH%" /unregister
)

echo.
echo [2/3] PowerPoint Add-in 레지스트리 제거 중...
reg delete "HKCU\Software\Microsoft\Office\PowerPoint\Addins\TeampptAddin.Connect" /f 2>nul

echo.
echo [3/3] 설치 폴더 제거 중...
if exist "%LOCALAPPDATA%\TeampptAddin\app" rmdir /S /Q "%LOCALAPPDATA%\TeampptAddin\app"

echo.
echo ========================================
echo  제거 완료!
echo ========================================
pause
