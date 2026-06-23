# 화면 공유 진단 — 설계 (A-2 첫 조각)

> 작성: 2026-06-23 · 로드맵 위치: 숲 "온전한 A" → A-2 실시간 공유엔진의 첫 조각
> 레퍼런스: Gemini 데스크톱 화면공유 UI (입력창 위 "공유 중" 인디케이터 + 맥락 추천질문 3개)

## 1. 목적과 의도

사용자가 **"지금 이 슬라이드 보고 도와줘"** 라고 느낄 때 한 번 누르는 버튼.
누르면 현재 슬라이드를 캡처해 LLM이 보고, **개선점 중심 진단** + 사용자가 이어서 궁금해할 **추천질문 3개**를 준다.

핵심 제약(사용자 확정):
- 웹 탭과 달리 슬라이드는 넘기기 쉽지만 LLM이 읽기는 비싸다 → **자동 폴링/슬라이드 변경 감지 없음.**
- **버튼식 1회성(one-shot)** — 실제로 도움받고 싶을 때 한 번씩.
- 사용자는 "실시간 공유" 느낌을 받지만 실제로는 트리거 시점에만 읽는다(이 인디케이터 연출이 그 환상을 만든다).

## 2. 범위

### 포함
- AI 탭 입력창 영역의 **「화면 공유 진단」 버튼**.
- 누르면 입력창 바로 위에 **"공유 중" 인디케이터 바**가 애니메이션으로 등장(라이브 펄스 닷 🔵 + `슬라이드 N 공유 중` + ✕ 닫기).
- 현재 슬라이드를 768px PNG로 캡처 → Gemini 멀티모달 **1회** 호출.
- 채팅에 **개선점 진단 메시지** + **추천질문 칩 3개**.
- 칩 클릭 시 그 질문을 일반 메시지로 전송(이미지가 대화 히스토리에 남아 있어 같은 슬라이드 맥락으로 답변, 이미지 재전송 없음).

### 제외 (지금 안 함)
- 슬라이드 변경 자동 감지·폴링·자동 재진단 ❌
- 텍스트/도형 XML 추출 ❌ (이미지 단독)
- 에셋 추천 연동 ❌ (A-3)
- 전체 덱 일관성 진단 ❌ (A-3)

## 3. 결정 사항 (확정)

| 결정 | 선택 | 이유 |
|---|---|---|
| 슬라이드 읽기 방식 | **이미지(PNG) 단독** | 768px=고정 ~258토큰(예측가능·저렴). 제품이 "바이브 디자이닝"이라 색·레이아웃 등 시각 정보가 본질. 인제스트 멀티모달 패턴 재사용. |
| 호출 시점 | **버튼 1회성** | 슬라이드 읽기 비용 누적 방지. 사용자 의도가 명확할 때만. |
| 진단 성격 | **개선점 진단 중심** | 강점/약점 + 구체 개선 제안. '진단' 버튼명과 일치, 디자인 도우미 가치. |
| AI 메서드 | **신규 `DiagnoseSlideAsync`** (`RecommendAsync`와 분리) | 출력 모양이 다름(추천 에셋 X, 진단문 + 질문 3개). 경계 분리. |

## 4. 컴포넌트 (경계)

각 단위는 하나의 책임, 잘 정의된 인터페이스, 독립 테스트 가능.

### 4.1 `SlideCaptureService` (신규)
- **무엇:** 현재 활성 슬라이드를 PNG로 캡처하고 그 경로와 슬라이드 번호를 반환.
- **사용:** `CaptureCurrentSlide() → (string pngPath, int slideNumber)`
- **의존:** `Globals.Application.ActiveWindow.View.Slide` (현재 편집 중 슬라이드), 기존 `SlideImageRenderer.Render(presentation, index, pngPath)`.
- **세부:**
  - presentation = `Globals.Application.ActiveWindow.Presentation`.
  - slideIndex = `ActiveWindow.View.Slide.SlideIndex`.
  - 출력 경로 = `%LocalAppData%\TeampptAddin\cache\screen-share\slide-{index}.png` (매 호출 덮어쓰기).
  - 활성 창/슬라이드가 없으면(예: 슬라이드쇼·창 없음) `null` 반환 → 호출부가 안내 메시지.

