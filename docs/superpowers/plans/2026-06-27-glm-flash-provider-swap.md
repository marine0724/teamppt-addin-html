# GLM-Flash 전면 교체(Gemini 즉시 복귀 가능) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 생성 LLM 호출 전부(텍스트 + 비전)를 z.ai 무료 GLM-Flash로 옮기되, api-keys.json의 `provider` 한 글자만 바꾸면 즉시 Gemini로 되돌아가는 provider 추상화를 만든다. 임베딩만 Gemini 유지.

**Architecture:** 모든 생성 호출은 이미 `GeminiAiService`의 `GenerateJsonAsync`(+ 레거시 `RecommendAsync`/`DiagnoseSlideAsync`)로 모인다. 이 메서드들을 `IAiService` 인터페이스로 끌어올리고, z.ai OpenAI-호환 엔드포인트를 때리는 `GlmAiService : IAiService`를 새로 만든다. 단일 스위치 지점 `AiServiceFactory.CreateGenerative()`가 `AiConfig.Provider`를 보고 둘 중 하나를 반환한다. 모든 `new GeminiAiService(key)` 생성 호출 지점을 이 팩토리로 교체한다. `EmbeddingService`는 손대지 않고 항상 Gemini.

**Tech Stack:** C# / .NET Framework 4.8, Newtonsoft.Json, `System.Net.Http.HttpClient`, xUnit. z.ai General 엔드포인트 `https://api.z.ai/api/paas/v4/chat/completions` (무료 티어, `Authorization: Bearer <key>`. ⚠️ Coding Plan 전용 `/api/coding/paas/v4`가 **아니라** General `/api/paas/v4`를 쓴다). **공식 docs 확인 결과**: `response_format`은 **`{"type":"json_object"}`만 지원**(json_schema 미지원), structured output 지원 모델 목록에 flash가 명시돼 있지 않음 → 스키마는 system 프롬프트에 임베딩하고 `json_object`로 받는 것을 **기본 전략**으로 한다. (출처: docs.z.ai/guides/capabilities/struct-output, docs.z.ai/guides/vlm/glm-4.6v) 모델 ID 확인: `glm-4.7-flash`(텍스트, 무료), `glm-4.6v-flash`(비전, "Completely Free"), `thinking:{type:enabled|disabled}`, 이미지 = `image_url:{url:"data:image/png;base64,..."}`.

## Global Constraints

- 모델: 텍스트 = `glm-4.7-flash`, 비전(이미지 포함) = `glm-4.6v-flash`. 이미지 유무로 자동 선택.
- 임베딩(`EmbeddingService`, `gemini-embedding-001`)은 **절대 건드리지 않는다** — 무료 임베딩 대체 없음 + 재인제스트 위험.
- 즉시 복귀: api-keys.json의 `"provider"` 값을 `"gemini"`로 바꾸면 빌드/코드 수정 없이 전체가 Gemini로 돌아가야 한다.
- 안전 폴백: `provider="glm"`인데 GLM 키가 비어 있으면 자동으로 Gemini를 쓴다(런타임 크래시 금지).
- API 키는 문서·커밋에 평문 금지(CLAUDE.md). 키는 api-keys.json에서만 읽는다.
- 순수 로직 Task는 UAC 없이 빌드·TDD. 실제 API 호출 검증은 마지막 Task에서 관리자 빌드 + 실제 덱 실행으로만 확인.
- 빌드는 foreground로만 실행(`run_in_background` 금지 — 응답 멈춤).

---

## 빌드 & 테스트 명령 (참조)

**[BUILD-LOGIC]** — 비-UAC 솔루션 빌드(전체 컴파일, COM 등록 없음). foreground:
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal
```
기대: 마지막 줄 `0 Error(s)`.

**[TEST]** — 빌드된 테스트 어셈블리에서 필터 실행(재빌드 안 함):
```powershell
dotnet test "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~<TestClass>"
```
> 주의: `[BUILD-LOGIC]`로 솔루션을 먼저 빌드해야 `--no-build` 테스트가 최신 코드를 본다. Task의 TDD 루프는 "BUILD-LOGIC → TEST" 순서로 돈다.

**[BUILD-DEPLOY]** — 마지막 Task 전용. 관리자 COM 등록 빌드(PPT 수동검증용):
```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
직후 검증: ① `stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll`(1분 이내) ② `tail -5 c:/Projects/teamppt-addin/build.log`(오류 0건).

---

## 파일 구조

| 파일 | 책임 | Task |
|---|---|---|
| `Services/AiConfig.cs` (신규) | provider + 키 로딩(api-keys.json), 정적 상태 | 1 |
| `Services/AiServiceFactory.cs` (신규) | `CreateGenerative()` 단일 스위치 지점 | 1, 6 |
| `Services/IAiService.cs` (수정) | `GenerateJsonAsync` 2개 오버로드 추가; MockAiService 스텁 | 2 |
| `Services/GlmSchema.cs` (신규) | responseSchema 표준화(소문자 type) → 프롬프트 임베딩용 | 3 |
| `Services/GlmRequestBuilder.cs` (신규) | chat/completions 요청 바디 빌더(순수, 모델선택 포함) | 4 |
| `Services/GlmAiService.cs` (신규) | `IAiService` 구현, z.ai HTTP 호출·재시도·파싱 | 5 |
| `Services/AiRecommendationParser.cs` (신규) | `GeminiAiService.ParseResponse` 추출(공유 파서) | 5 |
| `Services/GeminiAiService.cs` (수정) | `ParseResponse` → `AiRecommendationParser` 위임 | 5 |
| 6개 소비자 서비스 (수정) | 필드/생성자 타입 `GeminiAiService`→`IAiService` | 2 |
| `UI/TaskPaneHost.cs` + 4개 서비스 (수정) | `new GeminiAiService`→`AiServiceFactory.CreateGenerative()` | 6 |
| `api-keys.json` (수정, 로컬만) | `"provider"`, `"glm"` 필드 추가 | 1, 7 |

---

### Task 1: AiConfig + AiServiceFactory 골격 (provider 로딩)

**Files:**
- Create: `src/TeampptAddin/Services/AiConfig.cs`
- Create: `src/TeampptAddin/Services/AiServiceFactory.cs`
- Test: `src/TeampptAddin.Tests/AiConfigTest.cs`

