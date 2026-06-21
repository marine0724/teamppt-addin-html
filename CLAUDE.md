# TEAMPPT Add-in — 개발 가이드

## 개발 진행 보드 (PROGRESS-BOARD.md) — 매 세션 필수

- **매 세션 [PROGRESS-BOARD.md](PROGRESS-BOARD.md)를 함께 유지한다.** 세션 시작 시 먼저 읽고, 작업이 진척될 때마다 갱신한다. 이건 공식 인계사항 — 이 보드가 살아있어야 사용자가 진행을 관리할 수 있다. (새 세션이 이걸 빠뜨리는 일이 없도록 여기에 명문화함)
- **기록용 아카이브가 아니라 "지금 여기" 작업 보드다.** 완료된 작업을 계속 쌓아두는 로그가 아니다.
- 수행하던 **잎(Task)**이 끝나면 → 그 항목을 보드에서 지우고 다음 것으로 교체한다.
- 단, **숲(제품 로드맵)·나무(현재 plan) 단위가 끝난 게 아니면 그 골격은 그대로 둔다.** 완료된 세부 잎만 갈아끼우고, 상위 구조는 그 단위가 실제로 끝날 때까지 유지한다.
- 목적: 보드는 항상 top 문제(미해결 최상위)에서 현재 위치까지의 경로를 보여주고, **top 문제가 해결될 때까지** 끌고 간다.

## 빌드

- **MSBuild 경로**: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`
- **COM 등록 때문에 관리자 권한 필수** (`RegisterForComInterop=true`)
- 빌드 명령 (관리자 권한):

```powershell
Start-Process -FilePath "cmd.exe" -ArgumentList '/c "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal > "c:\Projects\teamppt-addin\build.log" 2>&1' -Verb RunAs -Wait -WindowStyle Hidden
```

- 빌드 결과는 `build.log` 파일 끝 5줄 확인 (`tail -5 build.log`)
- `/t:Build` 사용 (변경분만 빌드). 전체 재빌드 필요시 `/t:Rebuild`

## API 키

- API 키를 문서나 커밋에 평문으로 절대 포함하지 않는다
