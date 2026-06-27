# 리디자인 Phase 1 — 파일 진입 + 덱 구조 분석 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** [리디자인] 버튼으로 초안 .pptx를 통째로 받아, 디자인 관점으로 덱 구조(표지/본문 N장+라벨/엔드, 총 X장)를 분석해 "이쁜 구조 요약 박스"로 보여준다 (데모 hero ①).

**Architecture:** 사실=COM(슬라이드 텍스트·메트릭), 판단=LLM(역할·라벨). D1 2단 분석 중 **1단(텍스트만, 저가 1회 호출)**만 이 Phase에서 — 외부 .pptx를 창 없이 열어 전 슬라이드를 COM으로 읽고(`DeckFileReader`), 텍스트 요약을 LLM 1회 호출로 슬라이드별 `kind`+`label` 판정(`DeckStructureService`), 결과를 구조 박스로 렌더. 멀티모달 이해는 Phase 3에서 추천 들어가는 본문 장만.

**Tech Stack:** C#/.NET 4.8, Microsoft.Office.Interop.PowerPoint(COM), Newtonsoft.Json, Gemini Flash, WPF(패널), xUnit.

## Global Constraints

- **사실 = COM, 판단 = LLM.** 슬라이드 텍스트·도형 수는 COM 원문(토큰 0). LLM은 kind/label 판정만. 텍스트 내용 생성·수정 금지.
- **디자인-온리.** 구조(흐름)만 분석. 내용을 새로 기획하지 않는다.
- **비파괴.** 초안 파일은 **ReadOnly로 열고**(`Presentations.Open(path, msoTrue, msoFalse, msoFalse)`) 반드시 `Close()` + `Marshal.ReleaseComObject` (finally). 원본 불변. 창 없이(`WithWindow=msoFalse`).
- **COM은 STA(UI) 스레드에서.** 패널에서 호출 시 `ConfigureAwait(false)` 쓰지 않는다(COM 연속 실행을 UI 스레드 유지). 기존 `RunRecommendationAsync` 패턴 그대로.
- **새 `.cs` 파일은 메인 `TeampptAddin.csproj`의 `<Compile Include>`에 수동 등록 필수**(old-style). 테스트 프로젝트는 SDK-style 자동 포함.
- **모든 커밋 메시지 끝에** `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- API 키 평문 금지. 브랜치: `feat/asset-combination-recommendation`. 워크트리 쓰지 않음(빌드 경로 고정).

### 테스트 절차 (Run 명령이 가리키는 것)

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal
dotnet test "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~<클래스명>"
```
`RegisterForComInterop=false`라 관리자 불필요. `dotnet test` 단독은 NuGet 못 풀어 실패 → 반드시 MSBuild 먼저. COM/UI 태스크(2·3)는 단위테스트 대신 **빌드 0 에러 + 수동 검증**.

---

## Task 1: 덱 구조 분석 로직 (models + formatter + schema + parser + service) — TDD

순수 로직 + LLM 호출 래퍼. 파서·포매터·BuildUserText는 단위테스트(TDD). `AnalyzeAsync`만 네트워크(빌드로 검증).

