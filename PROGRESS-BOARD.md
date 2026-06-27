# 🗺️ TEAMPPT 개발 진행 보드

> **▶ 다음 세션 시작점:** **Phase 4.5 구현+리뷰 완료(READY TO MERGE). PPT 수동검증 후 main 머지.**

> 이 파일 하나만 열어두면 "지금 어디서 뭘 하는지" 보입니다. Claude가 매 세션 함께 유지.
> **기록용 아카이브가 아니라 "지금 여기" 작업 보드.** 끝난 잎(Task)은 지우고 교체, 숲·나무 단위는 끝날 때까지 유지. (규칙: CLAUDE.md)
> 계층: **나라 > 대지 > 숲 > 나무 > 잎** (2026-06-22 재정립)
> 최종 갱신: 2026-06-28 · 현재 작업: **Phase 4.5 구현+리뷰 완료 — PPT 수동검증 대기.**

### ▶ 다음 작업
**PPT 수동검증** → main 머지 → 이후 **Phase 5 재료이식** 또는 **컴포넌트 모델(6a/6b/6c, 디자이너 재저작 선행)**.

---

## 🌍 나라 — 제품 정체성: **바이브 디자이닝**

> 커서가 "바이브 코딩"이듯, TEAMPPT는 PPT 디자인을 그렇게. **전문 역량 → 노멀 역량이 되어도 같은 결과물.**
> 설계: [vibe-designing & A-first](docs/superpowers/specs/2026-06-22-vibe-designing-a-first-design.md) · 발표 자산: [docs/PITCH.md](docs/PITCH.md)

## 🏔️ 대지 — 고객 사용 루트 (A/B/C)

```
[A 에셋 조립·커서식]      [B 리디자인·위임식]       [C 기획+에셋]
 토대 ✅(계속 활용)          ▲ 지금 여기              (추후)
 곁에서 추천·삽입         통째로 한방 변환          백지부터 구성
 "어떤 에셋?"             "어떤 컨셉?"              "어떤 구성?"
```

> A·B의 유일한 차이 = "전체를 한 번에 갈아치우는 실행엔진"(=D)의 유무. 둘 다 같은 '읽기 엔진' 공유.
> **A 토대(인제스트→Supabase 벡터추천·해자) 위에 지금 B 리디자인을 올리는 중.** 단일 슬라이드 추천(A)이 검증됐고, 그걸 초안 파일 통째·덱 전체로 일반화한 게 B다. (C는 추후)

---

## 🌲 숲 — 지금 로드맵: **리디자인 (초안 파일 통째 → 덱 전체, Route B 일반화)**

> **왜:** 실사용 워크플로 = "초안 만들어 두고 더 이쁘게". 단일 슬라이드 추천(A)을 **파일 진입 + 덱 전체**로 일반화. 디자인-온리(내용 불변).
> 설계: [덱 리디자인 스펙](docs/superpowers/specs/2026-06-25-route-b-deck-redesign-design.md) · 하이브리드 데모 스코프: 임팩트 큰 곳(구조박스·박스별추천·두 유사도·조립덱)은 진짜 / overflow·표·완벽합성은 검증 위주.

```
Phase: 0 두유사도 ─▶ 1 파일진입 ─▶ 2 컨셉3 ─▶ 3 박스별추천 ─▶ 4 빈템플릿조립 ─▶ 4.5 겹침수정+출처 ─▶ 5 재료이식
        ✅          ✅           ✅        ✅            ✅(겹침발견)       🔵 다음◀            ⬜
                  (데모 hero ①)  (다리)    (데모 hero ②)                                    (본문1~2장)
```

> **밑단(A 토대, 계속 활용):** A-1 인제스트→Supabase 벡터추천(해자) ✅. A-2 화면공유 진단 첫 조각 ✅(B에서 확장 예정). 이 데이터·읽기 엔진 위에 리디자인이 얹힘.
> **Phase 0 (두 유사도 분리): ✅** — Task 1~4 (b29fd84·be10db8·c5676a6·437d924), 리뷰 READY TO MERGE, PPT 검증 ✅. 플랜: [Phase 0](docs/superpowers/plans/2026-06-25-redesign-phase0-dual-similarity.md).
> **Phase 1 (파일진입+덱구조): ✅** — Task 1~3 (dd2496a·6053a69·db0a3f2), 리뷰 READY TO MERGE, PPT 구조박스 검증 ✅(데모 hero ①). 플랜: [Phase 1](docs/superpowers/plans/2026-06-25-redesign-phase1-file-entry-deck-structure.md).
> **Phase 2 (컨설팅 컨셉): ✅** — Task 1 ConceptSuggester 로직(TDD 3/3)·Task 2 칩 질문→컨셉 3카드→확정 배너 UI (00f60f2·7d85e2c), final review READY TO MERGE, **PPT 수동검증 통과**(데모 다리). 플랜: [Phase 2](docs/superpowers/plans/2026-06-26-redesign-phase2-consulting-concept.md).
> **Phase 3 (박스별 추천): ✅** — PPT 수동검증 통과, main 머지 `31deab4`. 플랜: [Phase 3](docs/superpowers/plans/2026-06-27-glm-flash-provider-swap.md).

---

## 🌲 나무 — 현재 Phase: **Phase 4.5 — 깨끗한 빈 템플릿 조립 + 출처 기록**

