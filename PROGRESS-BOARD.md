# 🗺️ TEAMPPT 개발 진행 보드

> 이 파일 하나만 열어두면 "지금 어디서 뭘 하는지" 보입니다. Claude가 매 세션 함께 유지.
> **기록용 아카이브가 아니라 "지금 여기" 작업 보드.** 끝난 잎(Task)은 지우고 교체, 숲·나무 단위는 끝날 때까지 유지. (규칙: CLAUDE.md)
> 계층: **나라 > 대지 > 숲 > 나무 > 잎** (2026-06-22 재정립)
> 최종 갱신: 2026-06-25 · 현재 작업: **판단 파이프라인 Task 4~10 코드 완료·빌드 성공, PPT 수동 검증 대기.** 추천→배치 동작 확인 완료. B1·R1 완료. R2 완료.

### ▶ 다음 세션 시작점
**단계별 독백 + 병목 진단 정밀화** (Sonnet 실행). reasoning 한국어 독백 버블 + 검색 유사도 노출 + 검수 병목 4분류. `실행프롬프트.md` 붙여넣어 Task 1부터. PPT 수동검증은 이 플랜 Task 6에서 직전 판단 파이프라인과 함께. 브랜치 `feat/asset-combination-recommendation`.
> 설계: [독백+병목진단](docs/superpowers/specs/2026-06-25-step-narration-bottleneck-diagnosis-design.md) · 플랜: [구현 플랜](docs/superpowers/plans/2026-06-25-step-narration-bottleneck-diagnosis.md)

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

## 🍃 잎 — 지금 작업: 판단 파이프라인 (관찰가능성 + 깐깐한 검수자) — **코드 완료, PPT 수동 검증 대기**

> **왜:** 구성기가 *장님*(텍스트만 봄, 썸네일 X)이라 "예쁨" 판단 불가 + 판단 근거 안 보임. 목표 = **asset-bound 임계점**(로직은 다 건강한데 에셋만 부족한 상태 재현 = 0→1 끝).
> 설계: [판단 파이프라인](docs/superpowers/specs/2026-06-25-judgment-pipeline-observability-critic-design.md) · 플랜: [구현 플랜](docs/superpowers/plans/2026-06-25-judgment-pipeline-observability-critic.md) · 실행: [실행프롬프트.md](실행프롬프트.md)
> 파이프라인: ① 이해(멀티모달) → ② 검색(벡터) → ③ 구성(멀티모달, **썸네일 봄**) → ④ 배치(COM) → ⑤ 검수(렌더+루브릭, 온디맨드, 병목진단). 모든 LLM 단계 reasoning → Trace → "🔍 판단 과정" 패널.

| # | 무엇 | 상태 |
|---|------|------|
| 1 | 설계 스펙 작성 | ✅ |
| 2 | 구현 플랜 + 실행프롬프트 작성 | ✅ |
| 3 | Task1 Trace/Critique 모델 (TDD) | ✅ 647c10f |
| 4 | Task2-3 이해·구성 reasoning (TDD) | ✅ b18e691·c36113b |
| 5 | Task4-5 Gemini 다중이미지 + 구성기 멀티모달화 | ✅ ae1b7b1·9538a2f |
| 6 | Task6-7 검수자 루브릭·파서·서비스 | ✅ eac187c·48486a4 |
| 7 | Task8 RecommendationService Trace 조립 | ✅ 7c79da3 |
| 8 | Task9-10 Trace 패널 UI + 검수 버튼 | ✅ f9a200e·dcf35ec |
| 9 | **PPT 수동 검증** | ◀ 다음 |

> **빌드/테스트 절차(이번 세션 확립):** 새 .cs는 `TeampptAddin.csproj`의 `<Compile Include>`에 **수동 등록 필수**(old-style csproj). 단위테스트 = 관리자 MSBuild 솔루션 빌드(`/p:RegisterForComInterop=false`) → `dotnet test --no-build -p:BuildProjectReferences=false --filter`. (플랜의 "dotnet test 1순위"는 단독으론 NuGet 참조 못 풀어 실패.)

> **브랜치:** `feat/asset-combination-recommendation`. **구현은 Sonnet** — `실행프롬프트.md` 붙여넣어 시작.
> **추천→배치 동작 확인됨(2026-06-25):** "이 조합으로 배치" → 새 슬라이드에 header+layout shapes 합체 성공. 버그 2건 해결: ① 인제스트 503 재시도 중복적재(42행=35+7, kind는 깨끗 — 추후 중복정리), ② Storage 다운로드 400(`asset.File` 파일명 대신 `Extra["remote_file"]` 사용으로 수정).
> **⚠ UI 현황:** "AI 리디자인" 바 = **조합 추천**(`RunRecommendationAsync`). 통짜 리디자인(`RunRedesignAsync`)은 코드만 있고 버튼 미연결.

---

## 🚀 새 세션 실행 프롬프트 (이걸로 시작하면 바로 이어받음)

```
PROGRESS-BOARD.md를 먼저 읽어줘. 판단 파이프라인 구현 중이야.
Task 1~3(Trace/Critique 모델 + 이해·구성 reasoning) 완료·커밋됨.

지금 할 일: Task 4-5 (Gemini 다중이미지 입력 + 구성기 멀티모달화).
실행프롬프트.md를 읽고 Task 4부터 시작해줘.
브랜치: feat/asset-combination-recommendation.

빌드 주의: 새 .cs는 TeampptAddin.csproj의 <Compile Include>에 수동 등록
필수(old-style csproj). 단위테스트 = 관리자 MSBuild 솔루션 빌드
(/p:RegisterForComInterop=false) → dotnet test --no-build
-p:BuildProjectReferences=false --filter.

불변 원칙: 사실=COM/벡터, 판단=LLM(텍스트 생성 금지). API 키 문서/커밋
평문 금지. 빌드·검증은 CLAUDE.md 절차(관리자 MSBuild + DLL 타임스탬프 +
로그 0건), 순수 로직은 비-UAC 워크플로로 TDD.
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
