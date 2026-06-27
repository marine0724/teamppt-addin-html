# 리디자인 — 초안 파일 통째 → 덱 전체 리디자인 (Route B 일반화) — 설계

날짜: 2026-06-25
관련: [에셋 조합 추천](2026-06-24-asset-combination-recommendation-design.md)(단일 슬라이드 추천 — 이 스펙이 덱으로 일반화), [Route B 단일슬라이드](2026-06-24-route-b-single-slide-redesign-design.md)(이식 경로 원형), [vibe-designing & A-first](2026-06-22-vibe-designing-a-first-design.md), [독백+병목진단](2026-06-25-step-narration-bottleneck-diagnosis-design.md)(검수 LLM 확장 토대)

## 한 줄 요약 / 기능 정의

**리디자인**은 사용자가 만들어 둔 **초안 .pptx를 통째로 받아, 디자인 관점만으로 구조를 읽고(표지·본문 N장·엔드, 각 장의 역할 라벨), 그 구조의 각 자리에 가장 잘 맞는 에셋 조합(통짜 slide / 공통 header / 본문 layout / 부품 component)을 종류별로 추천하고, 사용자가 박스 순서대로 고르면 초안 구조에 맞는 빈 템플릿 덱을 비파괴로 조립한 뒤, 초안의 실제 텍스트·이미지를 그 템플릿 자리에 그대로(생성·수정 없이) 이주시키는** 기능이다.

**절대 제약(디자인-온리):** 이것은 기획(내용 구성) 기능이 아니라 **디자인 리디자인**이다. 초안을 분석할 때도 디자인 관련 요소만 본다. 내용을 새로 기획하거나 메시지를 바꾸지 않는다. LLM은 "역할 판단·매칭·배치"만 하고, 텍스트는 항상 COM 원문을 쓴다(내용 할루시네이션 0).

## 배경 — 왜 이 전환인가

현재 "새 슬라이드를 만들면 헤더가 이미 그려진 채 나오고, 그 위에 에셋을 또 얹으면 헤더가 중복돼 어색하다." → "슬라이드를 생성한 뒤 에셋을 얹는" 접근 대신 **초안 파일을 넣으면 그 초안 자체를 분석해 템플릿을 조립**하는 방향으로 전환한다. 본질은 새 알고리즘이 아니라 **이미 만든 조각들을 덱 스케일로 다시 붙이는 것**이다.

기존 단일 슬라이드 경로([에셋 조합 추천](2026-06-24-asset-combination-recommendation-design.md))에서 `추천 → 빈 템플릿 배치 → 조립 완성도 → 재료 이식(맨 끝)` 순서는 이미 검증·확정됐고 단일 슬라이드 배치·이식이 동작한다. 이 스펙은 그것을 **(1) 파일 진입 + (2) 덱 전체**로 일반화한다.

## 범위 — 하이브리드 데모 스코프 (확정)

데모데이의 "와우"는 세 장면이다: **① 구조 요약 박스, ② 박스별 추천 + 두 유사도 진단, ③ 빈 템플릿 덱 + 본문 1~2장 실제 변환.** 원칙: **임팩트 큰 곳은 전부 진짜, 깨지기 쉬운 곳(overflow·표/차트·완벽 합성)은 검증된 슬라이드 위주.**

| 영역 | 데모에서 |
|---|---|
| 파일 진입, 덱 구조 분석, 구조 요약 박스 | **진짜** |
| 컨셉 3블록 생성·선택 | **진짜** (가볍게) |
| 박스별 추천 + 두 유사도 배지 | **진짜** |
| 빈 템플릿 덱 조립 | **진짜**, 단 다중 에셋 합성은 검증된 조합 위주 |
| 재료 이식 | **부분 진짜** — 본문 1~2장 완전 이주, overflow/표 많은 장은 빈 템플릿/부분 이주 |

## 핵심 불변 원칙 (유지)

