# 단계별 속마음 독백 + 병목 진단 정밀화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 각 파이프라인 단계의 reasoning을 한국어 존대말 속마음 독백으로 실시간 버블에 흘리고, 검색 유사도와 검수자 병목 4분류로 trace를 "기능 병목이 사라질 때까지" 가려주는 진단 계기판으로 만든다.

**Architecture:** 기존 추천 워크플로(① 이해 → ② 검색 → ③ 구성 → ④ 배치 → ⑤ 검수)는 그대로. 프롬프트를 한국어·솔직하게 재작성하고, `thinkingBudget`을 단계별로 상향하며, 버려지던 검색 유사도를 살려 trace에 싣고, 검수자에게 trace 요약을 입력해 병목을 4분류로 종합 판정한다. `RunAsync`에 독백 콜백을 추가해 단계마다 버블을 흘린다.

**Tech Stack:** C# .NET Framework 4.8, WPF, Newtonsoft.Json, Gemini 2.5 Flash(멀티모달 JSON, thinkingConfig), Supabase(pgvector match_assets), xUnit.

설계 스펙: [2026-06-25 단계별 독백 + 병목 진단](../specs/2026-06-25-step-narration-bottleneck-diagnosis-design.md)

## Global Constraints

- **사실 = COM / 벡터, 판단 = LLM.** 유사도는 벡터 사실, 독백·채점은 LLM. LLM은 텍스트 내용을 생성·수정하지 않는다.
- API 키를 문서·커밋에 평문으로 넣지 않는다.
- 좌표 변환(CoordinateConverter) 폴백 추가 금지.
- 자동 재검색/재배치 루프 추가 금지 — trace는 진단·보고만.
- 빌드는 COM 등록 때문에 관리자 권한 필요. 단위테스트는 비-UAC 경로 사용.
- **새 .cs 파일은 `src/TeampptAddin/TeampptAddin.csproj`의 `<Compile Include>`에 수동 등록 필수**(old-style csproj). (이 플랜은 신규 .cs 없음 — 전부 기존 파일 수정.)

## Test Runner (비-UAC 단위테스트)

PowerPoint를 먼저 종료. 검증된 워크플로:

1. **솔루션 빌드(관리자, COM등록 끔):**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build-test.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```

(빌드는 **foreground** 실행 — `run_in_background` 금지. 완료 후 자동 응답 안 되는 문제 있음.)

2. **테스트 실행(빌드 없이):**

```bash
dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false -p:BuildProjectReferences=false --no-build --filter <TestName>
```

### 관리자 빌드 (COM/UI Task 검증용)

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```

빌드 후 검증(생략 금지): ① DLL 타임스탬프 1분 이내 — `stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll`, ② 로그 오류 0건 — `tail -5 c:/Projects/teamppt-addin/build.log`.

---

## File Structure

**수정 (신규 없음):**
- `src/TeampptAddin/Services/MatchQuery.cs` — similarity를 `Extra["similarity"]`로 싣기
- `src/TeampptAddin/Services/CombinationCandidateProvider.cs` — `LastRetrieveLines`에 유사도 범위 포함
- `src/TeampptAddin/Services/GeminiAiService.cs` — `GenerateJsonAsync`에 `thinkingBudget` 인자
- `src/TeampptAddin/Services/DraftUnderstandingService.cs` — thinkingBudget 768 전달
- `src/TeampptAddin/Services/CombinationRecommender.cs` — thinkingBudget 768 전달
- `src/TeampptAddin/Services/DesignCritiqueService.cs` — thinkingBudget 2048 + trace 요약 입력
- `src/TeampptAddin/Services/DraftUnderstandingSchema.cs` — reasoning 프롬프트 한국어 재작성
- `src/TeampptAddin/Services/CombinationRecommenderSchema.cs` — reasoning 프롬프트 한국어 재작성
- `src/TeampptAddin/Services/DesignCritiqueSchema.cs` — 병목 4분류 enum + 프롬프트 재작성
- `src/TeampptAddin/Services/RecommendationService.cs` — narrate 콜백 + CritiqueAsync에 retrieveLines 전달
- `src/TeampptAddin/UI/Wpf/AssetPanel.cs` — narrate → AddAiBubble 배선, 검수 reasoning 출력
- `src/TeampptAddin.Tests/MatchQuerySimilarityTest.cs` — 신규 테스트(테스트 프로젝트는 파일 자동 포함)
- `src/TeampptAddin.Tests/DesignCritiqueParserTest.cs` — 4분류 단언 추가

