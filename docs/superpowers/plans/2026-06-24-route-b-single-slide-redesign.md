# Route B 단일 슬라이드 풀변환 리디자인 — 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 본문 레이아웃 슬라이드 1장을 LLM이 읽고 → 적합 에셋 Top 2 추천 → 사용자 선택 → 초안 재료를 그 에셋 자리에 실제로 채워 비파괴 변환한다.

**Architecture:** COM이 사실(정확한 텍스트·도형·개수)을, LLM이 판단(역할·매칭·슬롯배정)을 맡는다. 초안 읽기는 런타임 저가 1회 호출(Gemini Flash). 매칭은 기존 임베딩·match_assets RPC·캐시 재사용. 적용은 `Slide.Duplicate()` 복제본에만(원본 불변).

**Tech Stack:** C# .NET Framework 4.8, Office Interop (PowerPoint), Newtonsoft.Json, Gemini 2.5 Flash (REST), Supabase pgvector, xUnit(테스트).

설계 문서: [2026-06-24-route-b-single-slide-redesign-design.md](../specs/2026-06-24-route-b-single-slide-redesign-design.md)

## Global Constraints

- 네임스페이스: 프로덕션 코드는 `TeampptAddin`, 테스트는 `TeampptAddin.Tests`.
- 빌드는 **관리자 권한 MSBuild** (CLAUDE.md). COM 등록 때문. cmd 래핑+redirect 금지.
- 빌드 후 검증 필수: DLL 타임스탬프 1분 이내 + build.log 오류 0건.
- LLM 호출 모델: `gemini-2.5-flash`, `responseMimeType=application/json` + `responseSchema`, `thinkingBudget=0`. 일시오류(503/429/500) 3회 백오프 재시도. (기존 `GeminiAiService` 패턴 그대로)
- **좌표 변환에 폴백 로직 추가 금지** — `CoordinateConverter` 실패는 폴백 없이 드러낸다.
- LLM은 텍스트 내용을 생성/수정하지 않는다. 표시·이주되는 텍스트는 항상 COM 원문.
- API 키는 문서·커밋에 평문 금지.
- 임베딩: `gemini-embedding-001`, 768차원 (`EmbeddingService` 재사용).
- COM/삽입/렌더 코드는 단위테스트 불가 → **수동 검증**(관리자 빌드 후 실제 PPT). 순수 로직만 xUnit TDD.

## 재사용 (수정 없음)

- `ShapeInserter.InsertToActiveSlide(string pptxPath) → PowerPoint.ShapeRange` — 에셋 pptx 도형을 활성 슬라이드에 삽입.
- `RemoteAssetCache.GetPptxAsync(string remoteFile) → Task<string>` — 에셋 pptx 로컬 경로.
- `SlideImageRenderer.Render(PowerPoint.Presentation pres, int index, string pngPath)` — 슬라이드 → PNG.
- `EmbeddingService.EmbedAsync(string) → Task<float[]>`, `SupabaseClient.RpcAsync("match_assets", args)`, `MatchQuery.BuildArgs/ParseResults`, `RecommendationCache`.
- `AssetLoader` — 로컬 카탈로그 로드.

## 파일 구조 (신규)

| 파일 | 책임 |
|---|---|
| `Models/DraftModels.cs` | `DraftShape`, `DraftProfile`, `DraftMaterial`, `DraftUnderstanding`, `SlotMapping`, `MappingResult` POCO |
| `Core/DraftSlideReader.cs` | COM으로 현재 슬라이드 → `DraftProfile` (정확 텍스트·위치·타입·shapeId) |
| `Core/TextMetrics.cs` | 텍스트프레임 → 글자수·불릿수·계층 계산 (순수, 테스트 가능) |
| `Services/DraftUnderstandingSchema.cs` | 응답 스키마 + 시스템 프롬프트 |
| `Services/DraftUnderstandingParser.cs` | LLM JSON 파싱 + COM 사실 덮어쓰기 |
| `Services/DraftUnderstandingService.cs` | Gemini Flash 멀티모달 1회 호출 (DraftProfile+PNG → DraftUnderstanding) |
| `Services/DraftMatchService.cs` | matchIntent → 임베딩 → match_assets → 후보 (캐시 폴백) |
| `Services/SlotMapSchema.cs` | 슬롯매핑 응답 스키마 + 시스템 프롬프트 |
| `Services/SlotMapParser.cs` | 매핑 LLM JSON 파싱 + overflow/empty 계산 (순수 부분 분리) |
| `Services/SlotMapper.cs` | Gemini 호출로 초안재료 ↔ 에셋도형 매핑 |
| `Core/AssetShapeInventory.cs` | 삽입된 에셋 도형 목록 읽기 (COM) → 매핑 입력용 인벤토리 |
| `Core/RedesignApplier.cs` | 비파괴 Duplicate→에셋삽입→매핑대로 채움→썸네일 |
| `Services/RedesignService.cs` | 오케스트레이터(읽기→이해→매칭→Top2→매핑→적용→썸네일), 진행 콜백 |

**수정:** `GeminiAiService.cs`(매핑/선택 호출 메서드 추가), `UI/Wpf/AssetPanel.cs`(리디자인 버튼+시안 2카드+선택 핸들러).

---

## Task 1: 데이터 모델 (DraftModels)

**Files:**
- Create: `src/TeampptAddin/Models/DraftModels.cs`
- Test: `src/TeampptAddin.Tests/DraftModelsTest.cs`

**Interfaces:**
- Produces:
  - `DraftShape { int Id; string Kind; string Text; float Left,Top,Width,Height; int CharCount,BulletCount,MaxLevel; }` (Kind ∈ text|image|table|chart)
  - `DraftProfile { int SlideIndex; float SlideWidth,SlideHeight; List<DraftShape> Shapes; }`
  - `DraftMaterial { string Role; string Type; int SourceShapeId; string Text; int CharCount,BulletCount,Level; string Emphasis; }`
  - `DraftUnderstanding { List<DraftMaterial> Materials; Dictionary<string,int> Counts; string LayoutShape,DesignSummary; List<string> DominantColors; string MatchIntent,SlideKind; }`
  - `SlotMapping { int DraftShapeId; string AssetShapeId; string FitNote; double Confidence; }`
  - `MappingResult { List<SlotMapping> Mappings; List<int> Overflow; List<string> Empty; }`

- [ ] **Step 1: 모델 작성** (`DraftModels.cs`) — 위 6개 클래스를 `namespace TeampptAddin`에 POCO로 정의. 컬렉션은 생성자에서 `new List<>()`로 초기화(널 방지).