**Interfaces:**
- Produces:
  - `static class AiConfig { static string Provider; static string GlmKey; static string GeminiKey; static void Load(string assetsDir); static void LoadFromJson(string json); static bool UseGlm { get; } }`
  - `static class AiServiceFactory { static IAiService CreateGenerative(); }`

- [ ] **Step 1: 실패 테스트 작성**

`src/TeampptAddin.Tests/AiConfigTest.cs`:
```csharp
using TeampptAddin;
using Xunit;

public class AiConfigTest
{
    [Fact]
    public void DefaultsToGlm_WhenProviderMissing()
    {
        AiConfig.LoadFromJson("{\"gemini\":\"g-key\",\"glm\":\"z-key\"}");
        Assert.Equal("glm", AiConfig.Provider);
        Assert.True(AiConfig.UseGlm);
        Assert.Equal("z-key", AiConfig.GlmKey);
        Assert.Equal("g-key", AiConfig.GeminiKey);
    }

    [Fact]
    public void RespectsExplicitGeminiProvider()
    {
        AiConfig.LoadFromJson("{\"provider\":\"gemini\",\"gemini\":\"g\",\"glm\":\"z\"}");
        Assert.False(AiConfig.UseGlm);
    }

    [Fact]
    public void FallsBackToGemini_WhenGlmKeyEmpty()
    {
        AiConfig.LoadFromJson("{\"provider\":\"glm\",\"gemini\":\"g\"}");
        Assert.Equal("glm", AiConfig.Provider);
        Assert.False(AiConfig.UseGlm); // 키 없으면 GLM 안 씀
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~AiConfigTest"`).
기대: 컴파일 실패(`AiConfig` 없음).

- [ ] **Step 3: AiConfig 구현**

`src/TeampptAddin/Services/AiConfig.cs`:
```csharp
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>
    /// 생성 LLM provider 선택 + 키. 시작 시 api-keys.json에서 1회 로딩.
    /// provider="gemini"로 바꾸면 전체가 Gemini로 즉시 복귀.
    /// </summary>
    public static class AiConfig
    {
        public static string Provider = "glm";
        public static string GlmKey = "";
        public static string GeminiKey = "";

        // provider가 glm이고 키가 실제로 있을 때만 GLM 사용(안전 폴백)
        public static bool UseGlm =>
            string.Equals(Provider, "glm", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(GlmKey);

        public static void Load(string assetsDir)
        {
            try
            {
                var path = Path.Combine(assetsDir, "api-keys.json");
                LoadFromJson(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Logger.Log($"[AiConfig] 로딩 실패(기본값 유지): {ex.Message}");
            }
        }

        public static void LoadFromJson(string json)
        {
            var obj = JObject.Parse(json);
            Provider = obj["provider"]?.ToString() ?? "glm";
            GlmKey = obj["glm"]?.ToString() ?? "";
            GeminiKey = obj["gemini"]?.ToString() ?? "";
            Logger.Log($"[AiConfig] provider={Provider}, glmKey={(string.IsNullOrEmpty(GlmKey) ? "없음" : "있음")}, useGlm={UseGlm}");
        }
    }
}
```

- [ ] **Step 4: AiServiceFactory 골격 구현(아직 GLM 미구현 → Gemini만)**

`src/TeampptAddin/Services/AiServiceFactory.cs`:
```csharp
namespace TeampptAddin
{
    /// <summary>생성 LLM 서비스의 단일 생성 지점. provider 스위치는 여기 한 곳.</summary>
    public static class AiServiceFactory
    {
        public static IAiService CreateGenerative()
        {
            // Task 5에서 GlmAiService 분기 추가. 지금은 Gemini만.
            return new GeminiAiService(AiConfig.GeminiKey);
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~AiConfigTest"`).
기대: 3개 PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/AiConfig.cs src/TeampptAddin/Services/AiServiceFactory.cs src/TeampptAddin.Tests/AiConfigTest.cs
git commit -m "feat(glm): AiConfig provider 로딩 + AiServiceFactory 골격 (Task 1, TDD 3/3)"
```

---

### Task 2: IAiService 인터페이스 확장 + 소비자 타입 전환

기존 6개 소비자는 구체 타입 `GeminiAiService`에 의존한다. `GenerateJsonAsync`를 `IAiService`로 끌어올리고, 소비자 필드/생성자 타입을 인터페이스로 바꿔 GLM 주입이 가능하게 한다. 동작 변화 없음(컴파일 그린).

**Files:**
- Modify: `src/TeampptAddin/Services/IAiService.cs`
- Modify: `src/TeampptAddin/Services/ConceptSuggester.cs:14-15`
- Modify: `src/TeampptAddin/Services/DeckStructureService.cs:14-15`
- Modify: `src/TeampptAddin/Services/SlotMapper.cs:14-15`
- Modify: `src/TeampptAddin/Services/CombinationRecommender.cs:15-16`
- Modify: `src/TeampptAddin/Services/DraftUnderstandingService.cs:12-13`
- Modify: `src/TeampptAddin/Services/DesignCritiqueService.cs:8-9`

**Interfaces:**
- Consumes: `IAiService` (Task 0 기존)
- Produces: `IAiService.GenerateJsonAsync(string systemPrompt, string userText, string pngPathOrNull, JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)` 및 `IEnumerable<string> pngPaths` 오버로드.

- [ ] **Step 1: IAiService에 GenerateJsonAsync 2개 오버로드 추가**

`src/TeampptAddin/Services/IAiService.cs` — `using` 추가 및 인터페이스 본문 확장:
```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public interface IAiService
    {
        Task<AiRecommendation> RecommendAsync(
            string userIntent,
            IEnumerable<HeaderAsset> assets,
            IEnumerable<StylePalette> palettes,
            IEnumerable<StyleFont> fonts);

        Task<SlideDiagnosis> DiagnoseSlideAsync(string pngPath);

        Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, string pngPathOrNull,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0);

        Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, IEnumerable<string> pngPaths,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0);
    }
