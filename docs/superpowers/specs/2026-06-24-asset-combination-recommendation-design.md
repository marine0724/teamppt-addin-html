# 에셋 조합 추천 (Route B 리디자인 — 추천 단계) — 설계

날짜: 2026-06-24
관련: [Route B 단일슬라이드 풀변환](2026-06-24-route-b-single-slide-redesign-design.md)(폐기·대체), [kind 3분류](2026-06-23-kind-three-way-classification-design.md)(4분류로 확장), [vibe-designing & A-first](2026-06-22-vibe-designing-a-first-design.md)

## 한 줄 요약

초안 슬라이드를 이해해서, **무엇을 한 장 만들어 넣어주는 게 아니라**, 그 초안에 어떤 에셋들의 **조합**(헤더 + 레이아웃 + 컴포넌트들, 또는 통짜 slide 한 장)이 어울릴지 **추천**해준다. 이번 스펙은 **추천까지만** — 슬라이드에 실제 배치하거나 재료를 이식하지 않는다.

## 배경 — 왜 방향을 바꾸나

직전 구현([Route B 단일슬라이드](2026-06-24-route-b-single-slide-redesign-design.md))은 "버튼 → 에셋 **하나** 찾기 → 그 에셋에 초안 재료를 **즉시 주입**"이었다. 실사용해보니 결과물이 엉망이었고, 순서 자체가 틀렸다:

- 에셋 **하나**만 넣는다 → 초안이 필요로 하는 구성(제목 + 본문 배치 + 부품 N개)을 담지 못함.
- 재료를 **즉시 이식**한다 → 템플릿이 예쁘게 자리잡기도 전에 텍스트/이미지를 욱여넣어 깨짐.

올바른 순서(사용자 확정):

1. **추천** ← *이번 스펙* — 초안을 보고 **필요한 에셋 조합**을 종류별로 하나씩 골라 제시.
2. **배치** — 추천대로 새 슬라이드에 에셋들을 다 옮겨 **예쁜 빈 템플릿** 완성.
3. **조립 완성도** — 영역 합성을 매끄럽게.
4. **재료 이식** — 초안의 텍스트·이미지를 템플릿 자리에 자연스럽게.

"이쁜 건 나중이라도, 일단 재료 종류·양·의도·목적에 맞는 조합을 제대로 추천"하는 1단계를 토대로 깐다.

## 핵심 불변 원칙 (유지)

- **사실 = COM / 벡터, 판단 = LLM.** 텍스트·개수·도형은 COM 원문. 역할·매칭·조합 배정은 LLM.
- **LLM은 텍스트 내용을 생성·수정하지 않는다.**
- 이번 스펙은 **비파괴 이전 단계** — 슬라이드를 건드리지 않는다(추천만). 배치는 다음 스펙.
- 좌표 변환(CoordinateConverter) 폴백 추가 금지 — 배치 스펙에서 적용될 때.
- API 키를 문서·커밋에 평문으로 넣지 않는다.

---

## 설계

### 1. 에셋 taxonomy — 4분류로 확장

현재 `kind`는 코드상 2분류(`layout`/`component`), [3분류 설계](2026-06-23-kind-three-way-classification-design.md)에서 `slide` 추가가 계획돼 있었다. 이번에 **`header`를 component에서 분리**하여 최종 4분류로 간다:

| kind | 의미 | 조합에서의 역할 |
|---|---|---|
| `slide` | 통짜 슬라이드 (오픈표지·엔드표지). 페이지 통째가 완결 단위 | cover/end일 때 **단독**으로 한 장 |
| `header` | 중간 슬라이드 **상단 제목 영역** | body/section의 **상단** 1개 |
| `layout` | 중간 슬라이드에서 **헤더를 제외한 본문 배치 영역** | body/section의 **본문** 1개 |
| `component` | 본문 위에 얹는 **부품** (카드·아이콘블록·그래프·표·다이어그램) | 재료 양에 따라 **N개** |

판정 우선순위: 오픈/엔드 표지에 적합한 통짜면 `slide` → 상단 제목 영역이면 `header` → 채워 쓰는 본문 틀이면 `layout` → 부품이면 `component`.

> **주의:** `scope` 컬럼에도 `"slide"` 문자열이 존재(다른 축 = 적용 범위). `kind="slide"`와 컬럼이 달라 충돌 없음. 혼동 금지.