```csharp
using System.Collections.Generic;

namespace TeampptAddin
{
    public class DraftShape
    {
        public int Id { get; set; }
        public string Kind { get; set; }        // text|image|table|chart
        public string Text { get; set; } = "";
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int CharCount { get; set; }
        public int BulletCount { get; set; }
        public int MaxLevel { get; set; }
    }

    public class DraftProfile
    {
        public int SlideIndex { get; set; }
        public float SlideWidth { get; set; }
        public float SlideHeight { get; set; }
        public List<DraftShape> Shapes { get; set; } = new List<DraftShape>();
    }

    public class DraftMaterial
    {
        public string Role { get; set; }
        public string Type { get; set; }
        public int SourceShapeId { get; set; }
        public string Text { get; set; } = "";
        public int CharCount { get; set; }
        public int BulletCount { get; set; }
        public int Level { get; set; }
        public string Emphasis { get; set; }
    }

    public class DraftUnderstanding
    {
        public List<DraftMaterial> Materials { get; set; } = new List<DraftMaterial>();
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
        public string LayoutShape { get; set; } = "";
        public string DesignSummary { get; set; } = "";
        public List<string> DominantColors { get; set; } = new List<string>();
        public string MatchIntent { get; set; } = "";
        public string SlideKind { get; set; } = "";
    }

    public class SlotMapping
    {
        public int DraftShapeId { get; set; }
        public string AssetShapeId { get; set; }
        public string FitNote { get; set; } = "";
        public double Confidence { get; set; }
    }

    public class MappingResult
    {
        public List<SlotMapping> Mappings { get; set; } = new List<SlotMapping>();
        public List<int> Overflow { get; set; } = new List<int>();
        public List<string> Empty { get; set; } = new List<string>();
    }
}
```

- [ ] **Step 2: 실패 테스트 작성** (`DraftModelsTest.cs`)

```csharp
using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftModelsTest
    {
        [Fact]
        public void DraftProfile_Defaults_NonNull_Collections()
        {
            var p = new DraftProfile();
            Assert.NotNull(p.Shapes);
            Assert.Empty(p.Shapes);
        }

        [Fact]
        public void DraftUnderstanding_Defaults_NonNull()
        {
            var u = new DraftUnderstanding();
            Assert.NotNull(u.Materials);
            Assert.NotNull(u.Counts);
            Assert.Equal("", u.MatchIntent);
        }
    }
}
```

- [ ] **Step 3: 테스트 실행 — 통과 확인**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj --filter DraftModelsTest`
Expected: PASS (2 tests)

> 빌드가 net48 COM 참조로 `dotnet test`에서 실패하면, 관리자 MSBuild로 솔루션 빌드 후 `vstest.console.exe`로 테스트 실행. (아래 모든 테스트 동일)

- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Models/DraftModels.cs src/TeampptAddin.Tests/DraftModelsTest.cs
git commit -m "feat(redesign): Route B 데이터 모델 추가"
```

---

## Task 2: 텍스트 메트릭 (TextMetrics)

**Files:**
- Create: `src/TeampptAddin/Core/TextMetrics.cs`
- Test: `src/TeampptAddin.Tests/TextMetricsTest.cs`

**Interfaces:**
- Consumes: 없음 (순수 문자열 입력)
- Produces:
  - `TextMetrics.CharCount(string text) → int` — 공백 제외 글자수
  - `TextMetrics.BulletCount(IEnumerable<string> paragraphs) → int` — 비어있지 않은 문단 수
  - `TextMetrics.MaxLevel(IEnumerable<int> paragraphLevels) → int` — 최대 들여쓰기 레벨(0부터)

> COM `TextFrame`에서 문단 텍스트·레벨을 뽑는 건 Task 4(DraftSlideReader)에서. 여기선 그 값으로 메트릭만 계산하는 순수 함수.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using Xunit;

namespace TeampptAddin.Tests
{
    public class TextMetricsTest
    {
        [Fact]
        public void CharCount_Excludes_Whitespace()
            => Assert.Equal(5, TextMetrics.CharCount("a b\nc d e"));

        [Fact]
        public void BulletCount_Counts_NonEmpty_Paragraphs()
            => Assert.Equal(2, TextMetrics.BulletCount(new[] { "first", "", "  ", "second" }));

        [Fact]
        public void MaxLevel_Returns_Highest()
            => Assert.Equal(2, TextMetrics.MaxLevel(new[] { 0, 1, 2, 1 }));

