# Phase E: Gemini Flash API 연동 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** MockAiService를 GeminiAiService로 교체하여 gemini-2.5-flash 기반 에셋+스타일 추천 구현

**Architecture:** GeminiAiService가 IAiService를 구현. 내부에서 CatalogBuilder로 컴팩트 카탈로그를 만들어 Gemini 프롬프트에 포함(두 토큰 예산 원칙). Gemini REST API에 JSON 응답을 요청하고, file명으로 원본 HeaderAsset을 역참조하여 AiRecommendation을 조립한다. 매 호출마다 토큰 사용량을 Logger로 기록.

**Tech Stack:** .NET Framework 4.8, System.Net.Http.HttpClient, Newtonsoft.Json 13.0.3, xUnit 2.9.2

## Global Constraints

- Target: .NET Framework 4.8 (SDK-style csproj 아님, 수동 Compile Include 필요)
- DI 프레임워크 없음, 직접 인스턴스화
- Newtonsoft.Json 13.0.3 사용 (System.Text.Json 아님)
- 테스트: xUnit 2.9.2, net48
- 한국어 UI/프롬프트
- API 키는 절대 git에 커밋하지 않음

---

### Task 1: API 키 저장 + .gitignore + csproj 설정

**Files:**
- Create: `src/TeampptAddin/Assets/api-keys.json`
- Modify: `.gitignore`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (Content Include 추가)

**Interfaces:**
- Produces: `Assets/api-keys.json`이 빌드 출력의 `Assets/` 폴더에 복사됨. 런타임에 `Globals.AssetsDir`에서 `api-keys.json` 경로로 접근 가능.

- [ ] **Step 1: .gitignore에 api-keys.json 추가**

`.gitignore` 파일 끝에 추가:

```
# API keys (secrets)
**/api-keys.json
```

- [ ] **Step 2: api-keys.json 생성**

`src/TeampptAddin/Assets/api-keys.json`:

```json
{
  "gemini": "AIzaSyB7Q3p-bhQTc5WZGKNEvr5BNGi17RDRthg"
}
```

- [ ] **Step 3: csproj에 Content Include 추가**

`src/TeampptAddin/TeampptAddin.csproj`의 기존 `<Content Include="Assets\styles.json">` 블록 뒤에 추가:

```xml
    <Content Include="Assets\api-keys.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
```

- [ ] **Step 4: 빌드 확인**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\TeampptAddin\TeampptAddin.csproj /t:Build /p:Configuration=Debug /v:minimal
```

Expected: 빌드 성공, `bin\Debug\Assets\api-keys.json` 존재

- [ ] **Step 5: Commit**

```bash
git add .gitignore src/TeampptAddin/TeampptAddin.csproj
git commit -m "chore: add api-keys.json config and gitignore"
```

> Note: api-keys.json 자체는 gitignore되므로 커밋에 포함되지 않음

---

### Task 2: GeminiPromptBuilder — 프롬프트 조립

**Files:**
- Create: `src/TeampptAddin/Services/GeminiPromptBuilder.cs`
- Create: `src/TeampptAddin.Tests/GeminiPromptBuilderTest.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (Compile Include 추가)

**Interfaces:**
- Consumes: `CatalogBuilder.Build(assets)` → `List<CatalogEntry>`, `StylePalette` (Id, Name, Mood, UseWhen), `StyleFont` (Name, Mood, UseWhen)
- Produces: `GeminiPromptBuilder.BuildSystemPrompt(catalog, palettes, fonts)` → `string`, `GeminiPromptBuilder.BuildUserPrompt(userIntent)` → `string`

- [ ] **Step 1: 테스트 작성**

`src/TeampptAddin.Tests/GeminiPromptBuilderTest.cs`:

```csharp
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class GeminiPromptBuilderTest
    {
        private List<CatalogEntry> MakeCatalog()
        {
            return new List<CatalogEntry>
            {
                new CatalogEntry
                {
                    File = "header_1.pptx",
                    Name = "깔끔한 제목",
                    Kind = "component",
                    Category = "헤더",
                    Scope = "slide",
                    Tags = new List<string> { "심플", "제목" },
                    UseWhen = "간결한 제목 슬라이드가 필요할 때",
                    SlotNames = new List<string> { "title", "subtitle" },
                    ColorRoles = new List<string> { "main", "text" },
                    FontRoles = new List<string> { "heading", "body" }
                }
            };
        }

        private List<StylePalette> MakePalettes()
        {
            return new List<StylePalette>
            {
                new StylePalette
                {
                    Id = "blue-professional",
                    Name = "블루 프로페셔널",
                    Mood = new List<string> { "신뢰", "전문성" },
                    UseWhen = "B2B 제안서"
                }
            };
        }

        private List<StyleFont> MakeFonts()
        {
            return new List<StyleFont>
            {
                new StyleFont
                {
                    Name = "Pretendard",
                    Mood = new List<string> { "모던" },
                    UseWhen = "범용"
                }
            };
        }

        [Fact]
        public void SystemPrompt_Contains_Catalog_Json()
        {
            var prompt = GeminiPromptBuilder.BuildSystemPrompt(
                MakeCatalog(), MakePalettes(), MakeFonts());

            Assert.Contains("header_1.pptx", prompt);
            Assert.Contains("깔끔한 제목", prompt);
            Assert.Contains("blue-professional", prompt);
            Assert.Contains("Pretendard", prompt);
        }

        [Fact]
        public void SystemPrompt_Excludes_Hex_Values()
        {
            var palettes = MakePalettes();
            palettes[0].Colors = new PaletteColors
            {
                Main = "#2563EB", Sub1 = "#3B82F6",
                Sub2 = "#93C5FD", Text = "#1E293B"
            };

            var prompt = GeminiPromptBuilder.BuildSystemPrompt(
                MakeCatalog(), palettes, MakeFonts());

            Assert.DoesNotContain("#2563EB", prompt);
            Assert.DoesNotContain("#3B82F6", prompt);
        }

        [Fact]
        public void SystemPrompt_Contains_Json_Response_Schema()
        {
            var prompt = GeminiPromptBuilder.BuildSystemPrompt(
                MakeCatalog(), MakePalettes(), MakeFonts());

            Assert.Contains("\"file\"", prompt);
            Assert.Contains("\"reason\"", prompt);
            Assert.Contains("\"palette\"", prompt);
            Assert.Contains("\"font\"", prompt);
        }

        [Fact]
        public void UserPrompt_Contains_Intent()
        {
            var result = GeminiPromptBuilder.BuildUserPrompt("깔끔한 발표");
            Assert.Contains("깔끔한 발표", result);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

```powershell
dotnet test src\TeampptAddin.Tests --filter "FullyQualifiedName~GeminiPromptBuilderTest" --no-restore -v minimal
```

Expected: 컴파일 에러 — `GeminiPromptBuilder` 없음

- [ ] **Step 3: GeminiPromptBuilder 구현**

`src/TeampptAddin/Services/GeminiPromptBuilder.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public static class GeminiPromptBuilder
    {
        public static string BuildSystemPrompt(
            List<CatalogEntry> catalog,
            IEnumerable<StylePalette> palettes,
            IEnumerable<StyleFont> fonts)
        {
            var catalogJson = JsonConvert.SerializeObject(catalog, Formatting.Indented);

            var paletteSummaries = palettes.Select(p => new
            {
                p.Id, p.Name, p.Mood, p.UseWhen
            });
            var palettesJson = JsonConvert.SerializeObject(paletteSummaries, Formatting.Indented);

            var fontSummaries = fonts.Select(f => new
            {
                f.Name, f.Mood, f.UseWhen
            });
            var fontsJson = JsonConvert.SerializeObject(fontSummaries, Formatting.Indented);

            return $@"너는 PPT 디자인 어시스턴트야. 사용자의 의도에 가장 적합한 에셋과 스타일을 추천해.

## 에셋 카탈로그
{catalogJson}

## 팔레트 목록
{palettesJson}

## 폰트 목록
{fontsJson}

## 응답 규칙
1. 사용자 의도에 가장 적합한 에셋 1~3개를 추천해.
2. 각 에셋 추천에 이유를 달아.
3. 가장 어울리는 팔레트 1개와 폰트 1개도 추천해.
4. 반드시 아래 JSON 형식으로만 응답해. 다른 텍스트는 포함하지 마.

```json
{{
  ""message"": ""추천 설명 메시지 (한국어, 1~2문장)"",
  ""assets"": [
    {{ ""file"": ""header_N.pptx"", ""reason"": ""추천 이유"" }}
  ],
  ""palette"": {{ ""id"": ""팔레트id"", ""reason"": ""추천 이유"" }},
  ""font"": {{ ""name"": ""폰트이름"", ""reason"": ""추천 이유"" }}
}}
```";
        }

        public static string BuildUserPrompt(string userIntent)
        {
            return userIntent;
        }
    }
}
```

- [ ] **Step 4: csproj에 Compile Include 추가**

`src/TeampptAddin/TeampptAddin.csproj`의 기존 `<Compile Include="Services\IAiService.cs" />` 뒤에:

```xml
    <Compile Include="Services\GeminiPromptBuilder.cs" />