> 참고: 테스트 프로젝트(`TeampptAddin.Tests.csproj`)는 SDK-style이라 .cs 자동 포함 — csproj 등록 불필요. 본 프로젝트(`TeampptAddin.csproj`)만 수동 등록 대상이며, 이 플랜은 본 프로젝트에 신규 .cs를 추가하지 않는다.

---

## Task 1: ② 검색 유사도를 Extra·trace에 노출

**Files:**
- Modify: `src/TeampptAddin/Services/MatchQuery.cs:21-32`
- Modify: `src/TeampptAddin/Services/CombinationCandidateProvider.cs:61-62`
- Test: `src/TeampptAddin.Tests/MatchQuerySimilarityTest.cs`

**Interfaces:**
- Consumes: `SupabaseAssetMapper.Map(JObject row):HeaderAsset`(기존, 항상 `Extra` 생성), `HeaderAsset.Extra:Dictionary<string,JToken>`.
- Produces: `MatchQuery.ParseResults(string rpcJson)`가 각 asset의 `Extra["similarity"]`(double)를 채운다. `CombinationCandidateProvider.LastRetrieveLines`가 `"header 5개 (유사도 0.71~0.62)"` 형식.

- [ ] **Step 1: Write the failing test**

```csharp
// src/TeampptAddin.Tests/MatchQuerySimilarityTest.cs
using Xunit;

namespace TeampptAddin.Tests
{
    public class MatchQuerySimilarityTest
    {
        [Fact]
        public void ParseResults_Sets_Similarity_In_Extra()
        {
            const string json = @"[
              {""name"":""A"",""kind"":""header"",""file"":""a.pptx"",""thumb"":""a.png"",""similarity"":0.71},
              {""name"":""B"",""kind"":""header"",""file"":""b.pptx"",""thumb"":""b.png"",""similarity"":0.62}
            ]";
            var list = MatchQuery.ParseResults(json);
            Assert.Equal(2, list.Count);
            Assert.True(list[0].Extra.ContainsKey("similarity"));
            Assert.Equal(0.71, list[0].Extra["similarity"].Value<double>(), 3);
            Assert.Equal(0.62, list[1].Extra["similarity"].Value<double>(), 3);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

먼저 비-UAC 솔루션 빌드(위 Test Runner 1번) 후:

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false -p:BuildProjectReferences=false --no-build --filter MatchQuerySimilarityTest`
Expected: FAIL — `Extra`에 `similarity` 키 없음(`ContainsKey` false).

- [ ] **Step 3: Write minimal implementation**

`MatchQuery.cs` — `ParseResults`의 루프를 교체(similarity를 Map 결과 Extra에 추가):

```csharp
        public static List<HeaderAsset> ParseResults(string rpcJson)
        {
            var arr = JArray.Parse(rpcJson);
            var result = new List<HeaderAsset>();
            foreach (var row in arr.OfType<JObject>())
            {
                var sim = row["similarity"]?.Value<double>() ?? 0;
                Logger.Log($"[Match] {row["name"]} sim={sim:F3}");
                var asset = SupabaseAssetMapper.Map(row);
                if (asset.Extra == null) asset.Extra = new Dictionary<string, JToken>();
                asset.Extra["similarity"] = sim;
                result.Add(asset);
            }
            return result;
        }
```

`MatchQuery.cs` 상단 using에 `using System.Collections.Generic;`이 이미 있는지 확인(있음 — `List<HeaderAsset>` 사용 중). `JToken`은 `Newtonsoft.Json.Linq`(이미 있음).

`CombinationCandidateProvider.cs` — `LastRetrieveLines.Add($"{kind} {list.Count}개");`(62행)를 유사도 범위 포함으로 교체:

```csharp
                    var sims = list
                        .Where(a => a.Extra != null && a.Extra.ContainsKey("similarity"))
                        .Select(a => a.Extra["similarity"].Value<double>())
                        .ToList();
                    var simText = sims.Count > 0 ? $" (유사도 {sims.Max():F2}~{sims.Min():F2})" : "";
                    LastRetrieveLines.Add($"{kind} {list.Count}개{simText}");
```

