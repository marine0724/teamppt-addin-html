# 리디자인 Phase 2 — 컨설팅 컨셉 (질문 → 컨셉 3블록 생성·선택) 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 1 구조 박스 다음에 "어디에 쓰나 / 어떤 느낌" 칩 질문을 받아, 구조 요약 + 답을 근거로 LLM이 서로 구별되는 디자인 컨셉 3개를 생성하고, 사용자가 카드로 골라 선택을 저장·확정(배너)하는 데모 다리.

**Architecture:** 사실=COM/구조(Phase 1 산출 `DeckStructure`), 판단=LLM(컨셉 생성). 새 `ConceptSuggester`가 구조요약+용도+느낌을 Gemini 텍스트 전용 **저가 1회** 호출로 `DesignConcept[3]`로 변환(기존 `DesignConcept` 모델·`ConceptSuggesterParser` 사용). 패널은 구조 박스 → 칩 질문 카드 → 컨셉 3카드 → 선택 시 `_selectedConcept` 저장 + 확정 배너를 렌더. **실제 적용**(검색 styleTags 가중 / `ConceptResolver` 색·폰트 override)은 소비자인 Phase 3 추천 오케스트레이터가 생길 때 붙는다 — 이 Phase는 **생성·선택·저장**까지(저장 + 확정 배너).

**Tech Stack:** C#/.NET 4.8, Microsoft.Office.Interop.PowerPoint(COM, 이번 Phase는 신규 COM 없음), Newtonsoft.Json, Gemini Flash, WPF(패널), xUnit.

## Global Constraints

- **사실 = COM/벡터, 판단 = LLM.** 컨셉은 역할(role)·스타일·색·폰트만. **텍스트 내용 생성·수정 금지**(컨셉 name/styleTags는 디자인 메타, 본문 텍스트는 손대지 않음).
- **디자인-온리.** 구조·스타일만. 내용 기획 안 함. **비파괴**(원본 초안 불변 — 이 Phase는 파일을 쓰지도 않음).
- **두 토큰 예산.** 컨셉 생성은 텍스트 전용 1회(이미지 없음, 장수에 비례 X). `GenerateJsonAsync(..., pngPathOrNull: (string)null, ...)`.
- **COM은 STA(UI) 스레드.** 패널 핸들러에서 `await _conceptSuggester.SuggestAsync(...)` 호출부에 `ConfigureAwait(false)` **금지**(이후 `_chatStack` 접근이 UI 스레드여야). 서비스 내부 await는 DeckStructureService와 동일하게 `ConfigureAwait(false)` 사용(이후 COM 없음, 순수 파싱만).
- **새 `.cs`는 메인 `TeampptAddin.csproj`의 `<Compile Include>`에 수동 등록 필수**(old-style csproj). 테스트 프로젝트는 SDK-style 자동 포함.
- **CoordinateConverter 폴백 로직 추가 금지**(이 Phase는 무관하나 원칙 유지). API 키 평문 금지.
- 브랜치: `feat/asset-combination-recommendation`. **워크트리 쓰지 않음**(빌드 경로 `c:\Projects\teamppt-addin` 고정 — 다른 경로면 PPT 수동검증이 안 맞음).
- 모든 커밋 메시지 끝에: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

### 테스트 절차 (Run 명령이 가리키는 것)

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal
dotnet test "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~<클래스명>"
```
`RegisterForComInterop=false`라 관리자 불필요. `dotnet test` 단독은 NuGet 못 풀어 실패 → **반드시 MSBuild 먼저**. UI 태스크(Task 2)는 단위테스트 대신 **빌드 0 에러 + 수동 검증**. 빌드는 **foreground**(run_in_background 금지).

## File Structure

| 파일 | 책임 | 신규/수정 |
|---|---|---|
| `src/TeampptAddin/Services/ConceptSuggesterSchema.cs` | LLM 시스템 프롬프트 + 응답 JSON 스키마(컨셉 3개, colors/fonts는 role 배열) | 신규 |
| `src/TeampptAddin/Services/ConceptSuggesterParser.cs` | LLM JSON → `List<DesignConcept>`(id 부여, role 배열→Dictionary, 빈 컨셉 드롭) | 신규(순수, TDD) |
| `src/TeampptAddin/Services/ConceptSuggester.cs` | `BuildUserText`(구조+용도+느낌, static, TDD) + `SuggestAsync`(LLM 1회) | 신규 |
| `src/TeampptAddin/Models/DesignConcept.cs` | 컨셉 모델(name/styleTags/colors/fonts) | 재사용(변경 없음) |
| `src/TeampptAddin/Services/ConceptResolver.cs` | unlocked 색/폰트 override | 재사용(Phase 3에서 소비, 이번엔 변경 없음) |
| `src/TeampptAddin/Services/DeckStructureFormatter.cs` | 구조 요약 라인(ConceptSuggester 입력에 재사용) | 재사용(변경 없음) |
| `src/TeampptAddin/UI/Wpf/AssetPanel.cs` | 칩 질문 카드 + 컨셉 3카드 + 선택·저장·확정 배너 + 서비스 필드 | 수정 |
| `src/TeampptAddin/UI/TaskPaneHost.cs` | `ConceptSuggester` 구성 + `InitAi`로 주입 | 수정 |
| `src/TeampptAddin/TeampptAddin.csproj` | 3 Compile Include | 수정 |
| `src/TeampptAddin.Tests/ConceptSuggesterParserTest.cs` | 파서 TDD | 신규 |
| `src/TeampptAddin.Tests/ConceptSuggesterUserTextTest.cs` | BuildUserText TDD | 신규 |

---

## Task 1: ConceptSuggester 로직 (schema + parser + service) — TDD

순수 로직(parser, BuildUserText)은 단위테스트(TDD). `SuggestAsync`만 네트워크(빌드로 검증). 기존 `DesignConcept` 모델 재사용 — 새 모델 없음.

**Files:**
- Create: `src/TeampptAddin/Services/ConceptSuggesterSchema.cs`
- Create: `src/TeampptAddin/Services/ConceptSuggesterParser.cs`
- Create: `src/TeampptAddin/Services/ConceptSuggester.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (3 Compile Include)
- Test: `src/TeampptAddin.Tests/ConceptSuggesterParserTest.cs`, `src/TeampptAddin.Tests/ConceptSuggesterUserTextTest.cs`