> **왜:** Phase 4(`DeckAssembler`)는 구현·리뷰 완료(READY TO MERGE)지만 **PPT 수동검증서 컴포넌트 겹침 발견**(스샷). 원인 = 레이아웃에 이미 내장된 컴포넌트 위에 별도 컴포넌트 에셋을 또 얹음([DeckAssembler.cs:60-61]). → 본문을 **header+layout만**으로 조립하면 겹침 해소. 동시에 **출처 기록**(어떤 슬라이드에 어떤 에셋이 갔는지 슬라이드 Tags에 JSON)을 깔아 후속 컴포넌트교체·슬라이드별 대화형 UX 토대 마련.
> 플랜: [Phase 4.5](docs/superpowers/plans/2026-06-27-redesign-phase4_5-clean-assembly-provenance.md) (Phase 4 플랜: [link](docs/superpowers/plans/2026-06-27-redesign-phase4-deck-assembly.md)).

### 🍃 잎 — **Phase 4.5 구현+리뷰 완료 — PPT 수동검증 대기**

> **Phase 4.5 (2026-06-28): subagent-driven 구현 완료.** 3 Task 전부 구현+리뷰 Approved. 최종 브랜치 리뷰(opus): ✅ READY TO MERGE (Critical/Important 0).
> **커밋:** `51433df` body header+layout만(TDD 8/8) · `61833ff` SlideProvenance(TDD 3/3) · `c03e650` 출처 태그 기록(COM, 179/179 regression).
> **남은 것:** PPT 수동검증 → main 머지.
>
> **🗺️ 후속 로드맵(플랜의 로드맵 섹션에 박제, 디자이너 재저작·인제스트 선행이라 별도 플랜):**
> - **6a 컴포넌트 데이터 모델:** 디자이너가 레이아웃 저작 시 교체될 요소를 Group 묶고 이름 접두사 **`comp_`**(선택 창). 인제스트가 `comp_` 그룹을 ①레이아웃 일부 + ②독립 컴포넌트 에셋으로 동시 수확(pptx조각·임베딩·썸네일, `parent_layout`·`bbox`·**종횡비** 태그). DB는 일반화된 **`slot_type`**(나중 `img_`/`chart_` 무료 확장). LLM이 그룹마다 **설명** 생성(검색은 이름 아닌 임베딩으로). 벡터 이웃 **top-3 미리저장**.
> - **6b 클릭-교체:** 컴포넌트 선택 → 패널이 미리저장 3개 → bbox 먼저기록→새거삽입→**fit(비율유지 균일스케일)**→원본삭제→태그재부여+컨셉색→1회Undo. 종횡비 필터로 찌그러짐·텍스트넘침 회피.
> - **6c 슬라이드별 대화형 UX:** 출처기록(4.5)이 토대. 슬라이드 이동 감지(화면공유 조각 확장)→슬라이드 태그 조회→적용에셋 박스+슬라이드별 추천블록(이웃, 비전0)+스코프 Q&A. "슬라이드마다 채팅방" = 위임→대화형 조립 전환.
> - **유연성:** 새 마킹 필요 기능은 소스 재저작 필요(공짜 X). 로고·사진 등 자동검출 가능 슬롯은 일회성 배치 enrichment. `slot_type` 일반화 + 소스 pptx 보관으로 대부분 흡수.

> **빌드/테스트 절차(이번 세션 확립):** 새 .cs는 `TeampptAddin.csproj`의 `<Compile Include>`에 **수동 등록 필수**(old-style csproj). 단위테스트 = 관리자 MSBuild 솔루션 빌드(`/p:RegisterForComInterop=false`) → `dotnet test --no-build -p:BuildProjectReferences=false --filter`. (플랜의 "dotnet test 1순위"는 단독으론 NuGet 참조 못 풀어 실패.)

> **브랜치:** `feat/asset-combination-recommendation`. **구현은 Sonnet** — `실행프롬프트.md` 붙여넣어 시작.
> **추천→배치 동작 확인됨(2026-06-25):** "이 조합으로 배치" → 새 슬라이드에 header+layout shapes 합체 성공. 버그 2건 해결: ① 인제스트 503 재시도 중복적재(42행=35+7, kind는 깨끗 — 추후 중복정리), ② Storage 다운로드 400(`asset.File` 파일명 대신 `Extra["remote_file"]` 사용으로 수정).
> **⚠ UI 현황:** 패널에 바 2개 — ① "AI 리디자인" 바 = **단일 슬라이드 조합 추천**(`RunRecommendationAsync`), ② "📂 리디자인 (초안 파일)" 바 = **덱 파일진입+구조분석**(`RunDeckRedesignAsync`, Phase 1 신규·동작✅). 통짜 리디자인(`RunRedesignAsync`)은 코드만 있고 버튼 미연결.

---

## 🚀 새 세션 실행 프롬프트 (이걸로 시작하면 바로 이어받음)

```
PROGRESS-BOARD.md를 먼저 읽어줘.

Phase 4.5 구현+리뷰 완료, PPT 수동검증 통과.
feat/asset-combination-recommendation 브랜치를 main에 머지해줘.
이후 다음 Phase 방향 논의 (Phase 5 재료이식 vs 컴포넌트 모델 6a/6b/6c).

브랜치: feat/asset-combination-recommendation
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