```

- [ ] **Step 5: 테스트 통과 확인**

```powershell
dotnet test src\TeampptAddin.Tests --filter "FullyQualifiedName~GeminiPromptBuilderTest" --no-restore -v minimal
```

Expected: 4 passed

- [ ] **Step 6: Commit**

```bash
git add src/TeampptAddin/Services/GeminiPromptBuilder.cs src/TeampptAddin/TeampptAddin.csproj src/TeampptAddin.Tests/GeminiPromptBuilderTest.cs
git commit -m "feat: GeminiPromptBuilder with compact catalog prompt assembly"
```

---

### Task 3: GeminiAiService — API 호출 + 파싱 + 토큰 로깅

**Files:**
- Create: `src/TeampptAddin/Services/GeminiAiService.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (Compile Include + System.Net.Http Reference)

**Interfaces:**
- Consumes: `GeminiPromptBuilder.BuildSystemPrompt(...)` → `string`, `GeminiPromptBuilder.BuildUserPrompt(...)` → `string`, `CatalogBuilder.Build(assets)` → `List<CatalogEntry>`, `Logger.Log(msg)` → `void`
- Produces: `GeminiAiService : IAiService` — `RecommendAsync(userIntent, assets, palettes, fonts)` → `Task<AiRecommendation>`

- [ ] **Step 1: csproj에 System.Net.Http 참조 추가**

`src/TeampptAddin/TeampptAddin.csproj`의 `<Reference Include="System.Core" />` 뒤에:

```xml
    <Reference Include="System.Net.Http" />
```

그리고 Compile Include에 추가:

```xml
    <Compile Include="Services\GeminiAiService.cs" />
```

- [ ] **Step 2: GeminiAiService 구현**

`src/TeampptAddin/Services/GeminiAiService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public class GeminiAiService : IAiService
    {
        private static readonly HttpClient Http = new HttpClient();
        private readonly string _apiKey;

        public GeminiAiService(string apiKey)
        {
            _apiKey = apiKey;
        }

        public static GeminiAiService FromAssetsDir(string assetsDir)
        {
            var path = Path.Combine(assetsDir, "api-keys.json");
            var json = File.ReadAllText(path, Encoding.UTF8);
            var obj = JObject.Parse(json);
            var key = obj["gemini"]?.ToString()
                ?? throw new InvalidOperationException("api-keys.json에 'gemini' 키가 없습니다.");
            return new GeminiAiService(key);
        }

        public async Task<AiRecommendation> RecommendAsync(
            string userIntent,
            IEnumerable<HeaderAsset> assets,
            IEnumerable<StylePalette> palettes,
            IEnumerable<StyleFont> fonts)
        {
            var assetList = assets.ToList();
            var paletteList = palettes.ToList();
            var fontList = fonts.ToList();

            var catalog = CatalogBuilder.Build(assetList);

            var systemPrompt = GeminiPromptBuilder.BuildSystemPrompt(
                catalog, paletteList, fontList);
            var userPrompt = GeminiPromptBuilder.BuildUserPrompt(userIntent);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    responseMimeType = "application/json"
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody),
                Encoding.UTF8, "application/json");

            var response = await Http.PostAsync(url, content).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Gemini API 오류 ({(int)response.StatusCode}): {body}");

            var root = JObject.Parse(body);

            LogTokenUsage(root);

            var text = root["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException("Gemini 응답에 텍스트가 없습니다.");

            return ParseResponse(text, assetList, paletteList, fontList);
        }

        private void LogTokenUsage(JObject root)
        {
            var usage = root["usageMetadata"];
            if (usage == null) return;

            var input = usage["promptTokenCount"]?.Value<int>() ?? 0;
            var output = usage["candidatesTokenCount"]?.Value<int>() ?? 0;
            var total = usage["totalTokenCount"]?.Value<int>() ?? 0;

            Logger.Log($"[Gemini] 토큰 사용: input={input}, output={output}, total={total}");
        }

        private AiRecommendation ParseResponse(
            string json,
            List<HeaderAsset> assets,
            List<StylePalette> palettes,
            List<StyleFont> fonts)
        {
            var obj = JObject.Parse(json);

            var message = obj["message"]?.ToString() ?? "";

            var assetSuggestions = new List<AssetSuggestion>();
            var assetArray = obj["assets"] as JArray;
            if (assetArray != null)
            {
                foreach (var item in assetArray)
                {
                    var file = item["file"]?.ToString();
                    var reason = item["reason"]?.ToString() ?? "";
                    var match = assets.FirstOrDefault(a =>
                        string.Equals(a.File, file, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        assetSuggestions.Add(new AssetSuggestion { Asset = match, Reason = reason });
                }
            }

            StylePalette matchedPalette = null;
            string paletteReason = "";
            var paletteObj = obj["palette"];
            if (paletteObj != null)
            {
                var pid = paletteObj["id"]?.ToString();
                paletteReason = paletteObj["reason"]?.ToString() ?? "";
                matchedPalette = palettes.FirstOrDefault(p =>
                    string.Equals(p.Id, pid, StringComparison.OrdinalIgnoreCase));
            }

            StyleFont matchedFont = null;
            string fontReason = "";
            var fontObj = obj["font"];
            if (fontObj != null)
            {
                var fname = fontObj["name"]?.ToString();
                fontReason = fontObj["reason"]?.ToString() ?? "";
                matchedFont = fonts.FirstOrDefault(f =>
                    string.Equals(f.Name, fname, StringComparison.OrdinalIgnoreCase));
            }

            return new AiRecommendation
            {
                Message = message,
                Assets = assetSuggestions,
                Style = new StyleSuggestion
                {
                    Palette = matchedPalette,
                    Font = matchedFont,
                    Reason = string.Join("; ", new[] { paletteReason, fontReason }
                        .Where(r => !string.IsNullOrEmpty(r)))
                }
            };
        }
    }
}
```

