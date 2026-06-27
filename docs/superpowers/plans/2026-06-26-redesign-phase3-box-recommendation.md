# 리디자인 Phase 3 — 박스별 덱 추천 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 1이 읽은 덱 구조(`DeckStructure`)와 Phase 2가 저장한 선택 컨셉(`_selectedConcept`)을 소비해, 구조 박스마다(표지·공통헤더·본문 패턴별·목차·간지·엔드) 적합 에셋 조합을 추천하고 추천 카드에 두 유사도 배지(재료적합·컨셉적합)를 붙여 보여준다.

**Architecture:** 순수 유닛 3개(`BodyPatternClusterer`·`ConceptFitScorer`·`DeckBoxPlanner`, 토큰0·TDD)가 본문 패턴 묶기·컨셉적합 채점·박스 순서를 결정한다. COM 유닛 `DeckSlideImageExporter`가 대표 본문 장만 PNG로 내보내고, 통합 브레인 `DeckRecommendationOrchestrator`가 재사용 서비스(`DraftUnderstandingService`/`CombinationCandidateProvider`/`CombinationRecommender`/`MaterialFitScorer`)를 엮어 박스별 추천을 만든다. `AssetPanel`이 컨셉 선택 직후 이를 자동 실행해 박스 카드로 렌더한다. 멀티모달·LLM 호출은 덱 장수가 아니라 본문 패턴 수(2~4)에만 비례한다.

**Tech Stack:** C# / .NET Framework 4.8, old-style csproj(신규 .cs는 `<Compile Include>` 수동 등록), WPF(`AssetPanel`), Microsoft.Office.Interop.PowerPoint(COM/STA), Newtonsoft.Json, xUnit 테스트(`TeampptAddin.Tests`, SDK-style → 자동 include).

## Global Constraints

- **사실 = COM/벡터/계산, 판단 = LLM.** 텍스트·개수·도형은 COM 원문, 두 배지는 계산값, 역할·조합 선택만 LLM. LLM은 텍스트 내용을 생성·수정하지 않는다.
- **비파괴.** 원본 초안 파일은 ReadOnly hidden open, 어떤 경로에서도 불변. 반드시 `Close` + `Marshal.ReleaseComObject`.
- **두 토큰 예산.** 멀티모달 이해·LLM 조합 추천 호출은 본문 패턴 수(2~4)에만 비례. slide-box·공통 header는 top1(LLM0).
- **좌표 변환** `CoordinateConverter` 규칙 준수, 폴백 로직 추가 금지.
- **API 키를 문서·커밋에 평문으로 넣지 않는다.**
- **이 Phase의 끝점 = 추천 카드 표시까지.** 빈 템플릿 덱 조립(다중 에셋 합성)·LLM 비전 `designConcept` 결과채점은 Phase 4/5 별도 범위.
- **신규 .cs는 `src/TeampptAddin/TeampptAddin.csproj`의 `<Compile Include>`에 수동 등록 필수**(old-style csproj). 테스트 프로젝트는 SDK-style이라 자동 include.
- **COM/STA:** COM 호출(`DeckSlideImageExporter.Export`)은 첫 네트워크 `await` 이전, UI STA 스레드에서 동기로 끝나야 한다. 오케스트레이터·패널 코드는 기존 `RecommendationService`/`RunRecommendationAsync`와 동일하게 **`ConfigureAwait(false)`를 쓰지 않는다**(UI 컨텍스트 유지).

---

## 실행 DAG · 모델/effort 배분 (스펙 A~I 표 확정)

> 스펙 `2026-06-26-redesign-phase3-box-recommendation-design.md`의 "실행 전략" 표를 의존·병렬·subagent 모델 지정까지 확정한 것. **구현은 superpowers:subagent-driven-development(순차, Task마다 fresh subagent + 2단계 리뷰)** 로 한다. 아래 "Wave"는 의존 위상(병렬 *가능* 구간)을 보여주며, 순차 실행 시 같은 Wave는 임의 순서로 처리한다(순차라 csproj 충돌 없음). 동시 실행을 원하면 Wave 1은 worktree 격리가 필요하다.

| Task | 컴포넌트 | 구현 모델 | 리뷰 모델 | effort | 의존(after) | Wave |
|---|---|---|---|---|---|---|
| A | `DeckRecommendationModels`(타입 스캐폴딩) | Sonnet | — (빌드만) | 낮음 | — | 0 |
| B | `BodyPatternClusterer`(순수·TDD) | Sonnet | Sonnet 로직 | 표준 | A | 1 |
| C | `ConceptFitScorer`(순수·TDD) | Sonnet | Sonnet 로직 | 표준 | A | 1 |
| D | `DeckBoxPlanner`(순수·TDD) | Sonnet | Sonnet 로직 | 표준 | A(타입만; B와 독립) | 1 |
| E | `CombinationCandidateProvider` 컨셉 주입(**공유·검증 코드**) | Sonnet | **Opus max 필수** | 높음 | — (A 불요) | 1 |
| F | `DeckSlideImageExporter`(COM) | Sonnet | Sonnet 로직 | 표준 | A(타입 불요지만 순서상) | 1 |
| G | `DeckRecommendationOrchestrator`(**통합 브레인**) | **Opus max** | Opus | 높음 | B·C·D·E·F | 2 |
| H | `AssetPanel` UI 연결·자동실행·박스카드·두 배지 | Sonnet | **Opus 최종** | 표준 | G | 3 |
| I | 최종 통합 리뷰 + PPT 수동검증 게이트 | **Opus max** | — | 높음 | H | 4 |

```
        ┌── B (Sonnet/TDD) ──┐
        ├── C (Sonnet/TDD) ──┤
A ──────┼── D (Sonnet/TDD) ──┼──► G (Opus) ──► H (Sonnet+Opus리뷰) ──► I (Opus 게이트)
        ├── E (Sonnet/Opus리뷰)┤
        └── F (Sonnet/COM) ───┘
```

- **D 메모:** `DeckBoxPlanner`는 `BodyPattern` *타입*(A에서 정의)만 소비하므로 B의 구현과 독립 — Wave 1에서 B와 병렬 가능. (스펙의 "D(B 후)"는 개념적 순서일 뿐 코드 의존 아님.)
- **E 메모:** A에 의존하지 않음(기존 타입만 수정). 가장 먼저 착수해도 됨. **Route A 회귀 차단이 핵심** → Opus 리뷰 필수.
- Agent `model` 파라미터: `opus`(G·I 구현, E·H 리뷰) / `sonnet`(나머지 구현·로직리뷰). 이 환경에서는 glm으로 자동 매핑됨.
- 브랜치: `feat/asset-combination-recommendation` (현재 브랜치 그대로). 통합(머지/PR)은 Task I 이후 `superpowers:finishing-a-development-branch`로 결정.

---

## 빌드 & 테스트 명령 (참조)

순수 로직 Task(A·B·C·D·E)는 **UAC 없이** 빌드·테스트한다(`RegisterForComInterop=false`). COM/UI Task(F·G·H)는 컴파일만 비-UAC 빌드로 검증하고, 실제 동작은 Task I의 관리자 빌드 + PPT 수동검증에서 확인한다.

**[BUILD-LOGIC]** — 비-UAC 솔루션 빌드(전체 컴파일, COM 등록 없음). foreground로 실행(`run_in_background` 금지):
```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal
```
기대: 마지막 줄 `0 Error(s)`. 오류가 있으면 컴파일 실패 — 고치고 재빌드.

**[TEST]** — 빌드된 테스트 어셈블리에서 필터 실행(재빌드 안 함):
```powershell
dotnet test "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~<TestClass>"
```