(`CombinationCandidateProvider.cs`는 `using System.Linq;`가 이미 있음 — `Where/Select/ToList` 사용 가능.)

- [ ] **Step 4: Run test to verify it passes**

비-UAC 솔루션 빌드(Test Runner 1번) 후:

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false -p:BuildProjectReferences=false --no-build --filter MatchQuerySimilarityTest`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/TeampptAddin/Services/MatchQuery.cs src/TeampptAddin/Services/CombinationCandidateProvider.cs src/TeampptAddin.Tests/MatchQuerySimilarityTest.cs
git commit -m "feat(trace): 검색 유사도를 Extra·LastRetrieveLines에 노출"
```

---

## Task 2: thinkingBudget 파라미터화 + 단계별 상향

**Files:**
- Modify: `src/TeampptAddin/Services/GeminiAiService.cs:214-264`
- Modify: `src/TeampptAddin/Services/DraftUnderstandingService.cs:18-21`
- Modify: `src/TeampptAddin/Services/CombinationRecommender.cs:28-32`
- Modify: `src/TeampptAddin/Services/DesignCritiqueService.cs:24-26`

**Interfaces:**
- Produces: `GenerateJsonAsync(..., double temperature = 0.4, int thinkingBudget = 0)` 두 오버로드. 기본값 0이라 미지정 호출부(SlotMapper 등)는 무영향.
- Consumes: 이해·구성은 `thinkingBudget: 768`, 검수는 `thinkingBudget: 2048`.

API 호출이라 단위테스트 불가 → 컴파일/빌드로 검증.

- [ ] **Step 1: GenerateJsonAsync에 thinkingBudget 인자 추가**

`GeminiAiService.cs` — 단일 위임 오버로드:

```csharp
        public Task<string> GenerateJsonAsync(string systemPrompt, string userText, string pngPathOrNull, JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
        {
            var imgs = pngPathOrNull == null ? new string[0] : new[] { pngPathOrNull };
            return GenerateJsonAsync(systemPrompt, userText, imgs, responseSchema, temperature, thinkingBudget);
        }
```

다중 이미지 오버로드 시그니처와 thinkingConfig 줄:

```csharp
        public async Task<string> GenerateJsonAsync(string systemPrompt, string userText, IEnumerable<string> pngPaths, JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
```

같은 메서드 본문의 `generationConfig`에서:

```csharp
                    ["thinkingConfig"] = new JObject { ["thinkingBudget"] = 0 }
```

를 다음으로:

```csharp
                    ["thinkingConfig"] = new JObject { ["thinkingBudget"] = thinkingBudget }
```

- [ ] **Step 2: 단계별 호출부에 budget 전달**

`DraftUnderstandingService.cs` — `UnderstandAsync`의 호출:

```csharp
            var json = await _gemini.GenerateJsonAsync(
                DraftUnderstandingSchema.BuildSystemPrompt(),
                userText, pngPath,
                DraftUnderstandingSchema.BuildResponseSchema(), thinkingBudget: 768).ConfigureAwait(false);
```

`CombinationRecommender.cs` — `RecommendAsync`의 호출:

```csharp
            var thumbs = CollectThumbs(candidatesByKind, 3);
            var json = await _gemini.GenerateJsonAsync(
                CombinationRecommenderSchema.BuildSystemPrompt(),
                userText, thumbs,
                CombinationRecommenderSchema.BuildResponseSchema(), thinkingBudget: 768).ConfigureAwait(false);
```

`DesignCritiqueService.cs` — `CritiqueAsync`의 호출:

```csharp
            var json = await _gemini.GenerateJsonAsync(
                DesignCritiqueSchema.BuildSystemPrompt(), userText, imgs,
                DesignCritiqueSchema.BuildResponseSchema(), thinkingBudget: 2048).ConfigureAwait(false);
```

(세 곳 모두 `temperature`는 named 생략 → 기본 0.4 유지, `thinkingBudget:`만 named 지정.)

- [ ] **Step 3: 관리자 빌드 + 검증**

위 "관리자 빌드" 실행 후: DLL 타임스탬프 1분 이내 + `tail -5 build.log` 오류 0건.

