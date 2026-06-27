# Phase 4.5 — 깨끗한 빈 템플릿 조립 + 출처 기록 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 덱 조립 시 본문 슬라이드에 **header+layout만** 넣어 컴포넌트 겹침을 제거하고(스샷 깨짐 수정), 각 조립 슬라이드에 **어떤 에셋이 적용됐는지 출처를 슬라이드 태그로 기록**한다(후속 컴포넌트 교체·슬라이드별 대화형 UX의 토대).

**Architecture:** `DeckAssembler.BuildSlideOrder`의 body 분기에서 컴포넌트 슬롯을 빼면(레이아웃에 컴포넌트가 이미 내장돼 있으므로) 겹침이 사라진다. 별도 순수 클래스 `SlideProvenance`가 적용 에셋 목록을 JSON 태그 문자열로 직렬화/역직렬화하고, `AssembleAsync`가 슬라이드 빌드 직후 `Slide.Tags`에 기록한다. 복제 본문 슬라이드는 `Duplicate()`가 태그를 복사하므로 자동 상속된다.

**Tech Stack:** C# (.NET Framework 4.8), COM Interop (PowerPoint), Newtonsoft.Json, xUnit

## Global Constraints

- **비파괴:** 원본 초안 파일은 절대 수정하지 않는다. 조립은 현재 열린 PPT(새 덱)에서만.
- **COM STA:** COM 호출은 UI 스레드. `ConfigureAwait(false)` 금지(COM 경로).
- **old-style csproj:** 새 `.cs`는 `TeampptAddin.csproj`의 `<Compile Include>`에 수동 등록 필수.
- **API 키:** 문서·커밋에 평문 금지.
- **테스트:** `RegisterForComInterop=false` 솔루션 빌드 → `dotnet test --no-build -p:BuildProjectReferences=false --filter`.
- **DeckRecommendation 불변:** Phase 3 추천 결과(Components 포함)는 건드리지 않는다. 조립이 컴포넌트를 **붙이지 않을 뿐**, 추천 데이터는 후속 단계가 쓰도록 그대로 둔다.

## File Structure

| 파일 | 책임 | 신규/수정 |
|---|---|---|
| `src/TeampptAddin/Core/DeckAssembler.cs` | body 슬롯에서 컴포넌트 제외 + 조립 시 출처 태그 기록 | 수정 |
| `src/TeampptAddin/Core/SlideProvenance.cs` | 적용 에셋 ↔ JSON 태그 직렬화/역직렬화(순수) | 신규 |
| `src/TeampptAddin.Tests/DeckAssemblerTest.cs` | body=header+layout 검증으로 갱신 | 수정 |
| `src/TeampptAddin.Tests/SlideProvenanceTest.cs` | Format/Parse 라운드트립·폴백 | 신규 |
| `src/TeampptAddin/TeampptAddin.csproj` | `SlideProvenance.cs` 등록 | 수정 |

---

### Task 1: 본문 조립 = header+layout만 (컴포넌트 겹침 제거)

**Files:**
- Modify: `src/TeampptAddin/Core/DeckAssembler.cs:54-74` (`BuildSlideOrder` body 분기)
- Modify: `src/TeampptAddin.Tests/DeckAssemblerTest.cs` (기존 테스트 1개 교체)

**Interfaces:**
- Consumes: `DeckRecommendation`, `BoxRecommendation`, `CombinationRecommendation`(`Header`/`Layout`/`Components`), `RecommendedSlot`, `HeaderAsset.Kind`
- Produces: `BuildSlideOrder`의 body 대표 슬라이드 `Slots` = `[commonHeader?, layout?]` (컴포넌트 제외). 시그니처·반환타입 불변.

- [ ] **Step 1: 기존 테스트를 "header+layout만" 으로 교체(실패 유도)**

`src/TeampptAddin.Tests/DeckAssemblerTest.cs`에서 `BuildSlideOrder_BodyRepresentative_MergesHeaderLayoutComponent` 메서드를 통째로 아래로 교체:

```csharp
[Fact]
public void BuildSlideOrder_BodyRepresentative_HeaderAndLayoutOnly()
{
    var deck = ThreeBoxDeck();
    var order = DeckAssembler.BuildSlideOrder(deck);
    var bodyRep = order[1]; // 대표 장
    Assert.True(bodyRep.IsRepresentative);
    // Phase 4.5: 컴포넌트 제외 → header + layout = 2개
    Assert.Equal(2, bodyRep.Slots.Count);
    Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "header");
    Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "layout");
    Assert.DoesNotContain(bodyRep.Slots, s => s.Asset.Kind == "component");
}
```