### 2. 에셋 스키마 — 조합 판단 필드 추가 + 재인제스트

추천 엔진이 "재료 양 적합 / 조합 일관성"을 판단하려면 인제스트 메타데이터에 단서가 필요하다. 기존 필드(`use_when`, `content_fit`, `tags`, `slots`, `grid_columns`, `colors`, `fonts`, `source_deck`, `example_intents`)에 더해:

| 필드 | 용도 | 비고 |
|---|---|---|
| `kind` (enum 확장) | `["slide","header","layout","component"]` | `UnderstandingSchema` enum + 프롬프트 규약 |
| `capacity` | 이 에셋이 담기 좋은 **재료 블록 수** (예: 카드 3개, 단 2개). 정수 또는 범위 `{min,max}` | 양 적합 판단 — layout/component 위주 |
| `material_kinds` | 담기 좋은 재료 타입 배열 (text/image/chart/table/bullet) | 종류 적합 |
| `pairing_key` | 조합 일관성 키 — 같은 디자인 계통 묶음(보통 `source_deck` 재사용) | 헤더·레이아웃·컴포넌트가 같은 톤으로 골라지도록 |

> 기존 DB는 표지류가 `kind="layout"`, 헤더류가 `component`로 적재돼 있을 수 있음. **재인제스트** 필요(파일 UNIQUE + upsert라 중복 없음). 인제스트 LLM 프롬프트·스키마를 위 4분류 + 신규 필드로 갱신 후, 번들 에셋을 다시 인식시킨다.

### 3. 초안 이해 — "필요한 조합 명세"로 확장

현재 `DraftUnderstanding`(materials, counts, layoutShape, matchIntent, slideKind)에 **무엇이 필요한지**를 더한다. 기존 `DraftUnderstandingSchema`/`Parser`를 확장:

- `slideKind` (cover/toc/body/section/end) — 이미 있음. **분기 기준**.
- `purpose` — 이 슬라이드의 **의도·목적** 한 문장 (예: "3개 핵심 기능을 동등 비교"). *신규.*
- `neededCombination` — 필요한 조합 명세 *(신규)*:
  - cover/end → `{ slide: 1 }`
  - body/section → `{ header: 1, layout: 1, component: N }` — N과 각 component의 재료성격은 counts·materials에서 도출.

LLM은 **역할·의도·필요수량 판단만**. 텍스트·개수는 COM 원문이 사실(파서가 덮어씀).

### 4. 추천 엔진 Ⓑ — 벡터로 후보 추리고 → LLM이 조합 선택

```
DraftUnderstanding (purpose, neededCombination, counts, materials)
        │
        ▼
[A] 분기: slideKind == cover/end ?
        ├─ 예 → kind=slide 후보만 벡터검색 → 통짜 1장 추천 (조합 LLM 생략 가능)
        └─ 아니오 ↓
[B] 후보 풀 구성 — 필요한 각 kind(header/layout/component)마다
     matchIntent+purpose 임베딩으로 match_assets RPC, kind 필터, top-K씩
        │
        ▼
[C] LLM 조합 선택 (1회 호출)
     입력: DraftUnderstanding 요약 + kind별 후보 목록(메타 포함)
     출력: 선택된 조합 { header: assetId, layout: assetId, components: [assetId…] }
            + 각 선택 근거(fitNote) + confidence + 미충족 슬롯 표시
     규칙: 재료 양·종류 적합 우선, 같은 pairing_key(=source_deck) 선호(일관성),
           적합한 게 없으면 비워 둠(욱여넣기 금지). 텍스트 생성 금지.
        │
        ▼
RecommendationResult (조합 + 근거) — 슬라이드는 건드리지 않음
```

- 벡터검색(해자)은 **후보 추리기**로 유지. LLM은 그 위에서 **조합 판단**.
- `match_assets` RPC에 **kind 필터** 인자 추가 필요(또는 결과를 클라이언트에서 kind로 분류). 후보 풀은 kind별로 분리해 LLM에 제시.
- Supabase 실패 시 기존 `RecommendationCache` 폴백 패턴 유지.

### 5. 출력 / UI — 헤더·레이아웃 따로

