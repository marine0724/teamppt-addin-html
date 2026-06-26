# 리디자인 Phase 3 — 박스별 덱 추천 (슬라이드 단위 에셋 매핑) — 설계

날짜: 2026-06-26
관련(부모): [덱 리디자인 스펙](2026-06-25-route-b-deck-redesign-design.md)(이 문서가 그 "Phase 3" 절을 정련·확정), [에셋 조합 추천](2026-06-24-asset-combination-recommendation-design.md)(단일 슬라이드 = Route A, 이 문서가 덱으로 일반화)
선행 완료: Phase 0(두 유사도 분리) ✅ · Phase 1(파일진입+덱구조) ✅ · Phase 2(컨설팅 컨셉3) ✅

## 한 줄 정의 / 범위

**Phase 3**은 Phase 1이 읽은 덱 구조(`DeckStructure`)와 Phase 2가 저장한 선택 컨셉(`_selectedConcept`)을 소비해, **구조 박스마다(표지·공통헤더·본문 패턴별·엔드) 가장 잘 맞는 에셋 조합을 추천하고, 추천 카드에 두 유사도 배지(재료적합·컨셉적합)를 붙여 보여주는** 단계다. 단일 슬라이드 추천(Route A, 검증됨)을 **덱 전체로 일반화**한다. 데모데이 hero ②.

**이 Phase의 끝점 = 추천 카드 표시까지.** 빈 템플릿 덱 조립(다중 에셋 합성)은 Phase 4의 별도 범위다. 추천 시점엔 조립 결과 이미지가 없으므로, 결과 기반 LLM 비전 채점(`designConcept`)은 Phase 4/5로 미룬다.

## 부모 스펙 대비 3개 정련 (이 문서가 확정)

1. **두 번째 배지 = 계산 컨셉적합 프록시**(LLM 비전 아님). 추천은 조립 *전*이라 결과 이미지가 없다. 따라서 추천 카드의 둘째 배지는 토큰0 계산값(에셋 Tags ∩ 컨셉 StyleTags + 색/폰트 role 커버). `designConcept`(LLM 비전 결과채점)는 Phase 4/5 조립 후 `DesignCritiqueService`로.
2. **본문 = 패턴 그룹 단위.** 본문 장을 도형 시그니처로 2~4개 패턴으로 묶어 패턴마다 1회 추천(각 본문 장은 자기 패턴 추천을 공유). 멀티모달·LLM 호출이 장수가 아니라 패턴수(2~4)에 비례 → 비용·지연 고정(스펙 D1 "비용 선형폭증 방지" 충족).
3. **Phase 3는 추천 카드까지.** 조립=Phase 4 분리.

## 핵심 불변 원칙 (부모 스펙에서 유지)

- **사실 = COM/벡터/계산, 판단 = LLM.** 텍스트·개수·도형은 COM 원문, 두 배지는 계산값, 역할·조합 선택만 LLM.
- **LLM은 텍스트 내용을 생성·수정하지 않는다.**
- **비파괴.** 원본 초안 파일은 ReadOnly hidden open, 어떤 경로에서도 불변.
- **두 토큰 예산.** 비싼 이해는 인제스트 타임. 런타임은 싼 매칭 — 본문 패턴(2~4)에만 멀티모달.
- **좌표 변환** `CoordinateConverter` 규칙 준수, 폴백 로직 추가 금지.
- API 키를 문서·커밋에 평문으로 넣지 않는다.

## 재사용 자산 (검증된 인터페이스)

