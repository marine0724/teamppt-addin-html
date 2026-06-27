@echo off
setlocal
chcp 65001 >nul

echo ========================================
echo  TEAMPPT Add-in 설치
echo ========================================

rem ── 설치 위치를 고정한다 (%%LOCALAPPDATA%%\TeampptAddin\app) ──
rem    이 경로를 RegAsm로 등록하므로, 이후 업데이트는 이 폴더의 파일만
rem    덮어쓰면 된다(자동 업데이트). 압축 푼 위치가 어디든 여기로 복사.

set SRC=%~dp0
set APP=%LOCALAPPDATA%\TeampptAddin\app

rem 빌드 산출물(DLL) 위치 결정: Release 우선, 없으면 Debug, 없으면 스크립트 폴더 자체
set BIN=%SRC%bin\Release
if not exist "%BIN%\TeampptAddin.dll" set BIN=%SRC%bin\Debug
if not exist "%BIN%\TeampptAddin.dll" set BIN=%SRC%

rem 트레일링 백슬래시 제거: robocopy "C:\path\" 는 \" 가 따옴표를 이스케이프해
rem 인자가 깨지고 0개 복사된다(exit 0이라 조용히 실패). %~dp0는 항상 \로 끝나므로 필수.
if "%BIN:~-1%"=="\" set BIN=%BIN:~0,-1%

if not exist "%BIN%\TeampptAddin.dll" (
    echo [오류] TeampptAddin.dll을 찾을 수 없습니다.
    pause
    exit /b 1
)

echo.
echo [1/3] 설치 폴더로 복사: %APP%
if not exist "%APP%" mkdir "%APP%"
robocopy "%BIN%" "%APP%" /E /R:3 /W:1 >nul
if %ERRORLEVEL% GEQ 8 (
    echo [오류] 파일 복사 실패. robocopy 코드 %ERRORLEVEL%
    pause
    exit /b 1
)
rem updater.bat은 빌드 산출물에 포함됨(Content). 누락 시 스크립트 폴더에서 보강.
if not exist "%APP%\updater.bat" if exist "%SRC%updater.bat" copy /Y "%SRC%updater.bat" "%APP%\updater.bat" >nul

set DLL_PATH=%APP%\TeampptAddin.dll

echo.
echo [2/3] COM 등록 중...
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe "%DLL_PATH%" /codebase /tlb
if errorlevel 1 (
    echo.
    echo [오류] RegAsm 실패. 관리자 권한으로 실행하세요.
    pause
    exit /b 1
)

echo.
echo [3/3] PowerPoint Add-in 레지스트리 등록 중...
reg add "HKCU\Software\Microsoft\Office\PowerPoint\Addins\TeampptAddin.Connect" /v "FriendlyName" /t REG_SZ /d "TEAMPPT" /f
reg add "HKCU\Software\Microsoft\Office\PowerPoint\Addins\TeampptAddin.Connect" /v "Description" /t REG_SZ /d "TEAMPPT Header Assets Add-in" /f
reg add "HKCU\Software\Microsoft\Office\PowerPoint\Addins\TeampptAddin.Connect" /v "LoadBehavior" /t REG_DWORD /d 3 /f

echo.
echo ========================================
echo  설치 완료! PowerPoint를 재시작하세요.
echo  (이후 업데이트는 PowerPoint 안에서 자동 적용됩니다)
echo ========================================
pause
