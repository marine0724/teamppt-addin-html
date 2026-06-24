# 🗺️ TEAMPPT 개발 진행 보드

> 이 파일 하나만 열어두면 "지금 어디서 뭘 하는지" 보입니다. Claude가 매 세션 함께 유지.
> **기록용 아카이브가 아니라 "지금 여기" 작업 보드.** 끝난 잎(Task)은 지우고 교체, 숲·나무 단위는 끝날 때까지 유지. (규칙: CLAUDE.md)
> 계층: **나라 > 대지 > 숲 > 나무 > 잎** (2026-06-22 재정립)
> 최종 갱신: 2026-06-24 · 현재 작업: **에셋 조합 추천 코드 완료(10커밋, 110 tests) — 수동 검증(SQL 재실행 + 재인제스트 + PPT 동작) 대기**

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

## 🍃 잎 — 지금 작업: 에셋 조합 추천 (Route B 1단계) — 코드 구현 완료, 수동 검증 대기

> **방향 전환(2026-06-24):** 직전 "단일 에셋 + 즉시 재료 주입" 폐기. 올바른 순서 = **① 추천 → ② 배치 → ③ 조립 → ④ 재료 이식.** 이번은 **①추천까지만.**
> 설계: [에셋 조합 추천](docs/superpowers/specs/2026-06-24-asset-combination-recommendation-design.md) (확정). taxonomy = **slide / header / layout / component**.

| # | 무엇 | 상태 |
|---|------|------|
| 1 | 설계 스펙 작성 | ✅ |
| 2 | 구현 플랜 작성 | ✅ |
| 3 | 인제스트 4분류+판단필드 스키마 (T1-T3) | ✅ |
| 4 | 번들 재인제스트 (수동: SQL 재실행 + PPT 재인제스트) | 🔵 사용자 조치 |
| 5 | 초안 이해 확장 (T5) | ✅ |
| 6 | 추천 엔진 Ⓑ + 파서 + 후보 풀 + 리커맨더 (T6-T8) | ✅ |
| 7 | 오케스트레이터 + UI 카드 + 배선 (T9-T10) | ✅ |
| 8 | PowerPoint 수동 검증 (본문/표지 슬라이드 추천 동작) | 🔵 다음 |
| 9 | 다음 스펙(배치 = 빈 템플릿) | ⬜ |

> **브랜치:** `feat/asset-combination-recommendation` (10 commits, 110/110 tests).
> **수동 조치:** (1) Supabase SQL Editor에서 `docs/SUPABASE-SETUP.md`의 `match_assets` 함수 재실행(DROP + CREATE). (2) PowerPoint에서 번들 재인제스트. (3) AI탭 리디자인 바 클릭 → 추천 카드 동작 확인.

---

## 🚀 새 세션 실행 프롬프트 (이걸로 시작하면 바로 이어받음)

```
PROGRESS-BOARD.md와 설계 스펙
docs/superpowers/specs/2026-06-24-asset-combination-recommendation-design.md
를 먼저 읽어줘. "에셋 조합 추천" 1단계(추천까지만)를 구현할 차례야.

방향: 직전 "단일 에셋 + 즉시 재료 주입"은 폐기됐고, 이제는 초안의
재료 종류·양·의도·목적을 보고 에셋 조합(헤더+레이아웃+컴포넌트N,
또는 cover/end는 통짜 slide)을 추천만 한다. 슬라이드 배치·재료 이식은
다음 스펙이니 이번엔 하지 마.

순서: writing-plans 스킬로 구현 플랜부터 만들고, 내가 검토한 뒤 진행.
플랜은 (1) 인제스트 4분류+판단필드 스키마, (2) 번들 재인제스트,
(3) 초안 이해 확장(purpose·neededCombination), (4) 추천 엔진 Ⓑ
(kind별 벡터 후보 → LLM 조합 선택), (5) UI 종류별 추천 카드 순서로.

불변 원칙: 사실=COM/벡터, 판단=LLM. LLM은 텍스트 생성 금지.
이번 스펙은 슬라이드 비파괴 이전(추천만). API 키 문서/커밋 평문 금지.
빌드·검증은 CLAUDE.md 절차(관리자 MSBuild + DLL 타임스탬프 + 로그 0건),
순수 로직은 비-UAC 워크플로로 TDD.
```

## 🐛 버그 / 📋 요구사항 (다음 세션)

| # | 종류 | 내용 | 상태 |
|---|------|------|------|
| B1 | 버그 | 인제스트 재시도 시 애니메이션 안 뜸 → `ResumeIngestAsync`에서 `_ingestLastIndex=-1` 리셋 누락이 원인. 수정+빌드 완료(로그 0건). | ✅ |
| R1 | 요구 | 팀원 자동 배포. 설계: [auto-update spec](docs/superpowers/specs/2026-06-24-auto-update.md). **핵심: 업데이트엔 UAC 불필요**(GUID·경로 고정→파일 덮어쓰기만). 흐름=시작 시 version.json 확인→zip(Releases) staging→재시작 시 updater.bat 스왑. | 🔵 진행 중 |

> **R1 진행 상황 (2026-06-24) — 코드 전부 완료, 빌드 0건:**
> - ✅ 설계 스펙 · AssemblyVersion 체계 · `UpdateService.cs` · `updater.bat` · `docs/version.json` · `docs/download.html` · Connect.cs 시작 트리거 · **패널 상단 업데이트 배너(마커 폴링 방식)** · **install/uninstall.bat 고정경로(`%LOCALAPPDATA%\TeampptAddin\app\`) 재작성**
> - ✅ Pages 주소 확인됨(`marine0724.github.io/teamppt-addin-html/`) → ManifestUrl 정확. Release 빌드 0건. **키 제외 릴리스 zip 생성**: `dist/TeampptAddin-1.0.0.zip`(801KB, api-keys.json 제외, README+install/uninstall 동봉). 키 전달=비공개 별도(팀원이 `%LOCALAPPDATA%\TeampptAddin\app\Assets\`에 1회 배치, updater가 안 지움).
> - ⬜ **다음(외부 작업):** ① 코드+docs 커밋·푸시(Pages에 download.html·version.json 반영). ② GitHub Releases에 `v1.0.0` 생성하고 `dist/TeampptAddin-1.0.0.zip` 업로드(gh 미인증 → 수동 또는 `gh auth login`). ③ 팀원에게 api-keys.json 비공개 전달. ④ end-to-end: AssemblyVersion↑→Release빌드→zip→새 Release→version.json 갱신→PPT 재시작 시 배너→"지금 적용" 자동교체 확인.

> **보류(Route B 이후):** UIUX 프롬프트 수정 · kind 3분류 · Scratch 삽입 · 이미지 미화 · 덱 전체 일괄

> **보류:**
> - 한글 폰트 커버리지 (영문 전용 폰트 → `Font.NameFarEast` 이원 체계 필요)
> - 폰트+팔레트 동시 적용 UX (버그3)
> - 덱 전체 통일 (Scratch 삽입 이후)
> - 화면공유 진단 확장 (리디자인 B 진입 시 — 슬라이드 변경 감지, 에셋 추천 연동, 전체덱 일관성 진단)