        [Fact]
        public void MaxLevel_Empty_Is_Zero()
            => Assert.Equal(0, TextMetrics.MaxLevel(new int[0]));
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

Run: `dotnet test --filter TextMetricsTest`
Expected: FAIL (TextMetrics 없음)

- [ ] **Step 3: 구현**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public static class TextMetrics
    {
        public static int CharCount(string text)
            => string.IsNullOrEmpty(text) ? 0 : text.Count(c => !char.IsWhiteSpace(c));

        public static int BulletCount(IEnumerable<string> paragraphs)
            => paragraphs?.Count(p => !string.IsNullOrWhiteSpace(p)) ?? 0;

        public static int MaxLevel(IEnumerable<int> paragraphLevels)
        {
            var list = paragraphLevels?.ToList() ?? new List<int>();
            return list.Count == 0 ? 0 : list.Max();
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인** — Run: `dotnet test --filter TextMetricsTest` → PASS (4)

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Core/TextMetrics.cs src/TeampptAddin.Tests/TextMetricsTest.cs
git commit -m "feat(redesign): 텍스트 메트릭 순수 함수"
```

---

## Task 3: 초안 이해 스키마 (DraftUnderstandingSchema)

**Files:**
- Create: `src/TeampptAddin/Services/DraftUnderstandingSchema.cs`
- Test: `src/TeampptAddin.Tests/DraftUnderstandingSchemaTest.cs`

**Interfaces:**
- Produces:
  - `DraftUnderstandingSchema.BuildResponseSchema() → JObject`
  - `DraftUnderstandingSchema.BuildSystemPrompt() → string`

설계 §3 필드 기준. LLM은 `role/type/emphasis/layoutShape/designSummary/dominantColors/matchIntent/slideKind`와 `counts`를 채운다. `text/charCount/sourceShapeId`는 응답 스키마에 포함하되 **파서가 COM 값으로 덮어쓴다**(Task 5).

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftUnderstandingSchemaTest
    {
        [Fact]
        public void Schema_Has_Materials_And_MatchIntent()
        {
            var s = DraftUnderstandingSchema.BuildResponseSchema();
            var props = s["properties"];
            Assert.NotNull(props["materials"]);
            Assert.NotNull(props["matchIntent"]);
            Assert.NotNull(props["counts"]);
            Assert.NotNull(props["slideKind"]);
        }

        [Fact]
        public void SystemPrompt_Mentions_RoleJudgment()
        {
            var p = DraftUnderstandingSchema.BuildSystemPrompt();
            Assert.Contains("역할", p);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인** — `dotnet test --filter DraftUnderstandingSchemaTest` → FAIL

- [ ] **Step 3: 구현** — `UnderstandingSchema.cs` 패턴 그대로. (간결성을 위해 핵심 구조만; 모든 properties를 실제로 채울 것)

```csharp
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DraftUnderstandingSchema
    {
        public static JObject BuildResponseSchema()
        {
            JObject Str() => new JObject { ["type"] = "string" };
            JObject Int() => new JObject { ["type"] = "integer" };
            JObject StrArr() => new JObject { ["type"] = "array", ["items"] = Str() };

            var material = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["role"] = Str(),
                    ["type"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "text", "image", "table", "chart" } },
                    ["sourceShapeId"] = Int(),
                    ["emphasis"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "heading", "normal", "small" } }
                },
                ["required"] = new JArray { "role", "type", "sourceShapeId", "emphasis" }
            };

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["materials"] = new JObject { ["type"] = "array", ["items"] = material },
                    ["counts"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["textBlocks"] = Int(), ["bullets"] = Int(), ["images"] = Int(),
                            ["tables"] = Int(), ["charts"] = Int()
                        },
                        ["required"] = new JArray { "textBlocks", "bullets", "images", "tables", "charts" }
                    },
                    ["layoutShape"] = Str(),
                    ["designSummary"] = Str(),
                    ["dominantColors"] = StrArr(),
                    ["matchIntent"] = Str(),
                    ["slideKind"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "cover", "toc", "body", "section", "end" } }
                },
                ["required"] = new JArray { "materials", "counts", "layoutShape", "designSummary", "dominantColors", "matchIntent", "slideKind" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 초안 슬라이드를 분석하는 엔진이야. 슬라이드 이미지 1장과 도형 목록(JSON)을 받아, 이 초안을 적합한 디자인 에셋과 매칭하기 위한 구조화 표현을 만들어.

## 입력
- 이미지: 초안 슬라이드 렌더.
- 도형 목록: 각 도형의 id, kind(text/image/table/chart), 텍스트, 위치/크기, 글자수/불릿수. 이 값들이 사실이다.

## 네 일 (역할 판단만)
- materials: 각 도형(id)에 역할(title/subtitle/body/bullet/caption/image/table/chart/logo)과 강조(heading/normal/small)를 부여. sourceShapeId는 입력 도형 id를 그대로 써라. 텍스트 내용은 만들지 마라(사실은 입력에 있다).
- counts: 종류별 개수.
- layoutShape: 현재 골격을 짧게 (예: 'title-top + body-left + image-right').
- designSummary: 디자인 현황과 약점 1~2문장.
- dominantColors: 보이는 주요 색 hex 1~3개.
- matchIntent: 이 초안에 어울리는 에셋을 검색할 자연어 한 문장 (재료 종류·양 반영).
- slideKind: cover/toc/body/section/end 중 하나.
모르면 지어내지 말고 보수적으로.";
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인** — PASS (2)

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Services/DraftUnderstandingSchema.cs src/TeampptAddin.Tests/DraftUnderstandingSchemaTest.cs
git commit -m "feat(redesign): 초안 이해 응답 스키마+프롬프트"
```

---

## Task 4: 초안 이해 파서 (DraftUnderstandingParser)

**Files:**
- Create: `src/TeampptAddin/Services/DraftUnderstandingParser.cs`
- Test: `src/TeampptAddin.Tests/DraftUnderstandingParserTest.cs`

**Interfaces:**
- Consumes: `DraftProfile`(Task 1), LLM JSON 문자열
- Produces: `DraftUnderstandingParser.Parse(string llmJson, DraftProfile profile) → DraftUnderstanding`

**핵심:** LLM이 준 `materials`의 `role/type/emphasis`만 신뢰하고, `text/charCount/bulletCount/level`은 `profile`의 같은 `sourceShapeId` 도형 값으로 **덮어쓴다**. profile에 없는 sourceShapeId는 버린다(환각 도형 방지).

- [ ] **Step 1: 실패 테스트 작성**

```csharp
using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftUnderstandingParserTest
    {
        [Fact]
        public void Overwrites_Text_From_Profile_Not_Llm()
        {
            var profile = new DraftProfile
            {
                Shapes = { new DraftShape { Id = 3, Kind = "text", Text = "진짜 제목", CharCount = 4, BulletCount = 1 } }
            };
            const string llm = @"{
              ""materials"": [ { ""role"": ""title"", ""type"": ""text"", ""sourceShapeId"": 3, ""emphasis"": ""heading"" } ],
              ""counts"": { ""textBlocks"":1, ""bullets"":1, ""images"":0, ""tables"":0, ""charts"":0 },
              ""layoutShape"":""x"", ""designSummary"":""y"", ""dominantColors"":[""#000""],
              ""matchIntent"":""제목 슬라이드"", ""slideKind"":""body""
            }";

            var u = DraftUnderstandingParser.Parse(llm, profile);

            Assert.Single(u.Materials);
            Assert.Equal("title", u.Materials[0].Role);          // LLM
            Assert.Equal("진짜 제목", u.Materials[0].Text);        // COM 덮어씀
            Assert.Equal(4, u.Materials[0].CharCount);            // COM 덮어씀
            Assert.Equal("제목 슬라이드", u.MatchIntent);
        }

        [Fact]
        public void Drops_Material_With_Unknown_ShapeId()
        {
            var profile = new DraftProfile();   // 도형 없음
            const string llm = @"{
              ""materials"": [ { ""role"":""title"", ""type"":""text"", ""sourceShapeId"":99, ""emphasis"":""heading"" } ],
              ""counts"": { ""textBlocks"":0,""bullets"":0,""images"":0,""tables"":0,""charts"":0 },
              ""layoutShape"":"""", ""designSummary"":"""", ""dominantColors"":[], ""matchIntent"":""x"", ""slideKind"":""body""
            }";

            var u = DraftUnderstandingParser.Parse(llm, profile);
            Assert.Empty(u.Materials);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인** — FAIL

- [ ] **Step 3: 구현**

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DraftUnderstandingParser
    {
        public static DraftUnderstanding Parse(string llmJson, DraftProfile profile)
        {
            var o = JObject.Parse(llmJson);
            var byId = profile.Shapes.ToDictionary(s => s.Id);

            var materials = new List<DraftMaterial>();
            foreach (var m in (o["materials"] as JArray) ?? new JArray())
            {
                var id = m["sourceShapeId"]?.Value<int>() ?? -1;
                if (!byId.TryGetValue(id, out var shape)) continue;   // 환각 도형 제거
                materials.Add(new DraftMaterial
                {
                    Role = m["role"]?.ToString(),
                    Type = m["type"]?.ToString() ?? shape.Kind,
                    Emphasis = m["emphasis"]?.ToString(),
                    SourceShapeId = id,
                    Text = shape.Text,                 // COM 사실
                    CharCount = shape.CharCount,        // COM 사실
                    BulletCount = shape.BulletCount,    // COM 사실
                    Level = shape.MaxLevel
                });
            }

            var counts = new Dictionary<string, int>();
            if (o["counts"] is JObject c)
                foreach (var p in c.Properties())
                    counts[p.Name] = p.Value.Value<int>();

            return new DraftUnderstanding
            {
                Materials = materials,
                Counts = counts,
                LayoutShape = o["layoutShape"]?.ToString() ?? "",
                DesignSummary = o["designSummary"]?.ToString() ?? "",
                DominantColors = (o["dominantColors"] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>(),
                MatchIntent = o["matchIntent"]?.ToString() ?? "",
                SlideKind = o["slideKind"]?.ToString() ?? ""
            };
        }
    }
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인** — PASS (2)

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Services/DraftUnderstandingParser.cs src/TeampptAddin.Tests/DraftUnderstandingParserTest.cs
git commit -m "feat(redesign): 초안 이해 파서 — COM 사실 덮어쓰기"
```

---

## Task 5: 초안 슬라이드 리더 (DraftSlideReader) — COM, 수동 검증

**Files:**
- Create: `src/TeampptAddin/Core/DraftSlideReader.cs`

**Interfaces:**
- Consumes: `TextMetrics`(Task 2), `DraftProfile/DraftShape`(Task 1)
- Produces: `DraftSlideReader.ReadCurrentSlide() → DraftProfile` (활성 윈도우 없으면 null)

도형 종류 판정: `Shape.HasTextFrame == MsoTrue && TextFrame.HasText`이면 text; `Shape.Type == msoPicture`면 image; `HasTable`이면 table; `HasChart`이면 chart. Id는 1부터 순번. text는 문단별 `Text`/`IndentLevel` 모아 `TextMetrics`로 메트릭 계산.

- [ ] **Step 1: 구현**

```csharp
using System.Collections.Generic;
using System.Linq;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    public static class DraftSlideReader
    {
        public static DraftProfile ReadCurrentSlide()
        {
            var app = Globals.Application;
            var win = app?.ActiveWindow;
            if (win == null) return null;

            PowerPoint.Slide slide;
            try { slide = (PowerPoint.Slide)win.View.Slide; }
            catch { return null; }
            if (slide == null) return null;

            var pres = win.Presentation;
            var profile = new DraftProfile
            {
                SlideIndex = slide.SlideIndex,
                SlideWidth = pres.PageSetup.SlideWidth,
                SlideHeight = pres.PageSetup.SlideHeight
            };

            int id = 1;
            foreach (PowerPoint.Shape sh in slide.Shapes)
            {
                var ds = new DraftShape
                {
                    Id = id++,
                    Left = sh.Left, Top = sh.Top, Width = sh.Width, Height = sh.Height,
                    Kind = "text"
                };

                if (sh.HasTable == MsoTriState.msoTrue) ds.Kind = "table";
                else if (sh.HasChart == MsoTriState.msoTrue) ds.Kind = "chart";
                else if (sh.Type == MsoShapeType.msoPicture) ds.Kind = "image";
                else if (sh.HasTextFrame == MsoTriState.msoTrue &&
                         sh.TextFrame.HasText == MsoTriState.msoTrue)
                {
                    ds.Kind = "text";
                    var paras = new List<string>();
                    var levels = new List<int>();
                    var tr = sh.TextFrame.TextRange;
                    foreach (PowerPoint.TextRange p in tr.Paragraphs())
                    {
                        paras.Add(p.Text);
                        levels.Add(p.IndentLevel);
                    }
                    ds.Text = tr.Text;
                    ds.CharCount = TextMetrics.CharCount(tr.Text);
                    ds.BulletCount = TextMetrics.BulletCount(paras);
                    ds.MaxLevel = TextMetrics.MaxLevel(levels);
                }
                else
                {
                    continue; // 빈 장식 도형은 스킵
                }

                profile.Shapes.Add(ds);
            }

            Logger.Log($"[DraftReader] slide={profile.SlideIndex} shapes={profile.Shapes.Count}");
            return profile;
        }
    }
}
```

- [ ] **Step 2: 빌드** — 관리자 MSBuild (CLAUDE.md 명령). build.log 오류 0 + DLL 타임스탬프 확인.

- [ ] **Step 3: 수동 검증** — 본문 레이아웃 슬라이드를 열고, 임시로 `RedesignService`(Task 9) 또는 디버그 로그를 통해 `ReadCurrentSlide()` 호출 → `Logger` 로그에 도형 수·종류·글자수가 실제 슬라이드와 일치하는지 확인. (이 Task 단독 검증은 Task 9 배선 후 함께)

- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Core/DraftSlideReader.cs
git commit -m "feat(redesign): 초안 슬라이드 COM 리더"
```

---

## Task 6: 초안 이해 서비스 (DraftUnderstandingService) — LLM, 수동 검증

**Files:**
- Create: `src/TeampptAddin/Services/DraftUnderstandingService.cs`
- Modify: `src/TeampptAddin/Services/GeminiAiService.cs` (멀티모달 JSON 호출 공용 메서드 노출)

**Interfaces:**
- Consumes: `DraftProfile`, PNG 경로, `DraftUnderstandingSchema`, `DraftUnderstandingParser`
- Produces: `DraftUnderstandingService.UnderstandAsync(DraftProfile profile, string pngPath) → Task<DraftUnderstanding>`

`GeminiAiService`에 재사용 가능한 저수준 호출을 추가한다(기존 `DiagnoseSlideAsync`의 멀티모달 본문을 일반화):

- [ ] **Step 1: GeminiAiService에 공용 호출 추가**

```csharp
// GeminiAiService.cs 내부에 추가 (history 미사용, 1회성 호출)
public async Task<string> GenerateJsonAsync(string systemPrompt, string userText, string pngPathOrNull, JObject responseSchema, double temperature = 0.4)
{
    var parts = new JArray();
    if (pngPathOrNull != null)
        parts.Add(new JObject { ["inline_data"] = new JObject {
            ["mime_type"] = "image/png",
            ["data"] = Convert.ToBase64String(File.ReadAllBytes(pngPathOrNull)) } });
    parts.Add(new JObject { ["text"] = userText });

    var requestBody = new JObject
    {
        ["contents"] = new JArray { new JObject { ["role"] = "user", ["parts"] = parts } },
        ["systemInstruction"] = new JObject { ["parts"] = new JArray { new JObject { ["text"] = systemPrompt } } },
        ["generationConfig"] = new JObject
        {
            ["temperature"] = temperature,
            ["responseMimeType"] = "application/json",
            ["responseSchema"] = responseSchema,
            ["thinkingConfig"] = new JObject { ["thinkingBudget"] = 0 }
        }
    };

    var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
    var bodyString = requestBody.ToString(Formatting.None);

    const int maxAttempts = 3;
    HttpResponseMessage response = null; string body = null;
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        var content = new StringContent(bodyString, Encoding.UTF8, "application/json");
        Http.DefaultRequestHeaders.Authorization = null;
        response = await Http.PostAsync(url, content).ConfigureAwait(false);
        body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (response.IsSuccessStatusCode) break;
        var status = (int)response.StatusCode;
        bool transient = status == 503 || status == 429 || status == 500;
        if (transient && attempt < maxAttempts) { await Task.Delay(500 * (1 << (attempt - 1))).ConfigureAwait(false); continue; }
        throw new HttpRequestException($"Gemini API 오류 ({status}): {body}");
    }
    var root = JObject.Parse(body);
    LogTokenUsage(root);
    var text = root["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
    if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("Gemini 응답에 텍스트가 없습니다.");
    return text;
}
```

- [ ] **Step 2: DraftUnderstandingService 구현**

```csharp
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public class DraftUnderstandingService
    {
        private readonly GeminiAiService _gemini;
        public DraftUnderstandingService(GeminiAiService gemini) { _gemini = gemini; }

        public async Task<DraftUnderstanding> UnderstandAsync(DraftProfile profile, string pngPath)
        {
            var userText = "도형 목록(JSON):\n" + JsonConvert.SerializeObject(profile.Shapes);
            var json = await _gemini.GenerateJsonAsync(
                DraftUnderstandingSchema.BuildSystemPrompt(),
                userText, pngPath,
                DraftUnderstandingSchema.BuildResponseSchema()).ConfigureAwait(false);
            return DraftUnderstandingParser.Parse(json, profile);
        }
    }
}
```

- [ ] **Step 3: 빌드** (관리자 MSBuild, 검증 2종)
- [ ] **Step 4: 수동 검증** — Task 9 배선 후 실제 슬라이드로 호출, 로그의 `matchIntent`·`materials.role`이 슬라이드 내용과 맞는지 확인.
- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Services/DraftUnderstandingService.cs src/TeampptAddin/Services/GeminiAiService.cs
git commit -m "feat(redesign): 초안 이해 서비스 + Gemini 공용 JSON 호출"
```

---

## Task 7: 초안 매칭 (DraftMatchService)

**Files:**
- Create: `src/TeampptAddin/Services/DraftMatchService.cs`

**Interfaces:**
- Consumes: `EmbeddingService`, `SupabaseClient`, `MatchQuery`, `RecommendationCache`, `DraftUnderstanding`
- Produces: `DraftMatchService.FindCandidatesAsync(DraftUnderstanding u, int topN = 8) → Task<List<HeaderAsset>>`

`VectorRecommendService`의 매칭 부분과 동일 패턴. 쿼리 텍스트 = `u.MatchIntent` (+ counts 요약 문자열). 실패 시 캐시 폴백.

- [ ] **Step 1: 구현**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    public class DraftMatchService
    {
        private readonly EmbeddingService _embed;
        private readonly SupabaseClient _supa;
        private readonly RecommendationCache _cache = new RecommendationCache();

        public DraftMatchService(EmbeddingService embed, SupabaseClient supa) { _embed = embed; _supa = supa; }

        public async Task<List<HeaderAsset>> FindCandidatesAsync(DraftUnderstanding u, int topN = 8)
        {
            var countsText = string.Join(", ", u.Counts.Select(kv => $"{kv.Key}:{kv.Value}"));
            var query = $"{u.MatchIntent} ({countsText})";
            try
            {
                var vector = await _embed.EmbedAsync(query).ConfigureAwait(false);
                var rpcJson = await _supa.RpcAsync("match_assets", MatchQuery.BuildArgs(vector, topN)).ConfigureAwait(false);
                var candidates = MatchQuery.ParseResults(rpcJson);
                if (candidates.Count > 0) _cache.Save(candidates);
                Logger.Log($"[DraftMatch] 후보 {candidates.Count}개 — query='{query}'");
                return candidates;
            }
            catch (Exception ex)
            {
                Logger.Log($"[DraftMatch] Supabase 실패 → 캐시 폴백: {ex.Message}");
                return _cache.Load();
            }
        }
    }
}
```

> `EmbeddingService.EmbedAsync`/`SupabaseClient.RpcAsync`/`MatchQuery`/`RecommendationCache`의 실제 시그니처를 코딩 직전 확인하고 일치시킬 것 (이 플랜은 `VectorRecommendService.cs` 기준).

- [ ] **Step 2: 빌드** (관리자 MSBuild, 검증)
- [ ] **Step 3: 커밋**

```bash
git add src/TeampptAddin/Services/DraftMatchService.cs
git commit -m "feat(redesign): 초안 기반 벡터 매칭 서비스"
```

---

## Task 8: 슬롯매핑 스키마 + 파서 (SlotMapSchema, SlotMapParser)

**Files:**
- Create: `src/TeampptAddin/Services/SlotMapSchema.cs`
- Create: `src/TeampptAddin/Services/SlotMapParser.cs`
- Test: `src/TeampptAddin.Tests/SlotMapParserTest.cs`

**Interfaces:**
- Produces:
  - `SlotMapSchema.BuildResponseSchema() → JObject` (mappings 배열: draftShapeId, assetShapeId, fitNote, confidence)
  - `SlotMapSchema.BuildSystemPrompt() → string`
  - `SlotMapParser.Parse(string llmJson, IEnumerable<int> draftShapeIds, IEnumerable<string> assetShapeIds) → MappingResult` — overflow(미배정 draftShapeId)·empty(미배정 assetShapeId) 계산

- [ ] **Step 1: 실패 테스트 작성** (`SlotMapParserTest.cs`)

```csharp
using Xunit;

namespace TeampptAddin.Tests
{
    public class SlotMapParserTest
    {
        [Fact]
        public void Computes_Overflow_And_Empty()
        {
            const string llm = @"{ ""mappings"": [
                { ""draftShapeId"":1, ""assetShapeId"":""a1"", ""fitNote"":""제목"", ""confidence"":0.9 }
            ] }";
            var r = SlotMapParser.Parse(llm, new[] { 1, 2 }, new[] { "a1", "a2" });

            Assert.Single(r.Mappings);
            Assert.Equal(1, r.Mappings[0].DraftShapeId);
            Assert.Contains(2, r.Overflow);      // 배정 안된 초안 도형
            Assert.Contains("a2", r.Empty);      // 빈 에셋 슬롯
        }

        [Fact]
        public void Ignores_Mapping_With_Unknown_Ids()
        {
            const string llm = @"{ ""mappings"": [
                { ""draftShapeId"":99, ""assetShapeId"":""zz"", ""confidence"":0.5 }
            ] }";
            var r = SlotMapParser.Parse(llm, new[] { 1 }, new[] { "a1" });
            Assert.Empty(r.Mappings);
            Assert.Contains(1, r.Overflow);
            Assert.Contains("a1", r.Empty);
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인** — FAIL

- [ ] **Step 3: SlotMapParser 구현**

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class SlotMapParser
    {
        public static MappingResult Parse(string llmJson, IEnumerable<int> draftShapeIds, IEnumerable<string> assetShapeIds)
        {
            var drafts = new HashSet<int>(draftShapeIds);
            var assets = new HashSet<string>(assetShapeIds);
            var o = JObject.Parse(llmJson);

            var mappings = new List<SlotMapping>();
            foreach (var m in (o["mappings"] as JArray) ?? new JArray())
            {
                var did = m["draftShapeId"]?.Value<int>() ?? -1;
                var aid = m["assetShapeId"]?.ToString();
                if (!drafts.Contains(did) || aid == null || !assets.Contains(aid)) continue;
                mappings.Add(new SlotMapping
                {
                    DraftShapeId = did,
                    AssetShapeId = aid,
                    FitNote = m["fitNote"]?.ToString() ?? "",
                    Confidence = m["confidence"]?.Value<double>() ?? 0
                });
            }

            var usedDrafts = new HashSet<int>(mappings.Select(x => x.DraftShapeId));
            var usedAssets = new HashSet<string>(mappings.Select(x => x.AssetShapeId));
            return new MappingResult
            {
                Mappings = mappings,
                Overflow = drafts.Where(d => !usedDrafts.Contains(d)).ToList(),
                Empty = assets.Where(a => !usedAssets.Contains(a)).ToList()
            };
        }
    }
}
```

- [ ] **Step 4: SlotMapSchema 구현** (Task 3 패턴) — `mappings` 배열(`draftShapeId`:int, `assetShapeId`:string, `fitNote`:string, `confidence`:number) + 시스템 프롬프트("초안 재료를 에셋 도형에 역할·타입·개수로 배정만, 텍스트 생성 금지, 부족하면 남겨라").

```csharp
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class SlotMapSchema
    {
        public static JObject BuildResponseSchema()
        {
            var mapping = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["draftShapeId"] = new JObject { ["type"] = "integer" },
                    ["assetShapeId"] = new JObject { ["type"] = "string" },
                    ["fitNote"] = new JObject { ["type"] = "string" },
                    ["confidence"] = new JObject { ["type"] = "number" }
                },
                ["required"] = new JArray { "draftShapeId", "assetShapeId", "confidence" }
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["mappings"] = new JObject { ["type"] = "array", ["items"] = mapping } },
                ["required"] = new JArray { "mappings" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 초안 재료를 디자인 에셋의 빈 자리에 배정하는 엔진이야.
입력: 초안 재료 목록(id, 역할, 타입, 글자수)과 에셋의 실제 도형 목록(id, 종류, 위치, 샘플텍스트).
할 일: 각 초안 재료를 역할·타입·개수가 맞는 에셋 도형에 배정(draftShapeId→assetShapeId).
규칙: 텍스트 내용은 만들지 마라. 적합한 자리가 없으면 배정하지 말고 남겨라(욱여넣기 금지). confidence는 0~1.";
        }
    }
}
```

- [ ] **Step 5: 테스트 실행 — 통과 확인** — PASS (2)

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/SlotMapSchema.cs src/TeampptAddin/Services/SlotMapParser.cs src/TeampptAddin.Tests/SlotMapParserTest.cs
git commit -m "feat(redesign): 슬롯매핑 스키마+파서(overflow/empty)"
```

---

## Task 9: 에셋 도형 인벤토리 + 슬롯매퍼 (AssetShapeInventory, SlotMapper) — 일부 COM

**Files:**
- Create: `src/TeampptAddin/Core/AssetShapeInventory.cs`
- Create: `src/TeampptAddin/Services/SlotMapper.cs`

**Interfaces:**
- Produces:
  - `class AssetShapeInfo { string Id; string Kind; float Left,Top,Width,Height; string SampleText; }`
  - `AssetShapeInventory.Read(PowerPoint.ShapeRange shapes) → List<AssetShapeInfo>` — 삽입된 에셋 도형을 인벤토리로 (Id="a"+순번)
  - `SlotMapper.MapAsync(DraftUnderstanding u, List<AssetShapeInfo> assetShapes) → Task<MappingResult>`

- [ ] **Step 1: AssetShapeInventory 구현** (DraftSlideReader와 유사한 종류 판정; SampleText는 텍스트프레임 일부)

```csharp
using System.Collections.Generic;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    public class AssetShapeInfo
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string SampleText { get; set; } = "";
    }

    public static class AssetShapeInventory
    {
        public static List<AssetShapeInfo> Read(PowerPoint.ShapeRange shapes)
        {
            var list = new List<AssetShapeInfo>();
            if (shapes == null) return list;
            int n = 1;
            foreach (PowerPoint.Shape sh in shapes)
            {
                var info = new AssetShapeInfo
                {
                    Id = "a" + n++,
                    Left = sh.Left, Top = sh.Top, Width = sh.Width, Height = sh.Height,
                    Kind = "text"
                };
                if (sh.HasTable == MsoTriState.msoTrue) info.Kind = "table";
                else if (sh.HasChart == MsoTriState.msoTrue) info.Kind = "chart";
                else if (sh.Type == MsoShapeType.msoPicture) info.Kind = "image";
                else if (sh.HasTextFrame == MsoTriState.msoTrue && sh.TextFrame.HasText == MsoTriState.msoTrue)
                {
                    info.Kind = "text";
                    var t = sh.TextFrame.TextRange.Text ?? "";
                    info.SampleText = t.Length > 40 ? t.Substring(0, 40) : t;
                }
                list.Add(info);
            }
            return list;
        }
    }
}
```

- [ ] **Step 2: SlotMapper 구현**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public class SlotMapper
    {
        private readonly GeminiAiService _gemini;
        public SlotMapper(GeminiAiService gemini) { _gemini = gemini; }

        public async Task<MappingResult> MapAsync(DraftUnderstanding u, List<AssetShapeInfo> assetShapes)
        {
            var userText =
                "초안 재료:\n" + JsonConvert.SerializeObject(u.Materials) +
                "\n\n에셋 도형:\n" + JsonConvert.SerializeObject(assetShapes);
            var json = await _gemini.GenerateJsonAsync(
                SlotMapSchema.BuildSystemPrompt(), userText, null,
                SlotMapSchema.BuildResponseSchema(), 0.2).ConfigureAwait(false);
            return SlotMapParser.Parse(json,
                u.Materials.Select(m => m.SourceShapeId),
                assetShapes.Select(a => a.Id));
        }
    }
}
```