- [ ] **Step 4: Commit**

```bash
git add src/TeampptAddin/Services/GeminiAiService.cs src/TeampptAddin/Services/DraftUnderstandingService.cs src/TeampptAddin/Services/CombinationRecommender.cs src/TeampptAddin/Services/DesignCritiqueService.cs
git commit -m "feat(gemini): thinkingBudget 파라미터화 + 단계별 상향(이해·구성 768, 검수 2048)"
```

---

## Task 3: 이해·구성 reasoning 프롬프트 한국어 재작성

**Files:**
- Modify: `src/TeampptAddin/Services/DraftUnderstandingSchema.cs:82` (reasoning 지시 줄)
- Modify: `src/TeampptAddin/Services/CombinationRecommenderSchema.cs:47` (reasoning 지시 줄)

**Interfaces:**
- 응답 스키마/필드는 변경 없음(이미 `reasoning:string` 존재). 프롬프트 문자열만 교체.

프롬프트 문자열이라 단위테스트 불가 → 빌드로 컴파일 검증, 실효는 PowerPoint 수동 검증(Task 6).

- [ ] **Step 1: 이해 단계 reasoning 지시 교체**

`DraftUnderstandingSchema.cs` — `BuildSystemPrompt()`의 reasoning 줄(82행):

```
- reasoning: 위 판단(특히 slideKind·neededCombination)을 *왜* 그렇게 내렸는지 한두 문장. 근거가 보이게.
```

를 다음으로 교체:

```
- reasoning: **반드시 한국어 존대말**로, 이 초안을 어떻게 읽었는지 담백한 전문가 톤 1~2문장. slideKind·neededCombination 판단 근거를 밝히고, **추출(counts/materials)에서 애매했거나 자신 없던 부분이 있으면 솔직히** 말해라. '문제없음' 식의 무난한 답 금지.
```

- [ ] **Step 2: 구성 단계 reasoning 지시 교체**

`CombinationRecommenderSchema.cs` — `BuildSystemPrompt()`의 reasoning 줄(47행 부근):

```
- reasoning: 이 조합을 고른 이유와, 특정 종류를 미충족 처리했다면 *왜* 그런지 한두 문장.
```

를 다음으로 교체:

```
- reasoning: **반드시 한국어 존대말**로, 이 조합을 고른 이유를 담백한 전문가 톤 1~2문장. 미충족이 났다면 *풀에 맞는 후보가 없어서인지, 후보는 있는데 다 안 어울려서인지* 솔직히 구분해 말해라. '문제없음' 식의 무난한 답 금지.
```

- [ ] **Step 3: 관리자 빌드 + 검증** (DLL 타임스탬프 + 로그 0건)

- [ ] **Step 4: Commit**

```bash
git add src/TeampptAddin/Services/DraftUnderstandingSchema.cs src/TeampptAddin/Services/CombinationRecommenderSchema.cs
git commit -m "feat(prompt): 이해·구성 reasoning 한국어 존대말·약점 솔직 재작성"
```

---

## Task 4: 검수자 병목 4분류 + trace 요약 입력

**Files:**
- Modify: `src/TeampptAddin/Services/DesignCritiqueSchema.cs:27` (enum), `:38-46` (병목 프롬프트)
- Modify: `src/TeampptAddin/Services/DesignCritiqueService.cs:11-28` (시그니처 + userText)
- Modify: `src/TeampptAddin/Services/RecommendationService.cs:54-61` (CritiqueAsync 호출)
- Test: `src/TeampptAddin.Tests/DesignCritiqueParserTest.cs` (단언 추가)

**Interfaces:**
- Consumes: `RecommendationResult.Trace.RetrieveLines:List<string>`(Task 1에서 유사도 포함), `DraftUnderstanding.Reasoning`.
- Produces: `DesignCritique.Bottleneck` ∈ `{기능, 데이터추출, 에셋부족, 에셋품질}`. `DesignCritiqueService.CritiqueAsync(string resultPng, string draftPng, DraftUnderstanding u, CombinationRecommendation rec, List<string> retrieveLines)`.

- [ ] **Step 1: Write the failing test** — `DesignCritiqueParserTest.cs`에 4분류 파싱 단언 추가(기존 테스트 클래스 안에 새 메서드):