(`ThreeBoxDeck()`의 body 추천엔 `Components = { Slot("icon-card","component") }`가 있으므로 이건 진짜 제외 검증이다.)

- [ ] **Step 2: 테스트가 실패하는지 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~BuildSlideOrder_BodyRepresentative_HeaderAndLayoutOnly" -v minimal
```
Expected: FAIL — 현재 코드는 컴포넌트를 넣어 `Slots.Count == 3`.

- [ ] **Step 3: body 분기에서 컴포넌트 루프 제거**

`src/TeampptAddin/Core/DeckAssembler.cs`의 body 분기를 아래로 교체(컴포넌트 `foreach` 삭제):

```csharp
                if (box.Plan.BoxKind == "body")
                {
                    var rec = box.Recommendation ?? new CombinationRecommendation();
                    var slots = new List<RecommendedSlot>();
                    if (commonHeader != null) slots.Add(commonHeader);
                    if (rec.Layout != null) slots.Add(rec.Layout);
                    // Phase 4.5: 컴포넌트는 레이아웃에 이미 내장 → 별도 오버레이 삽입 안 함(겹침 제거).
                    //            컴포넌트 교체는 후속 단계(comp_ 모델, 로드맵 참조)에서 클릭-교체로 처리.

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
```

- [ ] **Step 4: 전체 DeckAssemblerTest 통과 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```bash
tail -5 c:/Projects/teamppt-addin/build.log
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~DeckAssemblerTest" -v minimal
```
Expected: 전부 PASS (`SortSlotsByLayer` 함수 자체는 안 건드렸으므로 그 테스트도 그대로 통과).

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Core/DeckAssembler.cs src/TeampptAddin.Tests/DeckAssemblerTest.cs
git commit -m "fix(phase4.5): 본문 조립 header+layout만 — 컴포넌트 오버레이 제거로 겹침 해소"
```

---

### Task 2: SlideProvenance — 적용 에셋 ↔ JSON 태그 (순수 로직)

**Files:**
- Create: `src/TeampptAddin/Core/SlideProvenance.cs`
- Create: `src/TeampptAddin.Tests/SlideProvenanceTest.cs`
- Modify: `src/TeampptAddin/TeampptAddin.csproj` (Compile Include 추가)

**Interfaces:**
- Consumes: `RecommendedSlot`, `HeaderAsset`(`Kind`/`Name`/`File`/`Extra["remote_file"]`)
- Produces:
  - `class ProvenanceEntry { string Kind; string Name; string RemoteFile; }`
  - `SlideProvenance.TagName` (const string `"TEAMPPT_PROVENANCE"`)
  - `SlideProvenance.Format(IEnumerable<RecommendedSlot>) → string` (JSON 배열)
  - `SlideProvenance.Parse(string) → List<ProvenanceEntry>` (null/빈/깨진 입력 → 빈 리스트)

- [ ] **Step 1: 실패 테스트 작성**

`src/TeampptAddin.Tests/SlideProvenanceTest.cs`:
```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class SlideProvenanceTest
    {
        [Fact]
        public void Format_Then_Parse_RoundTrips()
        {
            var slots = new List<RecommendedSlot>
            {
                new RecommendedSlot { Asset = new HeaderAsset {
                    Kind = "header", Name = "공통헤더",
                    Extra = new Dictionary<string, JToken> { ["remote_file"] = "assets/h.pptx" } } },
                new RecommendedSlot { Asset = new HeaderAsset {
                    Kind = "layout", Name = "2단", File = "local-l.pptx" } }
            };

            var json = SlideProvenance.Format(slots);
            var parsed = SlideProvenance.Parse(json);

            Assert.Equal(2, parsed.Count);
            Assert.Equal("header", parsed[0].Kind);
            Assert.Equal("공통헤더", parsed[0].Name);
            Assert.Equal("assets/h.pptx", parsed[0].RemoteFile);   // Extra["remote_file"] 우선
            Assert.Equal("layout", parsed[1].Kind);
            Assert.Equal("local-l.pptx", parsed[1].RemoteFile);     // remote_file 없으면 File 폴백
        }

        [Fact]
        public void Format_SkipsNullAssets()
        {
            var slots = new List<RecommendedSlot>
            {
                new RecommendedSlot { Asset = null },
                new RecommendedSlot { Asset = new HeaderAsset { Kind = "layout", Name = "L" } }
            };
            var parsed = SlideProvenance.Parse(SlideProvenance.Format(slots));
            Assert.Single(parsed);
            Assert.Equal("layout", parsed[0].Kind);
        }

        [Fact]
        public void Parse_NullOrGarbage_ReturnsEmpty()
        {
            Assert.Empty(SlideProvenance.Parse(null));
            Assert.Empty(SlideProvenance.Parse(""));
            Assert.Empty(SlideProvenance.Parse("not json {{{"));
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
Expected: FAIL — `SlideProvenance` 타입 없음(컴파일 에러).

- [ ] **Step 3: SlideProvenance 구현**

`src/TeampptAddin/Core/SlideProvenance.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public class ProvenanceEntry
    {
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string RemoteFile { get; set; } = "";
    }

    /// <summary>
    /// 조립된 슬라이드에 어떤 에셋이 적용됐는지 기록/복원. 슬라이드 Tags에 JSON으로 저장.
    /// 후속 단계(컴포넌트 교체·슬라이드별 대화형 추천)가 vision 없이 슬라이드→에셋을 역참조하는 토대.
    /// 포맷은 의도적으로 최소(Kind/Name/RemoteFile) — 소비자가 생기는 후속 단계에서 확장.
    /// </summary>
    public static class SlideProvenance
    {
        public const string TagName = "TEAMPPT_PROVENANCE";

        private static string RemotePath(HeaderAsset a)
            => a?.Extra != null && a.Extra.ContainsKey("remote_file")
                ? a.Extra["remote_file"].ToString()
                : a?.File ?? "";

        public static string Format(IEnumerable<RecommendedSlot> slots)
        {
            var entries = (slots ?? Enumerable.Empty<RecommendedSlot>())
                .Where(s => s?.Asset != null)
                .Select(s => new ProvenanceEntry
                {
                    Kind = s.Asset.Kind ?? "",
                    Name = s.Asset.Name ?? "",
                    RemoteFile = RemotePath(s.Asset)
                })
                .ToList();
            return JsonConvert.SerializeObject(entries);
        }

        public static List<ProvenanceEntry> Parse(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return new List<ProvenanceEntry>();
            try
            {
                return JsonConvert.DeserializeObject<List<ProvenanceEntry>>(tag)
                       ?? new List<ProvenanceEntry>();
            }
            catch
            {
                return new List<ProvenanceEntry>();
            }
        }
    }
}
```

- [ ] **Step 4: csproj에 Compile Include 추가**

`src/TeampptAddin/TeampptAddin.csproj`의 `<Compile Include>` 섹션(Core 파일들 근처, 예: `<Compile Include="Core\DeckAssembler.cs" />` 다음 줄)에 추가:
```xml
    <Compile Include="Core\SlideProvenance.cs" />
