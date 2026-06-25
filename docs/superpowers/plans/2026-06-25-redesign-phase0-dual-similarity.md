# 리디자인 Phase 0 — 두 유사도 분리(재료적합/디자인·컨셉) 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 추천·검수 결과를 두 점수로 분리해 보여준다 — `materialFit`(재료적합, 벡터유사도+capacity로 **계산**) / `designConcept`(디자인·컨셉, LLM 6차원 **종합**) — 그래서 "결과가 별로일 때 우리(추천·데이터) 문제인지 에셋 문제인지"를 점수로 가른다.

**Architecture:** 사실=벡터/계산, 판단=LLM 원칙 유지. `materialFit`은 토큰 0 순수 계산(`MaterialFitScorer`), `designConcept`은 기존 검수 LLM의 6차원 합(`DesignCritiqueParser`에서 결정). 계산한 `materialFit`은 (1) UI 배지로 표시하고 (2) 검수 LLM의 병목 판정 호출에 입력으로 넣는다. 기존 단일 슬라이드 추천·검수 경로([RecommendationService](../../../src/TeampptAddin/Services/RecommendationService.cs))에 얹어 먼저 검증한다.

**Tech Stack:** C# / .NET Framework 4.8, Newtonsoft.Json, xUnit(테스트), WPF(패널 UI), Gemini(검수 LLM).

## Global Constraints

- **사실 = COM/벡터/계산, 판단 = LLM.** `materialFit`은 LLM이 만들지 않는다(계산값). `designConcept`만 LLM 6차원에서 도출.
- **LLM은 텍스트 내용을 생성·수정하지 않는다.** (이 Phase는 텍스트를 건드리지 않음 — 점수/표시만.)
- **비파괴.** 슬라이드·원본을 건드리지 않는다(이 Phase는 모델·계산·표시만).
- **좌표 변환 폴백 금지** (이 Phase는 좌표 무관).
- **API 키를 문서·커밋에 평문 금지.**
- **새 `.cs` 파일은 메인 프로젝트 [TeampptAddin.csproj](../../../src/TeampptAddin/TeampptAddin.csproj)의 `<Compile Include>`에 수동 등록 필수** (old-style csproj). 테스트 프로젝트는 SDK-style이라 자동 포함.
- **모든 커밋 메시지 끝에** `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` 한 줄 추가.
- 브랜치: `feat/asset-combination-recommendation` (현재 브랜치 그대로).

### 테스트 절차 (이 플랜의 "Run" 명령이 가리키는 것)

순수 로직 단위테스트는 UAC 없이 가능 (memory: 비-UAC 단위테스트). 빌드는 COM 등록 끄고:

```powershell
# 1) 솔루션 빌드 (COM 등록 OFF — 관리자 불필요). UAC가 뜨면 Start-Process -Verb RunAs로.
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal

# 2) 필터링한 테스트만 실행 (NuGet 참조는 1)에서 이미 풀림)
dotnet test "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~<클래스명>"
```

> `dotnet test` 단독은 메인(old-style) 프로젝트의 NuGet 참조를 못 풀어 실패한다 — 반드시 1) MSBuild 빌드 먼저. (PROGRESS-BOARD.md에서 확립.)

---

## Task 1: `designConcept` 도출 (검수 모델 + 파서)

검수 LLM의 6차원 점수(정렬20·여백15·위계20·색15·타이포10·의도부합20=100) 합을 `DesignConcept`로 결정적으로 계산한다. `MaterialFit` 필드도 함께 추가(값은 Task 3에서 서비스가 채움).

**Files:**
- Modify: `src/TeampptAddin/Models/DesignCritique.cs` (필드 2개 추가)
- Modify: `src/TeampptAddin/Services/DesignCritiqueParser.cs` (DesignConcept 합산)
- Test: `src/TeampptAddin.Tests/DesignCritiqueParserTest.cs` (신규)

