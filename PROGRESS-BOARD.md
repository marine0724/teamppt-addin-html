# 🗺️ TEAMPPT 개발 진행 보드

> 이 파일 하나만 열어두면 "지금 어디서 뭘 하는지" 보입니다. Claude가 매 세션 함께 유지.
> **기록용 아카이브가 아니라 "지금 여기" 작업 보드.** 끝난 잎(Task)은 지우고 교체, 숲·나무 단위는 끝날 때까지 유지. (규칙: CLAUDE.md)
> 계층: **나라 > 대지 > 숲 > 나무 > 잎** (2026-06-22 재정립)
> 최종 갱신: 2026-06-25 · 현재 작업: **리디자인 Phase 0 완료(PPT검증✅). Phase 1(파일진입+덱구조) 플랜 작성완료 → 실행 대기(실행프롬프트.md).** 독백+병목진단·B1·R1·R2 완료.

### ▶ 다음 작업
**리디자인 Phase 1 실행** (subagent-driven). `실행프롬프트.md`를 새 세션에 붙여넣어 시작 → Task 1(구조 분석 로직, TDD) → 2(DeckFileReader, COM) → 3(파일진입+구조박스 UI, 데모 hero ①). Task 3 끝나면 PPT 수동검증(빈 PPT → "📂 리디자인(초안 파일)" → 구조 박스 확인).

---

## 🌍 나라 — 제품 정체성: **바이브 디자이닝**

> 커서가 "바이브 코딩"이듯, TEAMPPT는 PPT 디자인을 그렇게. **전문 역량 → 노멀 역량이 되어도 같은 결과물.**
> 설계: [vibe-designing & A-first](docs/superpowers/specs/2026-06-22-vibe-designing-a-first-design.md) · 발표 자산: [docs/PITCH.md](docs/PITCH.md)

## 🏔️ 대지 — 고객 사용 루트 (A/B/C)

```
[A 에셋 조립·커서식]      [B 리디자인·위임식]       [C 기획+에셋]
   ▲ 지금 여기                 (다음)                  (추후)
 곁에서 추천·삽입         통째로 한방 변환          백지부터 구성
 "어떤 에셋?"             "어떤 컨셉?"              "어떤 구성?"
```

> A·B의 유일한 차이 = "전체를 한 번에 갈아치우는 실행엔진"(=D)의 유무. 둘 다 같은 '읽기 엔진' 공유.
> **지금은 토대인 A를 온전하게.** A만 써도 감동이 나와야 B·C가 이상적으로 얹힌다. (D 한방엔진은 B 루트로 분리·후순위)

---

## 🌲 숲 — 지금 로드맵: **"온전한 A"** (데이터 → 공유 → 추천·진단 UX)

```
[A-1 데이터 토대]──▶[A-2 실시간 공유엔진]──▶[A-3 추천·진단 UX]      ┊  (분리) D 한방 실행엔진
      ✅ 완료              🔵 첫 조각 완료          ⬜               ┊   → B 루트·후순위
  인제스트→Supabase     슬라이드 읽기+공유표시     에셋추천+일관성진단
  벡터추천 (해자)        [슬라이드 N 공유중]
```

- **A-1:** ✅ 인제스트 LLM 이해 → 임베딩 → Supabase 업로드 + 벡터검색 읽기경로. 완료.
- **A-2:** 🔵 첫 조각(화면공유 진단) 완료. 리디자인(B) 진입 시 확장 예정.
- **A-3:** ⬜ 텍스트 질의 추천 → 현재슬라이드 추천 + 전체덱 일관성 진단.

---

## 🌲 나무 — 새 방향: **리디자인 (초안 파일 통째 → 덱 전체)** · 잎 = Phase 1 (파일진입+덱구조)

> **왜:** 실사용 워크플로 = "초안 만들어 두고 더 이쁘게". 단일 슬라이드 추천을 **파일 진입 + 덱 전체**로 일반화. 디자인-온리(내용 불변).
> 설계: [덱 리디자인 스펙](docs/superpowers/specs/2026-06-25-route-b-deck-redesign-design.md)
> 하이브리드 데모 스코프: 임팩트 큰 곳(구조박스·박스별추천·두 유사도·조립덱)은 진짜 / overflow·표·완벽합성은 검증 위주.