```

> `GeminiAiService`는 이미 두 오버로드를 동일 시그니처로 갖고 있으므로 추가 구현 불필요(인터페이스 멤버로 자동 충족).

- [ ] **Step 2: MockAiService에 스텁 추가**

`IAiService.cs`의 `MockAiService` 클래스 안, `DiagnoseSlideAsync` 다음에 추가:
```csharp
        public Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, string pngPathOrNull,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
            => Task.FromResult("{}");

        public Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, IEnumerable<string> pngPaths,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
            => Task.FromResult("{}");
```

- [ ] **Step 3: 6개 소비자 필드/생성자 타입을 IAiService로 전환**

각 파일에서 `GeminiAiService` → `IAiService`로 (필드 + 생성자 파라미터 두 곳). 정확한 치환:

`ConceptSuggester.cs:14-15`:
```csharp
        private readonly IAiService _gemini;
        public ConceptSuggester(IAiService gemini) { _gemini = gemini; }
```
`DeckStructureService.cs:14-15`:
```csharp
        private readonly IAiService _gemini;
        public DeckStructureService(IAiService gemini) { _gemini = gemini; }
```
`SlotMapper.cs:14-15`:
```csharp
        private readonly IAiService _gemini;
        public SlotMapper(IAiService gemini) { _gemini = gemini; }
```
`CombinationRecommender.cs:15-16`:
```csharp
        private readonly IAiService _gemini;
        public CombinationRecommender(IAiService gemini) { _gemini = gemini; }
```
`DraftUnderstandingService.cs:12-13`:
```csharp
        private readonly IAiService _gemini;
        public DraftUnderstandingService(IAiService gemini) { _gemini = gemini; }
```
`DesignCritiqueService.cs:8-9`:
```csharp
        private readonly IAiService _gemini;
        public DesignCritiqueService(IAiService gemini) { _gemini = gemini; }
```

> 본문에서 `_gemini.GenerateJsonAsync(...)` 호출은 그대로 동작(인터페이스 멤버). `AssetUnderstandingService`는 `GeminiAiService` 필드가 아니라 별도 경로(`FromAssetsDir`)이므로 이 Task에서 제외 — Task 6에서 점검.

- [ ] **Step 4: 컴파일 그린 확인**

`[BUILD-LOGIC]`.
기대: `0 Error(s)`. (호출 지점은 아직 `new GeminiAiService(...)`를 넘기지만 `GeminiAiService : IAiService`라 호환됨.)

- [ ] **Step 5: 기존 테스트 회귀 없음 확인**

```powershell
dotnet test "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" --no-build -p:BuildProjectReferences=false
```
기대: 기존 테스트 전부 PASS(이전과 동일 개수).

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/IAiService.cs src/TeampptAddin/Services/ConceptSuggester.cs src/TeampptAddin/Services/DeckStructureService.cs src/TeampptAddin/Services/SlotMapper.cs src/TeampptAddin/Services/CombinationRecommender.cs src/TeampptAddin/Services/DraftUnderstandingService.cs src/TeampptAddin/Services/DesignCritiqueService.cs
git commit -m "refactor(glm): GenerateJsonAsync를 IAiService로 승격, 소비자 6개 인터페이스 의존 전환 (Task 2)"
```

---

### Task 3: GlmSchema.Normalize (스키마 정규화)

스키마는 (response_format이 아니라) **system 프롬프트에 임베딩**돼 모델에 JSON 구조를 지시한다. 우리 스키마는 이미 소문자 type(`object`/`string`/`integer`/`number`/`array`)이지만, 미래에 Gemini식 대문자(`STRING`)가 섞여도 일관된 표준 JSON Schema 문자열이 되도록 소문자 정규화 + 입력 비파괴(clone)를 보장한다.

**Files:**
- Create: `src/TeampptAddin/Services/GlmSchema.cs`
- Test: `src/TeampptAddin.Tests/GlmSchemaTest.cs`

**Interfaces:**
- Produces: `static class GlmSchema { static JObject Normalize(JObject schema); }`

- [ ] **Step 1: 실패 테스트 작성**

`src/TeampptAddin.Tests/GlmSchemaTest.cs`:
```csharp
using Newtonsoft.Json.Linq;
using TeampptAddin;
using Xunit;

public class GlmSchemaTest
{
    [Fact]
    public void LowercasesTypes_Recursively()
    {
        var input = JObject.Parse(@"{
            ""type"":""OBJECT"",
            ""properties"":{
                ""items"":{ ""type"":""ARRAY"", ""items"":{ ""type"":""STRING"" } }
            }
        }");
        var outp = GlmSchema.Normalize(input);
        Assert.Equal("object", outp["type"].ToString());
        Assert.Equal("array", outp["properties"]["items"]["type"].ToString());
        Assert.Equal("string", outp["properties"]["items"]["items"]["type"].ToString());
    }

    [Fact]
    public void DoesNotMutateInput()
    {
        var input = JObject.Parse(@"{""type"":""OBJECT""}");
        GlmSchema.Normalize(input);
        Assert.Equal("OBJECT", input["type"].ToString()); // 원본 보존
    }

    [Fact]
    public void PassesThroughAlreadyLowercase()
    {
        var input = JObject.Parse(@"{""type"":""object"",""properties"":{""n"":{""type"":""number""}}}");
        var outp = GlmSchema.Normalize(input);
        Assert.Equal("object", outp["type"].ToString());
        Assert.Equal("number", outp["properties"]["n"]["type"].ToString());
    }
}
```

- [ ] **Step 2: 실패 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~GlmSchemaTest"`). 기대: 컴파일 실패.

- [ ] **Step 3: GlmSchema 구현**