**Interfaces:**
- Produces: `DesignCritique.DesignConcept` (int, 0-100 = dimensionScores 합), `DesignCritique.MaterialFit` (int, 0-100 = Task 3에서 주입). `DesignCritiqueParser.Parse(string json)` 시그니처 불변.

- [ ] **Step 1: 실패 테스트 작성** — `src/TeampptAddin.Tests/DesignCritiqueParserTest.cs`

```csharp
using Xunit;

namespace TeampptAddin.Tests
{
    public class DesignCritiqueParserTest
    {
        [Fact]
        public void DesignConcept_Is_Sum_Of_Dimension_Scores()
        {
            const string json = @"{
              ""score"": 80,
              ""dimensionScores"": {""정렬"":18,""여백"":12,""위계"":16,""색"":12,""타이포"":8,""의도부합"":16},
              ""verdict"": ""실무급"",
              ""bottleneck"": ""에셋품질"",
              ""suggestion"": ""대비 강화"",
              ""reasoning"": ""괜찮습니다""
            }";
            var c = DesignCritiqueParser.Parse(json);
            Assert.Equal(82, c.DesignConcept);   // 18+12+16+12+8+16
            Assert.Equal(0, c.MaterialFit);      // 파서는 0; 서비스가 Task 3에서 주입
            Assert.Equal("에셋품질", c.Bottleneck);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: 위 테스트 절차로 빌드 → `dotnet test ... --filter "FullyQualifiedName~DesignCritiqueParserTest"`
Expected: 컴파일 실패 또는 FAIL (`DesignConcept`/`MaterialFit` 미정의).

- [ ] **Step 3: 모델 필드 추가** — `src/TeampptAddin/Models/DesignCritique.cs`

기존 `public int Score { get; set; }` 아래에 두 줄 추가:

```csharp
        public int Score { get; set; }
        public int MaterialFit { get; set; }      // 0-100, 계산값(벡터유사도+capacity) — LLM 아님
        public int DesignConcept { get; set; }    // 0-100, dimensionScores 합(비전 채점)
```

- [ ] **Step 4: 파서에서 DesignConcept 합산** — `src/TeampptAddin/Services/DesignCritiqueParser.cs`

파일 상단 `using` 두 줄로 교체(System.Linq 추가):

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
```

`dimensionScores` 루프 직후, `return c;` 바로 위에 한 줄 추가:

```csharp
            if (o["dimensionScores"] is JObject dim)
                foreach (var p in dim.Properties())
                    c.DimensionScores[p.Name] = p.Value?.Value<int>() ?? 0;
            c.DesignConcept = c.DimensionScores.Values.Sum();
            return c;
```