추천 결과를 **종류별로 분리**해 카드로 보여준다. 슬라이드 배치는 없음(다음 스펙).

```
초안 분석: "3개 핵심 기능 동등 비교" (body, 의도=비교)

추천 조합
  헤더    [서비스 개요]        근거: 제목+한줄 요약형      88%
  레이아웃 [3단 가로]          근거: 카드 3개 수용         82%
  컴포넌트 [아이콘 카드] ×3     근거: 기능 비교 부품        79%

(미충족: 없음)   [이 조합으로 배치 ▶ — 다음 단계]
```

기존 [AssetPanel.cs](../../../src/TeampptAddin/UI/Wpf/AssetPanel.cs)의 리디자인 바를 **추천 표시**로 교체. "✨ AI 리디자인" → 진행 버블 → 추천 카드(종류별). 배치 버튼은 다음 스펙에서 활성화(이번엔 비활성/플레이스홀더).

### 6. 폐기·대체되는 것

직전 구현 중 **재료 즉시 주입·단일 에셋 배치**는 이번 방향과 충돌하므로 추천 단계에선 호출 경로에서 제외:

- `RedesignApplier`(복제+삽입+주입), `SlotMapper`/`SlotMapSchema`/`SlotMapParser`(재료↔도형 매핑), `AssetShapeInventory` — **배치/이식 단계용**이므로 이번 스펙에선 호출하지 않음(코드 삭제는 아님, 배치 스펙에서 재활용 검토).
- `RedesignService.RunAsync`의 "top2 각각에 BuildPreviewAsync" 흐름 → "조합 추천" 흐름으로 교체.
- 재활용: `DraftSlideReader`(초안 읽기), `DraftUnderstandingService`(초안 이해, 확장), `DraftMatchService`(벡터 후보, kind 필터 추가), `GeminiAiService.GenerateJsonAsync`.

---

## 컴포넌트 경계 (단위)

| 단위 | 책임 | 입력 → 출력 |
|---|---|---|
| `UnderstandingSchema`(인제스트) | 4분류 + 신규 필드 스키마/프롬프트 | 이미지+힌트 → 에셋 메타 |
| `DraftUnderstandingSchema/Parser`(확장) | purpose·neededCombination 추가 | 초안 → 이해+필요조합 |
| `CombinationCandidateProvider` *(신규)* | kind별 벡터 후보 풀 | 이해 → kind별 후보 목록 |
| `CombinationRecommender` *(신규)* | LLM 조합 선택 | 이해+후보 → 추천 조합+근거 |
| `RecommendationResult` 모델 *(신규)* | 추천 결과 구조 | — |
| AssetPanel 추천 표시 | 종류별 카드 렌더 | 추천 → UI |

## 데이터 흐름

`DraftSlideReader`(COM 사실) → `DraftUnderstandingService`(LLM 이해+필요조합) → `CombinationCandidateProvider`(벡터 후보) → `CombinationRecommender`(LLM 조합 판단) → `RecommendationResult` → AssetPanel.

## 에러 처리

- Supabase/임베딩 실패 → `RecommendationCache` 폴백, 진행 버블에 표시.
- LLM이 일부 kind를 못 채움 → 그 슬롯은 "미충족"으로 표시, 욱여넣기 금지.
- 후보 0개(해당 kind 에셋 미적재) → 그 kind는 빈 상태로 추천, 경고 로그.

## 테스트

- 순수 로직(비-UAC, vstest): `DraftUnderstandingParser`의 purpose/neededCombination 파싱·COM 덮어쓰기, `CombinationRecommender` 응답 파싱(미충족·근거 포함), candidate provider의 kind 분류.
- 비-UAC 워크플로: main을 `/p:RegisterForComInterop=false`로 빌드 → 테스트 프로젝트 `/p:BuildProjectReferences=false` → vstest.console.
- 수동(선택): 표지+본문 번들 재인제스트 → 4분류로 적재되는지 DB 확인 → 본문 초안에서 추천 카드가 헤더/레이아웃/컴포넌트로 분리돼 나오는지.

## 범위 밖 (다음 스펙들)

- 추천 조합을 새 슬라이드에 **배치**(영역 합성). ← 바로 다음.
- 조립 완성도(영역 정렬·간격).
- 재료 이식.
- slide 통짜 삽입 동작 자체의 정교화.