**[BUILD-DEPLOY]** — Task I 전용. 관리자 권한 COM 등록 빌드(PPT 수동검증용). CLAUDE.md 절차:
```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
직후 반드시 검증: ① `stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll`(1분 이내) ② `tail -5 c:/Projects/teamppt-addin/build.log`(오류 0건).

---

## File Structure

**신규 파일**
- `src/TeampptAddin/Models/DeckRecommendationModels.cs` — `BodyPattern`·`BoxPlan`·`ConceptFitResult`·`BoxRecommendation`·`DeckRecommendation` POCO (Task A)
- `src/TeampptAddin/Services/BodyPatternClusterer.cs` — 본문 패턴 묶기(순수) (Task B)
- `src/TeampptAddin/Services/ConceptFitScorer.cs` — 컨셉적합 채점(순수) (Task C)
- `src/TeampptAddin/Services/DeckBoxPlanner.cs` — 박스 순서 계획(순수) (Task D)
- `src/TeampptAddin/Core/DeckSlideImageExporter.cs` — 대표 장 PNG 내보내기(COM) (Task F)
- `src/TeampptAddin/Services/DeckRecommendationOrchestrator.cs` — 통합 브레인 (Task G)
- 테스트(SDK-style, 자동 include): `src/TeampptAddin.Tests/BodyPatternClustererTest.cs`(B)·`ConceptFitScorerTest.cs`(C)·`DeckBoxPlannerTest.cs`(D)·`CombinationCandidateProviderQueryTest.cs`(E)

**수정 파일**
- `src/TeampptAddin/Services/CombinationCandidateProvider.cs` — `GetCandidatesAsync`에 `DesignConcept concept = null` 주입 + `BuildQuery` 추출 (Task E)
- `src/TeampptAddin/UI/Wpf/AssetPanel.cs` — 필드·`RunDeckRedesignAsync` 보관·`OnConceptSelected` 자동실행·`RunDeckRecommendAsync`·박스 카드 (Task H)
- `src/TeampptAddin/UI/TaskPaneHost.cs` — 오케스트레이터 생성·`InitAi` 인자 (Task H)
- `src/TeampptAddin/TeampptAddin.csproj` — 신규 .cs `<Compile Include>` 5건 (각 Task가 자기 파일 등록)

---

### Task A: 모델/타입 스캐폴딩 (`DeckRecommendationModels`)

**Files:**
- Create: `src/TeampptAddin/Models/DeckRecommendationModels.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (Models 그룹에 Compile Include 추가)

**Interfaces:**
- Consumes: 기존 타입 `CombinationRecommendation`·`MaterialFitResult`·`DesignConcept`(이미 존재).
- Produces (이후 모든 Task가 의존하는 타입):
  - `BodyPattern { string Signature; List<int> SlideIndexes; int RepresentativeIndex }`
  - `BoxPlan { string BoxKind; string Label; List<int> CoveredSlideIndexes; int? RepresentativeIndex; string Signature }`
  - `ConceptFitResult { int Score; string Note }`
  - `BoxRecommendation { BoxPlan Plan; CombinationRecommendation Recommendation; MaterialFitResult MaterialFit; ConceptFitResult ConceptFit }`
  - `DeckRecommendation { List<BoxRecommendation> Boxes; DesignConcept Concept }`

- [ ] **Step 1: 모델 파일 생성**

```csharp
// src/TeampptAddin/Models/DeckRecommendationModels.cs
using System.Collections.Generic;

namespace TeampptAddin
{
    /// <summary>본문 슬라이드를 도형 시그니처로 묶은 패턴(대표 1장 + 같은 패턴 장들). 토큰0.</summary>
    public class BodyPattern
    {
        public string Signature { get; set; } = "";
        public List<int> SlideIndexes { get; set; } = new List<int>();   // 1-based
        public int RepresentativeIndex { get; set; }
    }

    /// <summary>박스 하나의 계획. BoxKind = cover/header/body/toc/section/end.</summary>
    public class BoxPlan
    {
        public string BoxKind { get; set; } = "";
        public string Label { get; set; } = "";
        public List<int> CoveredSlideIndexes { get; set; } = new List<int>();
        public int? RepresentativeIndex { get; set; }
        public string Signature { get; set; } = "";   // body 박스만(패턴 식별)
    }

    /// <summary>컨셉적합 점수(계산값, 토큰0).</summary>
    public class ConceptFitResult
    {
        public int Score { get; set; }     // 0-100
        public string Note { get; set; } = "";
    }

    /// <summary>박스 하나의 추천 결과 + 두 배지.</summary>
    public class BoxRecommendation
    {
        public BoxPlan Plan { get; set; }
        public CombinationRecommendation Recommendation { get; set; }
        public MaterialFitResult MaterialFit { get; set; }
        public ConceptFitResult ConceptFit { get; set; }
    }

    /// <summary>덱 전체 박스별 추천.</summary>
    public class DeckRecommendation
    {
        public List<BoxRecommendation> Boxes { get; set; } = new List<BoxRecommendation>();
        public DesignConcept Concept { get; set; }
    }
}
```

- [ ] **Step 2: csproj 등록**

`src/TeampptAddin/TeampptAddin.csproj`에서 `<Compile Include="Models\RecommendationModels.cs" />` 줄 바로 아래에 추가:

```xml
    <Compile Include="Models\DeckRecommendationModels.cs" />
```

- [ ] **Step 3: 빌드 검증**

Run: **[BUILD-LOGIC]**
Expected: `0 Error(s)`. (스캐폴딩이라 테스트 없음 — 컴파일 성공이 게이트.)

- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Models/DeckRecommendationModels.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase3): 박스별 추천 모델/타입 스캐폴딩 (Task A)"
```

---

### Task B: `BodyPatternClusterer` (순수·TDD)

**Files:**
- Create: `src/TeampptAddin/Services/BodyPatternClusterer.cs`
- Test: `src/TeampptAddin.Tests/BodyPatternClustererTest.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj`

**Interfaces:**
- Consumes: `DraftProfile { int SlideIndex; float SlideWidth; float SlideHeight; List<DraftShape> Shapes }`, `DraftShape { string Kind(text|image|table|chart); float Left; float Width }`, `BodyPattern`(Task A).
- Produces: `List<BodyPattern> BodyPatternClusterer.Cluster(List<DraftProfile> bodyProfiles)` (대표=최소 인덱스, 결과는 RepresentativeIndex 오름차순). 보조 static `Signature(DraftProfile)`·`EstimateColumns(DraftProfile)`.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
// src/TeampptAddin.Tests/BodyPatternClustererTest.cs
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class BodyPatternClustererTest
    {
        private static DraftProfile P(int idx, params (string kind, float left, float width)[] shapes)
        {
            var p = new DraftProfile { SlideIndex = idx, SlideWidth = 960, SlideHeight = 540 };
            foreach (var s in shapes)
                p.Shapes.Add(new DraftShape { Kind = s.kind, Left = s.left, Width = s.width });
            return p;
        }

        [Fact]
        public void Same_Signature_Groups_Into_One_Pattern()
        {
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile>
            {
                P(3, ("text", 0, 300), ("image", 600, 300)),
                P(4, ("text", 0, 300), ("image", 600, 300)),
            });
            Assert.Single(r);
            Assert.Equal(new[] { 3, 4 }, r[0].SlideIndexes.ToArray());
            Assert.Equal(3, r[0].RepresentativeIndex);
        }

        [Fact]
        public void Different_Shape_Counts_Make_Two_Patterns()
        {
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile>
            {
                P(2, ("text", 0, 300)),
                P(3, ("text", 0, 300), ("text", 0, 300), ("text", 0, 300)),
            });
            Assert.Equal(2, r.Count);
        }

        [Fact]
        public void Column_Difference_Makes_Two_Patterns()
        {
            var oneCol = P(2, ("text", 0, 300), ("text", 0, 300));      // 둘 다 왼쪽 1/3 → 1열
            var twoCol = P(3, ("text", 0, 300), ("text", 660, 300));    // 왼/오 → 2열
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile> { oneCol, twoCol });
            Assert.Equal(2, r.Count);
        }

        [Fact]
        public void Empty_Input_Returns_Empty()
        {
            Assert.Empty(BodyPatternClusterer.Cluster(new List<DraftProfile>()));
        }

        [Fact]
        public void Representative_Is_Lowest_Index_Regardless_Of_Order()
        {
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile>
            {
                P(7, ("text", 0, 300)),
                P(2, ("text", 0, 300)),
            });
            Assert.Single(r);
            Assert.Equal(2, r[0].RepresentativeIndex);
            Assert.Equal(new[] { 2, 7 }, r[0].SlideIndexes.ToArray());
        }
    }
}
```

- [ ] **Step 2: 최소 스텁 + csproj 등록 (컴파일되게, 일부러 빈 결과)**

```csharp
// src/TeampptAddin/Services/BodyPatternClusterer.cs
using System.Collections.Generic;

namespace TeampptAddin
{
    public static class BodyPatternClusterer
    {
        public static List<BodyPattern> Cluster(List<DraftProfile> bodyProfiles)
            => new List<BodyPattern>();   // stub — RED
    }
}
```

csproj `<Compile Include="Services\MaterialFitScorer.cs" />` 줄 아래에 추가:
```xml
    <Compile Include="Services\BodyPatternClusterer.cs" />
```

- [ ] **Step 3: 빌드 후 테스트 실행 → RED 확인**

Run: **[BUILD-LOGIC]** 그 다음 **[TEST]** with `BodyPatternClustererTest`
Expected: 4 FAIL (스텁이 빈 리스트 반환 → `Single`/`Equal(2)`/`Equal` 실패), `Empty_Input` 1 PASS.

