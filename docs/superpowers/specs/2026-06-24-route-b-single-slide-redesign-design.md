# Route B — 단일 슬라이드 풀변환 리디자인 설계

> 2026-06-24 브레인스토밍 확정. 상위 맥락: [AI 리디자인 5대 결정](2026-06-19-teamppt-ai-redesign-design.md), [vibe-designing & A-first](2026-06-22-vibe-designing-a-first-design.md), [kind 3분류](2026-06-23-kind-three-way-classification-design.md).

## 1. 배경 · 목표

데모 피치까지 가려면 **리디자인(Route B)** 까지 도달해야 한다. 실제 사용자는 "에셋 추천해달라"고 하지 않는다. **초안을 만들어 둔 상태에서 "더 이쁘게 해줘"** 가 진짜 워크플로다.

따라서 이 기능은: **초안 슬라이드의 재료(텍스트·이미지)를 LLM이 읽고, 가장 적합한 에셋을 추천 → 사용자가 선택 → 초안의 재료를 그 에셋의 자리에 실제로 채워 넣어 변환된 슬라이드를 만든다.**

**이번 범위:** 본문 레이아웃 슬라이드 **1장**에 대해 추천(Top 2) → 선택 → **재료 실제 이주**까지 끝까지 완성. 검증되면 덱 전체(회사소개서 표지~엔드)로 확장.

**최종(후순위) 목표:** 덱 전체 일괄 변환, 이미지 미화.

### 핵심 원칙

- **데이터 추출이 생명.** LLM이 초안을 읽었을 때 나오는 표현이 (1) 재료의 종류, (2) 양/개수, (3) 초안 디자인을 **매칭에 가장 적합하게** 담아야 한다. 전체의 성패가 여기 달림.
- **두 토큰 예산.** 비싼 "이해"는 인제스트 타임(에셋당 1회, 이미 구현됨). 런타임(리디자인 누를 때마다)은 **싼 매칭 예산** — 초안 읽기는 Gemini Flash 가벼운 단일 호출.
- **사실은 COM, 판단은 LLM.** 정확한 텍스트·도형 참조·개수는 COM에서 토큰 0으로 확보. LLM은 그 위에서 **역할 판단이라는 가벼운 일만** 한다. 이주 시 텍스트는 COM 원문을 쓰므로 내용 할루시네이션 0.
- **비파괴.** `Slide.Duplicate()` 복제본에만 작업. 원본은 어떤 실패에도 불변.

## 2. 파이프라인

```
[초안 슬라이드 (현재 화면)]
   │
   ├─▶ DraftSlideReader (COM)  ──▶ DraftProfile
   │     도형별 정확한 텍스트·위치·크기·타입(text/image/table/chart)
   │     + 텍스트 메트릭(글자수·불릿수·계층) + shapeId   ← 재료 "양/개수"의 사실 출처
   │
   └─▶ SlideImageRenderer ──▶ 초안 PNG  ← 디자인 "보이는 모습"
         │
         ▼
[DraftUnderstandingService] (Gemini Flash, 런타임 저가 1회 호출)
   입력: DraftProfile(JSON) + 초안 PNG
   출력: DraftUnderstanding (역할 판단만; 사실은 COM 값으로 덮어씀)
   │
   ▼
[EmbeddingService] matchIntent → 768벡터 ──▶ match_assets RPC ──▶ 후보 N개
   │  (실패 시 RecommendationCache 폴백 — 기존 경로 재사용)
   ▼
[GeminiAiService 선택기]  후보 + DraftUnderstanding ──▶ Top 2 + 선택이유
   │
   ▼  (Top 2 각각에 대해)
[SlotMapper] (Gemini)  DraftUnderstanding.materials + 에셋 slots + 에셋 실제도형 인벤토리
   ──▶ mapping  {draft material → 에셋의 실제 도형/슬롯}
   │
   ▼
[RedesignApplier] (COM, 비파괴)
   Slide.Duplicate() → 에셋 삽입 → mapping대로 텍스트·이미지 채움 → 썸네일 렌더
   │
   ▼
[AI탭 UI]  2개 시안 카드 → 사용자 선택 → 선택분만 남기고 다른 복제본 삭제 (비포/애프터)
```

### 설계 포인트

