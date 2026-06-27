# Phase 4 — 빈 템플릿 덱 조립 (DeckAssembler) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Phase 3의 박스별 추천(`DeckRecommendation`)을 받아, 현재 PPT에 박스 순서대로 빈 슬라이드를 생성하고 추천된 에셋(header+layout+component)을 합성하여 **빈 템플릿 덱**을 조립한다. 원본 초안 파일 불변(비파괴).

**Architecture:** `DeckAssembler`(순수 조립 로직)가 `DeckRecommendation.Boxes`를 순회하며, 박스 종류별로 에셋을 다운로드 → 새 슬라이드 생성 → `InsertFromFile`로 도형 삽입. body 박스는 header+layout+component를 한 장에 합성. 같은 패턴의 여러 장은 대표 1장만 조립(동일 레이아웃 복제). UI는 "이 추천으로 덱 조립" 버튼 + 진행 표시.

**Tech Stack:** C# (.NET Framework 4.8), COM Interop (PowerPoint), WPF (UI), xUnit (테스트)

## Global Constraints

- **비파괴:** 원본 초안 파일(`_deckPath`)은 절대 수정하지 않는다. 모든 조립은 현재 열린 PPT(새 덱)에서.
- **좌표 변환:** `CoordinateConverter` 규칙 준수 — 폴백 로직 추가 금지.
- **old-style csproj:** 새 `.cs` 파일은 `TeampptAddin.csproj`의 `<Compile Include>`에 수동 등록 필수.
- **COM STA:** 모든 COM 호출(슬라이드 생성·도형 삽입·삭제)은 UI 스레드(STA)에서. `ConfigureAwait(false)` 금지.
- **API 키:** 문서·커밋에 평문 포함 금지.
- **테스트:** `RegisterForComInterop=false` + `BuildProjectReferences=false` + `dotnet test --no-build --filter` 패턴.

---

### Task 1: DeckAssembler 핵심 로직 — 단일 slide-box 조립 (cover/end)