- [ ] **Step 4: 실제 구현**

```csharp
// src/TeampptAddin/Services/BodyPatternClusterer.cs
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>본문 DraftProfile들을 도형 시그니처(kind 멀티셋 버킷 + 대략 열 수)로 묶는다. 토큰0.</summary>
    public static class BodyPatternClusterer
    {
        public static List<BodyPattern> Cluster(List<DraftProfile> bodyProfiles)
        {
            var profiles = bodyProfiles ?? new List<DraftProfile>();
            var order = new List<BodyPattern>();
            var bySig = new Dictionary<string, BodyPattern>();
            foreach (var p in profiles)
            {
                var sig = Signature(p);
                if (!bySig.TryGetValue(sig, out var pat))
                {
                    pat = new BodyPattern { Signature = sig };
                    bySig[sig] = pat;
                    order.Add(pat);
                }
                pat.SlideIndexes.Add(p.SlideIndex);
            }
            foreach (var pat in order)
            {
                pat.SlideIndexes = pat.SlideIndexes.OrderBy(i => i).ToList();
                pat.RepresentativeIndex = pat.SlideIndexes.FirstOrDefault();
            }
            return order.OrderBy(g => g.RepresentativeIndex).ToList();
        }

        public static string Signature(DraftProfile p)
        {
            int t = 0, i = 0, b = 0, c = 0;
            foreach (var s in p.Shapes ?? new List<DraftShape>())
            {
                switch (s.Kind)
                {
                    case "text": t++; break;
                    case "image": i++; break;
                    case "table": b++; break;
                    case "chart": c++; break;
                }
            }
            return $"t{Bucket(t)}i{Bucket(i)}b{Bucket(b)}c{Bucket(c)}|col{EstimateColumns(p)}";
        }

        private static string Bucket(int n) => n >= 4 ? "4+" : n.ToString();

        /// <summary>도형 가로 중심을 1/3 밴드로 나눠 점유 밴드 수 = 대략 열 수.</summary>
        public static int EstimateColumns(DraftProfile p)
        {
            float w = p.SlideWidth > 0 ? p.SlideWidth : 1f;
            var thirds = new HashSet<int>();
            foreach (var s in p.Shapes ?? new List<DraftShape>())
            {
                if (s.Width <= 0) continue;
                float center = (s.Left + s.Width / 2f) / w;
                int third = center < 1f / 3f ? 0 : center < 2f / 3f ? 1 : 2;
                thirds.Add(third);
            }
            return thirds.Count;
        }
    }
}
```

- [ ] **Step 5: 빌드 후 테스트 → GREEN**

Run: **[BUILD-LOGIC]** 그 다음 **[TEST]** with `BodyPatternClustererTest`
Expected: 5 PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/BodyPatternClusterer.cs src/TeampptAddin.Tests/BodyPatternClustererTest.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase3): BodyPatternClusterer 본문 패턴 묶기 (Task B, TDD 5/5)"
```

---

### Task C: `ConceptFitScorer` (순수·TDD)

**Files:**
- Create: `src/TeampptAddin/Services/ConceptFitScorer.cs`
- Test: `src/TeampptAddin.Tests/ConceptFitScorerTest.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj`

**Interfaces:**
- Consumes: `HeaderAsset { List<string> Tags; List<AssetColor> Colors; List<AssetFont> Fonts }`, `AssetColor { string Role }`, `AssetFont { string Role }`, `DesignConcept { List<string> StyleTags; Dictionary<string,string> Colors; Dictionary<string,string> Fonts }`, `CombinationRecommendation`(Slide/Header/Layout/Components), `ConceptFitResult`(Task A).
- Produces: 두 오버로드
  - `ConceptFitResult ConceptFitScorer.Score(CombinationRecommendation rec, DesignConcept concept)`
  - `ConceptFitResult ConceptFitScorer.Score(HeaderAsset asset, DesignConcept concept)`
  - 가중합 = `0.5*tagCover + 0.25*colorRoleCover + 0.25*fontRoleCover`, 각 cover = `needed 비었으면 0.5(중립), 아니면 |needed∩have|/|needed|`(대소문자 무시). 에셋 없으면 Score 0.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
// src/TeampptAddin.Tests/ConceptFitScorerTest.cs
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class ConceptFitScorerTest
    {
        private static DesignConcept Concept() => new DesignConcept
        {
            StyleTags = new List<string> { "minimal", "trust" },
            Colors = new Dictionary<string, string> { ["main"] = "#111", ["text"] = "#222" },
            Fonts = new Dictionary<string, string> { ["heading"] = "Noto" }
        };

        private static HeaderAsset Asset(List<string> tags, List<string> colorRoles, List<string> fontRoles)
            => new HeaderAsset
            {
                Tags = tags,
                Colors = colorRoles.Select(r => new AssetColor { Role = r }).ToList(),
                Fonts = fontRoles.Select(r => new AssetFont { Role = r }).ToList()
            };

        [Fact]
        public void Full_Cover_Scores_100()
        {
            var a = Asset(new List<string> { "minimal", "trust" },
                new List<string> { "main", "text" }, new List<string> { "heading" });
            Assert.Equal(100, ConceptFitScorer.Score(a, Concept()).Score);
        }

        [Fact]
        public void Half_Tags_Full_Roles_Scores_75()
        {
            var a = Asset(new List<string> { "minimal" },
                new List<string> { "main", "text" }, new List<string> { "heading" });
            // 0.5*0.5 + 0.25*1 + 0.25*1 = 0.75
            Assert.Equal(75, ConceptFitScorer.Score(a, Concept()).Score);
        }

        [Fact]
        public void No_Overlap_Scores_0()
        {
            var a = Asset(new List<string> { "loud" },
                new List<string> { "accent" }, new List<string> { "body" });
            Assert.Equal(0, ConceptFitScorer.Score(a, Concept()).Score);
        }

        [Fact]
        public void Empty_Concept_StyleTags_Is_Neutral_Half()
        {
            var concept = new DesignConcept
            {
                StyleTags = new List<string>(),
                Colors = new Dictionary<string, string> { ["main"] = "#111" },
                Fonts = new Dictionary<string, string> { ["heading"] = "Noto" }
            };
            var a = Asset(new List<string>(), new List<string> { "main" }, new List<string> { "heading" });
            // tag 중립 0.5 → 0.5*0.5 + 0.25*1 + 0.25*1 = 0.75
            Assert.Equal(75, ConceptFitScorer.Score(a, concept).Score);
        }

        [Fact]
        public void No_Assets_Scores_0()
        {
            Assert.Equal(0, ConceptFitScorer.Score(new CombinationRecommendation(), Concept()).Score);
        }

        [Fact]
        public void Recommendation_Overload_Unions_Across_Slots()
        {
            var rec = new CombinationRecommendation
            {
                Header = new RecommendedSlot { Asset = Asset(new List<string> { "minimal" },
                    new List<string> { "main" }, new List<string>()) },
                Layout = new RecommendedSlot { Asset = Asset(new List<string> { "trust" },
                    new List<string> { "text" }, new List<string> { "heading" }) }
            };
            // union tags {minimal,trust}=1, colors {main,text}=1, fonts {heading}=1 → 100
            Assert.Equal(100, ConceptFitScorer.Score(rec, Concept()).Score);
        }
    }
}
```

(테스트 상단에 `using System.Linq;`가 필요하다 — `Select`/`ToList` 사용. 위 파일에 추가할 것.)

- [ ] **Step 2: 최소 스텁 + csproj 등록**

```csharp
// src/TeampptAddin/Services/ConceptFitScorer.cs
namespace TeampptAddin
{
    public static class ConceptFitScorer
    {
        public static ConceptFitResult Score(CombinationRecommendation rec, DesignConcept concept)
            => new ConceptFitResult { Score = 0 };   // stub — RED
        public static ConceptFitResult Score(HeaderAsset asset, DesignConcept concept)
            => new ConceptFitResult { Score = 0 };   // stub — RED
    }
}
```

csproj `<Compile Include="Services\BodyPatternClusterer.cs" />` 줄 아래에 추가:
```xml
    <Compile Include="Services\ConceptFitScorer.cs" />
```

- [ ] **Step 3: 빌드 후 테스트 → RED**

Run: **[BUILD-LOGIC]** 그 다음 **[TEST]** with `ConceptFitScorerTest`
Expected: `Full_Cover`/`Half_Tags`/`Empty_Concept`/`Recommendation_Overload` FAIL(0 반환), `No_Overlap`/`No_Assets` PASS.

- [ ] **Step 4: 실제 구현**