```

- [ ] **Step 5: 빌드 + 테스트 통과 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```bash
tail -5 c:/Projects/teamppt-addin/build.log
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false --filter "FullyQualifiedName~SlideProvenanceTest" -v minimal
```
Expected: 3 tests PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/TeampptAddin/Core/SlideProvenance.cs src/TeampptAddin.Tests/SlideProvenanceTest.cs src/TeampptAddin/TeampptAddin.csproj
git commit -m "feat(phase4.5): SlideProvenance — 적용 에셋 JSON 직렬화/역직렬화 (TDD 3/3)"
```

---

### Task 3: 조립 시 출처 태그 기록 (AssembleAsync 배선, COM)

**Files:**
- Modify: `src/TeampptAddin/Core/DeckAssembler.cs:151-217` (`AssembleAsync` Phase 2 슬라이드 빌드 루프)

**Interfaces:**
- Consumes: `SlideProvenance.Format(IEnumerable<RecommendedSlot>)`, `SlideProvenance.TagName` (Task 2), COM `Slide.Tags.Add(name, value)`
- Produces: 각 대표 빌드 슬라이드에 `Tags[SlideProvenance.TagName]` = 실제로 붙은 슬롯들의 JSON. 복제 본문 슬라이드는 `Duplicate()`가 태그를 복사하므로 자동 상속(코드 추가 없음).

**왜 단위테스트 없음:** `Slide.Tags`·COM은 PowerPoint 프로세스 필요. Format 로직은 Task 2에서 검증됨. 이 Task는 빌드 + 수동 PPT 검증.

