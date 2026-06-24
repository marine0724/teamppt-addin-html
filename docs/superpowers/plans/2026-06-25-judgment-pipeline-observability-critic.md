# 판단 파이프라인 — 관찰가능성 + 깐깐한 검수자 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 추천·배치 파이프라인의 각 LLM 단계 판단 근거를 전부 가시화(Trace)하고, 구성기를 멀티모달(썸네일)로 만들며, 실무급 루브릭 기반의 깐깐한 검수자가 결과를 채점·병목진단하게 한다.

**Architecture:** 워크플로(에이전트 아님). ① 이해(멀티모달) → ② 검색(벡터) → ③ 구성(멀티모달, 썸네일) → ④ 배치(COM) → ⑤ 검수(렌더+루브릭, 온디맨드). 모든 LLM 단계가 `reasoning`을 응답에 포함 → `RecommendationTrace`로 수집 → UI "🔍 판단 과정" 패널 + 로그.

**Tech Stack:** C# .NET Framework 4.8, WPF, Newtonsoft.Json, Gemini 2.5 Flash(멀티모달 JSON), Supabase(pgvector/Storage), xUnit.

설계 스펙: [2026-06-25 판단 파이프라인 설계](../specs/2026-06-25-judgment-pipeline-observability-critic-design.md)

## Global Constraints

- **사실 = COM / 벡터, 판단 = LLM.** LLM은 텍스트 내용을 생성·수정하지 않는다(검수자도 평가만).
- API 키를 문서·커밋에 평문으로 넣지 않는다.
- 좌표 변환(CoordinateConverter) 폴백 추가 금지.
- 워크플로 유지 — 자동 재검색/재배치 루프 추가 금지(검수자는 진단·보고만).
- 빌드는 COM 등록 때문에 관리자 권한 필요. **단위테스트는 비-UAC 경로**(아래 Test Runner) 사용.
- 토큰 통제: 검수자는 비교용 레퍼런스 이미지 0장(루브릭=텍스트, 결과 렌더 1장만). 구성기 썸네일은 kind별 상위 `ThumbTopK=3`개로 제한.

## Test Runner (비-UAC 단위테스트)

PowerPoint를 먼저 종료. 1순위:

```bash
dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false
```

특정 테스트만: `... -p:RegisterForComInterop=false --filter <TestName>`

폴백(legacy 빌드 오류 시): MSBuild로 본프로젝트 `/p:RegisterForComInterop=false` 빌드 → 테스트프로젝트 `/p:BuildProjectReferences=false` 빌드 → `vstest.console.exe`로 테스트 DLL 실행.

COM/UI가 얽힌 Task(4,5,7,8,9,10)는 단위테스트 불가 → **관리자 빌드**(CLAUDE.md 절차) 후 **PowerPoint 수동 검증**.