- [ ] **Step 5: 통과 확인**

Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~DesignCritiqueParserTest"`
Expected: PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Models/DesignCritique.cs src/TeampptAddin/Services/DesignCritiqueParser.cs src/TeampptAddin.Tests/DesignCritiqueParserTest.cs
git commit -m "feat(critique): designConcept=6차원 합 도출 + materialFit 필드 추가"
```

---

## Task 2: `MaterialFitScorer` — 재료적합 계산 (순수 로직)

선택된 조합의 **벡터 유사도 평균**(0-1)과 **레이아웃 capacity 대비 필요 블록수** 적합을 합쳐 0-100 점수를 낸다. COM·네트워크 없음 → 순수 단위테스트.

**공식 (고정):** `materialFit = round(100 * (0.6*simAvg + 0.4*capacityScore))`, 0-100 클램프.
- `simAvg` = 선택 슬롯(slide/header/layout/components) 중 `Extra["similarity"]` 있는 것들의 평균. 하나도 없으면 0.5(중립).
- `capacityScore` = 레이아웃이 있고 `Capacity`와 필요 블록수(`NeededCombination.Component`)가 둘 다 유효할 때만 계산, 아니면 1.0(중립). 블록수가 [Min,Max]면 1.0, 벗어나면 `max(0, 1 - dist/max(Max,1))`.

**Files:**
- Create: `src/TeampptAddin/Services/MaterialFitScorer.cs` (스코어러 + `MaterialFitResult` 한 파일)
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (Compile Include 1줄)
- Test: `src/TeampptAddin.Tests/MaterialFitScorerTest.cs` (신규)

**Interfaces:**
- Consumes: `CombinationRecommendation`(Slide/Header/Layout/Components 각 `RecommendedSlot.Asset:HeaderAsset`), `HeaderAsset.Extra["similarity"]:JToken`, `HeaderAsset.Capacity:AssetCapacity{Min,Max}`, `DraftUnderstanding.NeededCombination.Component:int`.
- Produces: `MaterialFitResult { int Score; double SimilarityAvg; double CapacityScore; string Note; }`, `static MaterialFitResult MaterialFitScorer.Score(CombinationRecommendation rec, DraftUnderstanding u)`.

- [ ] **Step 1: 실패 테스트 작성** — `src/TeampptAddin.Tests/MaterialFitScorerTest.cs`

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class MaterialFitScorerTest
    {
        private static HeaderAsset Asset(double sim, AssetCapacity cap = null) => new HeaderAsset
        {
            File = "a.pptx",
            Capacity = cap,
            Extra = sim >= 0 ? new Dictionary<string, JToken> { ["similarity"] = sim } : null
        };

        [Fact]
        public void Averages_Similarity_With_Neutral_Capacity()
        {
            var rec = new CombinationRecommendation
            {
                Header = new RecommendedSlot { Asset = Asset(0.8) },
                Layout = new RecommendedSlot { Asset = Asset(0.6) }
            };
            var r = MaterialFitScorer.Score(rec, new DraftUnderstanding());
            Assert.Equal(0.70, r.SimilarityAvg, 2);
            Assert.Equal(1.0, r.CapacityScore, 2);
            Assert.Equal(82, r.Score);   // round(100*(0.6*0.7 + 0.4*1.0))
        }

        [Fact]
        public void Penalizes_Capacity_Mismatch()
        {
            var rec = new CombinationRecommendation
            {
                Layout = new RecommendedSlot { Asset = Asset(0.5, new AssetCapacity { Min = 2, Max = 2 }) }
            };
            var u = new DraftUnderstanding { NeededCombination = new NeededCombination { Component = 5 } };
            var r = MaterialFitScorer.Score(rec, u);
            Assert.Equal(0.0, r.CapacityScore, 2);   // dist=3, denom=max(2,1)=2 → max(0,1-1.5)=0
            Assert.Equal(30, r.Score);               // round(100*(0.6*0.5 + 0.4*0))
        }

        [Fact]
        public void No_Similarity_Anywhere_Uses_Neutral_Half()
        {
            var rec = new CombinationRecommendation { Header = new RecommendedSlot { Asset = Asset(-1) } };
            var r = MaterialFitScorer.Score(rec, new DraftUnderstanding());
            Assert.Equal(0.5, r.SimilarityAvg, 2);
            Assert.Equal(70, r.Score);   // round(100*(0.6*0.5 + 0.4*1.0))
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~MaterialFitScorerTest"`
Expected: 컴파일 실패 (`MaterialFitScorer`/`MaterialFitResult` 미정의).