**Files:**
- Create: `src/TeampptAddin/Core/DeckAssembler.cs`
- Create: `src/TeampptAddin.Tests/DeckAssemblerTest.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (Compile Include 추가)

**Interfaces:**
- Consumes: `DeckRecommendation` (from `DeckRecommendationModels.cs`), `BoxRecommendation`, `BoxPlan`, `CombinationRecommendation`, `RecommendedSlot`, `HeaderAsset`
- Produces: `DeckAssembler.BuildSlideOrder(DeckRecommendation) → List<SlideAssemblyItem>` — 박스별 조립 순서를 계산하는 순수 함수. `SlideAssemblyItem { BoxPlan Plan, string BoxKind, List<RecommendedSlot> Slots, int SlideCount }`.

**왜 순수 함수부터:** COM 없이 테스트 가능한 조립 순서 계산을 먼저 만든다. 이 함수가 박스 종류별로 어떤 에셋 슬롯을 몇 장에 배치할지 결정한다.

- [ ] **Step 1: 실패 테스트 작성 — BuildSlideOrder 순서 계산**

`src/TeampptAddin.Tests/DeckAssemblerTest.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckAssemblerTest
    {
        private static HeaderAsset FakeAsset(string name, string kind) => new HeaderAsset
        {
            Name = name, Kind = kind, File = $"{name}.pptx",
            Extra = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
            {
                ["remote_file"] = $"assets/{name}.pptx"
            }
        };

        private static RecommendedSlot Slot(string name, string kind)
            => new RecommendedSlot { Asset = FakeAsset(name, kind), FitNote = kind, Confidence = 0.9 };

        private static DeckRecommendation ThreeBoxDeck()
        {
            return new DeckRecommendation
            {
                Boxes = new List<BoxRecommendation>
                {
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "cover", Label = "표지", CoveredSlideIndexes = new List<int>{1} },
                        Recommendation = new CombinationRecommendation
                        {
                            SlideKind = "cover",
                            Slide = Slot("cover-blue", "slide")
                        }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "header", Label = "공통 헤더", CoveredSlideIndexes = new List<int>{2,3,4} },
                        Recommendation = new CombinationRecommendation
                        {
                            Header = Slot("header-corp", "header")
                        }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan
                        {
                            BoxKind = "body", Label = "본문 패턴 (3장)",
                            CoveredSlideIndexes = new List<int>{2,3,4},
                            RepresentativeIndex = 2, Signature = "t2i1b0c0|col2"
                        },
                        Recommendation = new CombinationRecommendation
                        {
                            SlideKind = "body",
                            Layout = Slot("layout-2col", "layout"),
                            Components = new List<RecommendedSlot> { Slot("icon-card", "component") }
                        }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "end", Label = "마무리", CoveredSlideIndexes = new List<int>{5} },
                        Recommendation = new CombinationRecommendation
                        {
                            SlideKind = "end",
                            Slide = Slot("end-thankyou", "slide")
                        }
                    }
                }
            };
        }

        [Fact]
        public void BuildSlideOrder_CoverBodyEnd_CorrectOrder()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);

            // cover=1장, header는 독립 슬라이드 아님(body에 합성), body패턴=3장, end=1장 → 총 5항목
            Assert.Equal(5, order.Count);
            Assert.Equal("cover", order[0].BoxKind);
            Assert.Equal("body", order[1].BoxKind); // 본문 장 1 (대표)
            Assert.Equal("body", order[2].BoxKind); // 본문 장 2 (복제)
            Assert.Equal("body", order[3].BoxKind); // 본문 장 3 (복제)
            Assert.Equal("end", order[4].BoxKind);
        }

        [Fact]
        public void BuildSlideOrder_CoverSlideBox_HasSlideSlotOnly()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);
            var coverItem = order[0];
            Assert.Single(coverItem.Slots);
            Assert.Equal("slide", coverItem.Slots[0].Asset.Kind);
        }

        [Fact]
        public void BuildSlideOrder_BodyRepresentative_MergesHeaderLayoutComponent()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);
            var bodyRep = order[1]; // 대표 장
            Assert.True(bodyRep.IsRepresentative);
            // header + layout + component = 3개 슬롯
            Assert.Equal(3, bodyRep.Slots.Count);
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "header");
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "layout");
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "component");
        }

        [Fact]
        public void BuildSlideOrder_BodyClone_IsNotRepresentative()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);
            Assert.False(order[2].IsRepresentative);
            Assert.False(order[3].IsRepresentative);
            // 복제 장은 슬롯 없음(대표 장을 COM Duplicate)
            Assert.Empty(order[2].Slots);
        }

        [Fact]
        public void BuildSlideOrder_NullSlots_Skipped()
        {
            var deck = new DeckRecommendation
            {
                Boxes = new List<BoxRecommendation>
                {
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "cover", Label = "표지", CoveredSlideIndexes = new List<int>{1} },
                        Recommendation = new CombinationRecommendation { Slide = null }
                    }
                }
            };
            var order = DeckAssembler.BuildSlideOrder(deck);
            Assert.Single(order);
            Assert.Empty(order[0].Slots); // Slide가 null이면 슬롯 없음
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```bash
tail -5 c:/Projects/teamppt-addin/build.log
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~DeckAssemblerTest" -v minimal
```
Expected: FAIL — `DeckAssembler` 타입 없음.

- [ ] **Step 3: SlideAssemblyItem 모델 + BuildSlideOrder 구현**