**Files:**
- Create: `src/TeampptAddin/Models/DeckModels.cs`
- Create: `src/TeampptAddin/Services/DeckStructureSchema.cs`
- Create: `src/TeampptAddin/Services/DeckStructureParser.cs`
- Create: `src/TeampptAddin/Services/DeckStructureFormatter.cs`
- Create: `src/TeampptAddin/Services/DeckStructureService.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (5 Compile Include)
- Test: `src/TeampptAddin.Tests/DeckStructureParserTest.cs`, `src/TeampptAddin.Tests/DeckStructureFormatterTest.cs`

**Interfaces:**
- Consumes: `DraftProfile` (has `SlideIndex`, `Shapes` of `DraftShape{Kind,Text}`), `GeminiAiService.GenerateJsonAsync(string system, string userText, string pngPath, JObject schema, int thinkingBudget)` (pngPath null = 텍스트 전용 — DraftUnderstandingService와 동일 오버로드).
- Produces: `DeckSlideStructure{int Index; string Kind; string Label}`, `DeckStructure{List<DeckSlideStructure> Slides; int TotalCount}`; `DeckStructureParser.Parse(string json, int slideCount)`; `DeckStructureFormatter.ToSummaryLines(DeckStructure)`; `DeckStructureService.BuildUserText(List<DraftProfile>)` (static), `DeckStructureService.AnalyzeAsync(List<DraftProfile>)`.

- [ ] **Step 1: 실패 테스트 — parser** `src/TeampptAddin.Tests/DeckStructureParserTest.cs`

```csharp
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckStructureParserTest
    {
        [Fact]
        public void Parses_Slides_Drops_Hallucinated_Index_Sets_Total()
        {
            const string json = @"{
              ""slides"": [
                {""index"":1,""kind"":""cover"",""label"":""표지""},
                {""index"":2,""kind"":""toc"",""label"":""목차""},
                {""index"":3,""kind"":""body"",""label"":""회사소개""},
                {""index"":99,""kind"":""body"",""label"":""환각""}
              ]
            }";
            var d = DeckStructureParser.Parse(json, 3);
            Assert.Equal(3, d.TotalCount);
            Assert.Equal(3, d.Slides.Count);              // index 99 dropped (>slideCount)
            Assert.Equal("목차", d.Slides.Single(s => s.Index == 2).Label);
            Assert.Equal("cover", d.Slides.Single(s => s.Index == 1).Kind);
        }
    }
}
```

- [ ] **Step 2: 실패 테스트 — formatter** `src/TeampptAddin.Tests/DeckStructureFormatterTest.cs`

```csharp
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckStructureFormatterTest
    {
        [Fact]
        public void Groups_Cover_Body_End_With_Total()
        {
            var d = new DeckStructure
            {
                TotalCount = 5,
                Slides = new List<DeckSlideStructure>
                {
                    new DeckSlideStructure { Index = 1, Kind = "cover", Label = "표지" },
                    new DeckSlideStructure { Index = 2, Kind = "toc", Label = "목차" },
                    new DeckSlideStructure { Index = 3, Kind = "body", Label = "회사소개" },
                    new DeckSlideStructure { Index = 4, Kind = "body", Label = "장점 3단" },
                    new DeckSlideStructure { Index = 5, Kind = "end", Label = "마무리" }
                }
            };
            var lines = DeckStructureFormatter.ToSummaryLines(d);
            Assert.Equal("1. 표지", lines[0]);
            Assert.Equal("2. 본문 (3장)", lines[1]);
            Assert.Equal("   - 목차", lines[2]);
            Assert.Equal("   - 회사소개", lines[3]);
            Assert.Equal("   - 장점 3단", lines[4]);
            Assert.Equal("3. 엔드", lines[5]);
            Assert.Equal("총 슬라이드 → 5장", lines[6]);
        }
    }
}
```

- [ ] **Step 3: 실패 확인** — Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~DeckStructure"`. Expected: 컴파일 실패 (타입 미정의).

- [ ] **Step 4: models** `src/TeampptAddin/Models/DeckModels.cs`

```csharp
using System.Collections.Generic;

namespace TeampptAddin
{
    public class DeckSlideStructure
    {
        public int Index { get; set; }        // 1-based 슬라이드 인덱스
        public string Kind { get; set; } = ""; // cover/toc/body/section/end
        public string Label { get; set; } = ""; // 짧은 역할 라벨
    }

    public class DeckStructure
    {
        public List<DeckSlideStructure> Slides { get; set; } = new List<DeckSlideStructure>();
        public int TotalCount { get; set; }
    }
}
```

