# 자동 업데이트 — 설계 스펙 (R1 팀원 배포)

> 2026-06-24 확정. 목표: 팀원이 **최초 1회만 수동 설치**하면, 이후 업데이트는 애드인이 스스로 감지·다운로드하고 재시작 시 적용한다.

## 문제

- 현재 배포는 zip 수동 다운로드 + `install.bat`(관리자) 매번 실행 — 업데이트마다 반복 불가능.
- 이 애드인은 VSTO가 아닌 **순수 COM 애드인**(RegAsm + HKCU 레지스트리). ClickOnce 자동 업데이트 인프라가 없음.

## 핵심 기술 판단

1. **업데이트엔 UAC 불필요.** `RegAsm /codebase`는 HKCR에 `CLSID → DLL 절대경로`를 기록한다. COM GUID(`Connect`=`7B3A4D1E…`, TLB=`8A3F5E2D…`)와 설치 경로가 고정이면, DLL 내용만 바뀌는 업데이트는 레지스트리가 같은 경로를 계속 가리키므로 **재등록이 불필요**하다. → 업데이트 = 같은 경로 파일 덮어쓰기.
   - 예외: GUID 변경 또는 TLB 인터페이스 변경 시에만 RegAsm 재실행 필요. **GUID는 고정 유지**(규칙).
2. **실행 중 DLL은 잠긴다.** PowerPoint가 로드한 DLL은 덮어쓸 수 없다. 애드인 자신은 자기 DLL을 못 바꾼다. → **PPT 종료 후 외부 프로세스(updater)가 스왑**한다.
3. **설치 경로 고정.** `%LOCALAPPDATA%\TeampptAddin\app\`. 최초 설치가 여기로 복사+RegAsm, 모든 업데이트는 이 경로를 덮어쓴다.

## 흐름

```
[PPT 시작]
  애드인이 논블로킹으로 version.json(GitHub Pages) GET
    └ 원격 버전 > 로컬 버전?
         └ zip(GitHub Releases) 다운로드
              └ %LOCALAPPDATA%\TeampptAddin\update-staging\<version>\ 에 압축 해제
                   └ pending-update.json 마커 기록
                        └ 패널 상단 배너: "✨ v<X> 준비됨 — 지금 적용"
                             └ [지금 적용] 클릭
                                  └ updater.bat 실행(detached) + PPT 종료 요청
                                       └ updater: POWERPNT.EXE 종료 대기
                                            → app\ 덮어쓰기 → staging 정리 → PPT 재실행
```

## 구성 요소

### 1. 버전 체계
- 릴리스마다 `AssemblyVersion`/`AssemblyFileVersion` 증가(SemVer `MAJOR.MINOR.PATCH`).
- 런타임: `Assembly.GetExecutingAssembly().GetName().Version`.

### 2. `version.json` (docs/, GitHub Pages 호스팅)
```json
{
  "version": "1.1.0",
  "zipUrl": "https://github.com/<owner>/<repo>/releases/download/v1.1.0/TeampptAddin-1.1.0.zip",
  "notes": "인제스트 재시도 애니메이션 수정",
  "mandatory": false
}
```

### 3. `UpdateService.cs` (신규, 애드인 내)
- 시작 시 fire-and-forget(try/catch로 완전 격리, 실패해도 애드인 정상 동작).
- TLS 1.2 강제(인제스트 파이프라인과 동일 패턴).
- SemVer 비교. 원격 > 로컬일 때만 다운로드.
- zip → staging 폴더 압축 해제(`System.IO.Compression`, 이미 참조됨).
- 멱등: staging에 해당 버전 이미 있으면 다운로드 스킵.
- 완료 시 `UpdateReady(version, notes)` 이벤트.

### 4. 패널 업데이트 배너 (AssetPanel)
- 상단 슬림 배너 + "지금 적용" 버튼. 논블로킹(적용 전까지 구버전으로 계속 동작).

### 5. `updater.bat` (신규, app\에 동봉)
1. `POWERPNT.EXE` 종료 폴링 대기.
2. staging 파일을 `app\`에 덮어쓰기.
3. staging + 마커 정리.
4. PowerPoint 재실행.

### 6. `install.bat` 재작성
- 압축 푼 파일을 `%LOCALAPPDATA%\TeampptAddin\app\`로 복사.
- 그 고정 경로를 RegAsm `/codebase /tlb`(UAC, 최초 1회).
- HKCU 애드인 레지스트리 등록.

### 7. `docs/download.html`
- "최초 설치": 최신 zip(Releases) 다운로드 → install.bat(관리자, 1회) 안내.
- "이후 업데이트": 자동 — 안내만.
- version.json을 읽어 현재 최신 버전 표시.

## 호스팅
- `version.json` → docs/ (GitHub Pages).
- zip 바이너리 → GitHub Releases (git 히스토리에 바이너리 미적재).

## 작업량 추정
~반나절(4~6h). 위험 구간 = 실제 릴리스 1회로 자동 감지→적용 end-to-end 검증.

## 불변 원칙
- COM GUID 고정(절대 변경 금지 — 변경 시 자동 업데이트 깨짐).
- API 키 문서/커밋 평문 금지.
- 빌드·검증은 CLAUDE.md 절차(관리자 MSBuild + DLL 타임스탬프 + 로그 0건).
```