**Phase 로드맵:** `0 두 유사도 분리 ✅` → `1 파일진입+덱구조분석 ◀` → `2 컨셉3` → `3 박스별 덱추천` → `4 빈 템플릿 조립` → `5 재료이식(본문1~2장)`

> **Phase 0 (두 유사도 분리): ✅ 완료** — Task 1~4 (b29fd84·be10db8·c5676a6·437d924), 최종 리뷰 READY TO MERGE, PPT 수동검증 ✅(두 점수 정상). 플랜: [Phase 0](docs/superpowers/plans/2026-06-25-redesign-phase0-dual-similarity.md).

### 🍃 잎 — Phase 1: 파일 진입 + 덱 구조 분석 — **플랜 작성완료, 실행 대기**

> 플랜: [Phase 1](docs/superpowers/plans/2026-06-25-redesign-phase1-file-entry-deck-structure.md) · 실행: [실행프롬프트.md](실행프롬프트.md) (subagent-driven: opus 오케스트레이션 + sonnet 구현)

| # | 무엇 | 상태 |
|---|------|------|
| 1 | 덱 구조 분석 로직 (models+schema+parser+formatter+service, TDD) | ⬜ |
| 2 | DeckFileReader 외부 pptx 비파괴 읽기 (COM, 빌드+수동) | ⬜ |
| 3 | [리디자인] 파일진입 + 구조 요약 박스 UI (데모 hero ①, 빌드+수동) | ⬜ |

> **빌드/테스트 절차(이번 세션 확립):** 새 .cs는 `TeampptAddin.csproj`의 `<Compile Include>`에 **수동 등록 필수**(old-style csproj). 단위테스트 = 관리자 MSBuild 솔루션 빌드(`/p:RegisterForComInterop=false`) → `dotnet test --no-build -p:BuildProjectReferences=false --filter`. (플랜의 "dotnet test 1순위"는 단독으론 NuGet 참조 못 풀어 실패.)

> **브랜치:** `feat/asset-combination-recommendation`. **구현은 Sonnet** — `실행프롬프트.md` 붙여넣어 시작.
> **추천→배치 동작 확인됨(2026-06-25):** "이 조합으로 배치" → 새 슬라이드에 header+layout shapes 합체 성공. 버그 2건 해결: ① 인제스트 503 재시도 중복적재(42행=35+7, kind는 깨끗 — 추후 중복정리), ② Storage 다운로드 400(`asset.File` 파일명 대신 `Extra["remote_file"]` 사용으로 수정).
> **⚠ UI 현황:** "AI 리디자인" 바 = **조합 추천**(`RunRecommendationAsync`). 통짜 리디자인(`RunRedesignAsync`)은 코드만 있고 버튼 미연결.

---

## 🚀 새 세션 실행 프롬프트 (이걸로 시작하면 바로 이어받음)

```
PROGRESS-BOARD.md를 먼저 읽어줘.
독백+병목진단 Task 1~5 코드 완료·커밋됨. Task 6(PPT 수동검증)을 해야 해.
PowerPoint 재시작 후 "AI 리디자인" → 추천 → 배치 → 검수 흐름에서:
① 한국어 독백 버블 ② 유사도 표시 ③ 병목 4분류 ④ debug.log reasoning 확인.
브랜치: feat/asset-combination-recommendation.
```

## 🐛 버그 / 📋 요구사항 (다음 세션)