1. **두 출처 분리** — "양/개수·정확한 텍스트"는 COM(사실), "디자인 판단·역할 부여"는 비전. DraftUnderstanding이 둘을 합친 단일 표현이며 매칭과 이주 **양쪽의 입력**.
2. **매칭은 대칭 구조** — 에셋은 인제스트 때 `use_when/content_fit/slots`, 초안은 런타임에 `matchIntent/counts/materials`. 기존 임베딩·RPC·캐시 그대로 재사용.
3. **슬롯 바인딩은 런타임 해결** — 에셋 템플릿에 `slot.xxx` 이름이 안 박혀있는 열린 항목을, 삽입된 에셋의 **실제 도형 목록을 읽어** LLM이 초안 재료와 직접 매핑하는 방식으로 우회. 템플릿 사전 가공 불필요.

## 3. DraftUnderstanding 스키마 (생명)

매칭과 이주 양쪽 입력. 에셋 쪽 `UnderstandingSchema`와 대칭.

```jsonc
{
  // ── 재료 인벤토리: 이주의 출처, 매칭의 "종류" 신호 ──
  "materials": [
    {
      "role": "title",          // title|subtitle|body|bullet|caption|image|table|chart|logo
      "type": "text",           // text|image|table|chart
      "sourceShapeId": 3,       // COM이 부여 — 이주 시 정확히 이 도형에서 추출
      "text": "2025년 사업 전략", // text 타입일 때 COM 원문 (사실)
      "charCount": 9,
      "bulletCount": 0,
      "level": 0,               // 들여쓰기 계층 (불릿 구조 보존용)
      "emphasis": "heading"     // heading|normal|small
    }
  ],
  // ── 양: content_fit 매칭의 핵심 신호 ──
  "counts": { "textBlocks": 2, "bullets": 5, "images": 1, "tables": 0, "charts": 0 },
  // ── 디자인 현황 ──
  "layoutShape": "title-top + body-left + image-right",
  "designSummary": "제목 상단, 좌측 불릿 본문, 우측 사진 1장. 색 단조롭고 여백 좌측 쏠림.",
  "dominantColors": ["#1F4E79", "#FFFFFF"],
  // ── 매칭 질의: 에셋 use_when/example_intents와 맞댈 자연어 ──
  "matchIntent": "제목과 5개 불릿 본문, 보조 이미지 1장이 있는 사업 전략 본문 슬라이드",
  // ── 슬라이드 유형 ──
  "slideKind": "body"          // cover|toc|body|section|end
}
```

설계 의도:
- **`materials[].sourceShapeId`가 이주의 다리.** DraftSlideReader가 COM에서 부여한 ID를 들고 다니다가, RedesignApplier가 "이 ID 도형의 텍스트/이미지를 매핑된 에셋 슬롯에 넣는다". 텍스트는 COM 원문 → 할루시네이션 0.
- **`role`은 LLM이, `text/charCount/sourceShapeId`는 COM이.** LLM 응답 파싱 후 사실 필드는 COM 값으로 덮어써서 데이터가 흔들리지 않게 함.
- **`counts`+`matchIntent`가 매칭 품질을 좌우.** 에셋 `content_fit`과 양적으로 맞물림.
- **`emphasis`** 는 이주 시 폰트 역할 매핑(heading→에셋 title 슬롯)에 재사용.

## 4. 슬롯 매핑 — SlotMapper

선택기가 고른 Top 2 각각에 대해 초안 재료를 에셋 자리에 배정.

```
입력:  DraftUnderstanding.materials  (역할·개수·sourceShapeId)
       + 에셋 slots 메타 (DB, 이름·타입)
       + 에셋 실제 도형 인벤토리 (삽입된 pptx에서 COM으로 읽은 텍스트프레임/그림 위치·샘플텍스트)
출력:  mapping[]  { draftShapeId → 에셋도형ID, fitNote, confidence }
       + overflow[]  (에셋에 자리가 부족한 초안 재료)
       + empty[]     (초안에 재료가 없어 빈 채로 둘 에셋 슬롯)
```

- LLM은 **역할·타입·개수로 배정만** 결정. 텍스트 내용은 안 만짐.
- 슬롯 부족/남음을 명시적으로 다뤄, 초안 재료 7개를 슬롯 5개 에셋에 욱여넣다 깨지는 일 방지.

## 5. 비파괴 적용 — RedesignApplier