- [ ] **Step 1: 적용 슬롯 누적 + 태그 기록 추가**

`src/TeampptAddin/Core/DeckAssembler.cs`의 Phase 2 슬라이드 빌드 루프에서, 슬롯 삽입 `foreach` **직전**에 누적 리스트를 선언하고, **성공적으로 붙은 슬롯만** 누적한 뒤, 슬롯 루프 **직후** 태그를 기록한다. 아래처럼 교체:

```csharp
                // 새 빈 슬라이드 추가
                int insertAt = pres.Slides.Count + 1;
                var newSlide = pres.Slides.AddSlide(insertAt, masterLayout);

                // 기본 placeholder 도형 제거
                while (newSlide.Shapes.Count > 0)
                    newSlide.Shapes[1].Delete();

                // 이 슬라이드에 실제로 붙은 슬롯(출처 기록용)
                var appliedSlots = new List<RecommendedSlot>();

                // 에셋 슬롯 순서대로 삽입 (SortSlotsByLayer로 z-order 보장)
                foreach (var slot in SortSlotsByLayer(item.Slots))
                {
                    var remotePath = slot.Asset.Extra != null &&
                        slot.Asset.Extra.ContainsKey("remote_file")
                        ? slot.Asset.Extra["remote_file"].ToString()
                        : slot.Asset.File;

                    string localPath;
                    if (!prefetched.TryGetValue(remotePath, out localPath) ||
                        string.IsNullOrEmpty(localPath) ||
                        !System.IO.File.Exists(localPath))
                    {
                        result.Warnings.Add($"{slot.Asset.Name ?? remotePath} 파일 없음");
                        continue;
                    }

                    try
                    {
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
                        appliedSlots.Add(slot);   // 붙기 성공한 것만 출처에 기록
                    }
                    catch (Exception ex)
                    {
                        var warn = $"{slot.Asset.Name ?? remotePath} 슬롯 삽입 실패: {ex.Message}";
                        result.Warnings.Add(warn);
                        Logger.Log($"[DeckAssembler] {warn}");
                    }
                }

                // 출처 기록 — 복제 본문 슬라이드는 Duplicate()가 이 태그를 복사하므로 자동 상속됨
                if (appliedSlots.Count > 0)
                {
                    try { newSlide.Tags.Add(SlideProvenance.TagName, SlideProvenance.Format(appliedSlots)); }
                    catch (Exception ex) { Logger.Log($"[DeckAssembler] 출처 태그 실패: {ex.Message}"); }
                }

                if (item.BoxKind == "body" && item.IsRepresentative)
                    repSlideIndex = newSlide.SlideIndex;

                result.SlideCount++;
```

- [ ] **Step 2: 관리자 빌드(COM 등록) + DLL 타임스탬프 검증**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```bash
tail -5 c:/Projects/teamppt-addin/build.log
stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll
```
Expected: Build succeeded, 0 errors, DLL 타임스탬프 1분 이내.

- [ ] **Step 3: 기존 테스트 회귀 없음 확인**

```powershell
Start-Process -FilePath "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" -ArgumentList '"c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.sln" /t:Build /p:Configuration=Debug "/p:Platform=Any CPU" /p:RegisterForComInterop=false /verbosity:minimal /fileLogger /fileLoggerParameters:logfile=c:\Projects\teamppt-addin\build.log;verbosity=minimal' -Verb RunAs -Wait -WindowStyle Hidden
```
```powershell
dotnet test src/TeampptAddin.Tests --no-build -p:BuildProjectReferences=false -v minimal
```
Expected: 전체 PASS.

- [ ] **Step 4: PPT 수동 검증**