`src/TeampptAddin/Core/DeckAssembler.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public class SlideAssemblyItem
    {
        public string BoxKind { get; set; } = "";
        public BoxPlan Plan { get; set; }
        public List<RecommendedSlot> Slots { get; set; } = new List<RecommendedSlot>();
        public bool IsRepresentative { get; set; } = true;
        public int SourceSlideIndex { get; set; }
    }

    public static class DeckAssembler
    {
        public static List<SlideAssemblyItem> BuildSlideOrder(DeckRecommendation deck)
        {
            var items = new List<SlideAssemblyItem>();
            if (deck?.Boxes == null) return items;

            RecommendedSlot commonHeader = null;
            foreach (var box in deck.Boxes)
            {
                if (box.Plan.BoxKind == "header")
                {
                    commonHeader = box.Recommendation?.Header;
                    continue;
                }
                if (box.Plan.BoxKind == "body")
                {
                    var rec = box.Recommendation ?? new CombinationRecommendation();
                    var slots = new List<RecommendedSlot>();
                    if (commonHeader != null) slots.Add(commonHeader);
                    if (rec.Layout != null) slots.Add(rec.Layout);
                    foreach (var c in rec.Components ?? new List<RecommendedSlot>())
                        if (c != null) slots.Add(c);

                    var covered = box.Plan.CoveredSlideIndexes ?? new List<int>();
                    for (int i = 0; i < covered.Count; i++)
                    {
                        items.Add(new SlideAssemblyItem
                        {
                            BoxKind = "body",
                            Plan = box.Plan,
                            Slots = i == 0 ? slots : new List<RecommendedSlot>(),
                            IsRepresentative = i == 0,
                            SourceSlideIndex = covered[i]
                        });
                    }
                }
                else
                {
                    var rec = box.Recommendation ?? new CombinationRecommendation();
                    var slots = new List<RecommendedSlot>();
                    if (rec.Slide != null) slots.Add(rec.Slide);
                    items.Add(new SlideAssemblyItem
                    {
                        BoxKind = box.Plan.BoxKind,
                        Plan = box.Plan,
                        Slots = slots,
                        IsRepresentative = true,
                        SourceSlideIndex = box.Plan.CoveredSlideIndexes?.FirstOrDefault() ?? 0
                    });
                }
            }
            return items;
        }
    }
}
```

- [ ] **Step 4: csproj에 Compile Include 추가**

`src/TeampptAddin/TeampptAddin.csproj` — `<Compile Include>` 섹션에 추가:
```xml
    <Compile Include="Core\DeckAssembler.cs" />
```

- [ ] **Step 5: 빌드 + 테스트 통과 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```bash
tail -5 c:/Projects/teamppt-addin/build.log
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~DeckAssemblerTest" -v minimal
```
Expected: 5 tests PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Core/DeckAssembler.cs src/TeampptAddin.Tests/DeckAssemblerTest.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase4): DeckAssembler.BuildSlideOrder — 박스→슬라이드 조립 순서 (TDD 5/5)"
```

---

### Task 2: DeckAssembler.AssembleAsync — COM 기반 덱 조립 (에셋 다운로드 + 슬라이드 생성 + 도형 삽입)

**Files:**
- Modify: `src/TeampptAddin/Core/DeckAssembler.cs` (AssembleAsync 추가)

**Interfaces:**
- Consumes: `BuildSlideOrder()` (Task 1), `RemoteAssetCache.GetPptxAsync(string)`, COM `Presentation.Slides`, `InsertFromFile`
- Produces: `DeckAssembler.AssembleAsync(DeckRecommendation, RemoteAssetCache, Action<string>) → Task<DeckAssemblyResult>`. `DeckAssemblyResult { int SlideCount, List<string> Warnings }`.

**왜 별도 Task:** COM 코드는 단위 테스트 불가(PowerPoint 프로세스 필요). 순수 로직(Task 1)과 분리. 이 Task는 수동 PPT 검증 대상.

- [ ] **Step 1: DeckAssemblyResult 모델 + AssembleAsync 구현**

`src/TeampptAddin/Core/DeckAssembler.cs`에 추가:
```csharp
using System;
using System.Threading.Tasks;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

// 기존 using + namespace는 유지, 아래를 DeckAssembler 클래스 안에 추가:

public class DeckAssemblyResult
{
    public int SlideCount { get; set; }
    public List<string> Warnings { get; set; } = new List<string>();
}