```
1. 원본 슬라이드 Slide.Duplicate()  → 복제본에만 작업 (원본 불변)
2. 복제본 위에 에셋 도형 삽입 (에셋 pptx → 복제 슬라이드로 shape 복사)
3. mapping대로:
     - text 슬롯 ← 초안 sourceShapeId 도형의 COM 원문 텍스트 주입 (내용 100% 보존)
     - image 슬롯 ← 초안 이미지 도형을 슬롯 위치·크기에 맞춰 배치 (이미지 미수정 = 후순위)
4. 원본 초안 도형은 제거 (복제본에서만)
5. SlideImageRenderer로 썸네일 렌더
```

- 좌표 변환은 기존 `CoordinateConverter` 사용. **폴백 로직 추가 금지**(좌표 변환 실패는 폴백 없이 드러냄).

## 6. 시안 2개 생성 (실제 복제 방식)

- Top 2 각각을 §5를 거쳐 **별도 복제 슬라이드 2장**으로 만들고 썸네일 2개를 AI탭 카드로 제시.
- 사용자가 1개 선택 → 나머지 복제 슬라이드 삭제, 선택분만 남김.
- 데모용 비포/애프터: 원본(N) + 선택 복제본(N+1)이 썸네일 레일에 나란히 = 공짜 비교.
- **비용 메모:** 렌더(슬라이드→PNG)는 로컬 PowerPoint 작업이라 API·토큰 0. 토큰은 LLM 호출에서만 — 리디자인 1회당: 초안 이해 1 + 후보 선택 1 + 슬롯 매핑 2(시안당 1).

## 7. 신규 / 수정 파일

**신규** (기존 패턴 그대로)

| 파일 | 역할 |
|---|---|
| `DraftSlideReader.cs` | COM으로 현재 슬라이드 도형 읽기 → `DraftProfile` (정확한 텍스트·위치·타입·메트릭, shapeId 부여) |
| `DraftUnderstandingSchema.cs` | 런타임 저가 호출용 응답스키마+시스템프롬프트 (역할 판단만) |
| `DraftUnderstandingParser.cs` | LLM 응답 파싱 + COM 사실로 text/count 덮어쓰기 |
| `RedesignService.cs` | 오케스트레이터: 읽기→이해→매칭→선택(Top2)→매핑→적용→썸네일, 진행 콜백 |
| `SlotMapper.cs` | 초안 재료 ↔ 에셋 실제 도형 매핑 (LLM), overflow/empty 처리 |
| `RedesignApplier.cs` | 비파괴 Duplicate→에셋 삽입→매핑대로 채움 |

**수정**
- `GeminiAiService.cs` — DraftUnderstanding 기반 후보 선택(Top2) 메서드 추가 (기존 텍스트질의 `RecommendAsync`와 병존)
- `AssetPanel.cs` / AI탭 — "리디자인" 버튼 + 진행 버블 + 시안 2카드 + 선택 핸들러 (기존 진단 버튼 옆)
- `EmbeddingService`·`SupabaseClient`·`RecommendationCache`·`SlideImageRenderer`·`CoordinateConverter` — **재사용만**, 수정 없음

## 8. 에러 처리

- COM 읽기 실패 → 비전 단독 폴백으로 이해만 진행(이주 비활성, 추천까지만)
- 매칭(Supabase) 실패 → 기존 `RecommendationCache` 폴백
- 슬롯 매핑 confidence 낮음 → 해당 슬롯 빈 채로 + 플래그 표시(욱여넣기 금지)
- 좌표 변환 실패 → 폴백 없이 드러냄
- 전 과정 비파괴 → 원본 슬라이드는 어떤 실패에도 불변

## 9. 테스트

- **순수 로직 단위테스트:** DraftProfile→프롬프트 빌드, 매핑 파서, overflow/empty 계산, COM-사실-덮어쓰기 로직(목 도형)
- **수동 검증:** COM·삽입·렌더는 기존 워크플로대로 관리자 빌드 후 실제 PPT에서

## 10. 범위 (YAGNI)

- ✅ 본문 레이아웃 슬라이드 1장 풀변환(추천 Top2→선택→재료 이주)
- ❌ 덱 전체 일괄(검증 후 확장)
- ❌ 이미지 미화
- ❌ 컨셉/팔레트 재테마(스타일 탭이 별도 담당)
- ❌ 표/차트 슬롯 정교화(텍스트·이미지 우선)