`src/TeampptAddin/Services/GlmSchema.cs`:
```csharp
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>responseSchema(JObject) type을 소문자 표준 JSON Schema로 정규화(프롬프트 임베딩용). 입력 비파괴.</summary>
    public static class GlmSchema
    {
        public static JObject Normalize(JObject schema)
        {
            var clone = (JObject)schema.DeepClone();
            Walk(clone);
            return clone;
        }

        private static void Walk(JToken node)
        {
            if (node is JObject obj)
            {
                var t = obj["type"];
                if (t != null && t.Type == JTokenType.String)
                    obj["type"] = t.ToString().ToLowerInvariant();
                foreach (var prop in obj.Properties())
                    Walk(prop.Value);
            }
            else if (node is JArray arr)
            {
                foreach (var item in arr)
                    Walk(item);
            }
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~GlmSchemaTest"`). 기대: 3개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Services/GlmSchema.cs src/TeampptAddin.Tests/GlmSchemaTest.cs
git commit -m "feat(glm): GlmSchema 스키마 소문자 정규화(비파괴) (Task 3, TDD 3/3)"
```

---

### Task 4: GlmRequestBuilder (요청 바디 빌더, 순수 로직)

chat/completions 요청 바디를 만드는 순수 함수. 이미지 유무로 모델 자동 선택, 비전은 content 배열 + data URL, 텍스트는 content 문자열. 스키마는 system 프롬프트에도 덧붙여(이중 안전장치) 양 모델 모두에서 JSON 준수율을 높인다.

**Files:**
- Create: `src/TeampptAddin/Services/GlmRequestBuilder.cs`
- Test: `src/TeampptAddin.Tests/GlmRequestBuilderTest.cs`

**Interfaces:**
- Consumes: `GlmSchema.Normalize` (Task 3)
- Produces:
  - `static class GlmRequestBuilder { const string TextModel = "glm-4.7-flash"; const string VisionModel = "glm-4.6v-flash"; static JObject Build(string systemPrompt, string userText, IList<string> imageBase64, JObject responseSchema, double temperature, int thinkingBudget); }`
  - `imageBase64`: 이미 base64로 인코딩된 PNG 문자열 목록(파일 I/O는 호출자 책임 → 순수 테스트 가능). null/빈 목록이면 텍스트 모드.

- [ ] **Step 1: 실패 테스트 작성**

`src/TeampptAddin.Tests/GlmRequestBuilderTest.cs`:
```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TeampptAddin;
using Xunit;

public class GlmRequestBuilderTest
{
    private static JObject Schema() =>
        JObject.Parse(@"{""type"":""object"",""properties"":{""x"":{""type"":""string""}}}");

    [Fact]
    public void TextMode_UsesFlashModel_StringContent()
    {
        var body = GlmRequestBuilder.Build("sys", "hello", null, Schema(), 0.7, 0);
        Assert.Equal("glm-4.7-flash", body["model"].ToString());
        var msgs = (JArray)body["messages"];
        Assert.Equal("system", msgs[0]["role"].ToString());
        Assert.Equal("user", msgs[1]["role"].ToString());
        Assert.Equal(JTokenType.String, msgs[1]["content"].Type); // 텍스트는 문자열
        Assert.Equal("hello", msgs[1]["content"].ToString());
    }

    [Fact]
    public void ImageMode_UsesVisionModel_ContentArrayWithDataUrl()
    {
        var imgs = new List<string> { "QUJD" }; // base64
        var body = GlmRequestBuilder.Build("sys", "look", imgs, Schema(), 0.4, 0);
        Assert.Equal("glm-4.6v-flash", body["model"].ToString());
        var content = (JArray)body["messages"][1]["content"];
        Assert.Equal("text", content[0]["type"].ToString());
        Assert.Equal("image_url", content[1]["type"].ToString());
        Assert.Equal("data:image/png;base64,QUJD", content[1]["image_url"]["url"].ToString());
    }

    [Fact]
    public void ResponseFormat_IsJsonObject()
    {
        // z.ai docs: response_format은 json_object만 지원(json_schema 미지원)
        var body = GlmRequestBuilder.Build("sys", "hi", null, Schema(), 0.5, 0);
        Assert.Equal("json_object", body["response_format"]["type"].ToString());
    }

    [Fact]
    public void SchemaIsEmbeddedInSystemPrompt()
    {
        // 구조 강제는 response_format 대신 system 프롬프트의 스키마로 한다
        var body = GlmRequestBuilder.Build("sys", "hi", null, Schema(), 0.5, 0);
        var sys = body["messages"][0]["content"].ToString();
        Assert.Contains("\"x\"", sys); // 스키마 속성명이 시스템 프롬프트에 포함됨
    }

    [Fact]
    public void Thinking_EnabledWhenBudgetPositive_DisabledWhenZero()
    {
        Assert.Equal("enabled",
            GlmRequestBuilder.Build("s","u",null,Schema(),0.4,512)["thinking"]["type"].ToString());
        Assert.Equal("disabled",
            GlmRequestBuilder.Build("s","u",null,Schema(),0.4,0)["thinking"]["type"].ToString());
    }

    [Fact]
    public void Temperature_IsForwarded()
    {
        var body = GlmRequestBuilder.Build("s", "u", null, Schema(), 0.33, 0);
        Assert.Equal(0.33, body["temperature"].Value<double>(), 3);
    }
}
```

- [ ] **Step 2: 실패 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~GlmRequestBuilderTest"`). 기대: 컴파일 실패.

- [ ] **Step 3: GlmRequestBuilder 구현**