**Interfaces:**
- Consumes:
  - `DesignConcept { string Id; string Name; List<string> StyleTags; Dictionary<string,string> Colors; Dictionary<string,string> Fonts }` (기존, `Models/DesignConcept.cs`).
  - `DeckStructure { List<DeckSlideStructure> Slides; int TotalCount }`, `DeckStructureFormatter.ToSummaryLines(DeckStructure)` (Phase 1, 기존).
  - `GeminiAiService.GenerateJsonAsync(string systemPrompt, string userText, string pngPathOrNull, JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)` (기존, `Services/GeminiAiService.cs:214`).
- Produces:
  - `static List<DesignConcept> ConceptSuggesterParser.Parse(string json)`
  - `static string ConceptSuggester.BuildUserText(DeckStructure structure, string usage, string feeling)`
  - `ConceptSuggester(GeminiAiService gemini)` 생성자, `Task<List<DesignConcept>> ConceptSuggester.SuggestAsync(DeckStructure structure, string usage, string feeling)`
  - `static JObject ConceptSuggesterSchema.BuildResponseSchema()`, `static string ConceptSuggesterSchema.BuildSystemPrompt()`

- [ ] **Step 1: 실패 테스트 — parser** `src/TeampptAddin.Tests/ConceptSuggesterParserTest.cs`

```csharp
using Xunit;

namespace TeampptAddin.Tests
{
    public class ConceptSuggesterParserTest
    {
        [Fact]
        public void Parses_Three_Concepts_With_Ids_Tags_Colors_Fonts()
        {
            const string json = @"{
              ""concepts"": [
                { ""name"":""신뢰 블루"", ""styleTags"":[""trust"",""corporate""],
                  ""colors"":[{""role"":""main"",""value"":""#1D4ED8""},{""role"":""text"",""value"":""#0F172A""}],
                  ""fonts"":[{""role"":""heading"",""family"":""Pretendard""}] },
                { ""name"":""모던 미니멀"", ""styleTags"":[""minimal""],
                  ""colors"":[{""role"":""main"",""value"":""#111827""}],
                  ""fonts"":[{""role"":""heading"",""family"":""Noto Sans KR""}] },
                { ""name"":""웜 그레이"", ""styleTags"":[""warm""],
                  ""colors"":[{""role"":""main"",""value"":""#92400E""}],
                  ""fonts"":[{""role"":""heading"",""family"":""Gowun Dodum""}] }
              ]
            }";
            var list = ConceptSuggesterParser.Parse(json);
            Assert.Equal(3, list.Count);
            Assert.Equal("c1", list[0].Id);
            Assert.Equal("c3", list[2].Id);
            Assert.Equal("신뢰 블루", list[0].Name);
            Assert.Contains("trust", list[0].StyleTags);
            Assert.Equal("#1D4ED8", list[0].Colors["main"]);
            Assert.Equal("#0F172A", list[0].Colors["text"]);
            Assert.Equal("Pretendard", list[0].Fonts["heading"]);
        }

        [Fact]
        public void Drops_Concept_With_Blank_Name_And_Reassigns_Ids()
        {
            const string json = @"{
              ""concepts"": [
                { ""name"":""있음"", ""styleTags"":[], ""colors"":[], ""fonts"":[] },
                { ""name"":"""",   ""styleTags"":[], ""colors"":[], ""fonts"":[] }
              ]
            }";
            var list = ConceptSuggesterParser.Parse(json);
            Assert.Single(list);
            Assert.Equal("있음", list[0].Name);
            Assert.Equal("c1", list[0].Id);
        }
    }
}
```