| # | 종류 | 내용 | 상태 |
|---|------|------|------|
| B1 | 버그 | 인제스트 재시도 시 애니메이션 안 뜸 → `ResumeIngestAsync`에서 `_ingestLastIndex=-1` 리셋 누락이 원인. 수정+빌드 완료(로그 0건). | ✅ |
| R1 | 요구 | 팀원 자동 배포. 설계: [auto-update spec](docs/superpowers/specs/2026-06-24-auto-update.md). **핵심: 업데이트엔 UAC 불필요**(GUID·경로 고정→파일 덮어쓰기만). | ✅ 배포 완료 |
| R2 | 버그 | **install.bat robocopy 신규설치 0개 복사** — `%~dp0`가 `\`로 끝나 `robocopy "C:\path\"`의 `\"`가 따옴표 이스케이프→source 깨짐→0개(exit 0 조용한 실패)→이후 RegAsm이 "RegAsm 실패" 오해 유발. 개발은 register-addin.ps1(직접등록)이라 미발견. **수정+재릴리스 완료**(c78a73c). | ✅ |

> **R1 완료 (2026-06-24):**
> - ✅ 코드 전부(`UpdateService`·`updater.bat`·패널 배너·고정경로 install/uninstall) + `docs/download.html`·`version.json` (main 반영, Pages 라이브).
> - ✅ **GitHub Release `v1.0.0` 생성 + `TeampptAddin-1.0.0.zip` 업로드 완료** (다운로드 작동). 저장소 일원화: `teamppt-addin`은 archive, **`teamppt-addin-html` 원툴**.
> - ✅ 패키징 자동화 `scripts/package-release.ps1` (api-keys.json 제외 검증·pdb 제거·README 동봉).
> - ⬜ **남은 외부 작업(코딩 아님):** ① 팀원에게 `api-keys.json` 비공개 전달 + `%LOCALAPPDATA%\TeampptAddin\app\Assets\`에 배치 안내. ② 자동 업데이트 실측: 버전↑→새 Release→version.json 갱신→PPT 재시작 시 배너→"지금 적용" 교체 확인.
>
> **다음 릴리스 절차:** AssemblyInfo 버전↑ → Release 빌드 → `scripts/package-release.ps1` → 출력되는 `gh release create` 실행 + `docs/version.json` 갱신·푸시.

> **R2 완료 (2026-06-25) — 배포/설치 경로 전체 검수:**
> - ✅ **install.bat robocopy 버그 수정**(트레일링 백슬래시 제거 + robocopy 8+ 실패 시 중단). 시뮬레이션으로 0개→17개 복사 확인.
> - ✅ **v1.0.0 자산 교체 재업로드** (install.bat 픽스 + pdb 제거 반영). 다운로드 SHA=gh digest=로컬 `c674b311…` 일치 확인. version.json은 1.0.0 유지(변경 불필요).
> - ✅ **검수 통과 항목:** zip 다운로드 작동·Windows Expand-Archive 백슬래시 경로 정상 추출·Pages(version.json/download.html 200, 링크·안내 정확)·자동업데이트 매니페스트 URL 작동(UpdateService "⚠확인필요" 주석 해소)·**api-keys.json 없이도 애드인 정상 로드**(OnConnection 키 안읽음, 패널 lazy, 아이콘 try/catch)·updater.bat robocopy 경로 안전.
> - ⚠ **환경 caveat(코드 아님):** 다운로드 .bat은 SmartScreen/MOTW 경고 가능 / RegAsm Framework64라 **64비트 Office 가정**(32비트 PPT면 별도 처리 필요).
> - ⬜ **남은 외부 작업:** ① 팀원에게 `api-keys.json` 비공개 전달 + `%LOCALAPPDATA%\TeampptAddin\app\Assets\` 배치. ② 다른 PC에서 install.bat 실제 1회 검증(코드 시뮬레이션은 통과, 실기기 미검증).

> **보류(Route B 이후):** UIUX 프롬프트 수정 · kind 3분류 · Scratch 삽입 · 이미지 미화 · 덱 전체 일괄

> **보류:**
> - 한글 폰트 커버리지 (영문 전용 폰트 → `Font.NameFarEast` 이원 체계 필요)
> - 폰트+팔레트 동시 적용 UX (버그3)
> - 덱 전체 통일 (Scratch 삽입 이후)
> - 화면공유 진단 확장 (리디자인 B 진입 시 — 슬라이드 변경 감지, 에셋 추천 연동, 전체덱 일관성 진단)