- **사실 = COM / 벡터, 판단 = LLM.** 텍스트·개수·도형 참조는 COM 원문(토큰 0), 역할·매칭·조합·배치는 LLM.
- **LLM은 텍스트 내용을 생성·수정하지 않는다.** 이주 텍스트는 COM 원문.
- **비파괴.** 원본 초안 파일은 어떤 실패에도 불변. 조립은 새 덱(또는 빈 PPT)에서. 단일 슬라이드 이식도 `Slide.Duplicate()` 복제본에만.
- **좌표 변환** [CoordinateConverter](../../../src/TeampptAddin/Core/CoordinateConverter.cs) 규칙 준수 — 폴백 로직 추가 금지.
- **두 토큰 예산.** 비싼 "이해"는 인제스트 타임(에셋당 1회, 구현됨). 런타임은 싼 매칭 — 초안 분석은 가벼운 Gemini Flash 호출, 장수에 비례하지 않게 설계(D1).
- API 키를 문서·커밋에 평문으로 넣지 않는다.

## 재사용 자산 (대부분 이미 있음)

| 능력 | 위치 | 비고 |
|---|---|---|
| 슬라이드 COM 읽기 | [DraftSlideReader.cs](../../../src/TeampptAddin/Core/DraftSlideReader.cs) | 현재 1장 → 파일 N장 순회로 일반화 |
| 초안 디자인 이해(멀티모달 1회) | [DraftUnderstandingService.cs](../../../src/TeampptAddin/Services/DraftUnderstandingService.cs), [DraftModels.cs](../../../src/TeampptAddin/Models/DraftModels.cs) | 디자인-온리 이미 반영 |
| 재료량 모델(slide/header/layout/component 필요수) | `NeededCombination` | 있음 |
| 종류별 벡터 후보 + 유사도 노출 | [CombinationCandidateProvider.cs](../../../src/TeampptAddin/Services/CombinationCandidateProvider.cs) | `LastRetrieveLines` |
| LLM 조합 추천 | [CombinationRecommender.cs](../../../src/TeampptAddin/Services/CombinationRecommender.cs) | 덱 오케스트레이션만 신규 |
| 비파괴 배치 + 재료 이식 | [RedesignApplier.cs](../../../src/TeampptAddin/Core/RedesignApplier.cs) | 단일 슬라이드 동작 검증됨 |
| LLM 슬롯 매핑(overflow/empty 계산) | [SlotMapper.cs](../../../src/TeampptAddin/Services/SlotMapper.cs), `MappingResult` | 있음 |
| 검수 LLM(6차원 + 병목 4분류) | [DesignCritiqueService.cs](../../../src/TeampptAddin/Services/DesignCritiqueService.cs) | 두 유사도 분리로 확장(Phase 0) |
| 컨셉 색/폰트 적용 | [ConceptResolver.cs](../../../src/TeampptAddin/Services/ConceptResolver.cs) | 적용만 있음 → 생성 신규 |
| 파일 선택창 | `OpenFileDialog` in [AssetPanel.cs](../../../src/TeampptAddin/UI/Wpf/AssetPanel.cs) | 패턴 재사용 |

## 파이프라인 (덱 레벨)

```
[리디자인] 버튼 → OpenFileDialog → 초안.pptx (창 없이 hidden open, 원본 불변)
   │
   ├─ Phase 1  덱 구조 분석
   │     텍스트 패스(저가 1회) → 장별 cover/body/end + 라벨 + 총장수
   │     → "이쁜 구조 요약 박스" UI                          ← 데모 hero ①
   │
   ├─ Phase 2  컨설팅 컨셉
   │     "어디에 쓰나 / 어떤 느낌" 질문 → 컨셉 3블록 생성 → 선택
   │
   ├─ Phase 3  박스별 덱 추천
   │     표지박스(slide) → 공통 header 1회 → 본문 장별(layout+component) → 엔드박스(slide)
   │     각 추천: [재료적합 81 / 디자인·컨셉 74] 두 배지   ← 데모 hero ②
   │     (장별 멀티모달 이해는 추천 들어가는 본문 장만 — D1)
   │
   ├─ Phase 4  빈 템플릿 덱 조립 (비파괴, 새 덱)
   │     박스 순서대로 N장, 공통 header 합성                  ← 데모 hero ③(빈 덱)
   │
   └─ Phase 5  재료 이식 (하이브리드)
         본문 1~2장 완전 이주(텍스트·이미지), 나머지 빈/부분  ← 데모 hero ③(채워진 덱)
```