### 4.2 `SlideDiagnosis` 모델 (신규)
```
class SlideDiagnosis {
    string Message;                  // 개선점 진단문
    List<string> SuggestedQuestions; // 정확히 3개
}
```

### 4.3 `IAiService.DiagnoseSlideAsync` (신규 메서드)
- **시그니처:** `Task<SlideDiagnosis> DiagnoseSlideAsync(string pngPath)`
- **무엇:** PNG를 base64 `inline_data`(`mime_type=image/png`)로 실어 Gemini `gemini-2.5-flash` 멀티모달 호출. 진단 시스템 프롬프트 + `responseSchema`(message + questions[3])로 구조화 출력.
- **구현 패턴:** 기존 `AssetUnderstandingService`의 멀티모달 요청 본문(`inline_data`)과 `GeminiAiService`의 재시도/토큰로깅/`_history` 적재를 재사용.
- **히스토리:** 이미지+진단 응답을 `GeminiAiService._history`에 적재 → 칩 클릭 후속 질문이 같은 슬라이드 맥락 유지(이미지 재전송 불필요).
- **MockAiService**: 고정 진단문 + 더미 질문 3개 반환(빌드/무키 환경).

### 4.4 `AssetPanel` 진단 UI (확장)
- **「화면 공유 진단」 버튼**: 입력 바 영역에 배치(전송 버튼과 구분되는 보조 액션).
- **인디케이터 바**: 입력창 바로 위. 펄스 닷 + `슬라이드 N 공유 중` 라벨 + ✕. 등장 시 slide-in + fade(150ms, EaseOut).
- **로딩 연출**: 기존 인제스트 **스캔라인** 패턴 재사용("화면 읽는 중").
- **결과 렌더**: 진단 버블(기존 `AddAiBubble` 타이핑 애니메이션 재사용) + 추천질문 칩 3개(기존 `BuildChip` 재사용, 클릭 시 `SendAiMessage`).

## 5. 데이터 흐름

```
[화면 공유 진단] 클릭
  → 인디케이터 바 slide-in (펄스 닷) + 스캔 로딩 버블
  → SlideCaptureService.CaptureCurrentSlide()
       → ActiveWindow.View.Slide.SlideIndex → SlideImageRenderer.Render → PNG
       → 활성 슬라이드 없으면 안내 후 종료
  → IAiService.DiagnoseSlideAsync(png)
       → base64 inline_data + 진단 프롬프트 → Gemini 1회
       → SlideDiagnosis 파싱, _history 적재
  → 로딩 제거 → 진단 버블(타이핑) + 추천질문 칩 3개
  → (칩 클릭) → SendAiMessage(질문) → 히스토리의 이미지 맥락으로 답변
```

## 6. 에러 처리
- 활성 슬라이드 없음 → "공유할 슬라이드가 없어요. PowerPoint에서 슬라이드를 여세요." 안내, 인디케이터 미표시.
- 렌더 실패(`Export` 예외) → "슬라이드를 읽지 못했어요." 안내, 인디케이터 닫음.
- Gemini 오류/타임아웃 → 기존 재시도(503/429/500 백오프) 후 실패 시 "진단 중 오류: {메시지}".
- 모든 경로에서 로딩 버블·인디케이터 정리(중단 시 leak 방지).

## 7. 테스트
- `SlideCaptureService`: 활성 슬라이드 있음 → PNG 생성+경로 반환 / 없음 → null. (PowerPoint COM 의존이라 수동 검증 중심)
- `DiagnoseSlideAsync`: MockAiService로 진단문+질문3개 반환 단위 확인. 실제 Gemini는 수동 검증(이미지 1장, 토큰 로그 확인).
- UI: 버튼 클릭 → 인디케이터 등장 → 진단 버블+칩3 → 칩 클릭 후속질문 동작. (수동)

## 8. 빌드/검증
- MSBuild 관리자 권한(CLAUDE.md), 빌드 후 DLL 타임스탬프 + build.log 오류 0건 검증.
- 수동 검증: PowerPoint에서 슬라이드 열고 버튼 → 진단/칩/후속질문 확인, 토큰 로그로 이미지 1회만 전송됐는지 확인.