`src/TeampptAddin/Services/GlmRequestBuilder.cs`:
```csharp
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>z.ai chat/completions 요청 바디 빌더(순수). 이미지 유무로 모델 자동 선택.</summary>
    public static class GlmRequestBuilder
    {
        public const string TextModel = "glm-4.7-flash";
        public const string VisionModel = "glm-4.6v-flash";

        public static JObject Build(
            string systemPrompt, string userText, IList<string> imageBase64,
            JObject responseSchema, double temperature, int thinkingBudget)
        {
            bool hasImages = imageBase64 != null && imageBase64.Count > 0;
            var schema = GlmSchema.Normalize(responseSchema);

            // 이중 안전장치: system 프롬프트에 스키마를 명시(양 모델 JSON 준수율↑)
            var sys = new StringBuilder(systemPrompt);
            sys.AppendLine();
            sys.AppendLine("반드시 아래 JSON 스키마에 정확히 맞는 JSON 객체만 출력하라(설명·코드펜스 금지):");
            sys.AppendLine(schema.ToString(Formatting.None));

            JToken userContent;
            if (hasImages)
            {
                var arr = new JArray { new JObject { ["type"] = "text", ["text"] = userText } };
                foreach (var b64 in imageBase64)
                    arr.Add(new JObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JObject { ["url"] = $"data:image/png;base64,{b64}" }
                    });
                userContent = arr;
            }
            else
            {
                userContent = userText;
            }

            return new JObject
            {
                ["model"] = hasImages ? VisionModel : TextModel,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = sys.ToString() },
                    new JObject { ["role"] = "user", ["content"] = userContent }
                },
                ["temperature"] = temperature,
                // z.ai docs: response_format은 json_object만 지원. 구조 강제는 위 system 프롬프트의 스키마로.
                ["response_format"] = new JObject { ["type"] = "json_object" },
                ["thinking"] = new JObject { ["type"] = thinkingBudget > 0 ? "enabled" : "disabled" }
            };
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~GlmRequestBuilderTest"`). 기대: 6개 PASS.

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Services/GlmRequestBuilder.cs src/TeampptAddin.Tests/GlmRequestBuilderTest.cs
git commit -m "feat(glm): GlmRequestBuilder 요청 바디(모델 자동선택·json_object·thinking) (Task 4, TDD 6/6)"
```

---

### Task 5: GlmAiService (HTTP 호출·파싱) + 공유 파서 추출

z.ai를 실제로 때리는 `IAiService` 구현. `GenerateJsonAsync` 2개 오버로드를 빌더+HTTP로 구현하고, 레거시 `RecommendAsync`/`DiagnoseSlideAsync`도 구현(현재 코드 경로에서 도달 가능하므로 NotImplemented 크래시 금지). `RecommendAsync` 파싱은 `GeminiAiService.ParseResponse`를 `AiRecommendationParser`로 추출해 공유한다. HTTP 응답 파싱(`choices[0].message.content`)은 순수 헬퍼로 분리해 단위 테스트한다.

**Files:**
- Create: `src/TeampptAddin/Services/AiRecommendationParser.cs`
- Modify: `src/TeampptAddin/Services/GeminiAiService.cs:283-342` (ParseResponse → 위임)
- Create: `src/TeampptAddin/Services/GlmAiService.cs`
- Test: `src/TeampptAddin.Tests/GlmAiServiceTest.cs`

**Interfaces:**
- Consumes: `GlmRequestBuilder.Build` (Task 4), `GeminiPromptBuilder`(기존), `SlideDiagnosisSchema`/`SlideDiagnosisParser`(기존)
- Produces:
  - `static class AiRecommendationParser { static AiRecommendation Parse(string json, List<HeaderAsset> assets, List<StylePalette> palettes, List<StyleFont> fonts); }`
  - `class GlmAiService : IAiService` (생성자 `GlmAiService(string apiKey)`)
  - `static class GlmAiService { static string ExtractContent(string responseBody); }` — 정적 파싱 헬퍼(테스트용; 클래스 내부 `internal static`)

- [ ] **Step 1: ParseResponse를 AiRecommendationParser로 추출**

`src/TeampptAddin/Services/AiRecommendationParser.cs` 신규 — `GeminiAiService.cs:283-342`의 `ParseResponse` 본문을 그대로 옮긴다(시그니처를 public static로):
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class AiRecommendationParser
    {
        public static AiRecommendation Parse(
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
            if (paletteObj != null && paletteObj.Type != JTokenType.Null)
            {
                var pid = paletteObj["id"]?.ToString();
                paletteReason = paletteObj["reason"]?.ToString() ?? "";
                matchedPalette = palettes.FirstOrDefault(p =>
                    string.Equals(p.Id, pid, StringComparison.OrdinalIgnoreCase));
            }

            StyleFont matchedFont = null;
            string fontReason = "";
            var fontObj = obj["font"];
            if (fontObj != null && fontObj.Type != JTokenType.Null)
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

- [ ] **Step 2: GeminiAiService.ParseResponse를 위임으로 교체**

`GeminiAiService.cs:283-342`의 `private AiRecommendation ParseResponse(...)` 전체를 아래로 교체:
```csharp
        private AiRecommendation ParseResponse(
            string json, List<HeaderAsset> assets,
            List<StylePalette> palettes, List<StyleFont> fonts)
            => AiRecommendationParser.Parse(json, assets, palettes, fonts);
```

- [ ] **Step 3: 추출 회귀 없음 확인(컴파일 + 기존 테스트)**

`[BUILD-LOGIC]` → `[TEST]` (전체). 기대: `0 Error(s)`, 기존 테스트 PASS.

- [ ] **Step 4: GlmAiService 응답 파싱 헬퍼 실패 테스트 작성**

`src/TeampptAddin.Tests/GlmAiServiceTest.cs`:
```csharp
using TeampptAddin;
using Xunit;

public class GlmAiServiceTest
{
    [Fact]
    public void ExtractContent_ReturnsMessageContent()
    {
        var body = @"{""choices"":[{""message"":{""content"":""{\""x\"":1}""}}],
                      ""usage"":{""prompt_tokens"":5,""completion_tokens"":3,""total_tokens"":8}}";
        Assert.Equal("{\"x\":1}", GlmAiService.ExtractContent(body));
    }

    [Fact]
    public void ExtractContent_ThrowsWhenEmpty()
    {
        Assert.ThrowsAny<System.Exception>(() =>
            GlmAiService.ExtractContent(@"{""choices"":[]}"));
    }
}
```

- [ ] **Step 5: 실패 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~GlmAiServiceTest"`). 기대: 컴파일 실패.

- [ ] **Step 6: GlmAiService 구현**