- [ ] **Step 5: parser** `src/TeampptAddin/Services/DeckStructureParser.cs`

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DeckStructureParser
    {
        public static DeckStructure Parse(string json, int slideCount)
        {
            var d = new DeckStructure { TotalCount = slideCount };
            var o = JObject.Parse(json);
            foreach (var s in (o["slides"] as JArray) ?? new JArray())
            {
                int idx = s["index"]?.Value<int>() ?? -1;
                if (idx < 1 || idx > slideCount) continue;   // 환각 인덱스 제거
                d.Slides.Add(new DeckSlideStructure
                {
                    Index = idx,
                    Kind = s["kind"]?.ToString() ?? "body",
                    Label = s["label"]?.ToString() ?? ""
                });
            }
            return d;
        }
    }
}
```

- [ ] **Step 6: formatter** `src/TeampptAddin/Services/DeckStructureFormatter.cs`

```csharp
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>덱 구조를 구조 요약 박스 라인으로 변환(순수). UI는 이 라인을 렌더만 한다.</summary>
    public static class DeckStructureFormatter
    {
        public static List<string> ToSummaryLines(DeckStructure d)
        {
            var lines = new List<string>();
            if (d == null) return lines;
            var ordered = d.Slides.OrderBy(s => s.Index).ToList();
            var covers = ordered.Where(s => s.Kind == "cover").ToList();
            var ends = ordered.Where(s => s.Kind == "end").ToList();
            var body = ordered.Where(s => s.Kind != "cover" && s.Kind != "end").ToList();

            int n = 1;
            if (covers.Count > 0) lines.Add($"{n++}. 표지");
            if (body.Count > 0)
            {
                lines.Add($"{n++}. 본문 ({body.Count}장)");
                foreach (var s in body)
                    lines.Add($"   - {(string.IsNullOrEmpty(s.Label) ? s.Kind : s.Label)}");
            }
            if (ends.Count > 0) lines.Add($"{n++}. 엔드");
            lines.Add($"총 슬라이드 → {d.TotalCount}장");
            return lines;
        }
    }
}
```

- [ ] **Step 7: schema** `src/TeampptAddin/Services/DeckStructureSchema.cs`

```csharp
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DeckStructureSchema
    {
        public static JObject BuildResponseSchema()
        {
            var slide = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["index"] = new JObject { ["type"] = "integer" },
                    ["kind"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "cover", "toc", "body", "section", "end" } },
                    ["label"] = new JObject { ["type"] = "string" }
                },
                ["required"] = new JArray { "index", "kind", "label" }
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["slides"] = new JObject { ["type"] = "array", ["items"] = slide } },
                ["required"] = new JArray { "slides" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 덱의 '디자인 구조'를 분석하는 엔진이야. 슬라이드별 텍스트 요약 목록(JSON)을 받아, 각 슬라이드의 역할(kind)과 짧은 라벨을 매겨.
## 절대 제약
- 디자인 구조(흐름)만 본다. 내용을 새로 기획하거나 메시지를 바꾸지 마라.
## 네 일
- 각 슬라이드에 kind 부여: cover(표지)/toc(목차)/body(본문)/section(섹션 구분 표지)/end(마무리).
- label: 그 슬라이드 역할을 2~8자 한국어로 (예: 목차, 회사소개, 장점 3단, 팀 소개, 연락처). 표지/엔드는 '표지'/'마무리'.
- index는 입력으로 받은 슬라이드 번호를 그대로 써라.
- 모르면 지어내지 말고 보수적으로 body.";
        }
    }
}
```

- [ ] **Step 8: service** `src/TeampptAddin/Services/DeckStructureService.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 덱 구조 1단 분석: 슬라이드 텍스트 요약(사실)을 Gemini 저가 1회 호출로 kind/label 판정.
    /// 이미지 없이 텍스트만(D1 — 비용이 장수에 폭증 안 하게).
    /// </summary>
    public class DeckStructureService
    {
        private readonly GeminiAiService _gemini;
        public DeckStructureService(GeminiAiService gemini) { _gemini = gemini; }

        public static string BuildUserText(List<DraftProfile> slides)
        {
            var sb = new StringBuilder();
            foreach (var p in slides ?? new List<DraftProfile>())
            {
                var text = string.Join(" / ", p.Shapes.Where(s => s.Kind == "text").Select(s => s.Text))
                    .Replace("\r", " ").Replace("\n", " ").Trim();
                if (text.Length > 160) text = text.Substring(0, 160);
                int imgs = p.Shapes.Count(s => s.Kind == "image");
                sb.AppendLine($"슬라이드 {p.SlideIndex}: \"{text}\" (이미지 {imgs}개, 도형 {p.Shapes.Count}개)");
            }
            return sb.ToString();
        }

        public async Task<DeckStructure> AnalyzeAsync(List<DraftProfile> slides)
        {
            var json = await _gemini.GenerateJsonAsync(
                DeckStructureSchema.BuildSystemPrompt(), BuildUserText(slides), (string)null,
                DeckStructureSchema.BuildResponseSchema(), thinkingBudget: 512).ConfigureAwait(false);
            Logger.Log("[DeckStructure] raw↓ " + json);
            return DeckStructureParser.Parse(json, slides?.Count ?? 0);
        }
    }
}
```

- [ ] **Step 9: csproj** — add 5 includes near other Services/Models (e.g. after `Services\DesignCritiqueService.cs` and `Models\DraftModels.cs`):

```xml
    <Compile Include="Models\DeckModels.cs" />
    <Compile Include="Services\DeckStructureSchema.cs" />
    <Compile Include="Services\DeckStructureParser.cs" />
    <Compile Include="Services\DeckStructureFormatter.cs" />
    <Compile Include="Services\DeckStructureService.cs" />
