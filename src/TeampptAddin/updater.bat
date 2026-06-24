@echo off
setlocal
chcp 65001 >nul

rem ── TEAMPPT 자동 업데이터 ───────────────────────────────────────────
rem 인자: %1 = staging 폴더(새 버전 압축 해제됨), %2 = app 폴더(설치 위치)
rem 동작: PowerPoint 종료 대기 → staging을 app에 덮어쓰기 → 정리 → PowerPoint 재실행
rem COM 재등록 불필요(GUID·경로 고정 — spec 2026-06-24-auto-update.md 참고)

set STAGE=%~1
set APP=%~2

if "%STAGE%"=="" goto :bad
if "%APP%"=="" goto :bad

echo TEAMPPT 업데이트 적용 중... PowerPoint가 닫히면 자동으로 진행됩니다.

rem ── PowerPoint 종료 대기 ──
:waitloop
tasklist /FI "IMAGENAME eq POWERPNT.EXE" 2>nul | find /I "POWERPNT.EXE" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto :waitloop
)

rem 파일 핸들 해제 여유
timeout /t 1 /nobreak >nul

rem ── 파일 스왑 (.ready/.zip 등 마커 제외하고 전부 복사) ──
robocopy "%STAGE%" "%APP%" /E /XF ".ready" /R:3 /W:1 >nul

rem ── 정리 (staging 폴더와 그 부모의 마커 제거) ──
del /Q "%STAGE%\..\pending-update.json" 2>nul
if exist "%STAGE%" rmdir /S /Q "%STAGE%"

rem ── PowerPoint 재실행 ──
start "" powerpnt.exe
goto :eof

:bad
echo [오류] staging/app 경로 인자가 없습니다.
exit /b 1