- [ ] **Step 3: 빌드** (관리자 MSBuild, 검증)
- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Core/AssetShapeInventory.cs src/TeampptAddin/Services/SlotMapper.cs
git commit -m "feat(redesign): 에셋 도형 인벤토리 + 슬롯매퍼"
```

---

## Task 10: 비파괴 적용기 (RedesignApplier) — COM, 수동 검증

**Files:**
- Create: `src/TeampptAddin/Core/RedesignApplier.cs`

**Interfaces:**
- Consumes: `ShapeInserter`, `AssetShapeInventory`, `SlotMapper`, `MappingResult`, `DraftProfile`, `SlideImageRenderer`
- Produces:
  - `class RedesignPreview { int SlideIndex; string ThumbPath; HeaderAsset Asset; MappingResult Mapping; }`
  - `RedesignApplier.BuildPreviewAsync(int originalSlideIndex, DraftProfile profile, DraftUnderstanding u, HeaderAsset asset, string assetPptxPath, SlotMapper mapper) → Task<RedesignPreview>`

흐름: ① 원본 슬라이드 `Duplicate()` → 복제 인덱스 확보. ② 복제본을 활성화하고 `ShapeInserter.InsertToActiveSlide(assetPptxPath)`로 에셋 삽입 → ShapeRange. ③ `AssetShapeInventory.Read` → `mapper.MapAsync` → MappingResult. ④ 매핑대로 복제본의 초안 도형(SourceShapeId 순번) 텍스트를 에셋 text 도형에 주입, image는 위치/크기 맞춰 배치. ⑤ 매핑된 원본 초안 도형 제거. ⑥ `SlideImageRenderer.Render`로 썸네일.

> 좌표·도형 조작은 `CoordinateConverter` 규칙 준수(폴백 추가 금지). 복제본에만 작업하므로 원본 불변.

- [ ] **Step 1: 구현** (핵심 골격 — 실제 COM 인덱스/활성화 디테일은 빌드하며 확정)

```csharp
using System.Linq;
using System.Threading.Tasks;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    public class RedesignPreview
    {
        public int SlideIndex { get; set; }
        public string ThumbPath { get; set; }
        public HeaderAsset Asset { get; set; }
        public MappingResult Mapping { get; set; }
    }

    public static class RedesignApplier
    {
        public static async Task<RedesignPreview> BuildPreviewAsync(
            int originalSlideIndex, DraftProfile profile, DraftUnderstanding u,
            HeaderAsset asset, string assetPptxPath, SlotMapper mapper)
        {
            var app = Globals.Application;
            var pres = app.ActivePresentation;

            // ① 복제 (원본 바로 뒤에 생성됨)
            var original = pres.Slides[originalSlideIndex];
            var dupRange = original.Duplicate();
            var dup = pres.Slides[originalSlideIndex + 1];
            dup.Select();   // 활성 슬라이드로

            // ② 에셋 삽입
            var assetShapes = ShapeInserter.InsertToActiveSlide(assetPptxPath);
            var inventory = AssetShapeInventory.Read(assetShapes);

            // ③ 매핑
            var mapping = await mapper.MapAsync(u, inventory).ConfigureAwait(false);

            // ④ 주입: draft 도형(순번=SourceShapeId)의 텍스트를 에셋 text 도형에
            //    (dup.Shapes에서 에셋 삽입 전 원본 도형은 앞쪽, 에셋은 뒤쪽 — Id 매핑은 삽입 순서로 추적)
            //    상세 구현은 빌드 단계에서 인덱싱 확정. text 주입 = assetShape.TextFrame.TextRange.Text = draftText.

            // ⑤ 매핑된 원본 초안 도형 제거

            // ⑥ 썸네일
            var thumb = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "TeampptAddin", "cache", "redesign", $"preview-{dup.SlideIndex}.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(thumb));
            SlideImageRenderer.Render(pres, dup.SlideIndex, thumb);

            return new RedesignPreview { SlideIndex = dup.SlideIndex, ThumbPath = thumb, Asset = asset, Mapping = mapping };
        }
    }
}
```

> Step 1 주: ④⑤의 도형 인덱싱(원본 초안 도형 vs 삽입 에셋 도형 구분)은 COM에서 삽입 전후 Shapes 스냅샷을 떠서 확정한다. 구현자는 빌드하며 실제 PPT로 인덱스를 맞춘다. **텍스트 주입은 항상 `profile` 원문**(LLM 생성 금지).

- [ ] **Step 2: 빌드** (관리자 MSBuild, 검증 2종)
- [ ] **Step 3: 수동 검증** — 본문 슬라이드에서 호출 → 복제본이 원본 뒤에 생기고, 에셋이 삽입되고, 초안 텍스트가 에셋 자리에 들어가며, **원본은 그대로**인지 확인. 썸네일 PNG 생성 확인.
- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Core/RedesignApplier.cs
git commit -m "feat(redesign): 비파괴 적용기(복제+에셋삽입+재료주입+썸네일)"
```