```csharp
        [Fact]
        public void Parses_New_Bottleneck_Categories()
        {
            var c = DesignCritiqueParser.Parse(@"{""score"":70,""bottleneck"":""에셋부족""}");
            Assert.Equal("에셋부족", c.Bottleneck);
        }
```

- [ ] **Step 2: Run test to verify it fails or passes**

비-UAC 솔루션 빌드(Test Runner 1번) 후:

Run: `dotnet test src/TeampptAddin.Tests/TeampptAddin.Tests.csproj -p:RegisterForComInterop=false -p:BuildProjectReferences=false --no-build --filter DesignCritiqueParserTest`
Expected: PASS — `DesignCritiqueParser`는 bottleneck을 문자열로 그대로 읽으므로 4분류 값도 파싱된다(이 테스트는 회귀 방지용 — 스키마/프롬프트 변경이 파서를 깨지 않음을 고정).

- [ ] **Step 3: enum + 병목 프롬프트 교체**

`DesignCritiqueSchema.cs` — `BuildResponseSchema()`의 bottleneck enum(27행 부근):

```csharp
                    ["bottleneck"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "기능", "데이터스키마", "에셋" } },
```

를 다음으로:

```csharp
                    ["bottleneck"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "기능", "데이터추출", "에셋부족", "에셋품질" } },
```

`DesignCritiqueSchema.cs` — `BuildSystemPrompt()`의 "## 병목 진단" 블록 전체(reasoning 줄 포함)를 교체:

```
## 병목 진단 (bottleneck: 기능 / 데이터추출 / 에셋부족 / 에셋품질 중 하나)
입력으로 받은 '초안 이해 요약'과 '검색 유사도'를 근거로 종합 판정하라.
- 기능: 결과가 재료가 된 원본 에셋들보다 못나 보임(배치·조립 문제).
- 에셋품질: 원본 에셋만큼은 나오는데도 별로임(에셋 천장 — 질).
- 에셋부족: 검색 유사도가 낮아 애초에 잘 맞는 후보가 풀에 없었음(양). 유사도 요약이 전반적으로 낮으면 이쪽.
- 데이터추출: 의도·구성이 애초에 어긋났음(이해·인제스트 문제). 이해 요약이 슬라이드와 안 맞으면 이쪽.
suggestion: 그 병목을 풀려면 다음에 뭘 해야 하는지 한 줄.
reasoning: **반드시 한국어 존대말**로, 위 판단 근거 한두 문장. 텍스트 내용을 생성·수정하지 마라(평가만).
```

- [ ] **Step 4: CritiqueAsync에 retrieveLines 입력**

`DesignCritiqueService.cs` — 시그니처와 userText 교체:

```csharp
        public async Task<DesignCritique> CritiqueAsync(
            string resultPngPath, string draftPngPath, DraftUnderstanding u, CombinationRecommendation rec, List<string> retrieveLines)
        {
            var userText =
                $"초안 의도(purpose): {u?.Purpose}\n" +
                $"초안 이해 요약: {u?.Reasoning}\n" +
                $"검색 유사도: {string.Join(" / ", retrieveLines ?? new List<string>())}\n" +
                $"적용된 조합: header={rec?.Header?.Asset?.Name}, layout={rec?.Layout?.Asset?.Name}, " +
                $"components={rec?.Components?.Count ?? 0}, 미충족={string.Join("/", rec?.Unmet ?? new List<string>())}\n" +
                "첫 이미지=배치 결과 슬라이드, 둘째 이미지=초안. 결과를 채점하라.";

            var imgs = new List<string>();
            if (!string.IsNullOrEmpty(resultPngPath)) imgs.Add(resultPngPath);
            if (!string.IsNullOrEmpty(draftPngPath)) imgs.Add(draftPngPath);

            var json = await _gemini.GenerateJsonAsync(
                DesignCritiqueSchema.BuildSystemPrompt(), userText, imgs,
                DesignCritiqueSchema.BuildResponseSchema(), thinkingBudget: 2048).ConfigureAwait(false);
            Logger.Log("[Critique] raw↓ " + json);
            return DesignCritiqueParser.Parse(json);
        }
```

(`using System.Collections.Generic;`이 이미 있음 — `List<string>` 사용 중.)