- [ ] **Step 2: 실패 테스트 — BuildUserText** `src/TeampptAddin.Tests/ConceptSuggesterUserTextTest.cs`

```csharp
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class ConceptSuggesterUserTextTest
    {
        [Fact]
        public void BuildUserText_Includes_Usage_Feeling_And_Structure_Label()
        {
            var structure = new DeckStructure
            {
                TotalCount = 3,
                Slides = new List<DeckSlideStructure>
                {
                    new DeckSlideStructure { Index = 1, Kind = "cover", Label = "표지" },
                    new DeckSlideStructure { Index = 2, Kind = "body",  Label = "회사소개" },
                    new DeckSlideStructure { Index = 3, Kind = "end",   Label = "마무리" }
                }
            };
            var t = ConceptSuggester.BuildUserText(structure, "투자유치", "신뢰감");
            Assert.Contains("투자유치", t);
            Assert.Contains("신뢰감", t);
            Assert.Contains("회사소개", t);   // 구조 라벨이 입력으로 흘러들어감(DeckStructureFormatter 경유)
        }
    }
}
```

- [ ] **Step 3: 실패 확인** — Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~ConceptSuggester"`. Expected: 컴파일 실패(타입 미정의).

- [ ] **Step 4: parser** `src/TeampptAddin/Services/ConceptSuggesterParser.cs`

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>LLM JSON → DesignConcept 리스트(순수). 빈 name 컨셉은 드롭, 생존 순서로 id(c1,c2,...) 부여.</summary>
    public static class ConceptSuggesterParser
    {
        public static List<DesignConcept> Parse(string json)
        {
            var result = new List<DesignConcept>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            var o = JObject.Parse(json);
            int n = 1;
            foreach (var c in (o["concepts"] as JArray) ?? new JArray())
            {
                var name = c["name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;   // 환각/빈 컨셉 드롭

                var concept = new DesignConcept
                {
                    Id = "c" + n,
                    Name = name.Trim(),
                    StyleTags = new List<string>(),
                    Colors = new Dictionary<string, string>(),
                    Fonts = new Dictionary<string, string>()
                };

                foreach (var t in (c["styleTags"] as JArray) ?? new JArray())
                {
                    var tag = t?.ToString();
                    if (!string.IsNullOrWhiteSpace(tag)) concept.StyleTags.Add(tag.Trim());
                }
                foreach (var col in (c["colors"] as JArray) ?? new JArray())
                {
                    var role = col["role"]?.ToString();
                    var val = col["value"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(val)
                        && !concept.Colors.ContainsKey(role)) concept.Colors[role] = val.Trim();
                }
                foreach (var fo in (c["fonts"] as JArray) ?? new JArray())
                {
                    var role = fo["role"]?.ToString();
                    var fam = fo["family"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(fam)
                        && !concept.Fonts.ContainsKey(role)) concept.Fonts[role] = fam.Trim();
                }

                result.Add(concept);
                n++;
            }
            return result;
        }
    }
}
```

- [ ] **Step 5: schema** `src/TeampptAddin/Services/ConceptSuggesterSchema.cs`

```csharp
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class ConceptSuggesterSchema
    {
        public static JObject BuildResponseSchema()
        {
            var roleColor = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["role"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "main", "accent", "text", "bg" } },
                    ["value"] = new JObject { ["type"] = "string" }   // #RRGGBB
                },
                ["required"] = new JArray { "role", "value" }
            };
            var roleFont = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["role"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "heading", "body" } },
                    ["family"] = new JObject { ["type"] = "string" }
                },
                ["required"] = new JArray { "role", "family" }
            };
            var concept = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["name"] = new JObject { ["type"] = "string" },
                    ["styleTags"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } },
                    ["colors"] = new JObject { ["type"] = "array", ["items"] = roleColor },
                    ["fonts"] = new JObject { ["type"] = "array", ["items"] = roleFont }
                },
                ["required"] = new JArray { "name", "styleTags", "colors", "fonts" }
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["concepts"] = new JObject { ["type"] = "array", ["items"] = concept } },
                ["required"] = new JArray { "concepts" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 덱 '디자인 컨셉'을 제안하는 컨설턴트야. 덱 구조 요약 + 사용 용도 + 원하는 느낌을 받아, 서로 뚜렷이 구별되는 디자인 방향 3개를 제안해.
## 절대 제약
- 디자인(색·폰트·무드)만 제안한다. 내용을 새로 기획하거나 텍스트를 바꾸지 마라.
## 출력 규칙
- 정확히 3개. 셋은 같은 용도·느낌을 만족하되 팔레트·타이포 personality가 서로 달라야 한다(예: 진중 블루 / 모던 미니멀 / 웜 톤).
- name: 컨셉 이름 2~8자 한국어.
- styleTags: 영문 소문자 키워드 2~4개(나중 검색 가중용. 예: trust, corporate, minimal, warm, bold).
- colors: 역할별 HEX. role은 main/accent/text/bg 중에서. 최소 main·text 포함. value는 #RRGGBB.
- fonts: 역할별 글꼴. role은 heading/body. 최소 heading 포함. 한글 지원 글꼴 위주(예: Pretendard, Noto Sans KR, Gowun Dodum).
- 가독성 유지(text는 어둡게, bg는 밝게).";
        }
    }
}
```

- [ ] **Step 6: service** `src/TeampptAddin/Services/ConceptSuggester.cs`

```csharp
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 덱 구조 요약 + 용도 + 느낌을 Gemini 저가 텍스트 1회 호출로 받아
    /// 서로 구별되는 DesignConcept 3개를 생성한다(디자인-온리, 텍스트 내용 불변).
    /// 적용(검색 가중·색/폰트 override)은 소비자(Phase 3)가 담당.
    /// </summary>
    public class ConceptSuggester
    {
        private readonly GeminiAiService _gemini;
        public ConceptSuggester(GeminiAiService gemini) { _gemini = gemini; }

        public static string BuildUserText(DeckStructure structure, string usage, string feeling)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"용도: {usage}");
            sb.AppendLine($"원하는 느낌: {feeling}");
            sb.AppendLine("[덱 구조]");
            foreach (var line in DeckStructureFormatter.ToSummaryLines(structure))
                sb.AppendLine(line);
            return sb.ToString();
        }

        public async Task<List<DesignConcept>> SuggestAsync(DeckStructure structure, string usage, string feeling)
        {
            var json = await _gemini.GenerateJsonAsync(
                ConceptSuggesterSchema.BuildSystemPrompt(),
                BuildUserText(structure, usage, feeling),
                (string)null,
                ConceptSuggesterSchema.BuildResponseSchema(),
                temperature: 0.7,        // 3안 간 다양성 위해 약간 높임
                thinkingBudget: 512).ConfigureAwait(false);
            Logger.Log("[ConceptSuggester] raw↓ " + json);
            return ConceptSuggesterParser.Parse(json);
        }
    }
}
```

- [ ] **Step 7: csproj** — `Services\DeckStructureService.cs` Include 근처에 3줄 추가:

```xml
    <Compile Include="Services\ConceptSuggesterSchema.cs" />
    <Compile Include="Services\ConceptSuggesterParser.cs" />
    <Compile Include="Services\ConceptSuggester.cs" />
