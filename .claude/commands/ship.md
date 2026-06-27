---
description: 개발 업데이트 반영 — 빌드, 업데이트 기록, 진행표·웹사이트·배포파일 최신화
allowed-tools: Bash, Read, Edit, Write, Glob
argument-hint: "[이번 업데이트 한 줄 요약]"
---

새 업데이트가 생겼을 때 산출물 전반을 최신화하는 루틴이다. 아래를 **순서대로** 수행하고 각 단계 끝에 결과 한 줄만 보고. 실패하면 그 단계에서 멈추고 보고.

이번 업데이트 요약: $ARGUMENTS

## 1. 빌드 + 검증 (CLAUDE.md 규칙 준수)
- 관리자 권한 MSBuild로 빌드 (cmd 래핑/stdout redirect 금지):
  ```powershell
  Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
  ```
- 검증 (생략 금지): DLL 타임스탬프가 1분 이내인지, `build.log` 마지막에 오류 0건인지. 하나라도 실패면 빌드 안 된 것 — 멈추고 보고.

## 2. 업데이트 내용 기록
- 업데이트 로그에 이번 변경을 한 항목으로 추가 (날짜 + 의도 중심). 로그 파일이 없으면 `docs/CHANGELOG.md` 생성.

## 3. 진행표(progress-board) 최신화
- `PROGRESS-BOARD.md`를 이번 업데이트 반영해 갱신 (끝난 잎만 교체, 숲·나무 골격 유지).

## 4. 웹사이트 전반 최신화 (docs/) — 스타일 락 필수
- **먼저 `docs/STYLE.md`와 `docs/shared.css`를 읽고 시작한다. 디자인 시스템을 벗어나지 않는다.**
  - 색·여백·라운드·그림자는 `shared.css`의 CSS 변수만 사용. hex 하드코딩 금지.
  - 새 컴포넌트가 필요하면 인라인 금지 — `shared.css`에 클래스/토큰을 먼저 추가하고 그걸 쓴다.
  - 기존 컴포넌트 클래스(`.section-wrap`, `.rc`, `.atag` 등)를 재사용한다.
- 영향받는 HTML 갱신: `progress.html`, `download.html`, `features.html`, `overview.html`, `index.html` 중 해당되는 것.
- 진행률·기능·버전 표기를 1~3단계 내용과 일치시킨다. `progress.html`의 "최종 갱신 YYYY-MM-DD"도 오늘 날짜로.

## 5. 배포 다운로드 파일 최신화
- 빌드 산출물을 배포용으로 갱신하고 `docs/download.html`이 가리키는 파일/버전과 일치시킨다.
- 배포 아티팩트 경로·이름이 불명확하면 진행을 멈추고 사용자에게 정확한 배포 파일 위치를 물어본 뒤 거기에 반영.

## 완료 보고
1~5 각 단계 결과 한 줄씩. 끝에 "커밋이 필요하면 /handoff" 안내.