`src/TeampptAddin/Services/GlmAiService.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>z.ai 무료 GLM-Flash(OpenAI-호환) provider. 텍스트=glm-4.7-flash, 비전=glm-4.6v-flash.</summary>
    public class GlmAiService : IAiService
    {
        private const string Endpoint = "https://api.z.ai/api/paas/v4/chat/completions";
        private static readonly HttpClient Http = new HttpClient();
        private readonly string _apiKey;

        public GlmAiService(string apiKey) { _apiKey = apiKey; }

        public Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, string pngPathOrNull,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
        {
            var imgs = pngPathOrNull == null ? new string[0] : new[] { pngPathOrNull };
            return GenerateJsonAsync(systemPrompt, userText, imgs, responseSchema, temperature, thinkingBudget);
        }

        public async Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, IEnumerable<string> pngPaths,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
        {
            var b64 = (pngPaths ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .Select(p => Convert.ToBase64String(File.ReadAllBytes(p)))
                .ToList();

            var body = GlmRequestBuilder.Build(systemPrompt, userText, b64, responseSchema, temperature, thinkingBudget);
            var resp = await PostAsync(body, b64.Count > 0 ? "vision" : "text").ConfigureAwait(false);
            return ExtractContent(resp);
        }

        public async Task<AiRecommendation> RecommendAsync(
            string userIntent, IEnumerable<HeaderAsset> assets,
            IEnumerable<StylePalette> palettes, IEnumerable<StyleFont> fonts)
        {
            var assetList = assets.ToList();
            var paletteList = palettes.ToList();
            var fontList = fonts.ToList();
            var catalog = CatalogBuilder.Build(assetList);

            var sys = GeminiPromptBuilder.BuildSystemPrompt(catalog, paletteList, fontList);
            var user = GeminiPromptBuilder.BuildUserPrompt(userIntent);

            var json = await GenerateJsonAsync(
                sys, user, (string)null, GeminiPromptBuilder.BuildResponseSchema(), 0.7, 0).ConfigureAwait(false);
            return AiRecommendationParser.Parse(json, assetList, paletteList, fontList);
        }

        public async Task<SlideDiagnosis> DiagnoseSlideAsync(string pngPath)
        {
            var json = await GenerateJsonAsync(
                SlideDiagnosisSchema.BuildSystemPrompt(),
                "이 슬라이드를 개선점 중심으로 진단해줘.",
                pngPath,
                SlideDiagnosisSchema.BuildResponseSchema(), 0.6, 0).ConfigureAwait(false);
            return SlideDiagnosisParser.Parse(json);
        }

        private async Task<string> PostAsync(JObject body, string tag)
        {
            var bodyString = body.ToString(Formatting.None);
            const int maxAttempts = 4; // 무료 티어 레이트리밋 흡수 위해 1회 추가
            HttpResponseMessage response = null;
            string respBody = null;

            Logger.Log($"[GLM] {tag} 호출, model={body["model"]}, keyLen={_apiKey.Length}");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var content = new StringContent(bodyString, Encoding.UTF8, "application/json");
                var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                response = await Http.SendAsync(req).ConfigureAwait(false);
                respBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Logger.Log($"[GLM] attempt {attempt}: HTTP {(int)response.StatusCode}");
                if (response.IsSuccessStatusCode) { LogUsage(respBody); return respBody; }

                var status = (int)response.StatusCode;
                bool transient = status == 503 || status == 429 || status == 500;
                if (transient && attempt < maxAttempts)
                {
                    var delayMs = 800 * (1 << (attempt - 1)); // 800,1600,3200ms
                    Logger.Log($"[GLM] 일시 오류 {status}, {delayMs}ms 후 재시도 ({attempt}/{maxAttempts})");
                    await Task.Delay(delayMs).ConfigureAwait(false);
                    continue;
                }
                throw new HttpRequestException($"GLM API 오류 ({status}): {respBody}");
            }
            throw new InvalidOperationException("GLM 재시도 소진.");
        }

        internal static string ExtractContent(string responseBody)
        {
            var root = JObject.Parse(responseBody);
            var text = root["choices"]?[0]?["message"]?["content"]?.ToString();
            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException($"GLM 응답에 content 없음: {responseBody}");
            return text;
        }

        private static void LogUsage(string responseBody)
        {
            try
            {
                var u = JObject.Parse(responseBody)["usage"];
                if (u == null) return;
                Logger.Log($"[GLM] 토큰: input={u["prompt_tokens"]}, output={u["completion_tokens"]}, total={u["total_tokens"]}");
            }
            catch { }
        }
    }
}
```

- [ ] **Step 7: AiServiceFactory에 GLM 분기 추가**

`AiServiceFactory.cs`의 `CreateGenerative()`를 교체:
```csharp
        public static IAiService CreateGenerative()
        {
            if (AiConfig.UseGlm)
            {
                Logger.Log("[AiFactory] GLM-Flash provider 사용");
                return new GlmAiService(AiConfig.GlmKey);
            }
            Logger.Log("[AiFactory] Gemini provider 사용");
            return new GeminiAiService(AiConfig.GeminiKey);
        }
```

- [ ] **Step 8: 통과 확인**

`[BUILD-LOGIC]` → `[TEST]` (`--filter "FullyQualifiedName~GlmAiServiceTest"`) 후 전체 테스트.
기대: GLM 파싱 2개 PASS, 기존 테스트 회귀 없음.

- [ ] **Step 9: 커밋**

```bash
git add src/TeampptAddin/Services/AiRecommendationParser.cs src/TeampptAddin/Services/GeminiAiService.cs src/TeampptAddin/Services/GlmAiService.cs src/TeampptAddin/Services/AiServiceFactory.cs src/TeampptAddin.Tests/GlmAiServiceTest.cs
git commit -m "feat(glm): GlmAiService(z.ai 호출·재시도·파싱) + 공유 파서 추출 + 팩토리 GLM 분기 (Task 5)"
```

---

### Task 6: 모든 생성 호출 지점을 팩토리로 배선

남은 `new GeminiAiService(...)` 생성 호출 지점(생성 LLM용)을 전부 `AiServiceFactory.CreateGenerative()`로 교체한다. `EmbeddingService`는 손대지 않는다. 시작 시 `AiConfig.Load(assetsDir)`를 1회 호출한다.

**Files:**
- Modify: `src/TeampptAddin/UI/TaskPaneHost.cs:204-205,211-214,219` (+ Load 호출)
- Modify: `src/TeampptAddin/Services/VectorRecommendService.cs:18-20`
- Modify: `src/TeampptAddin/Services/RedesignService.cs:23`
- Modify: `src/TeampptAddin/Services/RecommendationService.cs:16`
- Modify: `src/TeampptAddin/Services/DeckRecommendationOrchestrator.cs:21`
- 점검: `src/TeampptAddin/Services/AssetUnderstandingService.cs:18` (`FromAssetsDir`)

**Interfaces:**
- Consumes: `AiServiceFactory.CreateGenerative()`, `AiConfig.Load(string)`

- [ ] **Step 1: AiConfig.Load를 초기화 진입점에 추가**