```csharp
// src/TeampptAddin/Services/ConceptFitScorer.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>컨셉적합(계산값, 토큰0): 에셋 Tags ∩ concept.StyleTags 커버 + 색/폰트 Role 커버 가중합.</summary>
    public static class ConceptFitScorer
    {
        public static ConceptFitResult Score(CombinationRecommendation rec, DesignConcept concept)
        {
            var assets = new List<HeaderAsset>();
            if (rec?.Slide?.Asset != null) assets.Add(rec.Slide.Asset);
            if (rec?.Header?.Asset != null) assets.Add(rec.Header.Asset);
            if (rec?.Layout?.Asset != null) assets.Add(rec.Layout.Asset);
            if (rec?.Components != null)
                assets.AddRange(rec.Components.Where(s => s?.Asset != null).Select(s => s.Asset));
            return ScoreAssets(assets, concept);
        }

        public static ConceptFitResult Score(HeaderAsset asset, DesignConcept concept)
            => ScoreAssets(asset != null ? new List<HeaderAsset> { asset } : new List<HeaderAsset>(), concept);

        private static ConceptFitResult ScoreAssets(List<HeaderAsset> assets, DesignConcept concept)
        {
            if (assets == null || assets.Count == 0)
                return new ConceptFitResult { Score = 0, Note = "선택 에셋 없음" };

            var tags = Union(assets.SelectMany(a => a.Tags ?? new List<string>()));
            var colorRoles = Union(assets.SelectMany(a => (a.Colors ?? new List<AssetColor>()).Select(c => c.Role)));
            var fontRoles = Union(assets.SelectMany(a => (a.Fonts ?? new List<AssetFont>()).Select(f => f.Role)));

            double tagScore = Cover(concept?.StyleTags, tags);
            double colorScore = Cover(concept?.Colors?.Keys, colorRoles);
            double fontScore = Cover(concept?.Fonts?.Keys, fontRoles);

            double weighted = 0.5 * tagScore + 0.25 * colorScore + 0.25 * fontScore;
            int score = Math.Max(0, Math.Min(100, (int)Math.Round(100.0 * weighted)));
            return new ConceptFitResult
            {
                Score = score,
                Note = $"스타일 {tagScore:P0}, 색 {colorScore:P0}, 폰트 {fontScore:P0}"
            };
        }

        private static HashSet<string> Union(IEnumerable<string> values)
            => new HashSet<string>((values ?? Enumerable.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant()));

        private static double Cover(IEnumerable<string> needed, HashSet<string> have)
        {
            var need = (needed ?? Enumerable.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant())
                .Distinct().ToList();
            if (need.Count == 0) return 0.5;   // 신호 없음 → 중립
            int matched = need.Count(n => have.Contains(n));
            return (double)matched / need.Count;
        }
    }
}
```

- [ ] **Step 5: 빌드 후 테스트 → GREEN**

Run: **[BUILD-LOGIC]** 그 다음 **[TEST]** with `ConceptFitScorerTest`
Expected: 6 PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/ConceptFitScorer.cs src/TeampptAddin.Tests/ConceptFitScorerTest.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase3): ConceptFitScorer 컨셉적합 채점 (Task C, TDD 6/6)"
```

---

### Task D: `DeckBoxPlanner` (순수·TDD)

**Files:**
- Create: `src/TeampptAddin/Services/DeckBoxPlanner.cs`
- Test: `src/TeampptAddin.Tests/DeckBoxPlannerTest.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj`

**Interfaces:**
- Consumes: `DeckStructure { List<DeckSlideStructure> Slides }`, `DeckSlideStructure { int Index; string Kind(cover/toc/body/section/end); string Label }`, `BodyPattern`(Task A).
- Produces: `List<BoxPlan> DeckBoxPlanner.Plan(DeckStructure structure, List<BodyPattern> patterns)`.
  - 순서: 구조를 Index 순회 — 비-body는 그 위치에 slide-box로, **첫 body를 만나면** 공통 `header` 박스 1개 + 패턴 박스들(RepresentativeIndex 오름차순)을 삽입, 이후 body는 skip.
  - body 0장이면 header 박스 없음.

- [ ] **Step 1: 실패 테스트 작성**

```csharp
// src/TeampptAddin.Tests/DeckBoxPlannerTest.cs
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckBoxPlannerTest
    {
        private static DeckStructure Struct(params (int idx, string kind)[] slides)
        {
            var st = new DeckStructure { TotalCount = slides.Length };
            foreach (var s in slides)
                st.Slides.Add(new DeckSlideStructure { Index = s.idx, Kind = s.kind, Label = s.kind });
            return st;
        }

        private static BodyPattern Pat(int rep, params int[] idx)
            => new BodyPattern { Signature = "sig" + rep, RepresentativeIndex = rep, SlideIndexes = idx.ToList() };

        [Fact]
        public void Cover_Body_End_Yields_Cover_Header_Body_End()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "body"), (3, "body"), (4, "end")),
                new List<BodyPattern> { Pat(2, 2, 3) });
            Assert.Equal(new[] { "cover", "header", "body", "end" }, boxes.Select(b => b.BoxKind).ToArray());
        }

        [Fact]
        public void No_Body_Skips_Header()
        {
            var boxes = DeckBoxPlanner.Plan(Struct((1, "cover"), (2, "end")), new List<BodyPattern>());
            Assert.Equal(new[] { "cover", "end" }, boxes.Select(b => b.BoxKind).ToArray());
        }

        [Fact]
        public void Two_Patterns_Become_Two_Body_Boxes_In_Rep_Order()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "body"), (3, "body"), (4, "body"), (5, "end")),
                new List<BodyPattern> { Pat(2, 2, 4), Pat(3, 3) });
            Assert.Equal(new[] { "cover", "header", "body", "body", "end" }, boxes.Select(b => b.BoxKind).ToArray());
            var bodyBoxes = boxes.Where(b => b.BoxKind == "body").ToList();
            Assert.Equal(2, bodyBoxes[0].RepresentativeIndex);
            Assert.Equal(3, bodyBoxes[1].RepresentativeIndex);
        }

        [Fact]
        public void Toc_Before_Body_Keeps_Its_Position()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "toc"), (3, "body"), (4, "end")),
                new List<BodyPattern> { Pat(3, 3) });
            Assert.Equal(new[] { "cover", "toc", "header", "body", "end" }, boxes.Select(b => b.BoxKind).ToArray());
        }

        [Fact]
        public void Header_Box_Covers_All_Body_Indexes_With_First_Rep()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "body"), (3, "body")),
                new List<BodyPattern> { Pat(2, 2, 3) });
            var header = boxes.First(b => b.BoxKind == "header");
            Assert.Equal(new[] { 2, 3 }, header.CoveredSlideIndexes.ToArray());
            Assert.Equal(2, header.RepresentativeIndex);
        }
    }
}
```

- [ ] **Step 2: 최소 스텁 + csproj 등록**

```csharp
// src/TeampptAddin/Services/DeckBoxPlanner.cs
using System.Collections.Generic;

namespace TeampptAddin
{
    public static class DeckBoxPlanner
    {
        public static List<BoxPlan> Plan(DeckStructure structure, List<BodyPattern> patterns)
            => new List<BoxPlan>();   // stub — RED
    }
}
```

csproj `<Compile Include="Services\ConceptFitScorer.cs" />` 줄 아래에 추가:
```xml
    <Compile Include="Services\DeckBoxPlanner.cs" />