---

## Task 11: 오케스트레이터 (RedesignService) — 수동 검증

**Files:**
- Create: `src/TeampptAddin/Services/RedesignService.cs`

**Interfaces:**
- Consumes: 모든 위 부품
- Produces:
  - `RedesignService(string supabaseUrl, string anonKey, string geminiKey)`
  - `RedesignService.RunAsync(System.Action<string> progress) → Task<List<RedesignPreview>>` (Top 2 시안)
  - `RedesignService.Commit(RedesignPreview chosen, List<RedesignPreview> all)` — 선택분 남기고 나머지 복제 슬라이드 삭제

흐름: 진행콜백("초안 읽는 중"→"이해하는 중"→"맞는 에셋 찾는 중"→"시안 만드는 중") 따라 ReadCurrentSlide → SlideImageRenderer로 초안 PNG → UnderstandAsync → FindCandidatesAsync → 상위 후보 중 **선택기(GeminiAiService)로 Top 2** → 각 후보 `RemoteAssetCache.GetPptxAsync` → `RedesignApplier.BuildPreviewAsync` → 2개 preview 반환.

- [ ] **Step 1: 구현** — 부품 조립. 선택기는 기존 `GeminiAiService.RecommendAsync`를 `u.MatchIntent`로 호출해 상위 2개 file을 고르거나, 후보 상위 2개를 그대로 사용(데모 단순화 가능). COM 작업(Read/Apply)은 UI 스레드에서 호출되도록 주의(AssetPanel가 디스패처에서 호출).

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    public class RedesignService
    {
        private readonly GeminiAiService _gemini;
        private readonly DraftUnderstandingService _understand;
        private readonly DraftMatchService _match;
        private readonly SlotMapper _mapper;
        private readonly RemoteAssetCache _assetCache;

        public RedesignService(string supabaseUrl, string anonKey, string geminiKey)
        {
            _gemini = new GeminiAiService(geminiKey);
            _understand = new DraftUnderstandingService(_gemini);
            _match = new DraftMatchService(new EmbeddingService(geminiKey), new SupabaseClient(supabaseUrl, anonKey));
            _mapper = new SlotMapper(_gemini);
            _assetCache = new RemoteAssetCache(/* 기존 생성자 인자에 맞춤 */);
        }

        public async Task<List<RedesignPreview>> RunAsync(Action<string> progress)
        {
            progress("초안 읽는 중…");
            var profile = DraftSlideReader.ReadCurrentSlide();
            if (profile == null) throw new InvalidOperationException("활성 슬라이드를 찾을 수 없습니다.");

            var png = SlideCaptureService.CaptureCurrentSlide()?.PngPath;

            progress("초안 이해하는 중…");
            var u = await _understand.UnderstandAsync(profile, png).ConfigureAwait(false);

            progress("맞는 에셋 찾는 중…");
            var candidates = await _match.FindCandidatesAsync(u).ConfigureAwait(false);
            var top2 = candidates.Take(2).ToList();

            progress("시안 만드는 중…");
            var previews = new List<RedesignPreview>();
            foreach (var asset in top2)
            {
                var pptx = await _assetCache.GetPptxAsync(asset.File).ConfigureAwait(false);
                var preview = await RedesignApplier.BuildPreviewAsync(
                    profile.SlideIndex, profile, u, asset, pptx, _mapper).ConfigureAwait(false);
                previews.Add(preview);
            }
            return previews;
        }

        public void Commit(RedesignPreview chosen, List<RedesignPreview> all)
        {
            var pres = Globals.Application.ActivePresentation;
            // 선택 안된 복제 슬라이드 삭제 (인덱스 큰 것부터)
            foreach (var p in all.Where(p => p != chosen).OrderByDescending(p => p.SlideIndex))
                pres.Slides[p.SlideIndex].Delete();
        }
    }
}
```

> `RemoteAssetCache` 생성자·`SlideCaptureService` 반환형은 코딩 직전 실제 시그니처 확인. 복제 슬라이드 다수 생성 시 SlideIndex가 밀리므로, Commit은 삭제 전 인덱스 재조회 또는 SlideID 기반으로 보강.

- [ ] **Step 2: 빌드** (관리자 MSBuild, 검증)
- [ ] **Step 3: 수동 검증** — Task 12 UI 배선 후 통합 검증.
- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Services/RedesignService.cs
git commit -m "feat(redesign): 오케스트레이터(읽기→이해→매칭→Top2→시안)"
```