- [ ] **Step 5: RecommendationService가 retrieveLines 전달**

`RecommendationService.cs` — `CritiqueAsync`의 critic 호출:

```csharp
        public async Task<DesignCritique> CritiqueAsync(string resultPng, RecommendationResult prior)
        {
            var critic = new DesignCritiqueService(_gemini);
            var c = await critic.CritiqueAsync(
                resultPng, prior.DraftPngPath, prior.Understanding, prior.Recommendation, prior.Trace.RetrieveLines);
            prior.Trace.Critique = c;
            foreach (var line in prior.Trace.ToReadableLines()) Logger.Log("[Trace] " + line);
            return c;
        }
```

- [ ] **Step 6: Run test + 관리자 빌드 + 검증**

테스트 재실행(Step 2 명령) → PASS 유지. 그다음 관리자 빌드 → DLL 타임스탬프 + 로그 0건.

- [ ] **Step 7: Commit**

```bash
git add src/TeampptAddin/Services/DesignCritiqueSchema.cs src/TeampptAddin/Services/DesignCritiqueService.cs src/TeampptAddin/Services/RecommendationService.cs src/TeampptAddin.Tests/DesignCritiqueParserTest.cs
git commit -m "feat(critique): 병목 4분류 + 검수자에 이해·유사도 요약 입력"
```

---

## Task 5: 실시간 버블 독백 배선

**Files:**
- Modify: `src/TeampptAddin/Services/RecommendationService.cs:22-52` (RunAsync narrate 콜백)
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (`RunRecommendationAsync` 호출부, `RunCritiqueAsync` reasoning 출력)

**Interfaces:**
- Produces: `RecommendationService.RunAsync(Action<string> progress, Action<string> narrate):Task<RecommendationResult>` (인자 추가).
- Consumes: `AssetPanel.AddAiBubble(string)`(기존), `_lastRecoResult.Trace.RetrieveLines`.

COM/UI 얽힘 → 관리자 빌드 + PowerPoint 수동 검증.

- [ ] **Step 1: RunAsync에 narrate 콜백 추가**

`RecommendationService.cs` — `RunAsync` 전체 교체:

```csharp
        public async Task<RecommendationResult> RunAsync(Action<string> progress, Action<string> narrate)
        {
            progress("초안 읽는 중…");
            var profile = DraftSlideReader.ReadCurrentSlide();
            if (profile == null) throw new InvalidOperationException("활성 슬라이드를 찾을 수 없습니다.");

            var png = SlideCaptureService.CaptureCurrentSlide()?.PngPath;

            progress("초안 이해하는 중…");
            var u = await _understand.UnderstandAsync(profile, png);
            if (!string.IsNullOrEmpty(u.Reasoning)) narrate(u.Reasoning);

            progress("어울리는 에셋 후보 찾는 중…");
            var pool = await _candidates.GetCandidatesAsync(u);
            if (_candidates.LastRetrieveLines.Count > 0)
                narrate("후보를 추렸어요 — " + string.Join(", ", _candidates.LastRetrieveLines));

            progress("조합 고르는 중…");
            var rec = await _recommender.RecommendAsync(u, pool);
            if (!string.IsNullOrEmpty(rec.Reasoning)) narrate(rec.Reasoning);

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
```

- [ ] **Step 2: AssetPanel 호출부에 narrate 연결**

`AssetPanel.cs` — `RunRecommendationAsync`의 호출(현재 `var result = await _recommend.RunAsync(msg => Dispatcher.Invoke(() => AddAiBubble(msg)));`):

```csharp
                var result = await _recommend.RunAsync(
                    msg => Dispatcher.Invoke(() => AddAiBubble(msg)),
                    voice => Dispatcher.Invoke(() => AddAiBubble(voice)));
                _lastRecoResult = result;
                ShowRecommendation(result.Recommendation);
```

- [ ] **Step 3: 검수 reasoning·병목을 버블에 노출**

`AssetPanel.cs` — `RunCritiqueAsync`의 결과 출력 줄(현재 `AddAiBubble($"검수 결과: {c.Score}점 — {c.Verdict}\n병목: {c.Bottleneck} · {c.Suggestion}");`)을 reasoning 포함으로:

```csharp
                AddAiBubble($"검수 결과: {c.Score}점 — {c.Verdict}\n병목: {c.Bottleneck} · {c.Suggestion}"
                    + (string.IsNullOrEmpty(c.Reasoning) ? "" : $"\n{c.Reasoning}"));
```

- [ ] **Step 4: 관리자 빌드 + 검증**

관리자 빌드 → DLL 타임스탬프 + 로그 0건.

- [ ] **Step 5: Commit**

```bash
git add src/TeampptAddin/Services/RecommendationService.cs src/TeampptAddin/UI/Wpf/AssetPanel.cs
git commit -m "feat(ui): 단계별 reasoning 실시간 버블 독백 + 검수 reasoning 노출"
```

---

## Task 6: PowerPoint 수동 검증 (통합)

이 Task는 코드 변경 없음 — 사용자(Michael)가 PowerPoint에서 전체 흐름을 검증한다. **구현자는 Task 5까지 끝낸 뒤 여기서 멈추고 사용자에게 넘긴다.**

- [ ] **검증 체크리스트** (PowerPoint 재시작 후 본문 슬라이드에서 "AI 리디자인" → 추천 → 배치 → "🔍 디자이너 검수 받기"):

  1. **한국어 독백 버블**이 단계별로 실시간으로 흐르는가 (이해 → 후보 추림 → 구성). 영어 아님.
  2. 후보 버블·접이식 패널에 **유사도**가 보이는가 (예 `header 5개 (유사도 0.71~0.62)`).
  3. 독백이 **담백한 존대말**이고 **약점을 솔직히** 짚는가 ("문제없음" 식 무난한 답이 아님).
  4. 검수 결과에 **병목이 4분류**(기능/데이터추출/에셋부족/에셋품질) 중 하나로 찍히고, reasoning이 한국어로 나오는가.
  5. `debug.log`에서 `[Trace]`·`[Critique] raw↓`의 reasoning이 한국어이고 진단이 구체적인가.

- [ ] **완료 조건(이 작업의 끝):** 추천 한 번의 독백 흐름만 읽고도 "지금 기능이 문제인지 / 에셋 양·질이 문제인지 / 데이터 추출이 문제인지"가 바로 읽힌다. 기능 병목이 더는 안 찍히면 0→1 도달.

---

## Self-Review (작성자 점검 결과)

- **스펙 커버리지:** §1 프롬프트 재작성 = Task 3(이해·구성) + Task 4(검수) / §2 thinkingBudget = Task 2 / §3 검색 유사도 노출 = Task 1 / §4 검수자 trace요약 입력 + 4분류 = Task 4 / §5 버블 독백 = Task 5 / §6 패널 유지 = 변경 없음(기존 `BuildTracePanel`·`ToReadableLines`가 유사도·병목 문자열을 그대로 렌더 — 회귀만 주의). ✅
- **플레이스홀더:** 없음(모든 코드 단계에 실제 코드).
- **타입 일관성:** `Extra["similarity"]`(Task 1, JToken double) → Task 1 CandidateProvider·Task 4 검수 유사도 요약에서 사용. `GenerateJsonAsync(..., thinkingBudget)`(Task 2) → Task 2·4 호출부 일치. `CritiqueAsync(..., List<string> retrieveLines)`(Task 4) → RecommendationService(Task 4 Step5)에서 `prior.Trace.RetrieveLines` 전달, 타입 `List<string>` 일치. `RunAsync(progress, narrate)`(Task 5) → AssetPanel(Task 5 Step2) 호출부 일치. Bottleneck 4분류 문자열은 파서·ToReadableLines가 문자열로 처리하므로 코드 변경 불필요(Task 4 Step2 테스트로 고정). ✅
- **주의(실행자):** Task 2에서 `GenerateJsonAsync` 시그니처가 바뀌지만 기본값(`thinkingBudget = 0`)이라 미지정 호출부는 무영향. Task 4에서 `DesignCritiqueService.CritiqueAsync` 시그니처가 바뀌므로 같은 Task의 RecommendationService 호출부(Step5)를 같은 커밋에서 고쳐야 컴파일된다. Task 5에서 `RunAsync` 시그니처가 바뀌므로 같은 Task의 AssetPanel 호출부(Step2)를 같은 커밋에서.