```

- [ ] **Step 8: 통과 확인** — Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal
dotnet test "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~ConceptSuggester"
```
Expected: PASS (parser 2 + usertext 1 = 3 tests green). 빌드 로그 0 에러.

- [ ] **Step 9: commit**

```bash
git add src/TeampptAddin/Services/ConceptSuggester*.cs src/TeampptAddin/TeampptAddin.csproj src/TeampptAddin.Tests/ConceptSuggester*.cs
git commit -m "feat(redesign): ConceptSuggester — 구조+용도+느낌→컨셉 3개 생성 로직 (Phase 2)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: UI — 칩 질문 → 컨셉 3카드 → 선택·저장·확정 배너 (데모 다리) — 빌드+수동

패널의 `RunDeckRedesignAsync`를 연장: 구조 박스 뒤에 칩 질문 카드 → "컨셉 만들기" → `ConceptSuggester.SuggestAsync` → 컨셉 3카드 → 카드 클릭 시 `_selectedConcept` 저장 + 확정 배너. COM 신규 없음 — 빌드 0 에러 + PPT 수동 검증.

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (필드 + RunDeckRedesignAsync 연장 + UI 빌더 + InitAi 주입)
- Modify: `src/TeampptAddin/UI/TaskPaneHost.cs` (ConceptSuggester 구성 + InitAi 인자)

**Interfaces:**
- Consumes: `ConceptSuggester.SuggestAsync(DeckStructure, string usage, string feeling)` (Task 1), 기존 `DeckStructure`(Phase 1), `AddAiBubble(string)`, `_chatStack`(Panel), `_chatScroll`(ScrollViewer), `ThemeResources.*`, `BrushFromHex(string)`(기존 `AssetPanel.cs:2861`).
- Produces: 새 패널 멤버 `_conceptSuggester`, `_lastStructure`, `_selectedConcept`(Phase 3가 읽어갈 선택 결과).

> **주의(구현자):** 아래 멤버명은 기존 패널의 것 — 시작 전 grep로 실재 확인: `_chatStack`, `_chatScroll`, `AddAiBubble`, `ThemeResources.{Accent,AccentBorder,BgCard,BgCardHover,BgChip,BgBadge,BgCategoryActive,BorderCard,TextMain,TextSub,TextAccent,FontBase}`, `BrushFromHex`. WPF using(System.Windows.Controls / .Media: `WrapPanel`, `StackPanel`, `Border`, `TextBlock`, `Brushes`, `SolidColorBrush`, `Cursors`)은 이미 파일 상단에 존재(패널 전체가 WPF). 없으면 추가.

- [ ] **Step 1: 필드 추가** — `AssetPanel.cs`에서 `private DeckStructureService _deckStructure;`(약 59행) 아래에:

```csharp
        private ConceptSuggester _conceptSuggester;
        private DeckStructure _lastStructure;
        private string _selFeeling;
        private string _selUsage;
        private DesignConcept _selectedConcept;
        private bool _conceptRunning;