- [ ] **Step 3: 빌드 확인**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\TeampptAddin\TeampptAddin.csproj /t:Build /p:Configuration=Debug /v:minimal
```

Expected: 빌드 성공

- [ ] **Step 4: Commit**

```bash
git add src/TeampptAddin/Services/GeminiAiService.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat: GeminiAiService with REST API call, JSON parsing, token logging"
```

---

### Task 4: TaskPaneHost 교체 + 통합 테스트

**Files:**
- Modify: `src/TeampptAddin/UI/TaskPaneHost.cs:167` — MockAiService → GeminiAiService

**Interfaces:**
- Consumes: `GeminiAiService.FromAssetsDir(assetsDir)` → `GeminiAiService`

- [ ] **Step 1: TaskPaneHost.cs 수정**

`src/TeampptAddin/UI/TaskPaneHost.cs`의 `LoadWpfCards()` 메서드에서 167행을 변경:

변경 전:
```csharp
            var ai     = new MockAiService();
```

변경 후:
```csharp
            IAiService ai;
            try
            {
                ai = GeminiAiService.FromAssetsDir(assetsDir);
            }
            catch (Exception ex)
            {
                Logger.Log($"[Gemini] API 키 로드 실패, MockAiService 사용: {ex.Message}");
                ai = new MockAiService();
            }
```

> api-keys.json이 없거나 키가 없을 경우에만 MockAiService로 폴백. 정상 환경에서는 항상 GeminiAiService 사용.

- [ ] **Step 2: 빌드 확인**

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" src\TeampptAddin\TeampptAddin.csproj /t:Build /p:Configuration=Debug /v:minimal
```

Expected: 빌드 성공

- [ ] **Step 3: 기존 테스트 전체 통과 확인**

```powershell
dotnet test src\TeampptAddin.Tests --no-restore -v minimal
```

Expected: 12 tests passed (기존 테스트 모두 통과)

- [ ] **Step 4: Commit**

```bash
git add src/TeampptAddin/UI/TaskPaneHost.cs
git commit -m "feat: replace MockAiService with GeminiAiService in TaskPaneHost"
```

---

### Task 5: 수동 통합 테스트 (PowerPoint에서 실행)

- [ ] **Step 1: 빌드 + 등록**

```powershell
Start-Process -Verb RunAs -FilePath "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList "src\TeampptAddin\TeampptAddin.csproj /t:Build /p:Configuration=Debug /v:minimal"
```

- [ ] **Step 2: PowerPoint 실행 → Task Pane 열기**

1. PowerPoint 열기
2. Task Pane에서 AI 채팅 입력란에 "깔끔한 제안서 발표" 입력
3. AI 응답 확인: 에셋 추천 카드 + 스타일 추천이 표시되는지

- [ ] **Step 3: 토큰 로그 확인**

```powershell
Get-Content "$env:LOCALAPPDATA\TeampptAddin\debug.log" -Tail 10
```

Expected: `[Gemini] 토큰 사용: input=NNN, output=NNN, total=NNN` 로그 확인

- [ ] **Step 4: 에러 케이스 테스트**

`api-keys.json`에서 키를 잘못된 값으로 변경 → PowerPoint에서 메시지 전송 → UI에 에러 메시지가 표시되는지 확인 → 키 복원

- [ ] **Step 5: 최종 Commit (있으면)**

변경사항이 있으면 커밋.
