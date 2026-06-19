# Phase E — Gemini Flash API 연동 설계

> 작성: 2026-06-19 · 상태: 설계 확정
> 상위 문서: [Master Spec](2026-06-19-teamppt-ai-redesign-design.md) §4-E

---

## 1. 목표

MockAiService를 GeminiAiService로 교체하여, 사용자 의도(userIntent)에 맞는 에셋+스타일 추천을 Gemini Flash가 수행하게 한다.

## 2. 결정 사항

| 항목 | 값 |
|------|-----|
| 모델 | `gemini-2.5-flash` |
| API 키 저장 | `src/TeampptAddin/Assets/api-keys.json` (gitignored) |
| 에러 처리 | 예외를 위로 던짐 → UI(AssetPanel)의 기존 catch에서 에러 메시지 표시 |
| 토큰 로깅 | 매 호출마다 input/output 토큰 수를 Logger로 기록 |
| 품질 원칙 | 토큰 절약보다 추천 품질 우선. 컴팩트 카탈로그는 토큰을 줄이되 품질에 필요한 정보는 모두 포함 |

## 3. 두 토큰 예산 원칙 적용

Master Spec §3.1에 따라:

- **런타임 프롬프트에는 CatalogEntry(컴팩트)를 전송** — hex 값, 폰트 family 등 매칭에 불필요한 무거운 값 제외
- CatalogBuilder가 HeaderAsset → CatalogEntry 변환을 이미 구현
- 팔레트/폰트도 id+name 수준으로 요약하여 전송

## 4. 아키텍처

```
AssetPanel (UI)
  ↓ RecommendAsync(intent, assets, palettes, fonts)
GeminiAiService : IAiService
  ├── CatalogBuilder.Build(assets) → CatalogEntry[]  (컴팩트)
  ├── GeminiPromptBuilder.Build(intent, catalog, palettes, fonts) → prompt
  ├── HTTP POST → Gemini REST API
  ├── JSON 파싱 → 에셋 file명 + 팔레트/폰트 id + 이유
  ├── file명으로 원본 HeaderAsset 역참조 → AiRecommendation 조립
  └── 토큰 사용량 로깅 (input/output tokens)
```

### 4.1 IAiService 인터페이스 — 변경 없음

```csharp
public interface IAiService
{
    Task<AiRecommendation> RecommendAsync(
        string userIntent,
        IEnumerable<HeaderAsset> assets,
        IEnumerable<StylePalette> palettes,
        IEnumerable<StyleFont> fonts);
}
```

### 4.2 GeminiAiService

- `IAiService` 구현
- 내부에서 CatalogBuilder로 컴팩트 카탈로그 생성
- Gemini REST API 호출 (System.Net.Http.HttpClient)
- JSON 응답 파싱 후 AiRecommendation 반환
- 토큰 사용량을 Logger로 기록

### 4.3 GeminiPromptBuilder

시스템 프롬프트 + 카탈로그 JSON + 사용자 의도를 조립하여 Gemini API 요청 본문 생성.

**시스템 프롬프트 역할:**
- "너는 PPT 디자인 어시스턴트. 사용자 의도에 가장 적합한 에셋과 스타일을 추천해라"
- 카탈로그(CatalogEntry[])를 JSON으로 제공
- 팔레트/폰트 목록을 요약하여 제공
- JSON 형식으로 응답하도록 지시

**Gemini 응답 스키마:**

```json
{
  "message": "추천 설명 메시지",
  "assets": [
    { "file": "header_3.pptx", "reason": "추천 이유" }
  ],
  "palette": { "name": "blue-professional", "reason": "이유" },
  "font": { "name": "Pretendard", "reason": "이유" }
}
```

### 4.4 API 키 관리

```json
// src/TeampptAddin/Assets/api-keys.json
{
  "gemini": "AIzaSy..."
}
```

- .gitignore에 `api-keys.json` 추가
- .csproj에 CopyToOutputDirectory 설정
- 키 로딩: 런타임에 JSON 파일에서 읽기

### 4.5 토큰 사용량 로깅

Gemini API 응답의 `usageMetadata` 필드에서:
- `promptTokenCount` (입력 토큰)
- `candidatesTokenCount` (출력 토큰)
- `totalTokenCount` (합계)

기존 `Logger.Log()` 활용하여 매 호출마다 기록.

## 5. 파일 변경 목록

| 파일 | 작업 |
|------|------|
| `Services/GeminiAiService.cs` | **신규** — IAiService 구현 |
| `Services/GeminiPromptBuilder.cs` | **신규** — 프롬프트 조립 |
| `Assets/api-keys.json` | **신규** — API 키 저장 |
| `UI/TaskPaneHost.cs` | **수정** — MockAiService → GeminiAiService 교체 |
| `.gitignore` | **수정** — api-keys.json 추가 |
| `TeampptAddin.csproj` | **수정** — api-keys.json CopyToOutput |

## 6. Phase B 참고 (인제스트 Vision)

- 에셋 이해용 Vision은 640px 해상도로 전송 (Phase B에서 구현)
- Phase E는 런타임 매칭만 담당, Vision 미사용

## 7. 테스트 전략

- GeminiPromptBuilder 단위 테스트: 프롬프트 구조 검증
- GeminiAiService 통합 테스트: 실제 API 호출 (수동)
- MockAiService는 테스트용으로 유지