```

- [ ] **Step 3: 빌드 후 테스트 → RED**

Run: **[BUILD-LOGIC]** 그 다음 **[TEST]** with `DeckBoxPlannerTest`
Expected: 5 FAIL (빈 리스트).

- [ ] **Step 4: 실제 구현**

```csharp
// src/TeampptAddin/Services/DeckBoxPlanner.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>덱 구조 + 본문 패턴 → 박스 계획. 표지→[공통헤더]→본문패턴들→엔드, toc/section은 위치대로 slide-box. 토큰0.</summary>
    public static class DeckBoxPlanner
    {
        public static List<BoxPlan> Plan(DeckStructure structure, List<BodyPattern> patterns)
        {
            var boxes = new List<BoxPlan>();
            var slides = structure?.Slides ?? new List<DeckSlideStructure>();
            var pats = (patterns ?? new List<BodyPattern>()).OrderBy(p => p.RepresentativeIndex).ToList();
            bool bodyInserted = false;

            foreach (var s in slides.OrderBy(x => x.Index))
            {
                if (string.Equals(s.Kind, "body", StringComparison.OrdinalIgnoreCase))
                {
                    if (bodyInserted) continue;
                    bodyInserted = true;

                    var allBodyIdx = pats.SelectMany(p => p.SlideIndexes).OrderBy(i => i).ToList();
                    boxes.Add(new BoxPlan
                    {
                        BoxKind = "header",
                        Label = "공통 헤더",
                        CoveredSlideIndexes = allBodyIdx,
                        RepresentativeIndex = pats.Count > 0 ? pats[0].RepresentativeIndex : (int?)null
                    });
                    foreach (var p in pats)
                        boxes.Add(new BoxPlan
                        {
                            BoxKind = "body",
                            Label = $"본문 패턴 ({p.SlideIndexes.Count}장)",
                            CoveredSlideIndexes = p.SlideIndexes.ToList(),
                            RepresentativeIndex = p.RepresentativeIndex,
                            Signature = p.Signature
                        });
                }
                else
                {
                    boxes.Add(new BoxPlan
                    {
                        BoxKind = (s.Kind ?? "").ToLowerInvariant(),
                        Label = string.IsNullOrEmpty(s.Label) ? s.Kind : s.Label,
                        CoveredSlideIndexes = new List<int> { s.Index },
                        RepresentativeIndex = s.Index
                    });
                }
            }
            return boxes;
        }
    }
}
```

- [ ] **Step 5: 빌드 후 테스트 → GREEN**

Run: **[BUILD-LOGIC]** 그 다음 **[TEST]** with `DeckBoxPlannerTest`
Expected: 5 PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/DeckBoxPlanner.cs src/TeampptAddin.Tests/DeckBoxPlannerTest.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase3): DeckBoxPlanner 박스 순서 계획 (Task D, TDD 5/5)"
```

---

### Task E: `CombinationCandidateProvider` 컨셉 주입 (공유·검증 코드 — Opus 리뷰 필수)

**Files:**
- Modify: `src/TeampptAddin/Services/CombinationCandidateProvider.cs`
- Test: `src/TeampptAddin.Tests/CombinationCandidateProviderQueryTest.cs`

**Interfaces:**
- Consumes: `DraftUnderstanding { string Purpose; string MatchIntent; Dictionary<string,int> Counts }`, `DesignConcept { List<string> StyleTags }`.
- Produces:
  - `static string CombinationCandidateProvider.BuildQuery(DraftUnderstanding u, DesignConcept concept)` — concept이 null이거나 StyleTags가 비면 기존 `"{Purpose} | {MatchIntent} ({counts})"` 그대로; 있으면 `"{Purpose} | {MatchIntent} | 스타일:{a,b} ({counts})"`.
  - `Task<Dictionary<string,List<HeaderAsset>>> GetCandidatesAsync(DraftUnderstanding u, DesignConcept concept = null, int topK = 5)` — concept을 임베딩 query에 주입. **concept=null이면 Route A와 완전 동일**(회귀 없음).
- **회귀 보장:** 유일 호출자 `RecommendationService.cs:37` `GetCandidatesAsync(u)`는 concept 기본값 null → 동작 불변.

- [ ] **Step 1: 회귀 + 신규 동작 테스트 작성**

```csharp
// src/TeampptAddin.Tests/CombinationCandidateProviderQueryTest.cs
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationCandidateProviderQueryTest
    {
        private static DraftUnderstanding U() => new DraftUnderstanding
        {
            Purpose = "P",
            MatchIntent = "M",
            Counts = new Dictionary<string, int> { ["text"] = 2 }
        };

        [Fact]
        public void BuildQuery_Without_Concept_Is_Unchanged()   // Route A 회귀 가드
        {
            Assert.Equal("P | M (text:2)", CombinationCandidateProvider.BuildQuery(U(), null));
        }

        [Fact]
        public void BuildQuery_With_StyleTags_Appends_Style()
        {
            var concept = new DesignConcept { StyleTags = new List<string> { "minimal", "trust" } };
            Assert.Equal("P | M | 스타일:minimal,trust (text:2)",
                CombinationCandidateProvider.BuildQuery(U(), concept));
        }

        [Fact]
        public void BuildQuery_Empty_StyleTags_Is_Unchanged()
        {
            var concept = new DesignConcept { StyleTags = new List<string>() };
            Assert.Equal("P | M (text:2)", CombinationCandidateProvider.BuildQuery(U(), concept));
        }
    }
}
```

- [ ] **Step 2: 빌드 후 테스트 → RED (컴파일 실패: BuildQuery 없음)**

Run: **[BUILD-LOGIC]**
Expected: 컴파일 에러 `'CombinationCandidateProvider' does not contain a definition for 'BuildQuery'`. (이게 RED.)

- [ ] **Step 3: `BuildQuery` 추출 + 시그니처에 concept 주입**

`src/TeampptAddin/Services/CombinationCandidateProvider.cs`:

(a) 현재 `GetCandidatesAsync` 시그니처(44행)를 교체:
```csharp
        public async Task<Dictionary<string, List<HeaderAsset>>> GetCandidatesAsync(
            DraftUnderstanding u, DesignConcept concept = null, int topK = 5)
        {
            var kinds = NeededKinds(u.NeededCombination);
            var query = BuildQuery(u, concept);
```
(기존 47–48행의 `var countsText = ...;`와 `var query = $"{u.Purpose} | {u.MatchIntent} ({countsText})";` 두 줄을 위의 `var query = BuildQuery(u, concept);` 한 줄로 대체.)

(b) 클래스에 static 메서드 추가(예: `NeededKinds` 위 또는 아래):
```csharp
        public static string BuildQuery(DraftUnderstanding u, DesignConcept concept)
        {
            var countsText = string.Join(", ", u.Counts.Select(kv => $"{kv.Key}:{kv.Value}"));
            var styleSuffix = (concept?.StyleTags != null && concept.StyleTags.Count > 0)
                ? $" | 스타일:{string.Join(",", concept.StyleTags)}"
                : "";
            return $"{u.Purpose} | {u.MatchIntent}{styleSuffix} ({countsText})";
        }
```

- [ ] **Step 4: 호출자 회귀 확인 (positional topK 없음 검증)**

Run (Grep): `GetCandidatesAsync(` 를 `src/` 에서 검색.
Expected: 유일 호출 `RecommendationService.cs` `await _candidates.GetCandidatesAsync(u);` — 두 번째 인자 없음 → concept=null로 바인딩(정상). **만약 `GetCandidatesAsync(u, 정수)`처럼 topK를 positional로 넘기는 호출이 나오면** `GetCandidatesAsync(u, null, 정수)`로 고친다(현재는 없음).

- [ ] **Step 5: 빌드 후 테스트 → GREEN**

Run: **[BUILD-LOGIC]** 그 다음 **[TEST]** with `CombinationCandidateProviderQueryTest`
Expected: 3 PASS. (기존 `CombinationCandidateProviderTest`도 깨지지 않는지 함께 실행: `--filter "FullyQualifiedName~CombinationCandidateProvider"`.)

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Services/CombinationCandidateProvider.cs src/TeampptAddin.Tests/CombinationCandidateProviderQueryTest.cs
git commit -m "feat(phase3): GetCandidatesAsync 컨셉 StyleTags 주입(BuildQuery), Route A 회귀 가드 (Task E)"
```

> **Opus 리뷰 포커스:** ① concept=null 경로가 기존 query 문자열과 바이트 단위로 동일한가(회귀). ② 시그니처에 concept을 topK 앞에 넣어 기존 positional 호출이 깨지지 않는가. ③ StyleTags 합성 위치가 스펙(`... | 스타일:... (counts)`)과 일치하는가.

---

### Task F: `DeckSlideImageExporter` (COM)

**Files:**
- Create: `src/TeampptAddin/Core/DeckSlideImageExporter.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj`

**Interfaces:**
- Consumes: `Globals.Application`(COM), `SlideImageRenderer.Render(PowerPoint.Presentation, int slideIndex, string outputPngPath, int longEdgePx=768)`, `Logger.Log`.
- Produces: `static Dictionary<int,string> DeckSlideImageExporter.Export(string pptxPath, IEnumerable<int> slideIndexes, string outDir = null)` — 인덱스→PNG경로. 실패한 인덱스는 dict에서 빠짐(폴백은 호출자 G가 처리). 비파괴(ReadOnly·창 없이 open → Close → Release).
- **COM/STA:** 호출자(G)는 첫 네트워크 await 이전에 이 동기 메서드를 호출한다.

- [ ] **Step 1: 파일 생성**

```csharp
// src/TeampptAddin/Core/DeckSlideImageExporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>외부 초안 .pptx를 창 없이 ReadOnly로 열어 지정 인덱스 슬라이드를 PNG로 내보낸다(비파괴). 대표 본문 장(2~4)만.</summary>
    public static class DeckSlideImageExporter
    {
        public static Dictionary<int, string> Export(string pptxPath, IEnumerable<int> slideIndexes, string outDir = null)
        {
            var result = new Dictionary<int, string>();
            var indexes = (slideIndexes ?? Enumerable.Empty<int>()).Distinct().ToList();
            var app = Globals.Application;
            if (app == null) { Logger.Log("[DeckExport] app is null"); return result; }
            if (!File.Exists(pptxPath)) { Logger.Log($"[DeckExport] 파일 없음 {pptxPath}"); return result; }
            if (indexes.Count == 0) return result;

            outDir = outDir ?? Path.Combine(Path.GetTempPath(), "TeampptAddin", "cache", "deckreco");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            PowerPoint.Presentation pres = null;
            try
            {
                pres = app.Presentations.Open(
                    pptxPath,
                    MsoTriState.msoTrue,    // ReadOnly
                    MsoTriState.msoFalse,   // Untitled
                    MsoTriState.msoFalse);  // WithWindow = False
                foreach (var idx in indexes)
                {
                    try
                    {
                        var png = Path.Combine(outDir, $"deck-slide-{idx}.png");
                        SlideImageRenderer.Render(pres, idx, png);
                        result[idx] = png;
                    }
                    catch (Exception ex) { Logger.Log($"[DeckExport] slide {idx} 실패: {ex.Message}"); }
                }
                pres.Close();
                Logger.Log($"[DeckExport] {result.Count}/{indexes.Count} PNG from {Path.GetFileName(pptxPath)}");
                return result;
            }
            finally { if (pres != null) Marshal.ReleaseComObject(pres); }
        }
    }
}
```

- [ ] **Step 2: csproj 등록**

`<Compile Include="Core\DeckFileReader.cs" />` 줄 아래에 추가:
```xml
    <Compile Include="Core\DeckSlideImageExporter.cs" />