---

## Task 12: AI탭 UI — 리디자인 버튼 + 시안 2카드 + 선택 (수동 검증)

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (화면공유 진단 바 인근, ~564~764줄 패턴 참고)

**Interfaces:**
- Consumes: `RedesignService`
- Produces: AI탭에 "리디자인" 진입 + 진행 버블 + 시안 2카드(썸네일) + 카드 선택 → `Commit`

- [ ] **Step 1: 버튼·핸들러 추가** — 기존 `_shareBar`(화면공유 진단) 옆에 "AI 리디자인" 바 추가. 클릭 시 `RunRedesignAsync()` 호출. 진행은 기존 AddAiBubble 패턴으로 progress 콜백 표시. 완료 시 2개 썸네일을 카드로 AI탭에 추가, 카드 클릭 → `_redesign.Commit(chosen, all)` + 확정 버블.

```csharp
private RedesignService _redesign;
private List<RedesignPreview> _lastPreviews;

private async Task RunRedesignAsync()
{
    try
    {
        if (_redesign == null)
            _redesign = new RedesignService(/* supabaseUrl */, /* anonKey */, /* geminiKey */); // 기존 키 로딩 경로 재사용
        AddAiBubble("리디자인을 시작할게요.");
        _lastPreviews = await _redesign.RunAsync(msg => Dispatcher.Invoke(() => AddAiBubble(msg)));
        Dispatcher.Invoke(() => ShowRedesignCards(_lastPreviews));
    }
    catch (Exception ex)
    {
        AddAiBubble($"리디자인 중 오류: {ex.Message}");
        Logger.Log($"[Redesign] 실패: {ex}");
    }
}

private void ShowRedesignCards(List<RedesignPreview> previews)
{
    foreach (var p in previews)
    {
        // 썸네일(p.ThumbPath) 이미지 + Asset.Name + 선택 버튼 카드 구성 (기존 카드 패턴 1454~ 참고)
        // 카드 클릭:
        //   _redesign.Commit(p, previews);
        //   AddAiBubble($"'{p.Asset.Name}'(으)로 변환했어요.");
    }
}
```