`TaskPaneHost.cs` — assetsDir이 정해지는 초기화 지점(현 `gemini`/`supaUrl` 등을 읽는 블록 직전, 대략 `:188` 부근)에 추가. assetsDir 변수명이 다르면 그 변수로:
```csharp
            AiConfig.Load(assetsDir);
```
> 이로써 `AiConfig.GeminiKey`/`GlmKey`/`Provider`가 채워진다. 기존 `gemini` 지역변수(EmbeddingService용)는 그대로 둔다.

- [ ] **Step 2: TaskPaneHost의 생성 LLM 생성자 4곳 교체**

`TaskPaneHost.cs:204-205`:
```csharp
                deckStructure = new DeckStructureService(AiServiceFactory.CreateGenerative());
                conceptSuggester = new ConceptSuggester(AiServiceFactory.CreateGenerative());
```
`TaskPaneHost.cs:213-214`:
```csharp
                deckStructure = new DeckStructureService(AiServiceFactory.CreateGenerative());
                conceptSuggester = new ConceptSuggester(AiServiceFactory.CreateGenerative());
```
`TaskPaneHost.cs:211` 및 `:219`의 `ai = GeminiAiService.FromAssetsDir(assetsDir);`(레거시 로컬 경로) 2곳:
```csharp
                try { ai = AiServiceFactory.CreateGenerative(); }
                catch { ai = new MockAiService(); }
```
> `ai`는 `IAiService`. GLM/Gemini 모두 `RecommendAsync`/`DiagnoseSlideAsync`를 구현하므로 안전. `FromAssetsDir`는 더 이상 이 경로에서 안 쓰이지만 메서드 자체는 보존(다른 참조 없으면 그대로 둠).

- [ ] **Step 3: 내부에서 new GeminiAiService 하던 4개 서비스 교체**

`VectorRecommendService.cs:20`:
```csharp
            _selector = AiServiceFactory.CreateGenerative();
```
필드 타입도 `:13`에서 `private readonly GeminiAiService _selector;` → `private readonly IAiService _selector;`로. (`_embed = new EmbeddingService(geminiKey);`는 그대로.)

`RedesignService.cs:23`:
```csharp
            _gemini = AiServiceFactory.CreateGenerative();
```
필드 타입 `:15` `private readonly GeminiAiService _gemini;` → `private readonly IAiService _gemini;`. (`:25`의 `new EmbeddingService(geminiKey)`는 그대로.)

`RecommendationService.cs:16`:
```csharp
            _gemini = AiServiceFactory.CreateGenerative();
```
필드 타입 `:9` `private readonly GeminiAiService _gemini;` → `private readonly IAiService _gemini;`. (`:20`의 EmbeddingService 그대로.)

`DeckRecommendationOrchestrator.cs:21`:
```csharp
            var gemini = AiServiceFactory.CreateGenerative();
```
이후 이 `gemini`를 `CombinationRecommender`/`DesignCritiqueService` 등에 넘기는 부분은 이미 Task 2에서 생성자가 `IAiService`를 받으므로 호환. (`:25`의 `new EmbeddingService(geminiKey)`는 그대로.)

- [ ] **Step 4: AssetUnderstandingService 점검**

`AssetUnderstandingService.cs:18` `FromAssetsDir`가 내부에서 `GeminiAiService`를 직접 만드는지 확인. 만든다면 인제스트 이해 호출(표의 AssetUnderstandingService 항목)도 GLM 대상이므로 동일하게 `AiServiceFactory.CreateGenerative()`로 교체하되, `AiConfig`가 로딩됐는지 보장(인제스트 경로가 TaskPaneHost를 안 거치면 `FromAssetsDir` 진입부에서 `AiConfig.Load(assetsDir)` 1회 호출 추가). 인제스트가 임베딩만 쓰고 생성 호출이 없으면 변경 불필요 — 코드 확인 후 결정.

- [ ] **Step 5: 컴파일 + 전체 테스트 회귀 확인**

`[BUILD-LOGIC]` → `[TEST]`(전체).
기대: `0 Error(s)`, 모든 테스트 PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/UI/TaskPaneHost.cs src/TeampptAddin/Services/VectorRecommendService.cs src/TeampptAddin/Services/RedesignService.cs src/TeampptAddin/Services/RecommendationService.cs src/TeampptAddin/Services/DeckRecommendationOrchestrator.cs
git commit -m "feat(glm): 모든 생성 LLM 배선을 AiServiceFactory로 교체, 임베딩은 Gemini 유지 (Task 6)"
```

---

### Task 7: 실제 API 라이브 검증 + provider 복귀 테스트 + 세션 동기화

순수 로직은 검증됐지만 z.ai 실호출(특히 flash 모델의 `response_format:json_object` 수용 여부 — docs에 flash가 지원 목록에 없음)은 실제 호출로만 확인된다. 관리자 빌드 후 실제 덱으로 텍스트·비전 경로를 한 번씩 태운다.

**Files:**
- Modify: `api-keys.json` (로컬 전용, 커밋 금지) — `"provider":"glm"`, `"glm":"<무료키>"` 추가
- Modify: `docs/site-data.json`, `docs/PITCH.md`, `PROGRESS-BOARD.md` (세션 종료 동기화)

- [ ] **Step 1: api-keys.json에 GLM 키/ provider 설정**

로컬 `api-keys.json`에 추가(평문 키 커밋 금지 — 이 파일은 .gitignore 대상이어야 함. 아니면 추가):
```json
{ "provider": "glm", "gemini": "<기존 gemini 키>", "glm": "<z.ai 무료 API 키>" }
```
확인: `git status`에 api-keys.json이 안 뜨는지(.gitignore). 뜨면 .gitignore에 추가하고 커밋.

- [ ] **Step 2: 관리자 COM 빌드**

`[BUILD-DEPLOY]` 실행 후 직후 검증 2종(DLL 타임스탬프 1분 이내 + build.log 오류 0건).

- [ ] **Step 3: 텍스트 경로 라이브 검증 (glm-4.7-flash)**

PowerPoint에서 애드인을 띄우고 컨셉 추천 흐름을 실행(ConceptSuggester/DeckStructure 호출 유발). `Logger` 로그 파일에서 확인:
- `[AiFactory] GLM-Flash provider 사용`
- `[GLM] text 호출, model=glm-4.7-flash`
- `[GLM] attempt 1: HTTP 200`
- `[GLM] 토큰: ...`
기대: 컨셉 3안이 정상 표시(파싱 성공). HTTP 4xx(특히 flash 모델이 `response_format:json_object`를 거부)면 Step 6 폴백 적용.

- [ ] **Step 4: 비전 경로 라이브 검증 (glm-4.6v-flash)**

초안 이해/조합 추천/결과 비평 중 이미지가 들어가는 흐름을 1회 실행. 로그 확인:
- `[GLM] vision 호출, model=glm-4.6v-flash`
- `HTTP 200` + 정상 파싱
기대: 비전 응답이 스키마대로 파싱됨. 만약 `glm-4.6v-flash`가 `response_format:json_object`를 거부(4xx)하면 Step 6 폴백.

- [ ] **Step 5: provider 즉시 복귀 검증**

`api-keys.json`의 `"provider"`를 `"gemini"`로 바꾸고 애드인 재시작(빌드 불필요). 로그에 `[AiFactory] Gemini provider 사용` + `[Gemini] ...`가 찍히고 정상 동작하는지 확인. 확인 후 다시 `"glm"`으로 되돌림.

- [ ] **Step 6: (조건부) flash가 response_format을 거부하면 폴백**

z.ai docs의 structured output 지원 모델 목록에 flash가 명시돼 있지 않다. Step 3/4에서 flash 모델이 `response_format:json_object`를 거부(4xx, 보통 "unsupported response_format")하면, `GlmRequestBuilder.Build`에서 `response_format` 키를 **아예 제거**한다(스키마는 이미 system 프롬프트에 임베딩돼 있어 파싱 유지):
```csharp
            var body = new JObject
            {
                ["model"] = hasImages ? VisionModel : TextModel,
                ["messages"] = new JArray { /* system+schema, user */ },
                ["temperature"] = temperature,
                ["thinking"] = new JObject { ["type"] = thinkingBudget > 0 ? "enabled" : "disabled" }
                // response_format 제거 — system 프롬프트의 스키마 지시만으로 JSON 유도
            };