```

- [ ] **Step 3: 컴파일 검증**

Run: **[BUILD-LOGIC]**
Expected: `0 Error(s)`. (COM 동작은 Task I PPT 수동검증에서 확인. 단위테스트 없음.)

- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Core/DeckSlideImageExporter.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase3): DeckSlideImageExporter 대표 장 PNG 내보내기(비파괴, COM) (Task F)"
```

---

### Task G: `DeckRecommendationOrchestrator` (통합 브레인 — Opus max)

**Files:**
- Create: `src/TeampptAddin/Services/DeckRecommendationOrchestrator.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj`

**Interfaces:**
- Consumes (모두 검증된 인터페이스):
  - `BodyPatternClusterer.Cluster(List<DraftProfile>) → List<BodyPattern>` (B)
  - `DeckBoxPlanner.Plan(DeckStructure, List<BodyPattern>) → List<BoxPlan>` (D)
  - `DeckSlideImageExporter.Export(string, IEnumerable<int>) → Dictionary<int,string>` (F)
  - `DraftUnderstandingService.UnderstandAsync(DraftProfile, string png) → Task<DraftUnderstanding>` (png=null → 텍스트-only 폴백)
  - `CombinationCandidateProvider.GetCandidatesAsync(DraftUnderstanding, DesignConcept, int) → Task<Dictionary<string,List<HeaderAsset>>>` (E)
  - `CombinationRecommender.RecommendAsync(DraftUnderstanding, Dictionary<...>) → Task<CombinationRecommendation>`, `CombinationRecommender.PickSlideOnly(DraftUnderstanding, List<HeaderAsset>) → CombinationRecommendation`
  - `MaterialFitScorer.Score(CombinationRecommendation, DraftUnderstanding) → MaterialFitResult`, `ConceptFitScorer.Score(CombinationRecommendation, DesignConcept) → ConceptFitResult` (C)
  - 생성자: `RecommendationService`와 동일 배선 `(string supabaseUrl, string anonKey, string geminiKey)`.
- Produces: `Task<DeckRecommendation> RecommendDeckAsync(string deckPath, List<DraftProfile> profiles, DeckStructure structure, DesignConcept concept, Action<string> progress)`.
- **실행 순서(스펙 §컴포넌트 경계):** ① 본문 패턴 understanding 먼저 모두 계산 → ② 첫 패턴 understanding으로 공통 header 도출 → ③ 디스플레이 순서(표지→header→본문→엔드)로 박스 조립.
- **COM/STA·ConfigureAwait:** `Export`(COM)는 첫 await 전에 실행. 메서드 내부 await에 `ConfigureAwait(false)`를 쓰지 않는다(기존 `RecommendationService`와 동일, UI 컨텍스트 유지).

- [ ] **Step 1: 파일 생성**

```csharp
// src/TeampptAddin/Services/DeckRecommendationOrchestrator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// Phase 3 통합 브레인: 덱 구조 + 선택 컨셉 → 박스별(표지·공통헤더·본문패턴·목차·간지·엔드) 추천 + 두 배지.
    /// 멀티모달·LLM 호출은 본문 패턴 수(2~4)에만 비례. 비파괴(원본 ReadOnly).
    /// </summary>
    public class DeckRecommendationOrchestrator
    {
        private readonly DraftUnderstandingService _understand;
        private readonly CombinationCandidateProvider _candidates;
        private readonly CombinationRecommender _recommender;

        public DeckRecommendationOrchestrator(string supabaseUrl, string anonKey, string geminiKey)
        {
            var gemini = new GeminiAiService(geminiKey);
            _understand = new DraftUnderstandingService(gemini);
            var supa = new SupabaseClient(supabaseUrl, anonKey);
            _candidates = new CombinationCandidateProvider(
                new EmbeddingService(geminiKey), supa, new RemoteAssetCache(supabaseUrl, anonKey));
            _recommender = new CombinationRecommender(gemini);
        }

        public async Task<DeckRecommendation> RecommendDeckAsync(
            string deckPath,
            List<DraftProfile> profiles,
            DeckStructure structure,
            DesignConcept concept,
            Action<string> progress)
        {
            progress?.Invoke("본문 패턴을 묶고 있어요…");
            var bodyIdx = new HashSet<int>(structure.Slides
                .Where(s => string.Equals(s.Kind, "body", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Index));
            var bodyProfiles = profiles.Where(p => bodyIdx.Contains(p.SlideIndex)).ToList();
            var patterns = BodyPatternClusterer.Cluster(bodyProfiles);
            var plans = DeckBoxPlanner.Plan(structure, patterns);

            // 1) 대표 본문 장 PNG 내보내기(COM — 첫 await 전, UI STA 동기)
            progress?.Invoke("대표 장을 이미지로 추출하는 중…");
            var pngMap = DeckSlideImageExporter.Export(deckPath, patterns.Select(p => p.RepresentativeIndex));

            // 2) 본문 패턴 understanding을 먼저 모두 계산(멀티모달 1회/패턴)
            var byProfile = profiles.GroupBy(p => p.SlideIndex).ToDictionary(g => g.Key, g => g.First());
            var patternU = new Dictionary<string, DraftUnderstanding>();
            foreach (var pat in patterns)
            {
                progress?.Invoke($"본문 패턴 이해 중… (대표 {pat.RepresentativeIndex}장)");
                byProfile.TryGetValue(pat.RepresentativeIndex, out var repProfile);
                pngMap.TryGetValue(pat.RepresentativeIndex, out var png);   // 없으면 null → 텍스트-only
                var u = await _understand.UnderstandAsync(repProfile, png);
                patternU[pat.Signature] = u;
            }

            // 3) 공통 header = 첫 본문 패턴 understanding 재사용 → header 후보 top1(LLM0)
            RecommendedSlot commonHeader = null;
            DraftUnderstanding firstU = null;
            if (patterns.Count > 0 && patternU.TryGetValue(patterns[0].Signature, out firstU))
            {
                progress?.Invoke("공통 헤더를 고르는 중…");
                var headerPool = await _candidates.GetCandidatesAsync(firstU, concept);
                var top = (headerPool.TryGetValue("header", out var hs) ? hs : new List<HeaderAsset>()).FirstOrDefault();
                if (top != null)
                    commonHeader = new RecommendedSlot { Asset = top, FitNote = "공통 헤더", Confidence = Sim(top) };
            }

            // 4) 디스플레이 순서(plans)대로 박스 조립
            var boxes = new List<BoxRecommendation>();
            foreach (var plan in plans)
            {
                CombinationRecommendation rec;
                DraftUnderstanding uForFit;

                if (plan.BoxKind == "body")
                {
                    var pat = patterns.First(p => p.Signature == plan.Signature);
                    var u = patternU[pat.Signature];
                    if (u.NeededCombination == null) u.NeededCombination = new NeededCombination();
                    u.NeededCombination.Header = 0;   // 공통 header는 별도 1회 → 본문 후보에서 제외
                    progress?.Invoke($"{plan.Label} 조합을 고르는 중…");
                    var pool = await _candidates.GetCandidatesAsync(u, concept);
                    rec = await _recommender.RecommendAsync(u, pool);
                    uForFit = u;
                }
                else if (plan.BoxKind == "header")
                {
                    rec = new CombinationRecommendation();
                    if (commonHeader != null) rec.Header = commonHeader;
                    else rec.Unmet.Add("header");
                    uForFit = firstU ?? new DraftUnderstanding();
                }
                else
                {
                    // slide-box: cover/toc/section/end → synthetic understanding → slide 후보 → PickSlideOnly(LLM0)
                    var synth = new DraftUnderstanding
                    {
                        Purpose = plan.Label,
                        MatchIntent = plan.Label,
                        SlideKind = plan.BoxKind,
                        NeededCombination = new NeededCombination { Slide = 1 }
                    };
                    var pool = await _candidates.GetCandidatesAsync(synth, concept);
                    var slideList = pool.TryGetValue("slide", out var sl) ? sl : new List<HeaderAsset>();
                    rec = CombinationRecommender.PickSlideOnly(synth, slideList);
                    uForFit = synth;
                }

                boxes.Add(new BoxRecommendation
                {
                    Plan = plan,
                    Recommendation = rec,
                    MaterialFit = MaterialFitScorer.Score(rec, uForFit),
                    ConceptFit = ConceptFitScorer.Score(rec, concept)
                });
            }

            progress?.Invoke("추천을 정리했어요.");
            return new DeckRecommendation { Boxes = boxes, Concept = concept };
        }

        private static double Sim(HeaderAsset a)
        {
            if (a?.Extra != null && a.Extra.TryGetValue("similarity", out var s) && s != null)
                return (double)s;
            return 1.0;
        }
    }
}
```