| 능력 | 위치 | 이번 사용 |
|---|---|---|
| 덱 N장 COM 읽기 | [DeckFileReader.cs](../../../src/TeampptAddin/Core/DeckFileReader.cs) `ReadFile(path)→List<DraftProfile>` | Phase 1에서 이미 호출, `_deckProfiles`로 보관 |
| 덱 구조 분석 | [DeckStructureService.cs](../../../src/TeampptAddin/Services/DeckStructureService.cs) `AnalyzeAsync→DeckStructure` | Phase 1 산출물 `_lastStructure` 입력 |
| 슬라이드 PNG 렌더 | [SlideImageRenderer.cs](../../../src/TeampptAddin/Core/SlideImageRenderer.cs) `Render(pres, idx, png)` | 신규 `DeckSlideImageExporter`가 래핑 |
| 초안 멀티모달 이해 | [DraftUnderstandingService.cs](../../../src/TeampptAddin/Services/DraftUnderstandingService.cs) `UnderstandAsync(profile, png)` | 본문 대표 장에만 |
| 종류별 벡터 후보 | [CombinationCandidateProvider.cs](../../../src/TeampptAddin/Services/CombinationCandidateProvider.cs) `GetCandidatesAsync` | **컨셉 주입 파라미터 추가(수정)** |
| LLM 조합 추천 | [CombinationRecommender.cs](../../../src/TeampptAddin/Services/CombinationRecommender.cs) `RecommendAsync` / `PickSlideOnly` | 본문 패턴=RecommendAsync, slide-box=PickSlideOnly |
| 재료적합 계산 | [MaterialFitScorer.cs](../../../src/TeampptAddin/Services/MaterialFitScorer.cs) `Score(rec, u)` | 박스마다 그대로 |
| 컨셉 색/폰트 적용 | [ConceptResolver.cs](../../../src/TeampptAddin/Services/ConceptResolver.cs) `ResolveColors/Fonts` | 카드 표시 시 unlocked override |
| 선택 컨셉 | `_selectedConcept`([DesignConcept](../../../src/TeampptAddin/Models/DesignConcept.cs): StyleTags/Colors/Fonts) | retrieve 가중 + 컨셉적합 채점 입력 |

## 컴포넌트 경계 (단위)

| 유닛 | 신규/수정 | 책임 · 입력→출력 · 의존 |
|---|---|---|
| `BodyPattern`/`BoxPlan`/`ConceptFitResult`/`DeckRecommendation`/`BoxRecommendation` | 신규·Models | 타입 스캐폴딩 |
| `BodyPatternClusterer` | 신규·**순수** | 본문 `List<DraftProfile>` → `List<BodyPattern>`. 시그니처 = 도형 kind 멀티셋(text/image/table/chart 개수 버킷) + 대략 열 수(x좌표 군집). 같은 시그니처끼리 묶고 대표 장(첫 장) 지정. 토큰0. 의존: 없음. **TDD** |
| `DeckBoxPlanner` | 신규·**순수** | `(DeckStructure, List<BodyPattern>) → List<BoxPlan>`. 박스 순서: 표지(cover) → [공통 header(본문 ≥1일 때 1개)] → 본문 패턴들 → 엔드(end). toc/section은 원래 위치대로 slide-box 삽입. 각 BoxPlan = `{BoxKind, Label, CoveredSlideIndexes, RepresentativeIndex?}`. 토큰0. 의존: 모델만. **TDD** |
| `ConceptFitScorer` | 신규·**순수** | `(CombinationRecommendation 또는 HeaderAsset, DesignConcept) → ConceptFitResult{Score 0-100, Note}`. = 선택 에셋 `Tags` ∩ `concept.StyleTags` 커버 + 색/폰트 `Role` 커버 비율 가중합. 토큰0. 의존: 모델만. **TDD** |
| `DeckSlideImageExporter` | 신규·Core | `(string path, IEnumerable<int> indices) → Dictionary<int,string>`. 파일 hidden ReadOnly open → 인덱스별 `SlideImageRenderer.Render` → close+release. 대표 본문 장(2~4)만. 의존: `SlideImageRenderer`, COM |
| `DeckRecommendationOrchestrator` | 신규 | `BoxPlan` 실행기. **실행 순서: 본문 패턴 understanding을 먼저 모두 계산 → 그중 첫 패턴 understanding으로 공통 header 도출 → 그 다음 디스플레이 순서(표지→header→본문→엔드)로 박스 조립.** (a) 비-body slide-box: synthetic `DraftUnderstanding`(Purpose=Label, NeededCombination.Slide=1) → `GetCandidatesAsync(u, concept)`(kind=slide) → `PickSlideOnly`(LLM0). (b) 공통 header: **첫 본문 패턴의 대표 understanding 재사용**(추가 멀티모달 호출 없음) → `GetCandidatesAsync`(kind=header, concept) → top1(LLM0). (c) 본문 패턴: 대표 PNG → `UnderstandAsync` → **`u.NeededCombination.Header=0` 설정 후** `GetCandidatesAsync`(→layout+component만) → `RecommendAsync`(LLM 1회/패턴). 박스마다 `MaterialFitScorer`+`ConceptFitScorer` 부착 → `DeckRecommendation`. 의존: 위 재사용 서비스 + 신규 순수유닛 |
| `CombinationCandidateProvider` | **수정** | `GetCandidatesAsync(DraftUnderstanding u, DesignConcept concept = null, int topK = 5)`. concept≠null이면 임베딩 query에 `| 스타일:{string.Join(",", concept.StyleTags)}` 합성. **Route A는 concept 미전달(null) → 기존과 완전 동일**(회귀 없음) |
| `AssetPanel` | **수정** | `_deckProfiles`/`_deckPath` 보관(`RunDeckRedesignAsync`). `OnConceptSelected` → `RunDeckRecommendAsync()` 자동 실행 → 진행 버블 → 박스별 `BuildBoxRecommendationCard`(에셋 이름·컨셉 색/폰트 미리보기 + 재료적합/컨셉적합 두 배지) 렌더. 확정 배너 "진행할게요"가 실제 동작 |
| `TeampptAddin.csproj` | **수정** | 신규 .cs `<Compile Include>` 수동 등록(old-style csproj) |