1. PowerPoint 새 빈 프레젠테이션 열기
2. AI 패널 → "📂 리디자인 (초안 파일)" → 초안 .pptx → 구조분석 → 컨셉 선택 → 박스별 추천
3. "이 추천으로 덱 조립 ▶" 클릭
4. **확인 ①(겹침 해소):** 본문 슬라이드가 header+layout만으로 깨끗하게 나오는지(이전 스샷처럼 컴포넌트가 겹쳐 박히지 않는지)
5. **확인 ②(출처 기록):** 임의 본문/표지 슬라이드 선택 후 매크로/즉시창에서 `ActiveWindow.Selection.SlideRange(1).Tags("TEAMPPT_PROVENANCE")`가 JSON 문자열을 반환하는지. (또는 디버그 로그로 조립 후 확인.) 복제 본문 슬라이드도 같은 태그를 갖는지.

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Core/DeckAssembler.cs
git commit -m "feat(phase4.5): 조립 슬라이드에 출처 태그 기록 — 후속 컴포넌트교체·대화형UX 토대"
```

---

## 🗺️ 로드맵 — 이 플랜 이후 (브레인스토밍 합의, 후속 플랜으로 분리)

> Phase 4.5는 즉시 코드만으로 가능. 아래는 **에셋 재저작·인제스트·스키마**가 선행돼야 하므로 별도 플랜 + 디자이너 작업 대기. 각 단계는 자기 spec→plan 사이클을 가진다.

### Phase 6a — 컴포넌트 데이터 모델 (인제스트·스키마, 디자이너 재저작 선행)
- **마킹:** 디자이너가 레이아웃 저작 시 교체될 요소를 Group으로 묶고 **이름 접두사 `comp_`** 부여(선택 창). 표시된 그룹만 컴포넌트로 추출(편의용 그룹은 무시). DB/인제스트는 **일반화된 `slot_type`** 필드로 설계 → 나중에 `img_`/`chart_` 추가 시 스키마 변경 0.
- **수확:** 인제스트가 `comp_` top-level 그룹을 ① 레이아웃의 일부 + ② 독립 컴포넌트 에셋으로 동시 저장(자기 pptx 조각·썸네일·임베딩). `parent_layout`·`slot`·`bbox`·**종횡비** 태그.
- **LLM 설명:** 기존 비전 패스를 그룹마다 적용해 **설명**(임베딩의 근거)을 생성 — 검색은 이름이 아니라 이 설명/임베딩으로 굴러간다. DB 에셋 id는 LLM 라벨(`comp_xxx`), pptx 그룹 이름은 디자이너가 친 그대로.
- **이웃 미리계산:** 각 컴포넌트의 벡터 이웃 top-3를 인제스트 시 `similar_ids`로 저장 → 런타임 라이브 쿼리 0. (라이브러리 변경 시 재계산.)

### Phase 6b — 컴포넌트 클릭-교체 (인터랙션)
- 컴포넌트(출처 태그 있는 그룹) 선택 → 패널이 **미리저장된 3개 대안** 표시(비전·쿼리 0).
- 교체: bbox **삭제 전에 먼저 기록** → 새 컴포넌트 삽입·**fit(비율유지 균일 스케일+중앙정렬)** → 원본 삭제 → `comp_`/출처 태그 재부여 + `ConceptResolver` 색 재적용 → `StartNewUndoEntry`로 한 방 Undo.
- 종횡비 필터로 후보가 비슷한 비율만 떠서 fit 시 거의 딱 맞음(찌그러짐·overflow 회피). 균일 스케일은 그룹 글꼴까지 같은 배율로 줄여 텍스트 넘침 없음.
- ⚠ 내용 이주(Phase 5) 이후 교체는 사용자 텍스트를 옮겨와야 함 — SlotMapper 재호출, 그 단계로 미룸.

### Phase 6c — 슬라이드별 대화형 조립 UX
- **출처 기록(이 플랜 Task 3)이 토대.** 슬라이드 이동 감지(화면공유 진단 조각 확장) → 슬라이드 태그(`TEAMPPT_PROVENANCE`) 조회 → 그 슬라이드에 적용된 에셋 박스 + **슬라이드별 추천 블록**(적용 에셋 id의 벡터 이웃, 비전 0) → "다른 3단 레이아웃" 같은 교체 + 스코프 Q&A(on-demand LLM). "슬라이드마다 채팅방" 느낌으로 위임 → 대화형 조립 전환.

### 유연성 / 재인제스트 노트
- 새 기능이 **이미 저장한 데이터 위 계산**이면 → 공짜 배치 재계산.
- 새 **디자이너 마킹**이 필요하면 → 소스 재저작 필요(공짜 아님). 단 로고·사진처럼 **자동검출 가능한 슬롯**은 일회성 배치 enrichment로 기존 에셋 일괄 마킹 가능, 에셋이 템플릿 패밀리면 패밀리당 1회. 자동화 불가한 사람 판단(컴포넌트 경계)은 1일차부터 `comp_`로 디자이너에 위임해 앞당겨 결제.
- 원칙: **`slot_type` 일반화 + 소스 pptx 보관** → 대부분의 후속 슬롯 타입은 싼 배치로 흡수.