```
이때 `GlmRequestBuilderTest`의 `ResponseFormat_IsJsonObject` 테스트는 삭제하고 `SchemaIsEmbeddedInSystemPrompt`만 유지(프롬프트 강제가 유일한 구조 보장이 되므로). 추가 방어: `GlmAiService.ExtractContent`에서 코드펜스(```json … ```)를 벗겨내는 정리 후 `JObject.Parse` 시도. `[BUILD-LOGIC]`→`[TEST]`로 그린 확인 후 Step 2부터 재검증. (거부가 없으면 이 스텝 건너뜀.)

- [ ] **Step 7: 세션 종료 동기화 (CLAUDE.md 절차)**

- `PROGRESS-BOARD.md`: GLM 전환 완료 반영, 다음 큐 갱신.
- `docs/site-data.json`: `features.llmModels`에 GLM-4.7-Flash/GLM-4.6V-Flash 추가(provider 스위치 가능 명시), `progress` 블록 갱신(`lastUpdated`=2026-06-27, `done` 맨 앞에 이번 항목).
- `docs/PITCH.md`: "비용 0 무료 LLM 전면 적용 + 한 줄 설정으로 Gemini 즉시 복귀" 누적 기록(비전문가 언어).

- [ ] **Step 8: 커밋**

```bash
git add docs/site-data.json docs/PITCH.md PROGRESS-BOARD.md .gitignore
git commit -m "feat(glm): 라이브 검증 완료 + provider 복귀 확인 + 세션 동기화 (Task 7)"
```

---

## Self-Review

**Spec coverage (이전 대화의 호출 지점 표):**
- ConceptSuggester(텍스트) → Task 2 의존 전환 + Task 6 팩토리 배선 ✅
- GeminiAiService.RecommendAsync(레거시) → GlmAiService.RecommendAsync(Task 5) + TaskPaneHost `ai` 배선(Task 6) ✅
- DraftUnderstandingService(PNG+도형) → Task 2/6, 비전 자동선택(Task 4) ✅
- CombinationRecommender(썸네일) → Task 2/6 ✅
- DesignCritiqueService(결과+초안) → Task 2/6 ✅
- AssetUnderstandingService(인제스트) → Task 6 Step 4에서 점검·배선 ✅
- DiagnoseSlideAsync(슬라이드 진단) → GlmAiService.DiagnoseSlideAsync(Task 5) ✅
- EmbeddingService → 명시적으로 변경 제외(Global Constraints + 각 Task에서 "그대로") ✅
- 모듈화/즉시 복귀 → AiConfig.Provider + AiServiceFactory 단일 지점(Task 1/5) + Task 7 Step 5 검증 ✅
- 무료 티어 레이트리밋 → GlmAiService 4회 지수백오프(Task 5) ✅

**Placeholder scan:** "적절히 처리" 류 없음. 조건부 스텝(Task 6 Step4, Task 7 Step6)은 코드 확인/라이브 결과에 따른 분기로, 양쪽 구체 코드 제시함.

**Type consistency:** `IAiService.GenerateJsonAsync` 시그니처가 기존 `GeminiAiService` 구현과 정확히 일치(파라미터·기본값). `AiServiceFactory.CreateGenerative()` 반환 `IAiService`를 모든 소비자(이제 `IAiService` 의존)가 수용. `GlmRequestBuilder.Build` 시그니처가 Task 4 정의와 Task 5 호출에서 동일. `AiRecommendationParser.Parse` 시그니처가 GeminiAiService 위임·GlmAiService 호출에서 동일.

---

**알려진 리스크/메모:**
- z.ai docs는 `response_format`에 `json_object`만 명시(json_schema 미지원)하고 structured output 지원 모델에 flash를 안 적었다 → flash가 `json_object`마저 거부할 가능성 미확정. Task 7 Step 6 폴백(response_format 제거 + 프롬프트 스키마 + 코드펜스 정리)으로 흡수.
- 퀄리티는 gemini-2.5-flash와 동급(업그레이드 아님). 진짜 이득은 비용 0.
- api-keys.json 평문 키는 절대 커밋 금지(.gitignore 확인이 Task 7 Step 1에 포함).