```

- [ ] **Step 2: InitAi에 주입** — `InitAi(...)` 시그니처(약 2935행) 끝에 파라미터 추가하고 본문에서 대입:

기존:
```csharp
        public void InitAi(IAiService aiService, StyleConfig styles, RemoteAssetCache remoteCache = null, RedesignService redesign = null, RecommendationService recommend = null, DeckStructureService deckStructure = null)
        {
            _aiService = aiService;
            _styleConfig = styles;
            _remoteCache = remoteCache;
            _redesign = redesign;
            _recommend = recommend;
            _deckStructure = deckStructure;
            PopulateStylePanel();
        }
```
변경:
```csharp
        public void InitAi(IAiService aiService, StyleConfig styles, RemoteAssetCache remoteCache = null, RedesignService redesign = null, RecommendationService recommend = null, DeckStructureService deckStructure = null, ConceptSuggester conceptSuggester = null)
        {
            _aiService = aiService;
            _styleConfig = styles;
            _remoteCache = remoteCache;
            _redesign = redesign;
            _recommend = recommend;
            _deckStructure = deckStructure;
            _conceptSuggester = conceptSuggester;
            PopulateStylePanel();
        }
```

- [ ] **Step 3: RunDeckRedesignAsync 연장** — 구조 박스 렌더 직후 상태 리셋 + 질문 카드 추가. 기존(약 752~754행):

```csharp
                var structure = await _deckStructure.AnalyzeAsync(profiles);   // STA 유지 — ConfigureAwait(false) 금지
                _chatStack.Children.Add(BuildStructureBox(structure));
                _chatScroll.ScrollToBottom();
```
변경:
```csharp
                var structure = await _deckStructure.AnalyzeAsync(profiles);   // STA 유지 — ConfigureAwait(false) 금지
                _chatStack.Children.Add(BuildStructureBox(structure));
                _lastStructure = structure;
                _selFeeling = null; _selUsage = null; _selectedConcept = null;
                if (_conceptSuggester != null)
                    _chatStack.Children.Add(BuildConceptQuestionCard());
                _chatScroll.ScrollToBottom();