- [ ] **Step 3: 스코어러 구현** — `src/TeampptAddin/Services/MaterialFitScorer.cs` (신규)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public class MaterialFitResult
    {
        public int Score { get; set; }            // 0-100
        public double SimilarityAvg { get; set; } // 0-1
        public double CapacityScore { get; set; } // 0-1
        public string Note { get; set; } = "";
    }

    /// <summary>
    /// 재료적합 점수(계산값, LLM 아님). 선택된 조합의 벡터유사도 평균 + 레이아웃 capacity 대비
    /// 필요 블록수 적합을 합산. 사실=벡터/계산 원칙: 토큰 0.
    /// </summary>
    public static class MaterialFitScorer
    {
        public static MaterialFitResult Score(CombinationRecommendation rec, DraftUnderstanding u)
        {
            var slots = new List<RecommendedSlot>();
            if (rec?.Slide != null) slots.Add(rec.Slide);
            if (rec?.Header != null) slots.Add(rec.Header);
            if (rec?.Layout != null) slots.Add(rec.Layout);
            if (rec?.Components != null) slots.AddRange(rec.Components);

            var sims = slots.Select(s => Sim(s?.Asset)).Where(v => v >= 0).ToList();
            double simAvg = sims.Count > 0 ? sims.Average() : 0.5;

            double capScore = 1.0;
            string capNote = "용량 제약 없음";
            var cap = rec?.Layout?.Asset?.Capacity;
            int need = u?.NeededCombination?.Component ?? 0;
            if (cap != null && need > 0)
            {
                if (need >= cap.Min && need <= cap.Max) { capScore = 1.0; capNote = $"용량 {cap.Min}-{cap.Max}, 블록 {need} — 맞음"; }
                else
                {
                    int dist = need < cap.Min ? cap.Min - need : need - cap.Max;
                    capScore = Math.Max(0.0, 1.0 - (double)dist / Math.Max(cap.Max, 1));
                    capNote = $"용량 {cap.Min}-{cap.Max}, 블록 {need} — 어긋남";
                }
            }

            int score = (int)Math.Round(100.0 * (0.6 * simAvg + 0.4 * capScore));
            score = Math.Max(0, Math.Min(100, score));
            return new MaterialFitResult
            {
                Score = score,
                SimilarityAvg = simAvg,
                CapacityScore = capScore,
                Note = $"유사도 {simAvg:F2}, {capNote}"
            };
        }

        private static double Sim(HeaderAsset a)
        {
            if (a?.Extra != null && a.Extra.TryGetValue("similarity", out var s) && s != null)
                return s.Value<double>();
            return -1;   // 신호 없음
        }
    }
}
```

- [ ] **Step 4: csproj 등록** — `src/TeampptAddin/TeampptAddin.csproj`

`<Compile Include="Services\DesignCritiqueService.cs" />` 줄 바로 아래에 추가:

```xml
    <Compile Include="Services\DesignCritiqueService.cs" />
    <Compile Include="Services\MaterialFitScorer.cs" />