public static async Task<DeckAssemblyResult> AssembleAsync(
    DeckRecommendation deck,
    RemoteAssetCache remoteCache,
    Action<string> progress)
{
    var result = new DeckAssemblyResult();
    var order = BuildSlideOrder(deck);
    if (order.Count == 0) return result;

    var app = Globals.Application;
    var pres = app.ActivePresentation;

    app.StartNewUndoEntry();

    // 기존 슬라이드 모두 삭제 (빈 덱에서 시작)
    while (pres.Slides.Count > 0)
        pres.Slides[1].Delete();

    // 빈 슬라이드 하나 만들어서 레이아웃 참조 확보
    var masterLayout = pres.SlideMaster.CustomLayouts[1];

    int repSlideIndex = -1; // 마지막 대표 body 슬라이드 인덱스 (복제용)

    foreach (var item in order)
    {
        progress?.Invoke($"{item.Plan?.Label ?? item.BoxKind} 조립 중…");

        if (!item.IsRepresentative && repSlideIndex > 0)
        {
            // 복제 장: 대표 body를 Duplicate
            var repSlide = pres.Slides[repSlideIndex];
            repSlide.Duplicate();
            result.SlideCount++;
            continue;
        }

        // 새 빈 슬라이드 추가
        int insertAt = pres.Slides.Count + 1;
        var newSlide = pres.Slides.AddSlide(insertAt, masterLayout);

        // 기본 placeholder 도형 제거
        while (newSlide.Shapes.Count > 0)
            newSlide.Shapes[1].Delete();

        // 에셋 슬롯 순서대로 삽입
        foreach (var slot in item.Slots)
        {
            string localPath = null;
            try
            {
                var remotePath = slot.Asset.Extra != null &&
                    slot.Asset.Extra.ContainsKey("remote_file")
                    ? slot.Asset.Extra["remote_file"].ToString()
                    : slot.Asset.File;
                localPath = await remoteCache.GetPptxAsync(remotePath);
            }
            catch (Exception ex)
            {
                var warn = $"{slot.Asset.Name ?? slot.Asset.File} 다운로드 실패: {ex.Message}";
                result.Warnings.Add(warn);
                Logger.Log($"[DeckAssembler] {warn}");
                continue;
            }

            if (string.IsNullOrEmpty(localPath) || !System.IO.File.Exists(localPath))
            {
                result.Warnings.Add($"{slot.Asset.Name} 파일 없음");
                continue;
            }

            // InsertFromFile → 임시 슬라이드 → 도형 복사 → 대상 슬라이드에 붙여넣기 → 임시 삭제
            int tempIdx = pres.Slides.Count;
            pres.Slides.InsertFromFile(localPath, tempIdx, 1, 1);
            var tempSlide = pres.Slides[tempIdx + 1];
            int shapeCount = tempSlide.Shapes.Count;
            if (shapeCount > 0)
            {
                var indices = new int[shapeCount];
                for (int i = 0; i < shapeCount; i++) indices[i] = i + 1;
                tempSlide.Shapes.Range(indices).Copy();
                newSlide.Shapes.Paste();
            }
            tempSlide.Delete();
        }

        if (item.BoxKind == "body" && item.IsRepresentative)
            repSlideIndex = newSlide.SlideIndex;

        result.SlideCount++;
    }

    // 첫 슬라이드로 이동
    if (pres.Slides.Count > 0)
        app.ActiveWindow.View.GotoSlide(1);

    progress?.Invoke($"덱 조립 완료! {result.SlideCount}장 생성.");
    return result;
}
```

**주의:** `DeckAssemblyResult` 클래스는 `DeckAssembler` 밖(네임스페이스 레벨)에 선언. `AssembleAsync`는 `DeckAssembler` static class 안에 선언.

- [ ] **Step 2: 빌드 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```bash
tail -5 c:/Projects/teamppt-addin/build.log
stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: 기존 테스트 회귀 없음 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false -v minimal
```
Expected: ALL tests PASS (including DeckAssemblerTest from Task 1).

- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin/Core/DeckAssembler.cs
git commit -m "feat(phase4): DeckAssembler.AssembleAsync — COM 기반 N장 조립+에셋 삽입"
```

---