- [ ] **Step 2: csproj 등록**

`<Compile Include="Services\RecommendationService.cs" />` 줄 아래에 추가:
```xml
    <Compile Include="Services\DeckRecommendationOrchestrator.cs" />
```

- [ ] **Step 3: 컴파일 검증**

Run: **[BUILD-LOGIC]**
Expected: `0 Error(s)`. (LLM/COM 동작은 Task I PPT 수동검증.)

- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Services/DeckRecommendationOrchestrator.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase3): DeckRecommendationOrchestrator 박스별 추천 통합 브레인 (Task G)"
```

> **Opus 리뷰 포커스:** ① 호출 횟수 = 본문 패턴 수(멀티모달·RecommendAsync 각 패턴 1회, header/slide-box는 LLM0)인지 — 장수 선형폭증 없음. ② `Export`가 첫 await 전에 끝나 COM/STA 안전한지, 이후 COM 미접근. ③ 본문 박스에서 `NeededCombination.Header=0` 후 후보가 layout+component만인지. ④ 에러 처리: 후보 0개 → Unmet, PNG 없음 → png=null 텍스트-only 폴백, 원본 불변.

---

### Task H: `AssetPanel` UI 연결 — 자동실행·박스카드·두 배지

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs`
- Modify: `src/TeampptAddin/UI/TaskPaneHost.cs`

**Interfaces:**
- Consumes: `DeckRecommendationOrchestrator.RecommendDeckAsync(...)` (G), `DeckRecommendation`/`BoxRecommendation`/`BoxPlan` (A), `ConceptResolver.ResolveColors/ResolveFonts(HeaderAsset, DesignConcept)`, 기존 패널 멤버 `AddAiBubble`·`_chatStack`·`_chatScroll`·`ThemeResources`·`ConceptSwatch`·`WrapPanel`.
- Produces: 컨셉 선택 → `RunDeckRecommendAsync()` 자동 실행 → 박스별 `BuildBoxRecommendationCard`(에셋 이름 + 컨셉 색/폰트 미리보기 + `[재료적합 NN]`·`[컨셉적합 NN]` 두 배지). `InitAi`에 `DeckRecommendationOrchestrator deckRecommend = null` 파라미터 추가.

- [ ] **Step 1: 필드 추가**

`AssetPanel.cs`에서 `private bool _deckRunning;`(66행 근처) 아래에 추가:
```csharp
        private DeckRecommendationOrchestrator _deckRecommend;
        private List<DraftProfile> _deckProfiles;
        private string _deckPath;
        private bool _deckRecoRunning;
        private DeckRecommendation _lastDeckRecommendation;
```

- [ ] **Step 2: `RunDeckRedesignAsync`에서 profiles/path 보관**

`RunDeckRedesignAsync`의 `_lastStructure = structure;`(760행) 바로 아래에 추가:
```csharp
                _deckProfiles = profiles;
                _deckPath = dlg.FileName;
```

- [ ] **Step 3: `OnConceptSelected`에서 자동 실행 트리거**

`OnConceptSelected`의 `_chatScroll.ScrollToBottom();`(1038행) 바로 아래, `Logger.Log(...)` 앞에 추가:
```csharp
            _ = RunDeckRecommendAsync();
```

- [ ] **Step 4: `RunDeckRecommendAsync` + 박스 카드 렌더 추가**

`BuildConceptConfirmBanner` 메서드 끝(1078행, 닫는 `}`) 다음에 메서드들 추가:
```csharp
        private async Task RunDeckRecommendAsync()
        {
            if (_deckRecoRunning) return;
            if (_deckRecommend == null) { AddAiBubble("박스별 추천은 Supabase·Gemini 설정이 있어야 동작해요."); return; }
            if (_deckProfiles == null || _lastStructure == null || _selectedConcept == null)
            { AddAiBubble("먼저 초안을 열고 컨셉을 선택해주세요."); return; }

            _deckRecoRunning = true;
            try
            {
                AddAiBubble("선택한 컨셉으로 박스별 에셋을 추천할게요.");
                var deckReco = await _deckRecommend.RecommendDeckAsync(
                    _deckPath, _deckProfiles, _lastStructure, _selectedConcept,
                    msg => Dispatcher.Invoke(() => AddAiBubble(msg)));
                _lastDeckRecommendation = deckReco;
                ShowDeckRecommendation(deckReco);
            }
            catch (Exception ex)
            {
                AddAiBubble($"박스별 추천 중 오류: {ex.Message}");
                Logger.Log($"[DeckReco] 실패: {ex}");
            }
            finally { _deckRecoRunning = false; _chatScroll.ScrollToBottom(); }
        }

        private static readonly Dictionary<string, string> BoxKindLabel = new Dictionary<string, string>
        {
            ["cover"] = "표지", ["header"] = "공통 헤더", ["body"] = "본문",
            ["toc"] = "목차", ["section"] = "간지", ["end"] = "마무리"
        };

        private void ShowDeckRecommendation(DeckRecommendation deck)
        {
            if (deck == null || deck.Boxes.Count == 0) { AddAiBubble("추천 결과가 없어요."); return; }
            _chatStack.Children.Add(new TextBlock
            {
                Text = "박스별 추천", FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub, Margin = new Thickness(12, 8, 0, 2)
            });
            foreach (var box in deck.Boxes)
                _chatStack.Children.Add(BuildBoxRecommendationCard(box, deck.Concept));
            _chatScroll.ScrollToBottom();
        }

        private Border BuildBoxRecommendationCard(BoxRecommendation box, DesignConcept concept)
        {
            var kindLabel = BoxKindLabel.TryGetValue(box.Plan.BoxKind, out var kl) ? kl : box.Plan.BoxKind;
            var col = new StackPanel();
            col.Children.Add(new TextBlock
            {
                Text = box.Plan.BoxKind == "body" ? $"{kindLabel} · {box.Plan.Label}" : kindLabel,
                FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase
            });

            var primary = PrimaryAsset(box.Recommendation);
            if (primary == null)
            {
                col.Children.Add(new TextBlock
                {
                    Text = "미충족 — 적합한 에셋 없음", FontSize = 11,
                    Foreground = ThemeResources.TextSub, Margin = new Thickness(0, 4, 0, 0)
                });
            }
            else
            {
                col.Children.Add(new TextBlock
                {
                    Text = primary.Name ?? primary.File ?? "에셋", FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeResources.TextMain, FontFamily = ThemeResources.FontBase,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0)
                });

                var sw = new WrapPanel { Margin = new Thickness(0, 5, 0, 0) };
                foreach (var c in ConceptResolver.ResolveColors(primary, concept))
                    sw.Children.Add(new Border
                    {
                        Width = 18, Height = 18, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 5, 0),
                        BorderBrush = ThemeResources.BorderCard, BorderThickness = new Thickness(1),
                        Background = ConceptSwatch(c.Value)
                    });
                if (sw.Children.Count > 0) col.Children.Add(sw);

                var fonts = ConceptResolver.ResolveFonts(primary, concept);
                if (fonts.Count > 0)
                    col.Children.Add(new TextBlock
                    {
                        Text = "글꼴 · " + string.Join(" / ", fonts.Select(f => f.Family).Distinct()),
                        FontSize = 10, Foreground = ThemeResources.TextSub, Margin = new Thickness(0, 4, 0, 0)
                    });
            }

            var badges = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            badges.Children.Add(Badge($"재료적합 {box.MaterialFit?.Score ?? 0}"));
            badges.Children.Add(Badge($"컨셉적합 {box.ConceptFit?.Score ?? 0}"));
            col.Children.Add(badges);

            return new Border
            {
                Background = ThemeResources.BgChip, CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 4, 12, 4), Padding = new Thickness(12, 10, 12, 10), Child = col
            };
        }

        private static HeaderAsset PrimaryAsset(CombinationRecommendation rec)
            => rec?.Slide?.Asset ?? rec?.Header?.Asset ?? rec?.Layout?.Asset
               ?? rec?.Components?.FirstOrDefault(s => s?.Asset != null)?.Asset;

        private Border Badge(string text) => new Border
        {
            Background = ThemeResources.BgCategoryActive, CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 3, 8, 3),
            Child = new TextBlock
            {
                Text = text, FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextAccent, FontFamily = ThemeResources.FontBase
            }
        };
```