## Phase별 설계

### Phase 0 — 두 유사도 분리 + 검수 노출 (저비용 선행)

당신의 Step 4 핵심: "재료량 적합 유사도 / 디자인·이쁨·컨셉 유사도가 각각 따로 나와야 뭐가 문제인지 판단된다." 현재 ~70% 있음 — 벡터 유사도는 검수에 텍스트로만 들어가고 검수는 디자인 6차원+병목만 출력한다. 이를 **두 점수 축으로 분리**한다.

두 축은 **출처가 다르다**(사실=벡터, 판단=LLM 원칙 유지):

- **`materialFit` (계산값, LLM 아님):** 재료량·종류가 에셋 capacity에 맞나. 이미 가진 신호로 도출 — 종류별 벡터 유사도([CombinationCandidateProvider](../../../src/TeampptAddin/Services/CombinationCandidateProvider.cs)의 `LastRetrieveLines`) + `capacity` 대비 실제 재료 개수. 토큰 0으로 계산해 designConcept와 비교 가능한 척도(0-100)로 정규화·표시.
- **`designConcept` (LLM 비전 채점):** 디자인 이쁨 + 컨셉 부합. [DesignCritiqueService](../../../src/TeampptAddin/Services/DesignCritiqueService.cs)가 출력 = 기존 6차원 `dimensionScores` 종합.

[DesignCritique.cs](../../../src/TeampptAddin/Models/DesignCritique.cs) / [DesignCritiqueSchema.cs](../../../src/TeampptAddin/Services/DesignCritiqueSchema.cs)의 LLM 출력은 `designConcept`(+기존 `dimensionScores`, `bottleneck` 등). `materialFit`은 계산해서 (1) UI 배지로 표시하고 (2) 병목 판정 호출에 입력으로 넣어준다. 검수 LLM은 이 두 축을 근거로 병목을 4분류한다:

- `materialFit` 낮음 → 애초에 맞는 후보가 없었음(**에셋부족=양**) 또는 의도가 어긋남(**데이터추출**).
- `materialFit` 높은데 `designConcept` 낮음 → 재료는 맞는데 결과가 별로(**에셋품질** 천장) 또는 배치 문제(**기능**).

추천 카드·검수 결과에 두 배지 노출. **전부 진짜.** 지금 단일 슬라이드 추천에 먼저 얹어 검증 → 덱으로 갈 때 그대로 재사용. *왜 먼저: 가장 싸고, 이후 모든 단계 품질을 측정 가능하게 만든다.*

### Phase 1 — 파일 진입 + 덱 구조 분석 (Step 1·2)

- **진입:** 빈 새 PPT에서 AI 패널 → [리디자인] 버튼 → `OpenFileDialog`(패턴 있음)로 초안 .pptx 선택.
- **파일 열기:** COM `Presentations.Open(path, ReadOnly, Untitled, WithWindow:=msoFalse)`로 **창 없이** 열어 슬라이드 순회. 끝나면 닫음. 원본 파일 불변.
- **구조 분석(D1 — 2단):** ① 전 장의 COM 텍스트·도형 메트릭만으로 **저가 1회** LLM 패스 → 장별 `cover/body/end` + 라벨(목차/회사소개/장점3단…) + 총장수. ② 멀티모달(이미지) 이해는 **추천이 실제로 들어가는 본문 장에만** Phase 3에서. → 비용이 장수에 선형 폭증하지 않음.
- **구조 요약 박스(데모 hero ①):** `표지 / 본문 N장(라벨 목록) / 엔드 / 총 X장`을 이쁜 박스로. **전부 진짜.**

신규 단위: `DeckStructureReader`(파일 N장 COM 읽기), `DeckStructureService`(저가 구조 패스), `DeckStructure` 모델, 구조 요약 박스 UI.