```

- [ ] **Step 10: 통과 확인** — Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~DeckStructure"`. Expected: PASS (parser + formatter).

- [ ] **Step 11: BuildUserText 테스트** append to `DeckStructureParserTest.cs` (or a new file) — proves text builder includes per-slide line:

```csharp
        [Fact]
        public void BuildUserText_Lists_Each_Slide()
        {
            var slides = new System.Collections.Generic.List<DraftProfile>
            {
                new DraftProfile { SlideIndex = 1, Shapes = { new DraftShape { Kind = "text", Text = "회사 소개" } } },
                new DraftProfile { SlideIndex = 2, Shapes = { new DraftShape { Kind = "image" } } }
            };
            var t = DeckStructureService.BuildUserText(slides);
            Assert.Contains("슬라이드 1:", t);
            Assert.Contains("회사 소개", t);
            Assert.Contains("슬라이드 2:", t);
        }
```
Run focused test → PASS.

- [ ] **Step 12: commit**

```bash
git add src/TeampptAddin/Models/DeckModels.cs src/TeampptAddin/Services/DeckStructure*.cs src/TeampptAddin/TeampptAddin.csproj src/TeampptAddin.Tests/DeckStructure*.cs
git commit -m "feat(redesign): 덱 구조 분석 로직 — models+schema+parser+formatter+service (Phase 1)"
```

---

## Task 2: 외부 .pptx 읽기 (DraftSlideReader 리팩터 + DeckFileReader) — COM, 빌드+수동

`DraftSlideReader`의 슬라이드별 도형 읽기 로직을 공유 메서드로 추출하고, 그걸로 외부 파일 전 슬라이드를 읽는 `DeckFileReader`를 만든다. COM이라 단위테스트 없음 — 빌드 0 에러 + Task 3 UI에서 수동 검증.