### 관리자 빌드 (COM/UI Task 검증용)

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```

빌드 후 검증(생략 금지): ① DLL 타임스탬프 1분 이내 — `stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll`, ② 로그 오류 0건 — `tail -5 c:/Projects/teamppt-addin/build.log`.

---

## File Structure

**신규:**
- `src/TeampptAddin/Models/RecommendationTrace.cs` — 단계별 판단 근거 수집 모델
- `src/TeampptAddin/Models/DesignCritique.cs` — 검수 결과 모델
- `src/TeampptAddin/Services/DesignCritiqueSchema.cs` — 루브릭 시스템 프롬프트 + 응답 스키마
- `src/TeampptAddin/Services/DesignCritiqueParser.cs` — 검수 응답 파싱
- `src/TeampptAddin/Services/DesignCritiqueService.cs` — 검수 오케스트레이터(렌더 입력)
- `src/TeampptAddin.Tests/RecommendationTraceTest.cs`
- `src/TeampptAddin.Tests/DesignCritiqueParserTest.cs`
- `src/TeampptAddin.Tests/DraftReasoningParseTest.cs`

**수정:**
- `src/TeampptAddin/Models/DraftModels.cs` — `DraftUnderstanding.Reasoning` 추가
- `src/TeampptAddin/Services/DraftUnderstandingSchema.cs` — reasoning 필드+프롬프트
- `src/TeampptAddin/Services/DraftUnderstandingParser.cs` — reasoning 파싱
- `src/TeampptAddin/Models/RecommendationModels.cs` — `CombinationRecommendation.Reasoning`
- `src/TeampptAddin/Services/CombinationRecommenderSchema.cs` — reasoning 필드
- `src/TeampptAddin/Services/CombinationRecommenderParser.cs` — reasoning 파싱
- `src/TeampptAddin/Services/GeminiAiService.cs` — 다중 이미지 GenerateJsonAsync 오버로드
- `src/TeampptAddin/Services/CombinationCandidateProvider.cs` — 썸네일 로컬 다운로드
- `src/TeampptAddin/Services/CombinationRecommender.cs` — 멀티모달(썸네일) + reasoning
- `src/TeampptAddin/Services/RecommendationService.cs` — Trace 조립·반환, 검수 진입점
- `src/TeampptAddin.Tests/CombinationRecommenderParserTest.cs` — reasoning 단언 추가
- `src/TeampptAddin/UI/Wpf/AssetPanel.cs` — Trace 패널 + 검수 버튼 + 배치후 캡처·검수 배선

---

## Task 1: RecommendationTrace + DesignCritique 모델

**Files:**
- Create: `src/TeampptAddin/Models/RecommendationTrace.cs`
- Create: `src/TeampptAddin/Models/DesignCritique.cs`
- Test: `src/TeampptAddin.Tests/RecommendationTraceTest.cs`

**Interfaces:**
- Produces:
  - `RecommendationTrace { UnderstandReasoning:string, RetrieveLines:List<string>, ComposeReasoning:string, Unmet:List<string>, Critique:DesignCritique }`
  - `DesignCritique { Score:int, DimensionScores:Dictionary<string,int>, Verdict:string, Bottleneck:string, Suggestion:string, Reasoning:string }`
  - `RecommendationTrace.ToReadableLines():List<string>` — UI/로그 공용 사람이 읽는 줄 목록.

- [ ] **Step 1: Write the failing test**

```csharp
// src/TeampptAddin.Tests/RecommendationTraceTest.cs
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class RecommendationTraceTest
    {
        [Fact]
        public void ToReadableLines_Includes_Each_Stage()
        {
            var t = new RecommendationTrace
            {
                UnderstandReasoning = "본문, 비전 설명형",
                RetrieveLines = new List<string> { "header 5(sim 0.70~0.72)" },
                ComposeReasoning = "layout 선택, component 미충족",
                Unmet = new List<string> { "component" },
                Critique = new DesignCritique
                {
                    Score = 78, Verdict = "여백 답답", Bottleneck = "에셋",
                    Suggestion = "차트 외 컴포넌트 확보", Reasoning = "위계는 좋음"
                }
            };

            var text = string.Join("\n", t.ToReadableLines());

            Assert.Contains("본문, 비전 설명형", text);
            Assert.Contains("header 5(sim 0.70~0.72)", text);
            Assert.Contains("component", text);
            Assert.Contains("78", text);
            Assert.Contains("에셋", text);
        }

        [Fact]
        public void ToReadableLines_Omits_Critique_When_Null()
        {
            var t = new RecommendationTrace { UnderstandReasoning = "x", ComposeReasoning = "y" };
            var lines = t.ToReadableLines();
            Assert.DoesNotContain(lines, l => l.Contains("검수"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter RecommendationTraceTest`
Expected: FAIL — `RecommendationTrace` / `DesignCritique` 형식 없음.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/TeampptAddin/Models/DesignCritique.cs
using System.Collections.Generic;

namespace TeampptAddin
{
    public class DesignCritique
    {
        public int Score { get; set; }
        public Dictionary<string, int> DimensionScores { get; set; } = new Dictionary<string, int>();
        public string Verdict { get; set; } = "";
        public string Bottleneck { get; set; } = "";   // 기능 / 데이터스키마 / 에셋
        public string Suggestion { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }
}
```

```csharp
// src/TeampptAddin/Models/RecommendationTrace.cs
using System.Collections.Generic;

namespace TeampptAddin
{
    public class RecommendationTrace
    {
        public string UnderstandReasoning { get; set; } = "";
        public List<string> RetrieveLines { get; set; } = new List<string>();
        public string ComposeReasoning { get; set; } = "";
        public List<string> Unmet { get; set; } = new List<string>();
        public DesignCritique Critique { get; set; }   // 검수 실행 시만 채워짐

        public List<string> ToReadableLines()
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(UnderstandReasoning))
                lines.Add("① 이해: " + UnderstandReasoning);
            foreach (var r in RetrieveLines)
                lines.Add("② 검색: " + r);
            if (!string.IsNullOrEmpty(ComposeReasoning))
                lines.Add("③ 구성: " + ComposeReasoning);
            if (Unmet != null && Unmet.Count > 0)
                lines.Add("   미충족: " + string.Join(", ", Unmet));
            if (Critique != null)
            {
                lines.Add($"⑤ 검수: {Critique.Score}점 — {Critique.Verdict}");
                if (!string.IsNullOrEmpty(Critique.Bottleneck))
                    lines.Add("   병목: " + Critique.Bottleneck + " · " + Critique.Suggestion);
            }
            return lines;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter RecommendationTraceTest`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TeampptAddin/Models/RecommendationTrace.cs src/TeampptAddin/Models/DesignCritique.cs src/TeampptAddin.Tests/RecommendationTraceTest.cs
git commit -m "feat(trace): RecommendationTrace + DesignCritique 모델"
```

---

## Task 2: ① 이해 단계에 reasoning 추가

**Files:**
- Modify: `src/TeampptAddin/Models/DraftModels.cs:39-50`
- Modify: `src/TeampptAddin/Services/DraftUnderstandingSchema.cs:42-60` (properties+required), `:63-82` (prompt)
- Modify: `src/TeampptAddin/Services/DraftUnderstandingParser.cs:44-56`
- Test: `src/TeampptAddin.Tests/DraftReasoningParseTest.cs`

**Interfaces:**
- Consumes: 기존 `DraftUnderstandingParser.Parse(string json, DraftProfile profile)`.
- Produces: `DraftUnderstanding.Reasoning:string` (왜 이 의도·조합으로 판단했는지).

- [ ] **Step 1: Write the failing test**

```csharp
// src/TeampptAddin.Tests/DraftReasoningParseTest.cs
using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftReasoningParseTest
    {
        [Fact]
        public void Parses_Reasoning_Field()
        {
            const string json = @"{
              ""materials"":[], ""counts"":{""textBlocks"":1,""bullets"":0,""images"":0,""tables"":0,""charts"":0},
              ""layoutShape"":""title-top"", ""designSummary"":""x"", ""dominantColors"":[],
              ""matchIntent"":""비전 설명"", ""slideKind"":""body"", ""purpose"":""비전 제시"",
              ""neededCombination"":{""slide"":0,""header"":1,""layout"":1,""component"":2},
              ""reasoning"":""좌측 텍스트 우측 시각이 필요해 header+layout 조합으로 판단""
            }";
            var u = DraftUnderstandingParser.Parse(json, new DraftProfile());
            Assert.Equal("좌측 텍스트 우측 시각이 필요해 header+layout 조합으로 판단", u.Reasoning);
        }
    }
}
```

(`DraftProfile`이 빈 생성자를 허용하는지 확인. 불가하면 기존 다른 테스트가 쓰는 생성 방식을 따른다 — `grep -rn "new DraftProfile" src/TeampptAddin.Tests`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter DraftReasoningParseTest`
Expected: FAIL — `DraftUnderstanding.Reasoning` 없음(컴파일 에러).

- [ ] **Step 3: Write minimal implementation**

`DraftModels.cs` — `DraftUnderstanding` 클래스에 추가(49행 `NeededCombination` 다음):

```csharp
        public string Reasoning { get; set; } = "";
```

`DraftUnderstandingSchema.cs` — `BuildResponseSchema()`의 properties에 `purpose` 옆에 추가, required에 `reasoning` 추가:

```csharp
                    ["purpose"] = Str(),
                    ["reasoning"] = Str(),
```
required 배열 끝에 `, "reasoning"` 추가.

프롬프트(`BuildSystemPrompt`) "네 일" 목록 끝에 한 줄 추가:

```
- reasoning: 위 판단(특히 slideKind·neededCombination)을 *왜* 그렇게 내렸는지 한두 문장. 근거가 보이게.
```

`DraftUnderstandingParser.cs` — Parse 객체 초기화에 추가(`Purpose` 다음):

```csharp
                Purpose = o["purpose"]?.ToString() ?? "",
                Reasoning = o["reasoning"]?.ToString() ?? "",
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter DraftReasoningParseTest`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/TeampptAddin/Models/DraftModels.cs src/TeampptAddin/Services/DraftUnderstandingSchema.cs src/TeampptAddin/Services/DraftUnderstandingParser.cs src/TeampptAddin.Tests/DraftReasoningParseTest.cs
git commit -m "feat(trace): 이해 단계 reasoning 필드 추가"
```

---

## Task 3: ③ 구성 단계에 reasoning 추가

**Files:**
- Modify: `src/TeampptAddin/Models/RecommendationModels.cs:12-21`
- Modify: `src/TeampptAddin/Services/CombinationRecommenderSchema.cs:21-32` (properties+required), `:35-46` (prompt)
- Modify: `src/TeampptAddin/Services/CombinationRecommenderParser.cs:11-24`
- Test: `src/TeampptAddin.Tests/CombinationRecommenderParserTest.cs` (단언 추가)

**Interfaces:**
- Consumes: `CombinationRecommenderParser.Parse(string llmJson, Dictionary<string,List<HeaderAsset>>)`.
- Produces: `CombinationRecommendation.Reasoning:string`.

- [ ] **Step 1: Write the failing test** — 기존 `Maps_Header_Layout_Components_From_Pool` 테스트의 JSON에 `"reasoning"` 추가하고 단언 추가:

기존 테스트의 `llm` 상수 끝(`""unmet"":[]` 앞)에 `"reasoning":"본문 항목 나열이라 좌측번호 레이아웃 선택",` 삽입하고, 메서드 끝에 추가:

```csharp
            Assert.Equal("본문 항목 나열이라 좌측번호 레이아웃 선택", r.Reasoning);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter CombinationRecommenderParserTest`
Expected: FAIL — `CombinationRecommendation.Reasoning` 없음(컴파일 에러).

- [ ] **Step 3: Write minimal implementation**

`RecommendationModels.cs` — `CombinationRecommendation`에 추가(`Unmet` 다음):

```csharp
        public string Reasoning { get; set; } = "";
```

`CombinationRecommenderSchema.cs` — `BuildResponseSchema()` properties에 추가:

```csharp
                    ["unmet"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } },
                    ["reasoning"] = new JObject { ["type"] = "string" }
```
required를 `new JArray { "components", "unmet", "reasoning" }`로.

프롬프트 규칙 끝에 한 줄:

```
- reasoning: 이 조합을 고른 이유와, 특정 종류를 미충족 처리했다면 *왜* 그런지 한두 문장.
```

`CombinationRecommenderParser.cs` — `Parse`에서 `rec.Unmet = ...` 다음에:

```csharp
            rec.Reasoning = o["reasoning"]?.ToString() ?? "";
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter CombinationRecommenderParserTest`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TeampptAddin/Models/RecommendationModels.cs src/TeampptAddin/Services/CombinationRecommenderSchema.cs src/TeampptAddin/Services/CombinationRecommenderParser.cs src/TeampptAddin.Tests/CombinationRecommenderParserTest.cs
git commit -m "feat(trace): 구성 단계 reasoning 필드 추가"
```

---

## Task 4: Gemini 다중 이미지 GenerateJsonAsync 오버로드

**Files:**
- Modify: `src/TeampptAddin/Services/GeminiAiService.cs:219-264`

**Interfaces:**
- Produces: `Task<string> GenerateJsonAsync(string systemPrompt, string userText, IEnumerable<string> pngPaths, JObject responseSchema, double temperature = 0.4)` — 여러 이미지를 parts에 순서대로 inline_data로 추가.
- 기존 단일 `string pngPathOrNull` 시그니처는 유지(이 오버로드로 위임).

이 Task는 API 호출이라 단위테스트 불가 → 컴파일/빌드로 검증, 실사용은 Task 5에서.

- [ ] **Step 1: 다중 이미지 오버로드 추가**

`GeminiAiService.cs`에서 기존 `GenerateJsonAsync(string systemPrompt, string userText, string pngPathOrNull, ...)` 본문 시작부의 parts 구성을 다음으로 교체하고, 새 오버로드를 추가한다.

기존 단일 메서드를 이렇게 위임형으로:

```csharp
        public Task<string> GenerateJsonAsync(string systemPrompt, string userText, string pngPathOrNull, JObject responseSchema, double temperature = 0.4)
        {
            var imgs = pngPathOrNull == null ? new string[0] : new[] { pngPathOrNull };
            return GenerateJsonAsync(systemPrompt, userText, imgs, responseSchema, temperature);
        }

        public async Task<string> GenerateJsonAsync(string systemPrompt, string userText, IEnumerable<string> pngPaths, JObject responseSchema, double temperature = 0.4)
        {
            var parts = new JArray();
            if (pngPaths != null)
                foreach (var p in pngPaths)
                {
                    if (string.IsNullOrEmpty(p) || !File.Exists(p)) continue;
                    parts.Add(new JObject { ["inline_data"] = new JObject {
                        ["mime_type"] = "image/png",
                        ["data"] = Convert.ToBase64String(File.ReadAllBytes(p)) } });
                }
            parts.Add(new JObject { ["text"] = userText });
```

그 아래(requestBody 구성부터 `return text;`까지)는 기존 본문을 그대로 둔다. **주의:** 기존 메서드의 `var parts = ...` ~ `parts.Add(text)` 부분만 위 오버로드로 옮기고, 단일 메서드에는 위임 코드만 남긴다(중복 제거).

- [ ] **Step 2: 관리자 빌드 + 검증**

위 "관리자 빌드" 실행 후: DLL 타임스탬프 1분 이내 + `tail -5 build.log` 오류 0건.

- [ ] **Step 3: Commit**

```bash
git add src/TeampptAddin/Services/GeminiAiService.cs
git commit -m "feat(gemini): 다중 이미지 GenerateJsonAsync 오버로드"
```

---

## Task 5: ③ 구성기 멀티모달화 (썸네일 입력)

**Files:**
- Modify: `src/TeampptAddin/Services/CombinationCandidateProvider.cs:42-70`
- Modify: `src/TeampptAddin/Services/CombinationRecommender.cs:18-66`
- Modify: `src/TeampptAddin/Services/RecommendationService.cs:18-44`

**Interfaces:**
- Consumes: `RemoteAssetCache.GetThumbAsync(string remoteThumb):Task<string>`(로컬 png 경로), `HeaderAsset.Extra["remote_thumb"]`, `GeminiAiService.GenerateJsonAsync(..., IEnumerable<string> pngPaths, ...)`(Task 4).
- Produces: 구성기가 후보 썸네일을 본다. `CombinationRecommender.RecommendAsync`가 `ThumbTopK=3` 기준으로 kind별 상위 후보 썸네일을 모아 멀티모달 호출.

이 Task는 COM/네트워크 통합 → PowerPoint 수동 검증.

- [ ] **Step 1: CandidateProvider가 썸네일을 로컬 다운로드**

`CombinationCandidateProvider` 생성자에 `RemoteAssetCache`를 추가하고, `GetCandidatesAsync`에서 각 후보의 `Extra["remote_thumb"]`를 `GetThumbAsync`로 받아 `Extra["local_thumb"]`(JToken 문자열)로 저장.

`CombinationCandidateProvider.cs`:

```csharp
        private readonly EmbeddingService _embed;
        private readonly SupabaseClient _supa;
        private readonly RemoteAssetCache _thumbs;
        private readonly RecommendationCache _cache = new RecommendationCache();

        public CombinationCandidateProvider(EmbeddingService embed, SupabaseClient supa, RemoteAssetCache thumbs)
        { _embed = embed; _supa = supa; _thumbs = thumbs; }
```

`GetCandidatesAsync`의 `result[kind] = list;` 다음에:

```csharp
                    foreach (var a in list)
                    {
                        if (a.Extra != null && a.Extra.TryGetValue("remote_thumb", out var rt) && _thumbs != null)
                        {
                            try { a.Extra["local_thumb"] = await _thumbs.GetThumbAsync(rt.ToString()).ConfigureAwait(false); }
                            catch (Exception tex) { Logger.Log($"[Combo] 썸네일 실패 {a.File}: {tex.Message}"); }
                        }
                    }
```

- [ ] **Step 2: RecommendationService가 RemoteAssetCache를 주입**

`RecommendationService.cs` 생성자에서 `RemoteAssetCache`를 만들어 provider에 전달:

```csharp
            var supa = new SupabaseClient(supabaseUrl, anonKey);
            _candidates = new CombinationCandidateProvider(
                new EmbeddingService(geminiKey), supa, new RemoteAssetCache(supabaseUrl, anonKey));
```

- [ ] **Step 3: 구성기가 썸네일을 멀티모달로 전달**

`CombinationRecommender.cs`의 LLM 호출부를 다중 이미지로 교체. `RecommendAsync`:

```csharp
            var userText = BuildUserText(u, candidatesByKind);
            Logger.Log("[Reco] userText↓\r\n" + userText);

            var thumbs = CollectThumbs(candidatesByKind, 3);
            var json = await _gemini.GenerateJsonAsync(
                CombinationRecommenderSchema.BuildSystemPrompt(),
                userText, thumbs,
                CombinationRecommenderSchema.BuildResponseSchema()).ConfigureAwait(false);

            Logger.Log("[Reco] raw↓ " + json);
            var rec = CombinationRecommenderParser.Parse(json, candidatesByKind);
```

그리고 헬퍼 추가(클래스 내부):

```csharp
        // kind별 상위 thumbTopK개의 로컬 썸네일 경로 수집. userText의 file= 와 짝이 맞게 순서 유지.
        public static List<string> CollectThumbs(Dictionary<string, List<HeaderAsset>> pool, int thumbTopK)
        {
            var paths = new List<string>();
            foreach (var kind in pool.Keys)
                foreach (var a in pool[kind].Take(thumbTopK))
                    if (a.Extra != null && a.Extra.TryGetValue("local_thumb", out var lt) && !string.IsNullOrEmpty(lt?.ToString()))
                        paths.Add(lt.ToString());
            return paths;
        }
```

`BuildUserText`에서 각 후보 줄 앞에 표식(예: `[썸네일 N]`)을 붙여 LLM이 이미지와 텍스트를 연결하게 한다. `pool[kind]` 루프를 인덱스 기반으로 바꿔, `thumbTopK`(=3) 이내 후보에 `[썸네일 {n}]` 접두사를 추가(순서는 `CollectThumbs`와 동일). 프롬프트 시스템 텍스트에 "이미지는 후보 [썸네일 N] 순서대로 첨부됨. 실제 생김새를 보고 어울림·미려함을 판단하라." 한 줄 추가(`CombinationRecommenderSchema.BuildSystemPrompt`).

- [ ] **Step 4: 관리자 빌드 + 검증**

관리자 빌드 → DLL 타임스탬프 + 로그 0건.

- [ ] **Step 5: PowerPoint 수동 검증**

PowerPoint 재시작 → 본문 슬라이드에서 "AI 리디자인" → 추천. `debug.log`에서:
- `[Combo] kind=... 후보 N개` 뒤 썸네일 다운로드 흔적(`[RemoteCache] GET thumb/...`),
- `[Reco] raw↓`의 reasoning이 *생김새*를 언급하는지(예: "여백이 넓어 보임").

기대: 이전과 다른(이미지 근거가 반영된) 선택/reasoning. 오류 없이 추천 카드 표시.

- [ ] **Step 6: Commit**

```bash
git add src/TeampptAddin/Services/CombinationCandidateProvider.cs src/TeampptAddin/Services/CombinationRecommender.cs src/TeampptAddin/Services/RecommendationService.cs
git commit -m "feat(compose): 구성기 멀티모달화 — 후보 썸네일 입력"
```

---

## Task 6: 검수자 스키마(루브릭) + 파서

**Files:**
- Create: `src/TeampptAddin/Services/DesignCritiqueSchema.cs`
- Create: `src/TeampptAddin/Services/DesignCritiqueParser.cs`
- Test: `src/TeampptAddin.Tests/DesignCritiqueParserTest.cs`

**Interfaces:**
- Produces:
  - `DesignCritiqueSchema.BuildSystemPrompt():string`(페르소나+루브릭), `BuildResponseSchema():JObject`.
  - `DesignCritiqueParser.Parse(string json):DesignCritique`.

- [ ] **Step 1: Write the failing test**

```csharp
// src/TeampptAddin.Tests/DesignCritiqueParserTest.cs
using Xunit;

namespace TeampptAddin.Tests
{
    public class DesignCritiqueParserTest
    {
        [Fact]
        public void Parses_Score_Dimensions_Bottleneck()
        {
            const string json = @"{
              ""score"":78,
              ""dimensionScores"":{""정렬"":18,""여백"":9,""위계"":18,""색"":12,""타이포"":8,""의도부합"":13},
              ""verdict"":""위계는 좋으나 여백이 답답"",
              ""bottleneck"":""에셋"",
              ""suggestion"":""차트 외 컴포넌트 에셋 확보"",
              ""reasoning"":""원본 에셋만큼은 나오지만 종류가 차트뿐""
            }";
            var c = DesignCritiqueParser.Parse(json);
            Assert.Equal(78, c.Score);
            Assert.Equal(18, c.DimensionScores["위계"]);
            Assert.Equal("에셋", c.Bottleneck);
            Assert.Equal("차트 외 컴포넌트 에셋 확보", c.Suggestion);
            Assert.Contains("차트", c.Reasoning);
        }

        [Fact]
        public void Defaults_When_Fields_Missing()
        {
            var c = DesignCritiqueParser.Parse(@"{""score"":50}");
            Assert.Equal(50, c.Score);
            Assert.Equal("", c.Bottleneck);
            Assert.NotNull(c.DimensionScores);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter DesignCritiqueParserTest`
Expected: FAIL — 형식 없음.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/TeampptAddin/Services/DesignCritiqueParser.cs
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DesignCritiqueParser
    {
        public static DesignCritique Parse(string json)
        {
            var o = JObject.Parse(json);
            var c = new DesignCritique
            {
                Score = o["score"]?.Value<int>() ?? 0,
                Verdict = o["verdict"]?.ToString() ?? "",
                Bottleneck = o["bottleneck"]?.ToString() ?? "",
                Suggestion = o["suggestion"]?.ToString() ?? "",
                Reasoning = o["reasoning"]?.ToString() ?? ""
            };
            if (o["dimensionScores"] is JObject dim)
                foreach (var p in dim.Properties())
                    c.DimensionScores[p.Name] = p.Value?.Value<int>() ?? 0;
            return c;
        }
    }
}
```

```csharp
// src/TeampptAddin/Services/DesignCritiqueSchema.cs
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DesignCritiqueSchema
    {
        public static JObject BuildResponseSchema()
        {
            JObject Str() => new JObject { ["type"] = "string" };
            JObject Int() => new JObject { ["type"] = "integer" };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["score"] = Int(),
                    ["dimensionScores"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["정렬"] = Int(), ["여백"] = Int(), ["위계"] = Int(),
                            ["색"] = Int(), ["타이포"] = Int(), ["의도부합"] = Int()
                        },
                        ["required"] = new JArray { "정렬", "여백", "위계", "색", "타이포", "의도부합" }
                    },
                    ["verdict"] = Str(),
                    ["bottleneck"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "기능", "데이터스키마", "에셋" } },
                    ["suggestion"] = Str(),
                    ["reasoning"] = Str()
                },
                ["required"] = new JArray { "score", "dimensionScores", "verdict", "bottleneck", "suggestion", "reasoning" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 실무 10년차 PPT 에이전시 시니어 디자이너다. 매우 깐깐하다.
배치된 슬라이드 렌더 1장과 초안 렌더를 보고, 아래 루브릭으로 채점한다. 후한 점수 금지.

## 루브릭 (합계 100)
- 정렬·격자 (20): 요소가 보이지 않는 격자에 맞나
- 여백·호흡 (15): 답답하지 않나, 여백이 의도적인가
- 위계 (20): 제목>소제목>본문 시선 흐름이 명확한가
- 색 대비·조화 (15): 배경/텍스트 대비, 팔레트 일관성
- 타이포 (10): 폰트 위계·자간·줄간격
- 초안 의도 부합 (20): 이 슬라이드가 의도(purpose)를 실제로 잘 수행하나
실무급=80↑ · 평범=60~79 · 미달=<60. dimensionScores는 각 차원 배점 내 정수.
(주의: dimensionScores 키는 정렬/여백/위계/색/타이포/의도부합. score는 6개 합과 대략 일치.)

## 병목 진단 (bottleneck: 기능 / 데이터스키마 / 에셋 중 하나)
- 결과물이 재료가 된 원본 에셋들보다 못나 보이면 → '기능'(배치·조립 문제).
- 원본 에셋만큼은 나오는데 그래도 별로면 → '에셋'(에셋 천장).
- 의도·구성이 애초에 어긋났으면 → '데이터스키마'(이해·분류 문제).
suggestion: 그 병목을 풀려면 다음에 뭘 해야 하는지 한 줄.
reasoning: 위 판단 근거 한두 문장. 텍스트 내용을 생성·수정하지 마라(평가만).";
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false --filter DesignCritiqueParserTest`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TeampptAddin/Services/DesignCritiqueSchema.cs src/TeampptAddin/Services/DesignCritiqueParser.cs src/TeampptAddin.Tests/DesignCritiqueParserTest.cs
git commit -m "feat(critique): 검수자 루브릭 스키마 + 파서"
```

---

## Task 7: 검수자 오케스트레이터 (DesignCritiqueService)

**Files:**
- Create: `src/TeampptAddin/Services/DesignCritiqueService.cs`

**Interfaces:**
- Consumes: `GeminiAiService.GenerateJsonAsync(systemPrompt, userText, IEnumerable<string> pngPaths, schema)`(Task 4), `DesignCritiqueSchema`(Task 6), `DesignCritiqueParser`(Task 6).
- Produces: `Task<DesignCritique> CritiqueAsync(string resultPngPath, string draftPngPath, DraftUnderstanding u, CombinationRecommendation rec)`.

API 호출 → 단위테스트 불가, 빌드로 검증(실사용 Task 10).

- [ ] **Step 1: 서비스 작성**

```csharp
// src/TeampptAddin/Services/DesignCritiqueService.cs
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 배치된 슬라이드 렌더를 실무급 루브릭으로 채점하고 병목(기능/스키마/에셋)을 진단.
    /// 비교용 레퍼런스 이미지 없음(루브릭=텍스트). 결과 렌더 + 초안 렌더 2장만 본다.
    /// </summary>
    public class DesignCritiqueService
    {
        private readonly GeminiAiService _gemini;
        public DesignCritiqueService(GeminiAiService gemini) { _gemini = gemini; }

        public async Task<DesignCritique> CritiqueAsync(
            string resultPngPath, string draftPngPath, DraftUnderstanding u, CombinationRecommendation rec)
        {
            var userText =
                $"초안 의도(purpose): {u?.Purpose}\n" +
                $"적용된 조합: header={rec?.Header?.Asset?.Name}, layout={rec?.Layout?.Asset?.Name}, " +
                $"components={rec?.Components?.Count ?? 0}, 미충족={string.Join("/", rec?.Unmet ?? new List<string>())}\n" +
                "첫 이미지=배치 결과 슬라이드, 둘째 이미지=초안. 결과를 채점하라.";

            var imgs = new List<string>();
            if (!string.IsNullOrEmpty(resultPngPath)) imgs.Add(resultPngPath);
            if (!string.IsNullOrEmpty(draftPngPath)) imgs.Add(draftPngPath);

            var json = await _gemini.GenerateJsonAsync(
                DesignCritiqueSchema.BuildSystemPrompt(), userText, imgs,
                DesignCritiqueSchema.BuildResponseSchema()).ConfigureAwait(false);
            Logger.Log("[Critique] raw↓ " + json);
            return DesignCritiqueParser.Parse(json);
        }
    }
}
```

- [ ] **Step 2: 관리자 빌드 + 검증** (DLL 타임스탬프 + 로그 0건)

- [ ] **Step 3: Commit**

```bash
git add src/TeampptAddin/Services/DesignCritiqueService.cs
git commit -m "feat(critique): 검수 오케스트레이터(렌더+루브릭)"
```

---

## Task 8: RecommendationService가 Trace 조립·반환

**Files:**
- Modify: `src/TeampptAddin/Services/RecommendationService.cs:27-44`
- Modify: `src/TeampptAddin/Services/CombinationCandidateProvider.cs` (검색 줄 수집 노출)

**Interfaces:**
- Produces:
  - `RecommendationResult { Recommendation:CombinationRecommendation, Trace:RecommendationTrace, DraftPngPath:string, Understanding:DraftUnderstanding }`.
  - `RecommendationService.RunAsync(Action<string> progress):Task<RecommendationResult>` (반환 타입 변경).
  - `RecommendationService.CritiqueAsync(string resultPng, RecommendationResult prior):Task<DesignCritique>` (온디맨드 검수).

COM 얽힘 → 빌드 + 수동 검증.

- [ ] **Step 1: 검색 줄을 trace용으로 노출**

`CombinationCandidateProvider`에 필드 `public List<string> LastRetrieveLines { get; } = new List<string>();` 추가. `GetCandidatesAsync` 시작에서 `LastRetrieveLines.Clear();`, kind 루프에서 `Logger.Log($"[Combo] kind={kind} 후보 {list.Count}개");` 다음에:

```csharp
                    var sims = string.Join(", ", list.Take(3).Select(a =>
                        a.Extra != null && a.Extra.TryGetValue("similarity", out var s) ? s.ToString() : "?"));
                    LastRetrieveLines.Add($"{kind} {list.Count}개");
```

(유사도 값을 후보에 싣고 싶으면 `MatchQuery.ParseResults`에서 `Extra["similarity"]` 저장 — 선택. 최소 구현은 개수만.)

- [ ] **Step 2: RunAsync가 RecommendationResult 반환**

`RecommendationService.cs`:

```csharp
        public async Task<RecommendationResult> RunAsync(Action<string> progress)
        {
            progress("초안 읽는 중…");
            var profile = DraftSlideReader.ReadCurrentSlide();
            if (profile == null) throw new InvalidOperationException("활성 슬라이드를 찾을 수 없습니다.");

            var png = SlideCaptureService.CaptureCurrentSlide()?.PngPath;

            progress("초안 이해하는 중…");
            var u = await _understand.UnderstandAsync(profile, png);

            progress("어울리는 에셋 후보 찾는 중…");
            var pool = await _candidates.GetCandidatesAsync(u);

            progress("조합 고르는 중…");
            var rec = await _recommender.RecommendAsync(u, pool);

            var trace = new RecommendationTrace
            {
                UnderstandReasoning = u.Reasoning,
                RetrieveLines = _candidates.LastRetrieveLines,
                ComposeReasoning = rec.Reasoning,
                Unmet = rec.Unmet
            };
            foreach (var line in trace.ToReadableLines()) Logger.Log("[Trace] " + line);

            return new RecommendationResult
            {
                Recommendation = rec, Trace = trace, DraftPngPath = png, Understanding = u
            };
        }

        public async Task<DesignCritique> CritiqueAsync(string resultPng, RecommendationResult prior)
        {
            var critic = new DesignCritiqueService(_gemini);
            var c = await critic.CritiqueAsync(resultPng, prior.DraftPngPath, prior.Understanding, prior.Recommendation);
            prior.Trace.Critique = c;
            foreach (var line in prior.Trace.ToReadableLines()) Logger.Log("[Trace] " + line);
            return c;
        }
```

`RecommendationResult` 클래스는 `RecommendationModels.cs`에 추가:

```csharp
    public class RecommendationResult
    {
        public CombinationRecommendation Recommendation { get; set; }
        public RecommendationTrace Trace { get; set; }
        public string DraftPngPath { get; set; }
        public DraftUnderstanding Understanding { get; set; }
    }
```

- [ ] **Step 3: AssetPanel 호출부 시그니처 맞추기(컴파일)**

`AssetPanel.RunRecommendationAsync`에서 `var rec = await _recommend.RunAsync(...)` → `var result = await _recommend.RunAsync(...)`, `ShowRecommendation(result.Recommendation)`로. `_lastRecommendation`과 함께 `_lastRecoResult = result;` 저장(필드 `private RecommendationResult _lastRecoResult;` 추가). (Task 10에서 검수에 사용.)

- [ ] **Step 4: 관리자 빌드 + 검증 + PowerPoint 수동 검증**

추천 실행 후 `debug.log`에 `[Trace] ① 이해: …`, `[Trace] ③ 구성: …` 줄이 보이는지.

- [ ] **Step 5: Commit**

```bash
git add src/TeampptAddin/Services/RecommendationService.cs src/TeampptAddin/Services/CombinationCandidateProvider.cs src/TeampptAddin/Models/RecommendationModels.cs src/TeampptAddin/UI/Wpf/AssetPanel.cs
git commit -m "feat(trace): RecommendationService Trace 조립·반환 + 온디맨드 검수 진입점"
```

---

## Task 9: "🔍 판단 과정" 패널 UI

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (`ShowRecommendation` 끝)

**Interfaces:**
- Consumes: `_lastRecoResult.Trace.ToReadableLines()`.

WPF UI → 수동 검증.

- [ ] **Step 1: 접이식 패널 추가**

`ShowRecommendation`에서 `_chatStack.Children.Add(BuildPlaceArrangeButton());` 앞에 trace 패널을 추가하는 헬퍼 호출 `_chatStack.Children.Add(BuildTracePanel());` 삽입. 헬퍼:

```csharp
        private System.Windows.UIElement BuildTracePanel()
        {
            var lines = _lastRecoResult?.Trace?.ToReadableLines() ?? new List<string>();
            var body = new StackPanel { Margin = new Thickness(14, 2, 12, 2), Visibility = Visibility.Collapsed };
            foreach (var l in lines)
                body.Children.Add(new TextBlock
                {
                    Text = l, FontSize = 10, Foreground = ThemeResources.TextSub,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1)
                });

            var header = new TextBlock
            {
                Text = "🔍 판단 과정 (펼치기)", FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub, Cursor = Cursors.Hand,
                Margin = new Thickness(14, 6, 12, 0)
            };
            header.MouseLeftButtonUp += (s, e) =>
            {
                body.Visibility = body.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                header.Text = body.Visibility == Visibility.Visible ? "🔍 판단 과정 (접기)" : "🔍 판단 과정 (펼치기)";
            };

            var wrap = new StackPanel();
            wrap.Children.Add(header);
            wrap.Children.Add(body);
            return wrap;
        }
```

- [ ] **Step 2: 관리자 빌드 + 검증 + PowerPoint 수동 검증**

추천 카드 아래 "🔍 판단 과정 (펼치기)" 보이고, 클릭 시 단계별 reasoning 펼쳐짐.

- [ ] **Step 3: Commit**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs
git commit -m "feat(ui): 🔍 판단 과정 접이식 패널"
```

---

## Task 10: 배치 후 캡처 + "디자이너 검수 받기" 버튼

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (`PlaceOnNewSlide` 반환, `PlaceRecommendationAsync` 끝에 검수 버튼)

**Interfaces:**
- Consumes: `SlideCaptureService.CaptureCurrentSlide().PngPath`, `_recommend.CritiqueAsync(resultPng, _lastRecoResult)`(Task 8).

WPF/COM → 수동 검증.

- [ ] **Step 1: 배치 후 결과 렌더 캡처 + 검수 버튼 노출**

`PlaceRecommendationAsync`의 `AddAiBubble("배치 완료! …")` 다음에:

```csharp
                var resultPng = SlideCaptureService.CaptureCurrentSlide()?.PngPath;
                _lastResultPng = resultPng;   // 필드: private string _lastResultPng;
                Dispatcher.Invoke(() => _chatStack.Children.Add(BuildCritiqueButton()));
```

검수 버튼 헬퍼:

```csharp
        private Border BuildCritiqueButton()
        {
            var border = new Border
            {
                Background = ThemeResources.BgChip, CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 4, 12, 8), Padding = new Thickness(12, 8, 12, 8),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "🔍 디자이너 검수 받기", FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                    FontFamily = ThemeResources.FontBase, HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            border.MouseLeftButtonUp += async (s, e) => await RunCritiqueAsync();
            return border;
        }

        private async Task RunCritiqueAsync()
        {
            if (_recommend == null || _lastRecoResult == null || string.IsNullOrEmpty(_lastResultPng)) return;
            if (_redesignRunning) return;
            _redesignRunning = true;
            try
            {
                AddAiBubble("실무 디자이너가 검수 중…");
                var c = await _recommend.CritiqueAsync(_lastResultPng, _lastRecoResult);
                AddAiBubble($"검수 결과: {c.Score}점 — {c.Verdict}\n병목: {c.Bottleneck} · {c.Suggestion}");
                // 판단 과정 패널 갱신(검수 줄 포함)
                Dispatcher.Invoke(() => _chatStack.Children.Add(BuildTracePanel()));
            }
            catch (Exception ex)
            {
                AddAiBubble($"검수 중 오류: {ex.Message}");
                Logger.Log($"[Critique] 실패: {ex}");
            }
            finally { _redesignRunning = false; _chatScroll.ScrollToBottom(); }
        }
```

- [ ] **Step 2: 관리자 빌드 + 검증 + PowerPoint 수동 검증**

추천 → 배치 → "🔍 디자이너 검수 받기" 클릭 → 점수·병목·제안 출력, 판단 과정 패널에 ⑤ 검수 줄 추가. `debug.log`에 `[Critique] raw↓`, `[Trace] ⑤ 검수: …`.

- [ ] **Step 3: Commit**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs
git commit -m "feat(critique): 배치 후 렌더 캡처 + 디자이너 검수 버튼"
```

---

## Self-Review (작성자 점검 결과)

- **스펙 커버리지:** §1 파이프라인 = Task 2~10 / §2 Trace = Task 1,8,9 / §3 검수자+루브릭 = Task 6,7,10 / §4 토큰통제(ThumbTopK=3, 레퍼런스 0장, 온디맨드) = Task 5,7,10 / §5 graceful degrade = 기존 "best available + confidence" 유지(이번 변경 없음, 회귀만 주의). ✅
- **플레이스홀더:** 없음(모든 코드 단계에 실제 코드).
- **타입 일관성:** `RecommendationResult`(Task 8 정의) → Task 8 Step3·Task 10에서 사용. `RecommendationTrace`/`DesignCritique`(Task 1) → Task 8,9,10. `GenerateJsonAsync(IEnumerable<string>)`(Task 4) → Task 5,7. `RemoteAssetCache.GetThumbAsync`(기존) → Task 5. 일치.
- **주의(실행자):** Task 8에서 `RunAsync` 반환 타입이 바뀌므로 AssetPanel 호출부(Task 8 Step3)를 같은 커밋에서 고쳐야 컴파일된다. `BuildTracePanel`(Task 9)은 Task 10에서도 호출되므로 Task 9를 먼저.