```

- [ ] **Step 4: 질문 카드 + 칩 빌더** — `BuildStructureBox`(약 788행) 뒤에 추가:

```csharp
        private static readonly string[] FeelingChips = { "신뢰감", "혁신적", "미니멀", "활기찬", "따뜻한", "고급스러운" };
        private static readonly string[] UsageChips = { "투자유치(IR)", "회사소개", "제품소개", "사내보고", "교육·강의" };

        private Border BuildConceptQuestionCard()
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "어떤 방향으로 만들까요?", FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 8)
            });

            panel.Children.Add(ConceptSectionLabel("느낌"));
            var feelingWrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 8) };
            var feelingChips = new List<Border>();
            foreach (var f in FeelingChips)
            {
                var captured = f;
                var chip = BuildConceptChip(f);
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    _selFeeling = captured;
                    foreach (var c in feelingChips) StyleConceptChip(c, c == chip);
                };
                feelingChips.Add(chip);
                feelingWrap.Children.Add(chip);
            }
            panel.Children.Add(feelingWrap);

            panel.Children.Add(ConceptSectionLabel("용도"));
            var usageWrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 8) };
            var usageChips = new List<Border>();
            foreach (var u in UsageChips)
            {
                var captured = u;
                var chip = BuildConceptChip(u);
                chip.MouseLeftButtonUp += (s, e) =>
                {
                    _selUsage = captured;
                    foreach (var c in usageChips) StyleConceptChip(c, c == chip);
                };
                usageChips.Add(chip);
                usageWrap.Children.Add(chip);
            }
            panel.Children.Add(usageWrap);

            var makeBtn = new Border
            {
                Background = ThemeResources.Accent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 7, 12, 7),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 0),
                Child = new TextBlock
                {
                    Text = "컨셉 만들기", FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White, FontFamily = ThemeResources.FontBase
                }
            };
            makeBtn.MouseLeftButtonUp += async (s, e) => await RunConceptSuggestAsync();
            panel.Children.Add(makeBtn);

            return WrapConceptCard(panel);
        }

        private TextBlock ConceptSectionLabel(string t) => new TextBlock
        {
            Text = t, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = ThemeResources.TextSub, FontFamily = ThemeResources.FontBase,
            Margin = new Thickness(0, 2, 0, 2)
        };

        private Border BuildConceptChip(string text)
        {
            return new Border
            {
                Background = ThemeResources.BgChip,
                BorderBrush = ThemeResources.BorderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(11, 5, 11, 5),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = text, FontSize = 12, FontFamily = ThemeResources.FontBase,
                    Foreground = ThemeResources.TextMain
                }
            };
        }

        private void StyleConceptChip(Border chip, bool selected)
        {
            chip.Background = selected ? ThemeResources.BgCategoryActive : ThemeResources.BgChip;
            chip.BorderBrush = selected ? ThemeResources.Accent : ThemeResources.BorderCard;
            var tb = (TextBlock)chip.Child;
            tb.Foreground = selected ? ThemeResources.TextAccent : ThemeResources.TextMain;
            tb.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private Border WrapConceptCard(UIElement child) => new Border
        {
            Background = ThemeResources.BgCard,
            BorderBrush = ThemeResources.BorderCard,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(10, 8, 10, 0),
            Padding = new Thickness(14, 12, 14, 12),
            Child = child
        };

        private static SolidColorBrush ConceptSwatch(string hex)
        {
            try { return BrushFromHex(hex); }
            catch { return BrushFromHex("#CCCCCC"); }   // LLM 불량 HEX 가드(기존 BrushFromHex 재사용)
        }
```

- [ ] **Step 5: 컨셉 생성 핸들러** — Step 4 코드 뒤에 추가(STA 유지 — 호출부 `ConfigureAwait(false)` 금지):

```csharp
        private async Task RunConceptSuggestAsync()
        {
            if (_conceptRunning) return;
            if (_conceptSuggester == null || _lastStructure == null)
            {
                AddAiBubble("컨셉 추천은 Gemini 설정과 구조 분석이 먼저 필요해요.");
                return;
            }
            if (string.IsNullOrEmpty(_selFeeling) || string.IsNullOrEmpty(_selUsage))
            {
                AddAiBubble("느낌과 용도를 하나씩 골라주세요.");
                return;
            }

            _conceptRunning = true;
            try
            {
                AddAiBubble($"'{_selUsage} · {_selFeeling}' 방향으로 컨셉 3개를 만들고 있어요…");
                var concepts = await _conceptSuggester.SuggestAsync(_lastStructure, _selUsage, _selFeeling); // STA 유지
                if (concepts.Count == 0) { AddAiBubble("컨셉을 만들지 못했어요. 다시 시도해주세요."); return; }
                _chatStack.Children.Add(BuildConceptCards(concepts));
                _chatScroll.ScrollToBottom();
            }
            catch (Exception ex)
            {
                AddAiBubble($"컨셉 생성 중 오류: {ex.Message}");
                Logger.Log($"[ConceptSuggest] 실패: {ex}");
            }
            finally { _conceptRunning = false; }
        }
```

- [ ] **Step 6: 컨셉 카드 + 선택 + 확정 배너** — Step 5 뒤에 추가:

```csharp
        private Border BuildConceptCards(List<DesignConcept> concepts)
        {
            var outer = new StackPanel();
            outer.Children.Add(new TextBlock
            {
                Text = "컨셉 3안 — 하나를 고르세요", FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var cardRefs = new List<Border>();
            foreach (var concept in concepts)
            {
                var captured = concept;
                var card = BuildOneConceptCard(concept);
                card.MouseLeftButtonUp += (s, e) => OnConceptSelected(captured, card, cardRefs);
                cardRefs.Add(card);
                outer.Children.Add(card);
            }
            return WrapConceptCard(outer);
        }

        private Border BuildOneConceptCard(DesignConcept concept)
        {
            var p = new StackPanel();
            p.Children.Add(new TextBlock
            {
                Text = concept.Name, FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.TextMain, FontFamily = ThemeResources.FontBase
            });

            if (concept.StyleTags != null && concept.StyleTags.Count > 0)
            {
                var tags = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
                foreach (var t in concept.StyleTags)
                    tags.Children.Add(new Border
                    {
                        Background = ThemeResources.BgBadge, CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 4, 4),
                        Child = new TextBlock { Text = t, FontSize = 10, Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase }
                    });
                p.Children.Add(tags);
            }

            var sw = new WrapPanel { Margin = new Thickness(0, 2, 0, 4) };
            if (concept.Colors != null)
                foreach (var kv in concept.Colors)
                    sw.Children.Add(new Border
                    {
                        Width = 22, Height = 22, CornerRadius = new CornerRadius(5),
                        Margin = new Thickness(0, 0, 5, 0),
                        BorderBrush = ThemeResources.BorderCard, BorderThickness = new Thickness(1),
                        Background = ConceptSwatch(kv.Value)
                    });
            p.Children.Add(sw);

            if (concept.Fonts != null && concept.Fonts.Count > 0)
                p.Children.Add(new TextBlock
                {
                    Text = "글꼴 · " + string.Join(" / ", concept.Fonts.Values),
                    FontSize = 11, Foreground = ThemeResources.TextSub, FontFamily = ThemeResources.FontBase,
                    Margin = new Thickness(0, 2, 0, 0)
                });

            return new Border
            {
                Background = ThemeResources.BgCard,
                BorderBrush = ThemeResources.BorderCard,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 7),
                Cursor = Cursors.Hand,
                Child = p
            };
        }

        private void OnConceptSelected(DesignConcept concept, Border selectedCard, List<Border> allCards)
        {
            _selectedConcept = concept;
            foreach (var c in allCards)
            {
                bool sel = c == selectedCard;
                c.BorderBrush = sel ? ThemeResources.Accent : ThemeResources.BorderCard;
                c.BorderThickness = new Thickness(sel ? 2 : 1);
                c.Background = sel ? ThemeResources.BgCardHover : ThemeResources.BgCard;
            }
            _chatStack.Children.Add(BuildConceptConfirmBanner(concept));
            _chatScroll.ScrollToBottom();
            Logger.Log($"[ConceptSuggest] 선택: {concept.Id} {concept.Name}");
        }

        private Border BuildConceptConfirmBanner(DesignConcept concept)
        {
            var p = new StackPanel();
            p.Children.Add(new TextBlock
            {
                Text = $"✓ '{concept.Name}' 방향으로 진행할게요",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase,
                TextWrapping = TextWrapping.Wrap
            });
            var sw = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            if (concept.Colors != null)
                foreach (var kv in concept.Colors)
                    sw.Children.Add(new Border
                    {
                        Width = 20, Height = 20, CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 5, 0),
                        BorderBrush = ThemeResources.BorderCard, BorderThickness = new Thickness(1),
                        Background = ConceptSwatch(kv.Value)
                    });
            p.Children.Add(sw);
            if (concept.Fonts != null && concept.Fonts.Count > 0)
                p.Children.Add(new TextBlock
                {
                    Text = "글꼴 · " + string.Join(" / ", concept.Fonts.Values),
                    FontSize = 11, Foreground = ThemeResources.TextSub, FontFamily = ThemeResources.FontBase,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            return new Border
            {
                Background = ThemeResources.BgCategoryActive,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(10, 8, 10, 0),
                Padding = new Thickness(14, 10, 14, 10),
                Child = p
            };
        }
```

- [ ] **Step 7: TaskPaneHost 구성·주입** — `src/TeampptAddin/UI/TaskPaneHost.cs`:

(a) `DeckStructureService deckStructure = null;`(약 196행) 아래에:
```csharp
            ConceptSuggester conceptSuggester = null;
```
(b) `deckStructure = new DeckStructureService(new GeminiAiService(gemini));`가 나오는 **두 곳**(약 202행 Supabase+Gemini 분기, 약 209행 Gemini-only 분기) 각각 바로 아래에:
```csharp
                conceptSuggester = new ConceptSuggester(new GeminiAiService(gemini));
```
(c) `InitAi` 호출(약 219행)을 교체:
```csharp
            _wpfPanel.InitAi(ai, styles, _remoteCache, redesign, recommend, deckStructure, conceptSuggester);
```

- [ ] **Step 8: 빌드 검증** — Run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal
```
Expected: 0 errors. 확인: `stat -c '%y' src/TeampptAddin/bin/Debug/TeampptAddin.dll` 타임스탬프가 방금(1분 이내)인지 + `tail -5 build.log` 오류 0.

> ⚠ **수동 검증(아래 Step 9)은 관리자 권한 COM 등록 빌드가 필요**할 수 있음. 단위테스트가 없는 Task라 빌드는 `RegisterForComInterop=false`로 0에러만 확인하고, 실제 PowerPoint 로드는 평소 관리자 빌드 절차(CLAUDE.md)로 한 번 더 빌드 후 검증. (사용자에게 넘김.)

- [ ] **Step 9: 수동 검증 (사용자 / 데모 다리)** — PowerPoint 재시작 → 빈 PPT → AI 패널 → "📂 리디자인 (초안 파일)" → 초안 .pptx 선택 → 구조 박스 다음에:
  1. **칩 질문 카드**(느낌 6칩 + 용도 5칩)가 뜨고, 각 그룹에서 하나 고르면 그 칩만 강조(단일 선택)되는지.
  2. "컨셉 만들기" 클릭 → 잠시 후 **컨셉 3카드**(이름 · styleTags 배지 · 색 스와치 · 글꼴)가 뜨는지.
  3. 카드 하나 클릭 → 그 카드 강조 + **"✓ '…' 방향으로 진행할게요" 확정 배너**(팔레트·글꼴 에코)가 뜨는지.
  4. 원본 .pptx는 변경되지 않아야 함(이 Phase는 파일을 쓰지 않음).

- [ ] **Step 10: commit**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs src/TeampptAddin/UI/TaskPaneHost.cs
git commit -m "feat(redesign): 컨셉 칩 질문 → 3카드 생성·선택·확정 배너 UI (Phase 2, 데모 다리)

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Self-Review

**1. Spec coverage (스펙 "Phase 2 — 컨설팅 컨셉(Step 3)"):**
- 질문 "어디에 쓰나/어떤 느낌" → Task 2 칩 질문 카드(느낌+용도) ✅.
- 컨셉 3블록 생성(LLM, name+styleTags+colors/fonts role 맵 = DesignConcept) → Task 1 `ConceptSuggester`+스키마(정확히 3개, role 배열) ✅.
- 사용자 카드 선택 → Task 2 `BuildConceptCards`/`OnConceptSelected` 단일 선택 ✅.
- D2 (a) 검색 styleTags 가중 / (b) ConceptResolver 색·폰트 override → **유보(Phase 3 소비자)**, 단 styleTags·colors·fonts를 가진 `_selectedConcept`를 저장해 Phase 3가 읽도록 배선 ✅ (브레인스토밍에서 "저장 + 확정 배너"로 확정).
- 신규 단위 = ConceptSuggester ✅ / 질문·컨셉 카드 UI ✅. 재사용 = DesignConcept ✅ / ConceptResolver(이번엔 미사용, Phase 3) ✅.
- 데모 스코프 "진짜(가볍게)" — 컨셉 생성·선택까지 진짜, 깊은 컨셉 엔진 없음 ✅.

**2. Placeholder scan:** 모든 Step에 실제 코드/명령. parser·BuildUserText는 실제 테스트 코드. UI는 기존 검증된 WPF 프리미티브(BuildStructureBox/BuildDeckRedesignBar 패턴)와 실재 `ThemeResources` 멤버·`BrushFromHex` 재사용. "수동 검증"은 COM/UI 태스크라 단위테스트 대체(규칙).

**3. Type consistency:**
- `DesignConcept`(Id/Name/StyleTags/Colors/Fonts) — Task 1 parser 생성 ↔ Task 2 카드 렌더 일치(`Colors`=Dictionary<string,string>, `Fonts`=Dictionary<string,string>, `StyleTags`=List<string>).
- `ConceptSuggester.SuggestAsync(DeckStructure, string, string)` (Task 1) ↔ Task 2 호출 `SuggestAsync(_lastStructure, _selUsage, _selFeeling)` 인자 순서 일치(**usage, feeling** 순서 — BuildUserText/호출 양쪽 동일).
- `ConceptSuggester(GeminiAiService)` 생성자 ↔ TaskPaneHost `new ConceptSuggester(new GeminiAiService(gemini))` 일치.
- `InitAi(..., DeckStructureService, ConceptSuggester)` 새 시그니처 ↔ TaskPaneHost 호출 인자 수 일치.
- `GenerateJsonAsync(system, userText, (string)null, schema, temperature:, thinkingBudget:)` — 기존 오버로드(`GeminiAiService.cs:214`)와 일치.
- `BrushFromHex`는 기존 static(재정의 금지) — `ConceptSwatch`만 신규 래퍼.

**4. Ambiguity:**
- 칩은 **그룹별 단일 선택**(느낌 1 + 용도 1) — `StyleConceptChip(c, c == chip)`로 같은 그룹 내 하나만 강조. 명시.
- "컨셉 만들기" 버튼은 항상 클릭 가능, 미선택 시 `RunConceptSuggestAsync`가 버블로 안내(enable/disable 플러밍 회피).
- 컨셉 카드 선택은 단일 — 마지막 클릭이 `_selectedConcept`. 재클릭 시 배너가 누적될 수 있으나(데모 비차단) 허용; 거슬리면 후속 폴리시.

## 알려진 리스크 (구현자 주의)

- **STA/COM:** Task 2의 `await _conceptSuggester.SuggestAsync(...)` 호출부에 `ConfigureAwait(false)` **금지**(이후 `_chatStack` 접근이 UI 스레드). 서비스 **내부** await만 `ConfigureAwait(false)`(이후 COM 없음 — DeckStructureService와 동일).
- **패널 멤버명·using:** `_chatStack`/`_chatScroll`/`AddAiBubble`/`ThemeResources.*`/`BrushFromHex`는 실재(grep 확인). WPF using 누락 시 추가.
- **두 GeminiAiService:** DeckStructure·ConceptSuggester가 각각 `new GeminiAiService(gemini)` — 기존 패턴(서비스별 자체 래핑)과 일치, 무해. 공유로 줄이고 싶으면 한 인스턴스를 두 생성자에 넘겨도 됨(선택).
- **LLM 불량 HEX:** `ConceptSwatch` try/catch로 가드(폴백 #CCCCCC). 기존 `BrushFromHex`는 6자리 가정이라 직접 호출 시 예외 가능.

## Execution Handoff

플랜 저장: `docs/superpowers/plans/2026-06-26-redesign-phase2-consulting-concept.md`.
실행: subagent-driven-development — 구현 sonnet, 리뷰 diff 크기별(Task 1 로직=sonnet, Task 2 UI diff=haiku/sonnet), 최종 통합 리뷰 opus. Task 1은 TDD, Task 2는 빌드 0에러 + 사용자 PPT 수동검증(구조박스 → 칩 질문 → 컨셉 3카드 → 선택·배너).
각 Task 리뷰 clean마다 `.superpowers/sdd/progress.md`에 한 줄, PROGRESS-BOARD 갱신.