### Phase 2 — 컨설팅 컨셉 (Step 3)

- 질문: **"어디에 쓰는 자료인가 / 어떤 느낌을 주고 싶은가."** (구조 분석 단계에서 LLM이 이미 어느 정도 컨설팅 단서를 잡고 있음.)
- **컨셉 3블록 생성(LLM):** 답 + 구조 요약을 근거로 방향 3개 추천(이름 + styleTags + colors/fonts role 맵 = [DesignConcept](../../../src/TeampptAddin/Models/DesignConcept.cs) 형태).
- **D2 — 컨셉 역할(둘 다, 가볍게):** (a) Phase 3 검색 쿼리에 styleTags 가중, (b) 선택 에셋의 unlocked 색/폰트를 [ConceptResolver](../../../src/TeampptAddin/Services/ConceptResolver.cs)로 override. 깊은 컨셉 엔진은 후순위.

신규 단위: `ConceptSuggester`(LLM 컨셉 3개 생성), 질문/컨셉 카드 UI.

### Phase 3 — 박스별 덱 추천 (Step 4, 데모 hero ②)

- **오케스트레이션(신규):** 박스 순서 = 표지박스(slide) → **공통 header 1회**(본문 전체 공통) → 본문 장별(layout+component, 헤더 재추천 X) → 엔드박스(slide).
- 표지·엔드는 `kind=slide` 단축 추천([CombinationRecommender.PickSlideOnly](../../../src/TeampptAddin/Services/CombinationRecommender.cs) 재사용). 본문은 장별 [CombinationCandidateProvider](../../../src/TeampptAddin/Services/CombinationCandidateProvider.cs) + [CombinationRecommender](../../../src/TeampptAddin/Services/CombinationRecommender.cs) 재사용.
- **공통 헤더:** 본문 장들의 이해를 종합해 header 후보를 한 번만 추천, 모든 본문 장에 동일 적용.
- 각 추천에 Phase 0의 **두 유사도 배지**. **전부 진짜.**

신규 단위: `DeckRecommendationOrchestrator`(박스 순서 + 공통 헤더 1회 로직), `DeckRecommendation` 모델.

### Phase 4 — 빈 템플릿 덱 조립 (Step 5, 데모 hero ③-빈)

- 새 덱(또는 현재 빈 PPT)에 박스 순서대로 N장 생성, 공통 header + layout + component 합성. [ShapeInserter](../../../src/TeampptAddin/Core/ShapeInserter.cs)·[RedesignApplier](../../../src/TeampptAddin/Core/RedesignApplier.cs) 삽입 경로 재사용.
- ⚠ **가장 어려운 곳:** 여러 에셋(header+layout+component N개)을 한 본문 장에 **겹치지 않고 깨끗이** 합성(조립 완성도). "결과물이 엉망"이었던 진짜 원인. 데모는 **검증된 조합 위주**로 안전화.
- 비파괴: 원본 초안 파일 불변.

신규 단위: `DeckAssembler`(N장 조립 + 다중 에셋 합성), 합성 규칙(헤더 상단 고정, layout 본문, component 배치).

### Phase 5 — 재료 이식 (Step 6, 하이브리드, 데모 hero ③-채워짐)

- 본문 **1~2장 완전 이주**: [SlotMapper](../../../src/TeampptAddin/Services/SlotMapper.cs)(초안 재료↔에셋 도형 LLM 매핑) → [RedesignApplier](../../../src/TeampptAddin/Core/RedesignApplier.cs)(COM 원문 텍스트 주입·이미지 재배치). **이미 동작하는 경로.**
- overflow/표·차트 많은 장은 빈 템플릿 또는 부분 이주.
- **부분 진짜.**

## 컴포넌트 경계 (단위)