## 박스 파이프라인 (LLM·멀티모달 호출 = 본문 패턴 수, 장수 무관 고정)

```
표지(cover)          slide-box  : Label+concept로 synthetic understanding → slide 후보 → PickSlideOnly(top1, LLM0)
[공통 header 1회]     header-box : 첫 본문 패턴 understanding 재사용 → header 후보 → top1(LLM0), 본문 박스들에 공통 표시
본문 패턴 ①②③…      body-box   : 대표 PNG export → UnderstandAsync(멀티모달 1회/패턴)
                                  → u.NeededCombination.Header=0 → 후보(layout+component) → RecommendAsync(LLM 1회/패턴)
엔드(end)            slide-box  : PickSlideOnly(LLM0)
목차/간지(toc/section) slide-box : 위치대로 PickSlideOnly(LLM0)
```

- 멀티모달 이해 호출 = 본문 패턴 수(2~4). LLM 조합 추천 호출 = 본문 패턴 수(2~4). slide-box·공통 header는 top1(LLM0). → 총 Gemini 호출이 덱 장수에 선형폭증하지 않음(스펙 D1).
- 본문 패턴에서 header를 후보에서 제외(공통 header는 별도 1회): `UnderstandAsync` 직후 `u.NeededCombination.Header=0`으로 설정 → `NeededKinds`가 layout+component만 반환.

## 두 배지 (둘 다 추천 시점 계산, 토큰0)

- **재료적합(materialFit)** = 기존 `MaterialFitScorer.Score(rec, u)` 재사용 — 선택 조합의 벡터 유사도 평균(0.6) + layout capacity 대비 블록수 적합(0.4).
- **컨셉적합(conceptFit)** = 신규 `ConceptFitScorer` — 선택 에셋 `Tags` ∩ `_selectedConcept.StyleTags` 커버 + 색/폰트 `Role` 커버. 0~100 정규화.
- 두 값을 추천 카드 배지로(`[재료적합 NN / 컨셉적합 NN]`). LLM 비전 `designConcept`는 Phase 4/5 조립 후로 명시 이월.

## StyleTags 가중 (retrieve)

선택 컨셉의 `StyleTags`를 `CombinationCandidateProvider`의 임베딩 query 텍스트에 합성: `{Purpose} | {MatchIntent} | 스타일:{styleTags} (counts)`. re-rank 2단계는 YAGNI로 보류. Route A(단일 슬라이드)는 concept=null → query 변화 없음.

## 데이터 흐름

`_deckProfiles`(Phase 1 보관) + `_lastStructure` + `_selectedConcept`
→ `BodyPatternClusterer`(본문 패턴) → `DeckBoxPlanner`(박스 순서)
→ `DeckRecommendationOrchestrator`( slide-box: synthetic→slide 후보→PickSlideOnly · 공통 header: top1 · body: `DeckSlideImageExporter`→`UnderstandAsync`→`GetCandidatesAsync(concept)`→`RecommendAsync` ) + 박스마다 `MaterialFitScorer`·`ConceptFitScorer`
→ `DeckRecommendation` → `AssetPanel` 박스 카드(두 배지, 컨셉 색/폰트는 `ConceptResolver`로 표시 override).

## 에러 처리