> 키 로딩(supabaseUrl/anonKey/geminiKey)은 기존 인제스트/추천 경로에서 쓰는 동일 소스를 재사용. COM 호출은 반드시 UI 디스패처 스레드에서.

- [ ] **Step 2: 빌드** (관리자 MSBuild, 검증 2종)
- [ ] **Step 3: 통합 수동 검증 (전체 흐름)** — 본문 레이아웃 슬라이드 열기 → AI 리디자인 클릭 → 진행 버블 표시 → 시안 2개 카드 표시 → 하나 선택 → 변환 슬라이드 남고 다른 시안 삭제, **원본 슬라이드 불변** 확인. 비포(원본)/애프터(선택) 썸네일 레일 나란히 확인.
- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs
git commit -m "feat(redesign): AI탭 리디자인 버튼+시안 2카드+선택"
```

---

## Self-Review 메모

- **스펙 커버리지:** §2 파이프라인=Task5~12, §3 스키마=Task1/3/4, §4 슬롯매핑=Task8/9, §5 비파괴적용=Task10, §6 시안2개=Task10/11, §7 파일=전 Task, §8 에러처리=각 서비스 try/캐시폴백·confidence는 SlotMapParser+Applier, §9 테스트=Task1~4/8 xUnit+나머지 수동, §10 범위=본문 1장.
- **타입 일관성:** `DraftProfile/DraftShape/DraftMaterial/DraftUnderstanding/SlotMapping/MappingResult/AssetShapeInfo/RedesignPreview` 전 Task 동일 사용. `GenerateJsonAsync` 시그니처 Task6 정의→Task6/9 사용 일치.
- **확인 필요(코딩 직전):** `EmbeddingService.EmbedAsync`/`SupabaseClient` 생성자/`RemoteAssetCache` 생성자/`SlideCaptureService` 반환형 실제 시그니처 — 플랜은 `VectorRecommendService.cs`·`SlideCaptureService.cs` 기준이며 일치할 가능성이 높으나 구현자가 최종 확인.
- **COM 인덱싱 리스크:** Task10 ④⑤(원본 도형 vs 에셋 도형 구분), Task11 Commit(복제 다수 시 인덱스 밀림) — 실제 PPT로 확정하는 단계 명시함.