```

- [ ] **Step 5: 통과 확인**

Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~MaterialFitScorerTest"`
Expected: PASS (3개).

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/MaterialFitScorer.cs src/TeampptAddin/TeampptAddin.csproj src/TeampptAddin.Tests/MaterialFitScorerTest.cs
git commit -m "feat(critique): MaterialFitScorer — 재료적합 계산(유사도+capacity)"
```

---

## Task 3: 검수에 materialFit 배선 (서비스 + 프롬프트 + 오케스트레이터)

`materialFit`을 (1) 검수 LLM 입력 텍스트에 넣어 병목 판정 근거로 쓰고, (2) 결과 `DesignCritique.MaterialFit`에 채운다. 검수 userText 빌더를 `public static`으로 추출해 테스트 가능하게 만든다(기존 `CombinationRecommender.BuildUserText` 패턴).

**Files:**
- Modify: `src/TeampptAddin/Services/DesignCritiqueService.cs` (BuildUserText 정적 추출 + 파라미터 추가 + MaterialFit 주입)
- Modify: `src/TeampptAddin/Services/DesignCritiqueSchema.cs` (병목 근거에 재료적합 점수 언급)
- Modify: `src/TeampptAddin/Services/RecommendationService.cs` (스코어 계산 후 전달)
- Test: `src/TeampptAddin.Tests/DesignCritiqueUserTextTest.cs` (신규)

**Interfaces:**
- Consumes: `MaterialFitScorer.Score(...)` → `MaterialFitResult` (Task 2), `DesignCritiqueParser.Parse` (Task 1).
- Produces: `static string DesignCritiqueService.BuildUserText(DraftUnderstanding u, CombinationRecommendation rec, List<string> retrieveLines, MaterialFitResult materialFit)`; `CritiqueAsync(..., MaterialFitResult materialFit)` 시그니처에 마지막 파라미터 추가.

- [ ] **Step 1: 실패 테스트 작성** — `src/TeampptAddin.Tests/DesignCritiqueUserTextTest.cs`

```csharp
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DesignCritiqueUserTextTest
    {
        [Fact]
        public void UserText_Includes_MaterialFit_Score()
        {
            var u = new DraftUnderstanding { Purpose = "3개 기능 비교", Reasoning = "본문 비교형입니다" };
            var rec = new CombinationRecommendation();
            var mf = new MaterialFitResult { Score = 81, SimilarityAvg = 0.79, Note = "유사도 0.79, 용량 맞음" };
            var text = DesignCritiqueService.BuildUserText(u, rec, new List<string> { "header 5개 (유사도 0.82~0.71)" }, mf);
            Assert.Contains("재료적합", text);
            Assert.Contains("81", text);
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~DesignCritiqueUserTextTest"`
Expected: 컴파일 실패 (`BuildUserText` 미정의/비공개).

- [ ] **Step 3: 서비스 리팩터 + 파라미터 추가** — `src/TeampptAddin/Services/DesignCritiqueService.cs`

파일 전체를 아래로 교체:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeampptAddin
{
    public class DesignCritiqueService
    {
        private readonly GeminiAiService _gemini;
        public DesignCritiqueService(GeminiAiService gemini) { _gemini = gemini; }

        public static string BuildUserText(
            DraftUnderstanding u, CombinationRecommendation rec, List<string> retrieveLines, MaterialFitResult materialFit)
        {
            return
                $"초안 의도(purpose): {u?.Purpose}\n" +
                $"초안 이해 요약: {u?.Reasoning}\n" +
                $"검색 유사도: {string.Join(" / ", retrieveLines ?? new List<string>())}\n" +
                $"재료적합 점수(계산값): {materialFit?.Score ?? 0} ({materialFit?.Note})\n" +
                $"적용된 조합: header={rec?.Header?.Asset?.Name}, layout={rec?.Layout?.Asset?.Name}, " +
                $"components={rec?.Components?.Count ?? 0}, 미충족={string.Join("/", rec?.Unmet ?? new List<string>())}\n" +
                "첫 이미지=배치 결과 슬라이드, 둘째 이미지=초안. 결과를 채점하라.";
        }

        public async Task<DesignCritique> CritiqueAsync(
            string resultPngPath, string draftPngPath, DraftUnderstanding u,
            CombinationRecommendation rec, List<string> retrieveLines, MaterialFitResult materialFit)
        {
            var userText = BuildUserText(u, rec, retrieveLines, materialFit);

            var imgs = new List<string>();
            if (!string.IsNullOrEmpty(resultPngPath)) imgs.Add(resultPngPath);
            if (!string.IsNullOrEmpty(draftPngPath)) imgs.Add(draftPngPath);

            var json = await _gemini.GenerateJsonAsync(
                DesignCritiqueSchema.BuildSystemPrompt(), userText, imgs,
                DesignCritiqueSchema.BuildResponseSchema(), thinkingBudget: 2048).ConfigureAwait(false);
            Logger.Log("[Critique] raw↓ " + json);
            var c = DesignCritiqueParser.Parse(json);
            c.MaterialFit = materialFit?.Score ?? 0;
            return c;
        }
    }
}
```

- [ ] **Step 4: 프롬프트에 재료적합 근거 추가** — `src/TeampptAddin/Services/DesignCritiqueSchema.cs`

병목 진단 안내 문장을 교체:

```csharp
입력으로 받은 '초안 이해 요약'과 '검색 유사도'를 근거로 종합 판정하라.
```
→
```csharp
입력으로 받은 '초안 이해 요약'·'검색 유사도'·'재료적합 점수(계산값)'를 근거로 종합 판정하라.
재료적합 점수가 낮으면 에셋부족(양) 또는 데이터추출 쪽, 높은데 결과가 별로면 에셋품질 또는 기능 쪽이다.
```

- [ ] **Step 5: 오케스트레이터에서 스코어 계산·전달** — `src/TeampptAddin/Services/RecommendationService.cs`

`CritiqueAsync` 메서드 본문을 교체:

```csharp
        public async Task<DesignCritique> CritiqueAsync(string resultPng, RecommendationResult prior)
        {
            var critic = new DesignCritiqueService(_gemini);
            var materialFit = MaterialFitScorer.Score(prior.Recommendation, prior.Understanding);
            var c = await critic.CritiqueAsync(
                resultPng, prior.DraftPngPath, prior.Understanding, prior.Recommendation,
                prior.Trace.RetrieveLines, materialFit);
            prior.Trace.Critique = c;
            foreach (var line in prior.Trace.ToReadableLines()) Logger.Log("[Trace] " + line);
            return c;
        }
```

- [ ] **Step 6: 통과 확인**

Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~DesignCritiqueUserTextTest"`
Expected: PASS. (다른 테스트도 깨지지 않았는지 전체: `dotnet test ... --no-build` 권장.)

- [ ] **Step 7: 커밋**

```bash
git add src/TeampptAddin/Services/DesignCritiqueService.cs src/TeampptAddin/Services/DesignCritiqueSchema.cs src/TeampptAddin/Services/RecommendationService.cs src/TeampptAddin.Tests/DesignCritiqueUserTextTest.cs
git commit -m "feat(critique): materialFit을 검수 입력·결과에 배선 + BuildUserText 추출"
```

---

## Task 4: 두 점수 노출 (trace + 패널 버블)

검수 결과 표시를 두 점수로 바꾼다. `RecommendationTrace.ToReadableLines()`(순수)는 단위테스트, 패널 버블은 빌드 후 수동 확인.

**Files:**
- Modify: `src/TeampptAddin/Models/RecommendationTrace.cs` (검수 라인 두 점수)
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs:993` (RunCritiqueAsync 버블)
- Test: `src/TeampptAddin.Tests/RecommendationTraceTest.cs` (신규)

**Interfaces:**
- Consumes: `DesignCritique.MaterialFit`/`DesignConcept` (Task 1·3).
- Produces: 표시 변경만. 새 공개 시그니처 없음.

- [ ] **Step 1: 실패 테스트 작성** — `src/TeampptAddin.Tests/RecommendationTraceTest.cs`

```csharp
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class RecommendationTraceTest
    {
        [Fact]
        public void Critique_Line_Shows_Both_Scores()
        {
            var trace = new RecommendationTrace
            {
                Critique = new DesignCritique
                {
                    MaterialFit = 81, DesignConcept = 72, Verdict = "평범", Bottleneck = "에셋품질", Suggestion = "대비 강화"
                }
            };
            var lines = trace.ToReadableLines();
            Assert.Contains(lines, l => l.Contains("재료적합 81") && l.Contains("디자인·컨셉 72"));
        }
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~RecommendationTraceTest"`
Expected: FAIL (현재 라인은 `{Critique.Score}점` 형식이라 매치 안 됨).

- [ ] **Step 3: trace 라인 수정** — `src/TeampptAddin/Models/RecommendationTrace.cs`

검수 라인 한 줄 교체:

```csharp
                lines.Add($"⑤ 검수: {Critique.Score}점 — {Critique.Verdict}");
```
→
```csharp
                lines.Add($"⑤ 검수: 재료적합 {Critique.MaterialFit} / 디자인·컨셉 {Critique.DesignConcept} — {Critique.Verdict}");
```

- [ ] **Step 4: 통과 확인**

Run: 빌드 → `dotnet test ... --filter "FullyQualifiedName~RecommendationTraceTest"`
Expected: PASS.

- [ ] **Step 5: 패널 버블 표시** — `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (RunCritiqueAsync, 약 993행)

기존 검수 결과 버블의 `AddAiBubble($"검수 결과: {c.Score}점 — {c.Verdict}\n병목: {c.Bottleneck} · {c.Suggestion}"...` 호출에서 첫 줄을 두 점수로 교체:

```csharp
                AddAiBubble($"검수 결과 — 재료적합 {c.MaterialFit} / 디자인·컨셉 {c.DesignConcept}\n" +
                            $"{c.Verdict}\n병목: {c.Bottleneck} · {c.Suggestion}");
```

(기존 호출의 나머지 인자/스타일은 그대로 둔다. `c.Score`만 두 점수로 대체.)

- [ ] **Step 6: 빌드 + 수동 확인**

Run: 빌드(위 절차) → 오류 0건 확인. 빌드 로그 tail 확인.
수동: PowerPoint 재시작 → 본문 슬라이드 "AI 리디자인" → 추천 → "🔍 디자이너 검수 받기" → 버블·trace 패널에 **재료적합 / 디자인·컨셉 두 점수**가 보이는지.

- [ ] **Step 7: 커밋**

```bash
git add src/TeampptAddin/Models/RecommendationTrace.cs src/TeampptAddin/UI/Wpf/AssetPanel.cs src/TeampptAddin.Tests/RecommendationTraceTest.cs
git commit -m "feat(ui): 검수 결과에 재료적합/디자인·컨셉 두 점수 노출"
```

---

## Self-Review

**1. Spec coverage (Phase 0):** spec의 Phase 0 = "materialFit(계산)/designConcept(LLM) 두 축 분리 + 추천·검수 카드에 두 배지 + 병목 4분류가 두 축으로 설명". → Task 1(designConcept 도출), Task 2(materialFit 계산), Task 3(materialFit을 병목 입력·결과에 배선), Task 4(두 점수 표시)로 전부 커버. 추천 *카드* 자체(검수 전)의 배지는 이 Phase에선 검수 결과 표시로 충족(추천 시점 배지는 후속 Phase 3 UI에서 확장 — spec도 "추천 카드·검수 결과"로 묶음).

**2. Placeholder scan:** TBD/TODO/"적절히 처리" 없음. 모든 코드 스텝에 실제 코드. 모든 Run에 실제 명령·기대결과.

**3. Type consistency:** `MaterialFitResult{Score,SimilarityAvg,CapacityScore,Note}` — Task 2 정의, Task 3에서 `materialFit.Score`/`.Note` 사용 일치. `DesignCritique.MaterialFit`/`.DesignConcept` — Task 1 정의, Task 3(주입)·Task 4(표시) 사용 일치. `BuildUserText(u, rec, retrieveLines, materialFit)` — Task 3 정의·테스트·CritiqueAsync 호출 일치. `MaterialFitScorer.Score(rec, u)` — Task 2 정의, Task 3(RecommendationService) 호출 일치. `HeaderAsset.Capacity`(AssetCapacity{Min,Max})·`Extra["similarity"]`·`NeededCombination.Component` — 기존 모델과 일치(확인됨).

**4. Ambiguity:** materialFit 공식(가중치 0.6/0.4, 중립 0.5/1.0, capacity dist 패널티)을 코드·테스트로 못박음. designConcept = 6차원 합으로 결정적.

## Execution Handoff

플랜 완료. 저장 위치: `docs/superpowers/plans/2026-06-25-redesign-phase0-dual-similarity.md`.

두 실행 옵션:
1. **Subagent-Driven (권장)** — 태스크마다 새 서브에이전트, 사이사이 리뷰, 빠른 반복.
2. **Inline Execution** — 이 세션에서 executing-plans로 체크포인트 배치 실행.