- [ ] **Step 5: `InitAi`에 오케스트레이터 주입**

`AssetPanel.cs` `InitAi` 시그니처(3225행)를 교체:
```csharp
        public void InitAi(IAiService aiService, StyleConfig styles, RemoteAssetCache remoteCache = null, RedesignService redesign = null, RecommendationService recommend = null, DeckStructureService deckStructure = null, ConceptSuggester conceptSuggester = null, DeckRecommendationOrchestrator deckRecommend = null)
```
본문 `_conceptSuggester = conceptSuggester;`(3233행) 아래에 추가:
```csharp
            _deckRecommend = deckRecommend;
```

- [ ] **Step 6: `TaskPaneHost`에서 오케스트레이터 생성·전달**

`TaskPaneHost.cs`:
(a) `ConceptSuggester conceptSuggester = null;`(197행) 아래에 선언 추가:
```csharp
            DeckRecommendationOrchestrator deckRecommend = null;
```
(b) Supabase+Gemini 분기의 `conceptSuggester = new ConceptSuggester(new GeminiAiService(gemini));`(204행) 아래에 추가:
```csharp
                deckRecommend = new DeckRecommendationOrchestrator(supaUrl, supaAnon, gemini);
```
(c) `InitAi` 호출(222행)을 교체:
```csharp
            _wpfPanel.InitAi(ai, styles, _remoteCache, redesign, recommend, deckStructure, conceptSuggester, deckRecommend);
```

- [ ] **Step 7: 컴파일 검증**

Run: **[BUILD-LOGIC]**
Expected: `0 Error(s)`. (UI 동작은 Task I PPT 수동검증.)

- [ ] **Step 8: 커밋**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs src/TeampptAddin/UI/TaskPaneHost.cs
git commit -m "feat(phase3): 컨셉 선택→박스별 추천 자동실행·박스카드·두 배지 UI (Task H)"
```

> **Opus 최종 리뷰 포커스:** ① `OnConceptSelected`의 fire-and-forget(`_ = RunDeckRecommendAsync();`)가 UI 스레드에서 시작되어 COM(Export)이 첫 await 전에 도는지. ② `RecommendDeckAsync` await가 ConfigureAwait 없이 UI 컨텍스트로 돌아와 `ShowDeckRecommendation`이 UI 스레드인지. ③ 두 배지 값이 `box.MaterialFit.Score`/`box.ConceptFit.Score`와 일치, 미충족 박스 처리. ④ 확정 배너 "진행할게요"가 실제 추천으로 이어지는지(Phase 2 카피 폴리시 해소).

---

### Task I: 최종 통합 리뷰 + PPT 수동검증 게이트 (Opus max)

**Files:** (코드 없음 — 게이트)

- [ ] **Step 1: 통합 diff Opus 리뷰**

`git diff main...feat/asset-combination-recommendation` 전체를 검토. 중점: E 회귀(Route A 단일 슬라이드), G 호출 횟수·COM 안전, H 스레딩.

- [ ] **Step 2: 관리자 COM 등록 빌드**

Run: **[BUILD-DEPLOY]**
직후 검증: ① DLL 타임스탬프 1분 이내 ② `build.log` 오류 0건. 둘 중 하나라도 실패면 빌드 안 된 것 — 고치고 재빌드.

- [ ] **Step 3: PPT 수동검증 (데모 hero ② 흐름)**

PowerPoint 재시작 → 패널에서:
1. "📂 리디자인 (초안 파일)" → 패턴 2~3개 있는 초안 .pptx 선택 → **구조 박스** 표시 확인.
2. 칩 질문(용도·느낌) 답 → **컨셉 3카드** 생성 확인.
3. 컨셉 1개 선택 → 확정 배너 → **자동으로 박스별 추천 카드** 표시 확인.
4. 각 박스 카드에 **에셋 이름 + 컨셉 색/폰트 미리보기 + `재료적합 NN`·`컨셉적합 NN` 두 배지** 확인.
5. 표지/공통헤더/본문패턴/엔드 박스가 스펙 순서로 나오는지, 본문이 패턴 단위로 묶였는지 확인.
- **회귀:** "AI 리디자인" 바(단일 슬라이드 추천)가 종전대로 동작하는지 확인(Route A 불변).
- **로그:** `Logger` 출력에서 멀티모달 호출 = 본문 패턴 수인지(장수 비례 아님) 확인.

- [ ] **Step 4: 게이트 판정 기록**

PPT 검증 결과를 `PROGRESS-BOARD.md` 현재 잎에 기록(통과/이슈). 실패 항목은 `superpowers:systematic-debugging`으로 분리 처리.

---

## 자체 점검 (Self-Review)

**스펙 커버리지:**
- 3개 정련 — ①둘째 배지=계산 컨셉적합(C `ConceptFitScorer`) ②본문=패턴 그룹(B `BodyPatternClusterer`, G가 패턴당 1회 호출) ③추천 카드까지(H, 조립은 Phase 4) → 모두 Task로 매핑됨.
- 컴포넌트 경계 표의 모든 유닛(BodyPattern/BoxPlan/ConceptFitResult/DeckRecommendation/BoxRecommendation=A, BodyPatternClusterer=B, ConceptFitScorer=C, DeckBoxPlanner=D, CombinationCandidateProvider 수정=E, DeckSlideImageExporter=F, DeckRecommendationOrchestrator=G, AssetPanel/csproj 수정=H) → 매핑됨.
- 박스 파이프라인(slide-box top1·공통 header top1·본문 RecommendAsync, Header=0 제외) → G에 구현.
- 두 배지·StyleTags 가중·데이터 흐름·에러 처리(후보0/PNG실패/본문0장) → G + H.
- 테스트 전략(순수 3개 TDD + 오케/UI/Exporter 수동) → B/C/D TDD, E 회귀 TDD, F/G/H 수동(I).

**타입 일관성:** `BodyPattern.RepresentativeIndex`(int)·`BoxPlan.Signature`(string, body 매칭)·`GetCandidatesAsync(u, concept, topK)` 인자 순서·`ConceptFitScorer.Score` 두 오버로드·`MaterialFitResult.Score`/`ConceptFitResult.Score`(int) — 모든 Task에서 동일 사용 확인.

**플레이스홀더:** 없음(모든 스텝에 실제 코드/명령/기대값).

---

## 실행 핸드오프

플랜 저장: `docs/superpowers/plans/2026-06-26-redesign-phase3-box-recommendation.md`.

- **권장: Subagent-Driven** (`superpowers:subagent-driven-development`) — Task마다 fresh subagent + 2단계 리뷰, Task 간 검토. 위 모델 배분표대로 모델 지정. 순서: A → (B·C·D·E·F) → G → H → I.
- 대안: Inline (`superpowers:executing-plans`) — 이 세션에서 배치 실행 + 체크포인트.

**통합(머지/PR) 결정:** Task I 게이트 통과 후 `superpowers:finishing-a-development-branch`로 `feat/asset-combination-recommendation` → main 통합 방식 확정.