**Files:**
- Modify: `src/TeampptAddin/Core/DraftSlideReader.cs` (extract `ReadSlide`)
- Create: `src/TeampptAddin/Core/DeckFileReader.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (1 Compile Include)

**Interfaces:**
- Produces: `static DraftProfile DraftSlideReader.ReadSlide(PowerPoint.Slide slide, float slideW, float slideH)`; `static List<DraftProfile> DeckFileReader.ReadFile(string pptxPath)`.
- `ReadCurrentSlide()` 동작 불변(추출 후 위임).

- [ ] **Step 1: DraftSlideReader 리팩터** — `src/TeampptAddin/Core/DraftSlideReader.cs` 전체 교체 (도형 읽기 로직을 `ReadSlide`로 추출, `ReadCurrentSlide`는 위임; 로직 동일):

```csharp
using System.Collections.Generic;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>
    /// 슬라이드를 COM으로 읽어 DraftProfile(정확한 텍스트·위치·타입·메트릭, shapeId)을 만든다.
    /// 사실의 출처. LLM 판단은 DraftUnderstandingService/DeckStructureService에서.
    /// </summary>
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
            return ReadSlide(slide, pres.PageSetup.SlideWidth, pres.PageSetup.SlideHeight);
        }

        /// <summary>주어진 슬라이드 1장을 DraftProfile로 읽는다(현재슬라이드/파일 공용).</summary>
        public static DraftProfile ReadSlide(PowerPoint.Slide slide, float slideW, float slideH)
        {
            var profile = new DraftProfile
            {
                SlideIndex = slide.SlideIndex,
                SlideWidth = slideW,
                SlideHeight = slideH
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

- [ ] **Step 2: DeckFileReader** `src/TeampptAddin/Core/DeckFileReader.cs` (창 없이 ReadOnly로 열고 전 슬라이드 읽고 닫기 — IngestRunner 패턴):

```csharp
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>
    /// 외부 초안 .pptx를 창 없이 ReadOnly로 열어 전 슬라이드를 DraftProfile[]로 읽는다(비파괴).
    /// 텍스트·메트릭만(D1 1단 — 이미지 렌더 없음). 반드시 Close + Release.
    /// </summary>
    public static class DeckFileReader
    {
        public static List<DraftProfile> ReadFile(string pptxPath)
        {
            var app = Globals.Application;
            var profiles = new List<DraftProfile>();
            if (app == null) { Logger.Log("DeckFileReader: app is null"); return profiles; }
            if (!File.Exists(pptxPath)) { Logger.Log($"DeckFileReader: 파일 없음 {pptxPath}"); return profiles; }

            PowerPoint.Presentation pres = null;
            try
            {
                pres = app.Presentations.Open(
                    pptxPath,
                    MsoTriState.msoTrue,    // ReadOnly
                    MsoTriState.msoFalse,   // Untitled
                    MsoTriState.msoFalse);  // WithWindow = False

                float w = pres.PageSetup.SlideWidth, h = pres.PageSetup.SlideHeight;
                foreach (PowerPoint.Slide slide in pres.Slides)
                    profiles.Add(DraftSlideReader.ReadSlide(slide, w, h));

                pres.Close();
                Logger.Log($"[DeckFileReader] {profiles.Count} slides from {Path.GetFileName(pptxPath)}");
                return profiles;
            }
            finally
            {
                if (pres != null) Marshal.ReleaseComObject(pres);
            }
        }
    }
}
```

- [ ] **Step 3: csproj** — add after `Core\DraftSlideReader.cs` include:

```xml
    <Compile Include="Core\DeckFileReader.cs" />
```

- [ ] **Step 4: 빌드 검증** — Run 빌드. Expected: 0 errors. (단위테스트 없음 — COM. 동작은 Task 3에서 수동 검증.) 확인: `stat -c '%y' src/TeampptAddin/bin/Debug/TeampptAddin.dll` 타임스탬프가 방금인지.

- [ ] **Step 5: commit**

```bash
git add src/TeampptAddin/Core/DraftSlideReader.cs src/TeampptAddin/Core/DeckFileReader.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(redesign): DeckFileReader — 외부 pptx 전 슬라이드 비파괴 읽기 + DraftSlideReader.ReadSlide 추출 (Phase 1)"
```

---

## Task 3: UI — [리디자인] 버튼 + 파일 선택 + 구조 요약 박스 (데모 hero ①) — 빌드+수동

패널에 덱 리디자인 진입을 추가: 새 바 클릭 → OpenFileDialog → 파일 읽기(`DeckFileReader`, STA) → 구조 분석(`DeckStructureService.AnalyzeAsync`) → 구조 요약 박스 렌더.

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (새 바 + 핸들러 + 구조박스, 서비스 구성)

**Interfaces:**
- Consumes: `DeckFileReader.ReadFile` (Task 2), `DeckStructureService.AnalyzeAsync`/`DeckStructureFormatter.ToSummaryLines` (Task 1), 기존 `AddAiBubble`, `_chatStack`, `ThemeResources`, `GeminiAiService`.

- [ ] **Step 1: 서비스 필드 + 구성** — `AssetPanel.cs`에서 `_recommend`(RecommendationService)를 구성하는 위치를 찾아(`grep "new RecommendationService"`), 같은 geminiKey로 옆에 추가:

```csharp
        private DeckStructureService _deckStructure;
```
그리고 `_recommend = new RecommendationService(supabaseUrl, anonKey, geminiKey);` 근처에:
```csharp
        _deckStructure = new DeckStructureService(new GeminiAiService(geminiKey));
```
(geminiKey 변수명이 다르면 그 위치의 실제 키 변수를 쓴다.)

- [ ] **Step 2: 새 진입 바** — `BuildRedesignBar`(약 670행) 패턴을 본떠 `BuildDeckRedesignBar()`를 추가하고, 패널 구성에서 `BuildRedesignBar()`를 추가하는 곳 근처에 이 바도 추가한다. 라벨 "📂 리디자인 (초안 파일)":

```csharp
        private Border BuildDeckRedesignBar()
        {
            var bar = new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(10, 8, 10, 0),
                Padding = new Thickness(12, 8, 12, 8),
                Cursor = Cursors.Hand
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(new TextBlock { Text = "📂", FontSize = 13, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock
            {
                Text = "리디자인 (초안 파일)", FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                VerticalAlignment = VerticalAlignment.Center
            });
            bar.Child = row;
            bar.MouseLeftButtonUp += async (s, e) => await RunDeckRedesignAsync();
            return bar;
        }
```

- [ ] **Step 3: 핸들러 (파일선택 → 읽기 → 분석 → 박스)** — STA에서 COM 호출(ConfigureAwait 미사용):

```csharp
        private bool _deckRunning;
        private async Task RunDeckRedesignAsync()
        {
            if (_deckRunning) return;
            if (_deckStructure == null) { AddAiBubble("리디자인은 Gemini 설정이 있어야 동작해요."); return; }

            var dlg = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "PowerPoint 초안 (*.pptx)|*.pptx",
                Title = "리디자인할 초안 파일 선택"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            _deckRunning = true;
            if (_emptyState != null && _emptyState.Visibility == Visibility.Visible)
                _emptyState.Visibility = Visibility.Collapsed;
            try
            {
                AddAiBubble("초안을 읽고 있어요…");
                var profiles = DeckFileReader.ReadFile(dlg.FileName);   // COM, STA
                if (profiles.Count == 0) { AddAiBubble("슬라이드를 읽지 못했어요. 파일을 확인해주세요."); return; }

                AddAiBubble($"{profiles.Count}장을 분석하고 있어요…");
                var structure = await _deckStructure.AnalyzeAsync(profiles);   // STA 유지
                _chatStack.Children.Add(BuildStructureBox(structure));
                _chatScroll.ScrollToBottom();
            }
            catch (Exception ex)
            {
                AddAiBubble($"구조 분석 중 오류: {ex.Message}");
                Logger.Log($"[DeckRedesign] 실패: {ex}");
            }
            finally { _deckRunning = false; }
        }
```

- [ ] **Step 4: 구조 요약 박스** — `DeckStructureFormatter.ToSummaryLines`를 세로 박스로 렌더:

```csharp
        private Border BuildStructureBox(DeckStructure structure)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "초안 구조", FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 6)
            });
            foreach (var line in DeckStructureFormatter.ToSummaryLines(structure))
                panel.Children.Add(new TextBlock
                {
                    Text = line, FontSize = 12, FontFamily = ThemeResources.FontBase,
                    Foreground = ThemeResources.TextPrimary, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 1, 0, 1)
                });
            return new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(10, 8, 10, 0),
                Padding = new Thickness(14, 10, 14, 10),
                Child = panel
            };
        }
```
> `ThemeResources.TextPrimary`가 없으면 기존 박스가 쓰는 본문 색 리소스명으로 맞춘다(`BuildTracePanel`/`AddAiBubble` 참고).

- [ ] **Step 5: 빌드 검증** — Run 빌드. Expected: 0 errors. DLL 타임스탬프 갱신 확인.

- [ ] **Step 6: 수동 검증 (사용자/데모 hero ①)** — PowerPoint 재시작 → 빈 PPT에서 AI 패널 → "📂 리디자인 (초안 파일)" → 초안 .pptx 선택 → 구조 요약 박스에 `표지 / 본문 N장(라벨들) / 엔드 / 총 X장`이 뜨는지. 원본 파일은 안 바뀌어야 함(ReadOnly).

- [ ] **Step 7: commit**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs
git commit -m "feat(redesign): [리디자인] 파일 진입 + 구조 요약 박스 UI (Phase 1, 데모 hero ①)"
```

---

## Self-Review

**1. Spec coverage (Phase 1 = Step 1·2):** 파일 진입(Task 3 OpenFileDialog) ✅, 외부 파일 비파괴 읽기(Task 2 DeckFileReader, ReadOnly+Close) ✅, 덱 구조 분석 D1 텍스트 1단(Task 1 DeckStructureService 텍스트전용 1회) ✅, 구조 요약 박스(Task 1 formatter + Task 3 박스) ✅. 멀티모달 2단·컨셉·추천은 Phase 2~3(범위 밖) ✅.

**2. Placeholder scan:** Task 1 전부 실제 코드+테스트. Task 2 COM 코드는 검증된 IngestRunner/ThumbnailGenerator Open 패턴 그대로. Task 3 UI는 BuildRedesignBar/standard OpenFileDialog 패턴; `_emptyState`/`_chatStack`/`_chatScroll`/`ThemeResources` 이름은 기존 패널의 것 — 구현자가 실제 이름 확인(특히 `ThemeResources.TextPrimary` 폴백 명시).

**3. Type consistency:** `DeckStructure`/`DeckSlideStructure` (Task 1) → formatter/parser/service/UI 일관. `DeckStructureService.AnalyzeAsync(List<DraftProfile>)` (Task 1) ← `DeckFileReader.ReadFile`→`List<DraftProfile>` (Task 2) → Task 3 호출 일치. `DraftSlideReader.ReadSlide(slide,w,h)` (Task 2) ← DeckFileReader 사용 일치. `GenerateJsonAsync(...,(string)null,...,thinkingBudget:)` 오버로드는 DraftUnderstandingService와 동일.

**4. Ambiguity:** D1 1단=텍스트전용 명시. 구조 박스 라인 포맷은 formatter 테스트로 못박음. 진입 = 기존 단일슬라이드 "AI 리디자인" 바와 **별도** 새 바(스펙의 "[리디자인] 버튼" 해석).

## 알려진 리스크 (구현자 주의)

- **STA/COM:** `DeckFileReader.ReadFile`은 UI 스레드에서 호출(핸들러가 await 전 동기 호출). `AnalyzeAsync`만 비동기 — 그 await에 ConfigureAwait(false) 금지(이후 `_chatStack` 접근이 UI 스레드여야).
- **패널 멤버명:** `_emptyState`, `_chatStack`, `_chatScroll`, `AddAiBubble`, `ThemeResources.*`는 기존 패널에 존재(추천 흐름이 사용). Task 3 구현자는 실제 이름·색 리소스를 grep로 확인 후 맞출 것.
- **키 변수:** `_deckStructure` 구성 시 geminiKey는 `_recommend` 구성 지점의 실제 키 변수를 재사용.

## Execution Handoff

플랜 완료. 저장: `docs/superpowers/plans/2026-06-25-redesign-phase1-file-entry-deck-structure.md`.
실행: subagent-driven-development — 구현 sonnet, 리뷰 diff 크기별(haiku/sonnet), 최종 통합 리뷰 opus. Task 1은 TDD, Task 2·3은 빌드+수동(데모 hero ① 확인).