### Task 3: UI — "이 추천으로 덱 조립" 버튼 + 진행 표시 + AssetPanel 연결

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs` (~1129행 ShowDeckRecommendation 뒤에 버튼 추가, 핸들러 추가)

**Interfaces:**
- Consumes: `DeckAssembler.AssembleAsync()` (Task 2), `_lastDeckRecommendation`, `_remoteCache`
- Produces: "이 추천으로 덱 조립 ▶" 버튼 → 클릭 시 `AssembleAsync` 호출 → 진행 표시 → 완료/실패 버블

- [ ] **Step 1: ShowDeckRecommendation 끝에 조립 버튼 추가**

`src/TeampptAddin/UI/Wpf/AssetPanel.cs`의 `ShowDeckRecommendation` 메서드 끝(line ~1129 `_chatScroll.ScrollToBottom();` 직전)에 추가:

```csharp
_chatStack.Children.Add(BuildDeckAssembleButton());
```

- [ ] **Step 2: BuildDeckAssembleButton + RunDeckAssembleAsync 구현**

`AssetPanel.cs`에 새 메서드 추가 (ShowDeckRecommendation 근처):

```csharp
private Border BuildDeckAssembleButton()
{
    var border = new Border
    {
        Background = ThemeResources.BgChip, CornerRadius = new CornerRadius(10),
        Margin = new Thickness(12, 8, 12, 8), Padding = new Thickness(12, 10, 12, 10),
        Cursor = System.Windows.Input.Cursors.Hand,
        Child = new TextBlock
        {
            Text = "이 추천으로 덱 조립 ▶",
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
            FontFamily = ThemeResources.FontBase,
            HorizontalAlignment = HorizontalAlignment.Center
        }
    };
    border.MouseLeftButtonUp += async (s, e) => await RunDeckAssembleAsync();
    return border;
}

private async Task RunDeckAssembleAsync()
{
    var deck = _lastDeckRecommendation;
    if (deck == null || _remoteCache == null) return;
    if (_redesignRunning) return;
    _redesignRunning = true;

    try
    {
        AddAiBubble("빈 템플릿 덱을 조립할게요…");
        var result = await DeckAssembler.AssembleAsync(
            deck, _remoteCache,
            msg => Dispatcher.Invoke(() => AddAiBubble(msg)));

        if (result.Warnings.Count > 0)
            AddAiBubble($"경고 {result.Warnings.Count}건: {string.Join(", ", result.Warnings)}");

        AddAiBubble($"덱 조립 완료! {result.SlideCount}장이 생성됐어요.");
    }
    catch (System.Exception ex)
    {
        AddAiBubble($"덱 조립 중 오류: {ex.Message}");
        Logger.Log($"[DeckAssemble] 실패: {ex}");
    }
    finally
    {
        _redesignRunning = false;
        _chatScroll.ScrollToBottom();
    }
}
```

- [ ] **Step 3: 관리자 빌드 + DLL 타임스탬프 검증**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```bash
tail -5 c:/Projects/teamppt-addin/build.log
stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll
```
Expected: Build succeeded, 0 errors, DLL 타임스탬프 갱신.

- [ ] **Step 4: PPT에서 수동 검증**

1. PowerPoint 열기 (새 빈 프레젠테이션)
2. AI 패널 → "📂 리디자인 (초안 파일)" → 초안 .pptx 선택
3. 구조 분석 완료 → 컨셉 질문 답변 → 컨셉 선택
4. 박스별 추천 표시 후 **"이 추천으로 덱 조립 ▶"** 버튼 확인
5. 클릭 → 진행 메시지 표시 → 완료 메시지 확인
6. 생성된 슬라이드 수가 초안 구조와 일치하는지 확인
7. 각 슬라이드에 에셋 도형이 삽입되어 있는지 확인

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs
git commit -m "feat(phase4): '이 추천으로 덱 조립' 버튼 + AssembleAsync 연결"
```

---

### Task 4: body 합성 개선 — 헤더 상단 고정 + layout 본문 영역 배치

**Files:**
- Modify: `src/TeampptAddin/Core/DeckAssembler.cs` (합성 규칙 추가)
- Modify: `src/TeampptAddin.Tests/DeckAssemblerTest.cs` (순수 로직 테스트 추가)

**Interfaces:**
- Consumes: `SlideAssemblyItem.Slots`, `HeaderAsset.Kind`
- Produces: `DeckAssembler.SortSlotsByLayer(List<RecommendedSlot>) → List<RecommendedSlot>` — 삽입 순서 결정: header(뒤=상단) → layout(앞=본문) → component(앞=최하단). PPT는 나중에 삽입한 도형이 위에 오므로, header를 마지막에 삽입해야 상단 고정.

**왜:** 에셋 합성의 핵심 문제 — 여러 에셋을 한 장에 넣을 때 겹침 방지. 삽입 순서(z-order)로 헤더가 layout 위에 오도록 보장.