- 박스 후보 0개(kind) → "미충족" 표시, 욱여넣기 금지(빈 박스 유지).
- PNG export 실패 → 해당 패턴 텍스트-only `UnderstandAsync(profile, null)` 폴백(이해 품질만 저하, 흐름 유지).
- Supabase/Gemini 실패 → 기존 `RecommendationCache` 폴백 패턴 유지.
- 본문 0장(전부 cover/end) → 공통 header 박스 생략, slide-box만.
- 원본 초안 파일 불변(ReadOnly hidden), 어떤 실패에도 영향 없음.

## 테스트

- **순수유닛 3개 TDD(UAC 불필요):** `BodyPatternClusterer`·`DeckBoxPlanner`·`ConceptFitScorer`. 절차 = 관리자 솔루션 빌드(`/p:RegisterForComInterop=false`) → `dotnet test --no-build -p:BuildProjectReferences=false --filter`.
- **오케스트레이터·UI·Exporter:** COM/LLM 의존 → PPT 수동검증(데모 흐름: 초안 진입 → 구조 박스 → 칩 질문 → 컨셉 3카드 → 선택 → **박스별 추천 카드 + 두 배지**).
- 회귀: Route A 단일 슬라이드 추천이 `GetCandidatesAsync` 시그니처 변경 후에도 동일 동작(concept=null 경로).

## 실행 전략 — 모델·effort 배분 (Opus 오케스트레이션 계획)

**원칙:** 정말 중요한 곳(검증된 공유코드 수정·통합 브레인·최종리뷰)만 Opus max(높은 effort). 잘 경계된 순수로직·모델·UI는 Sonnet으로 효율 집행. 각 Sonnet 구현 직후 Sonnet 로직리뷰, 통합 끝에 Opus 최종리뷰.

| Task | 내용 | 구현 모델 | 리뷰 | effort |
|---|---|---|---|---|
| A | 모델/타입 스캐폴딩 | Sonnet | — | 낮음 |
| B | `BodyPatternClusterer` (TDD) | Sonnet | Sonnet 로직 | 표준 |
| C | `ConceptFitScorer` (TDD) | Sonnet | Sonnet 로직 | 표준 |
| D | `DeckBoxPlanner` (TDD) | Sonnet | Sonnet 로직 | 표준 |
| E | `CombinationCandidateProvider` 컨셉 주입(**공유·검증 코드**) | Sonnet | **Opus max 필수** | 높음 |
| F | `DeckSlideImageExporter` (COM) | Sonnet | Sonnet 로직 | 표준 |
| G | `DeckRecommendationOrchestrator` (**통합 브레인**) | **Opus max** | Opus | 높음 |
| H | `AssetPanel` UI 연결·자동실행·박스카드·두 배지 | Sonnet | Opus 최종 | 표준 |
| I | 최종 통합 리뷰 + PPT 수동검증 게이트 | **Opus max** | — | 높음 |

의존: A 먼저 → B·C·E·F 병렬 가능 → D(B 후) → G(C·D·E·F 후) → H(G 후) → I(마지막). subagent 분배는 writing-plans에서 이 표를 Task DAG로 확정.

## 리스크 / 가장 불확실한 것

1. **구조 라벨·패턴 시그니처 정확도** — 도형 메트릭만으로 본문 패턴을 의미 있게 묶나. 검증 필요(텍스트-only 폴백 있음).
2. **공유코드(E) 회귀** — `GetCandidatesAsync` 시그니처 변경이 Route A를 깨지 않게(기본값 null). Opus 리뷰로 차단.
3. **본문 패턴이 1개로 뭉침** — 단조 덱이면 패턴 1개 → 본문 박스 1개. 의도된 동작이나 데모 풍성함 저하 가능 → 데모 덱은 패턴 2~3개 있는 초안 사용.
4. **공통 header top1의 적합성** — LLM 없이 top1 유사도 선택이라 가끔 빗나갈 수 있음. v1 허용, 필요 시 후속에서 LLM 1회로 승격.

## 데모 경계 (진짜 vs 이월)

진짜(이번): 패턴 클러스터링 · 박스 계획 · slide-box/공통header/본문패턴 추천 · 두 배지(계산) 전부. 이월: 빈 템플릿 덱 조립(Phase 4) · LLM 비전 designConcept 결과채점(Phase 4/5) · re-rank 2단계 retrieve.

## 보드 위치

승인 시 [PROGRESS-BOARD.md](../../../PROGRESS-BOARD.md)의 현재 나무(Phase 3) 아래 잎 = "writing-plans로 Task DAG 작성 → subagent-driven 실행". 브랜치 `feat/asset-combination-recommendation`.