| 단위 | 책임 | 입력 → 출력 | 신규/재사용 |
|---|---|---|---|
| `MaterialFitScorer` | 재료적합 계산(벡터유사도+capacity 대비 재료수) | 후보+이해 → materialFit(0-100) | 신규(계산, LLM 아님) |
| `DesignCritique`(확장) | designConcept(비전) + 병목, materialFit 입력 받음 | 결과+초안+materialFit → 점수+병목 | 확장 |
| `DeckStructureReader` | 초안 파일 N장 COM 읽기 | 파일 → DraftProfile[] | 신규(DraftSlideReader 일반화) |
| `DeckStructureService` | 저가 구조 패스 | 텍스트 메트릭 → 장별 역할+라벨+총수 | 신규 |
| `ConceptSuggester` | 컨셉 3블록 생성 | 답+구조 → DesignConcept[3] | 신규 |
| `DeckRecommendationOrchestrator` | 박스 순서 + 공통 헤더 1회 | 구조+컨셉 → 박스별 추천 | 신규(기존 추천 엔진 호출) |
| `DeckAssembler` | N장 조립 + 다중 에셋 합성 | 추천 → 빈 템플릿 덱 | 신규(삽입 경로 재사용) |
| 재료 이식 | 본문 1~2장 텍스트·이미지 이주 | 초안+템플릿 → 채워진 장 | 재사용(SlotMapper+RedesignApplier) |
| 패널 UI | 구조 박스·컨셉 카드·박스별 추천·두 배지 | 모델 → UI | 신규(AssetPanel 확장) |

## 데이터 흐름

`OpenFileDialog` → `DeckStructureReader`(COM 사실, N장) → `DeckStructureService`(저가 구조) → 구조 박스 → `ConceptSuggester`(컨셉 3) → `DeckRecommendationOrchestrator`(장별 `DraftUnderstandingService` 멀티모달 이해 → `CombinationCandidateProvider` 벡터 후보 → `CombinationRecommender` LLM 조합, 공통 헤더 1회) → 박스별 추천(+두 유사도) → `DeckAssembler`(빈 템플릿 덱) → 재료 이식(본문 1~2장).

## 에러 처리

- 파일 열기 실패/손상 .pptx → 사용자 메시지, 원본 영향 없음.
- Supabase/Gemini 미설정 또는 실패 → 기존 [RecommendationCache](../../../src/TeampptAddin/Services/RecommendationCache.cs) 폴백 패턴 유지.
- 추천 후보 0개(kind) → 해당 박스 "미충족" 표시, 욱여넣기 금지(빈 슬롯 유지).
- 이식 overflow/표/차트 → 빈 템플릿 유지(이주 스킵), 사용자에게 표시.

## 리스크 / 가장 불확실한 것

1. **다중 에셋 합성 완성도(Phase 4)** — 진짜 난관. Step 6보다 어려움. 데모는 검증된 조합으로 안전화.
2. **overflow 자동 맞춤** — 초안 텍스트가 슬롯보다 길 때. 모델(`MappingResult.Overflow`)은 있으나 오토핏/폰트 축소 로직 미구현. v1은 검증 장 위주.
3. **표/차트 이주** — [RedesignApplier](../../../src/TeampptAddin/Core/RedesignApplier.cs)에서 현재 스킵("범위 외"). 회사소개서엔 흔함.
4. **덱 레벨 신뢰성** — 장별 매핑 오차가 장수만큼 누적. D1의 2단 분석으로 비용·오차 통제.
5. **구조 라벨 정확도(Phase 1)** — 텍스트만 패스가 라벨(목차/회사소개 등)을 얼마나 잘 잡나. 검증 필요.

## 데모 경로 요약 (진짜 vs 목업 경계)

진짜: Phase 0 전부 · Phase 1 전부 · Phase 2 전부 · Phase 3 전부 · Phase 4 조립(검증 조합) · Phase 5 본문 1~2장 이주.
목업/후순위: overflow 자동 맞춤, 표·차트 이주, 임의 조합 완벽 합성, 덱 전체 자동 이식.

## 보드 위치

승인 시 [PROGRESS-BOARD.md](../../../PROGRESS-BOARD.md)의 다음 **나무**(현재 잎 "독백+병목진단 Task 6 검증" 이후)로 편입. Phase 0이 현재 검수 작업의 자연 연장이라 첫 잎으로 적합.