- [ ] **Step 1: 실패 테스트 — SortSlotsByLayer 순서**

`src/TeampptAddin.Tests/DeckAssemblerTest.cs`에 추가:
```csharp
[Fact]
public void SortSlotsByLayer_HeaderLastForTopZOrder()
{
    var slots = new List<RecommendedSlot>
    {
        new RecommendedSlot { Asset = new HeaderAsset { Kind = "header", Name = "h" } },
        new RecommendedSlot { Asset = new HeaderAsset { Kind = "layout", Name = "l" } },
        new RecommendedSlot { Asset = new HeaderAsset { Kind = "component", Name = "c" } }
    };
    var sorted = DeckAssembler.SortSlotsByLayer(slots);
    Assert.Equal("component", sorted[0].Asset.Kind); // 먼저 삽입 → 맨 아래
    Assert.Equal("layout", sorted[1].Asset.Kind);
    Assert.Equal("header", sorted[2].Asset.Kind);    // 마지막 삽입 → 맨 위
}

[Fact]
public void SortSlotsByLayer_SlideKindUnchanged()
{
    var slots = new List<RecommendedSlot>
    {
        new RecommendedSlot { Asset = new HeaderAsset { Kind = "slide", Name = "s" } }
    };
    var sorted = DeckAssembler.SortSlotsByLayer(slots);
    Assert.Single(sorted);
    Assert.Equal("slide", sorted[0].Asset.Kind);
}
```

- [ ] **Step 2: 실패 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~SortSlotsByLayer" -v minimal
```
Expected: FAIL — `SortSlotsByLayer` 미존재.

- [ ] **Step 3: SortSlotsByLayer 구현 + AssembleAsync에서 호출**

`src/TeampptAddin/Core/DeckAssembler.cs`의 `DeckAssembler` 클래스에 추가:

```csharp
private static readonly Dictionary<string, int> LayerOrder = new Dictionary<string, int>
{
    ["component"] = 0,  // 맨 먼저 삽입 → z-order 맨 아래
    ["layout"] = 1,
    ["slide"] = 2,
    ["header"] = 3      // 맨 마지막 삽입 → z-order 맨 위 (상단 고정)
};

public static List<RecommendedSlot> SortSlotsByLayer(List<RecommendedSlot> slots)
{
    return (slots ?? new List<RecommendedSlot>())
        .OrderBy(s => LayerOrder.TryGetValue(s.Asset?.Kind ?? "", out var v) ? v : 1)
        .ToList();
}
```

`AssembleAsync` 안의 에셋 삽입 루프를 수정 — `item.Slots` 대신 `SortSlotsByLayer(item.Slots)` 사용:
```csharp
// 기존: foreach (var slot in item.Slots)
// 변경:
foreach (var slot in SortSlotsByLayer(item.Slots))
```

- [ ] **Step 4: 테스트 통과 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~DeckAssemblerTest" -v minimal
```
Expected: ALL tests PASS.

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Core/DeckAssembler.cs src/TeampptAddin.Tests/DeckAssemblerTest.cs
git commit -m "feat(phase4): SortSlotsByLayer 합성 규칙 — header 상단 고정 z-order (TDD 2/2)"
```

---

### Task 5: toc/section 박스 지원 + PROGRESS-BOARD 갱신

**Files:**
- Modify: `src/TeampptAddin.Tests/DeckAssemblerTest.cs` (toc/section 테스트)
- Modify: `src/TeampptAddin/Core/DeckAssembler.cs` (toc/section이 slide-box로 처리되는지 확인 — 이미 else 분기에서 처리됨. 테스트로 보강)
- Modify: `PROGRESS-BOARD.md` (Phase 4 진행 상황 갱신)

**Interfaces:**
- Consumes: `BuildSlideOrder()` — toc/section이 cover/end와 동일하게 slide-box로 처리됨을 검증
- Produces: toc/section 포함 덱의 BuildSlideOrder 정합성 테스트

- [ ] **Step 1: toc+section 포함 덱 테스트**

`src/TeampptAddin.Tests/DeckAssemblerTest.cs`에 추가:
```csharp
[Fact]
public void BuildSlideOrder_TocAndSection_TreatedAsSlideBox()
{
    var deck = new DeckRecommendation
    {
        Boxes = new List<BoxRecommendation>
        {
            new BoxRecommendation
            {
                Plan = new BoxPlan { BoxKind = "cover", Label = "표지", CoveredSlideIndexes = new List<int>{1} },
                Recommendation = new CombinationRecommendation { Slide = Slot("cover", "slide") }
            },
            new BoxRecommendation
            {
                Plan = new BoxPlan { BoxKind = "toc", Label = "목차", CoveredSlideIndexes = new List<int>{2} },
                Recommendation = new CombinationRecommendation { Slide = Slot("toc", "slide") }
            },
            new BoxRecommendation
            {
                Plan = new BoxPlan { BoxKind = "header", Label = "공통 헤더", CoveredSlideIndexes = new List<int>{3,4} },
                Recommendation = new CombinationRecommendation { Header = Slot("header", "header") }
            },
            new BoxRecommendation
            {
                Plan = new BoxPlan
                {
                    BoxKind = "body", Label = "본문 (2장)",
                    CoveredSlideIndexes = new List<int>{3,4},
                    Signature = "t1i0b0c0|col1"
                },
                Recommendation = new CombinationRecommendation
                {
                    Layout = Slot("layout", "layout")
                }
            },
            new BoxRecommendation
            {
                Plan = new BoxPlan { BoxKind = "section", Label = "간지", CoveredSlideIndexes = new List<int>{5} },
                Recommendation = new CombinationRecommendation { Slide = Slot("section", "slide") }
            },
            new BoxRecommendation
            {
                Plan = new BoxPlan { BoxKind = "end", Label = "마무리", CoveredSlideIndexes = new List<int>{6} },
                Recommendation = new CombinationRecommendation { Slide = Slot("end", "slide") }
            }
        }
    };
    var order = DeckAssembler.BuildSlideOrder(deck);

    // cover + toc + body대표 + body복제 + section + end = 6
    Assert.Equal(6, order.Count);
    Assert.Equal("cover", order[0].BoxKind);
    Assert.Equal("toc", order[1].BoxKind);
    Assert.Equal("body", order[2].BoxKind);
    Assert.True(order[2].IsRepresentative);
    Assert.Equal("body", order[3].BoxKind);
    Assert.False(order[3].IsRepresentative);
    Assert.Equal("section", order[4].BoxKind);
    Assert.Equal("end", order[5].BoxKind);

    // toc/section = slide-box → Slide 슬롯 1개씩
    Assert.Single(order[1].Slots);
    Assert.Single(order[4].Slots);
}
```

- [ ] **Step 2: 테스트 실행**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~DeckAssemblerTest" -v minimal
```
Expected: ALL tests PASS (이미 else 분기에서 toc/section 처리됨).

- [ ] **Step 3: PROGRESS-BOARD.md 갱신**

보드를 Phase 4 진행 상태로 갱신:
- Phase 3 ✅ 완료 반영
- Phase 4 🔵 진행 중 표시
- 잎(현재 작업)을 Phase 4로 교체
- 다음 세션 프롬프트 갱신

- [ ] **Step 4: 커밋**

```bash
git add src/TeampptAddin.Tests/DeckAssemblerTest.cs PROGRESS-BOARD.md
git commit -m "test(phase4): toc/section slide-box 정합성 검증 + BOARD Phase 4 갱신"
```

---

## Task DAG

```
Task 1 (순수 로직 BuildSlideOrder, TDD) ──┐
                                           ├─▶ Task 2 (COM AssembleAsync) ──▶ Task 3 (UI 버튼 연결)
                                           │
Task 4 (합성 규칙 SortSlotsByLayer, TDD) ──┘
                                                                              │
Task 5 (toc/section 테스트 + BOARD) ────────────────────────────────────────────┘
```

- **Task 1, 4:** 독립(병렬 가능). 순수 로직 + TDD.
- **Task 2:** Task 1, 4에 의존. COM 코드.
- **Task 3:** Task 2에 의존. UI 연결 + 수동 검증.
- **Task 5:** Task 3 이후(또는 병렬). 테스트 보강 + 보드 갱신.
