# 에셋 조합 추천 (Route B 1단계 — 추천까지만) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 초안 슬라이드의 재료 종류·양·의도·목적을 보고, 어울리는 에셋 **조합**(cover/end는 통짜 `slide` 1장, 본문은 `header`+`layout`+`component`×N)을 **추천만** 한다. 슬라이드 배치·재료 이식은 다음 스펙(이번엔 하지 않음).

**Architecture:** 사실(텍스트·개수·도형)은 COM/벡터, 판단(역할·매칭·조합 배정)은 LLM. 흐름 = `DraftSlideReader`(COM 사실) → `DraftUnderstandingService`(LLM 이해+필요조합) → `CombinationCandidateProvider`(kind별 벡터 후보) → `CombinationRecommender`(LLM 조합 판단) → `CombinationRecommendation` → AssetPanel 카드. 인제스트 메타데이터를 4분류 + 조합 판단 필드로 확장하고 번들을 재인제스트한다.

**Tech Stack:** C# .NET Framework 4.8, WPF (TaskPane UI), Newtonsoft.Json, Gemini 2.5 Flash (구조화 JSON), gemini-embedding-001 (768d), Supabase pgvector(`match_assets` RPC), xunit + vstest(비-UAC 순수 로직).

## Global Constraints

- **사실 = COM / 벡터, 판단 = LLM.** 텍스트·개수·도형은 COM 원문이 사실(파서가 LLM 값을 덮어씀). 역할·매칭·조합 배정만 LLM.
- **LLM은 텍스트 내용을 생성·수정하지 않는다.**
- **이번 스펙은 비파괴 이전 단계 — 슬라이드를 건드리지 않는다(추천만).** `RedesignApplier`·`SlotMapper`/`SlotMapSchema`/`SlotMapParser`·`AssetShapeInventory`·`CoordinateConverter`는 이번 호출 경로에서 사용하지 않는다(삭제 아님, 배치 스펙용 보존). `CoordinateConverter`에 폴백 로직 추가 금지.
- **API 키를 문서·커밋에 평문으로 넣지 않는다.** 키는 `Assets/api-keys.json`·`%LOCALAPPDATA%\TeampptAddin\admin.json`(둘 다 gitignore)에서만 로드.
- **`kind="slide"`(taxonomy)와 `scope="slide"`(컬럼, 적용 범위)는 다른 축 — 혼동 금지.**
- **순수 로직은 비-UAC vstest 워크플로로 TDD.** COM/WPF가 섞인 단위만 관리자 MSBuild + DLL 타임스탬프 + 로그 0건으로 검증.
- 빌드 후 검증 생략 금지(CLAUDE.md): DLL 타임스탬프 1분 이내 + `build.log` 오류 0건.

### 공용 빌드/테스트 명령 (각 태스크가 참조)

**비-UAC 순수 로직 (Task 1–4, 6–7, 8·9의 순수 부분):**

```powershell
# 1) 본체를 COM 등록 없이 빌드 (UAC 불필요)
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "c:\Projects\teamppt-addin\src\TeampptAddin\TeampptAddin.csproj" `
  /t:Build /p:Configuration=Debug "/p:Platform=AnyCPU" /p:RegisterForComInterop=false /verbosity:minimal
# 2) 테스트 프로젝트를 본체 재빌드 없이 빌드
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\TeampptAddin.Tests.csproj" `
  /t:Build /p:Configuration=Debug /p:BuildProjectReferences=false /verbosity:minimal
# 3) 특정 테스트만 실행 (FQN 또는 클래스명으로 필터)
& "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe" `
  "c:\Projects\teamppt-addin\src\TeampptAddin.Tests\bin\Debug\net48\TeampptAddin.Tests.dll" `
  /TestCaseFilter:"FullyQualifiedName~<클래스명>"
```

**관리자 빌드 (Task 10–11, COM/WPF 검증):** CLAUDE.md의 Start-Process RunAs MSBuild 명령 그대로. 직후 DLL 타임스탬프(1분 이내) + `build.log` 오류 0건 확인.

---

## File Structure

**모델 (생성/수정):**
- `src/TeampptAddin/Models/AssetSchema.cs` — `AssetCapacity` 클래스 추가(modify)
- `src/TeampptAddin/Models/HeaderAsset.cs` — `Capacity`, `MaterialKinds`, `SourceDeck` 필드 추가(modify)
- `src/TeampptAddin/Models/DraftModels.cs` — `NeededCombination` 추가 + `DraftUnderstanding`에 `Purpose`·`NeededCombination`(modify)
- `src/TeampptAddin/Models/RecommendationModels.cs` — `RecommendedSlot`, `CombinationRecommendation`(create)

**인제스트 (수정):**
- `src/TeampptAddin/Services/UnderstandingSchema.cs` — kind 4분류 enum + capacity/material_kinds + 프롬프트(modify)
- `src/TeampptAddin/Services/UnderstandingParser.cs` — 신규 필드 파싱(modify)
- `src/TeampptAddin/Services/AssetRowBuilder.cs` — metadata에 capacity/material_kinds(modify)
- `src/TeampptAddin/Services/SupabaseAssetMapper.cs` — metadata·source_deck 역매핑(modify)
- `src/TeampptAddin/Services/MatchQuery.cs` — kind 필터 인자 오버로드(modify)

**초안 이해 (수정):**
- `src/TeampptAddin/Services/DraftUnderstandingSchema.cs` — purpose·neededCombination + 프롬프트(modify)
- `src/TeampptAddin/Services/DraftUnderstandingParser.cs` — 신규 필드 파싱(modify)

**추천 엔진 (생성):**
- `src/TeampptAddin/Services/CombinationRecommenderSchema.cs` — LLM 조합 선택 schema + prompt(create)
- `src/TeampptAddin/Services/CombinationRecommenderParser.cs` — LLM 응답 → 추천(create)
- `src/TeampptAddin/Services/CombinationCandidateProvider.cs` — kind별 벡터 후보 풀(create)
- `src/TeampptAddin/Services/CombinationRecommender.cs` — LLM 조합 선택 오케스트레이션(create)
- `src/TeampptAddin/Services/RecommendationService.cs` — 읽기→이해→후보→추천 오케스트레이터(create)

**UI (수정):**
- `src/TeampptAddin/UI/Wpf/AssetPanel.cs` — 리디자인 바를 추천 표시로 교체, 종류별 카드(modify)
- `src/TeampptAddin/UI/TaskPaneHost.cs` — `RecommendationService` 생성·주입(modify)

**SQL/문서 (수정):**
- `docs/SUPABASE-SETUP.md` — `match_assets` 함수에 `filter_kind` + `source_deck` 반환 추가(modify)

**테스트 (생성):** `src/TeampptAddin.Tests/` 아래 — `UnderstandingParserTest.cs`(modify), `AssetRowBuilderTest.cs`(modify), `SupabaseAssetMapperTest.cs`(modify), `MatchQueryTest.cs`(modify), `DraftUnderstandingParserTest.cs`(modify), `CombinationRecommenderParserTest.cs`(create), `CombinationCandidateProviderTest.cs`(create), `CombinationRecommenderTest.cs`(create).

---

## Task 1: 에셋 모델 — 4분류 kind + capacity + material_kinds (인제스트 스키마/파서)

**Files:**
- Modify: `src/TeampptAddin/Models/AssetSchema.cs` (AssetCapacity 추가)
- Modify: `src/TeampptAddin/Models/HeaderAsset.cs` (Capacity, MaterialKinds, SourceDeck)
- Modify: `src/TeampptAddin/Services/UnderstandingSchema.cs`
- Modify: `src/TeampptAddin/Services/UnderstandingParser.cs`
- Test: `src/TeampptAddin.Tests/UnderstandingParserTest.cs`

**Interfaces:**
- Produces:
  - `class AssetCapacity { int Min; int Max; }` (JsonProperty "min"/"max")
  - `HeaderAsset.Capacity` (AssetCapacity), `HeaderAsset.MaterialKinds` (List<string>), `HeaderAsset.SourceDeck` (string, JsonProperty "source_deck")
  - `UnderstandingSchema.BuildResponseSchema()` — kind enum = `["slide","header","layout","component"]`, 신규 `capacity`(object min/max int), `material_kinds`(array of enum)
  - `UnderstandingParser.Parse(string llmJson, string category, string file)` — 기존 시그니처 유지, HeaderAsset에 Capacity/MaterialKinds 채움

- [ ] **Step 1: `AssetCapacity` 모델 추가**

`src/TeampptAddin/Models/AssetSchema.cs` 끝(닫는 `}` 직전, `AssetSlot` 다음)에 추가:

```csharp
    public class AssetCapacity
    {
        [JsonProperty("min")] public int Min { get; set; }
        [JsonProperty("max")] public int Max { get; set; }
    }
```

- [ ] **Step 2: `HeaderAsset`에 필드 추가**

`src/TeampptAddin/Models/HeaderAsset.cs`에서 `[JsonExtensionData] public Dictionary<string, JToken> Extra` 줄 **위**에 추가:

```csharp
        [JsonProperty("capacity")] public AssetCapacity Capacity { get; set; }
        [JsonProperty("material_kinds")] public List<string> MaterialKinds { get; set; }
        [JsonProperty("source_deck")] public string SourceDeck { get; set; }
```

- [ ] **Step 3: 실패 테스트 작성**

`src/TeampptAddin.Tests/UnderstandingParserTest.cs`를 열어 기존 클래스에 테스트 추가(파일 상단 `using` 확인). 다음 두 테스트를 클래스 안에 추가:

```csharp
        [Fact]
        public void Parses_Slide_Kind_And_Capacity_And_MaterialKinds()
        {
            const string llm = @"{
              ""name"":""3단 카드 레이아웃"", ""kind"":""layout"",
              ""use_when"":""기능 비교"", ""content_fit"":[""카드""], ""tags"":[""3단""],
              ""example_intents"":[""기능 3개 비교""], ""slots"":[], ""colors"":[], ""fonts"":[],
              ""capacity"":{ ""min"":3, ""max"":3 },
              ""material_kinds"":[""text"",""image""]
            }";
            var u = UnderstandingParser.Parse(llm, "레이아웃", "pptx/x.pptx");
            Assert.Equal("layout", u.Asset.Kind);
            Assert.Equal(3, u.Asset.Capacity.Min);
            Assert.Equal(3, u.Asset.Capacity.Max);
            Assert.Equal(new[] { "text", "image" }, u.Asset.MaterialKinds.ToArray());
        }

        [Fact]
        public void Capacity_Defaults_Null_When_Absent()
        {
            const string llm = @"{
              ""name"":""표지"", ""kind"":""slide"", ""use_when"":""오프닝"",
              ""content_fit"":[], ""tags"":[], ""example_intents"":[], ""slots"":[], ""colors"":[], ""fonts"":[]
            }";
            var u = UnderstandingParser.Parse(llm, "표지", "pptx/c.pptx");
            Assert.Equal("slide", u.Asset.Kind);
            Assert.Null(u.Asset.Capacity);
            Assert.Empty(u.Asset.MaterialKinds);
        }
```

파일 상단에 `using System.Linq;`가 없으면 추가(`.ToArray()` 사용). 기존 파일 확인 후 없을 때만.

- [ ] **Step 4: 테스트 실패 확인**

공용 비-UAC 명령으로 빌드 후:
```
/TestCaseFilter:"FullyQualifiedName~UnderstandingParserTest"
```
Expected: 컴파일 실패 또는 `Parses_Slide_Kind...` FAIL (Capacity가 아직 파싱 안 됨).

- [ ] **Step 5: 스키마 갱신**

`src/TeampptAddin/Services/UnderstandingSchema.cs`의 `BuildResponseSchema()`에서 kind enum과 properties를 수정:

kind 줄을 교체:
```csharp
                    ["kind"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray { "slide", "header", "layout", "component" }
                    },
```

`["fonts"]` 블록 뒤(닫는 `}` 전), properties에 추가:
```csharp
,
                    ["capacity"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["min"] = new JObject { ["type"] = "integer" },
                            ["max"] = new JObject { ["type"] = "integer" }
                        },
                        ["required"] = new JArray { "min", "max" }
                    },
                    ["material_kinds"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JArray { "text", "image", "chart", "table", "bullet" }
                        }
                    }
```

`["required"]` 배열은 그대로 둔다(capacity/material_kinds는 선택 — 통짜 slide엔 capacity가 없을 수 있음).

- [ ] **Step 6: 프롬프트 갱신 (4분류 + 신규 필드)**

`BuildSystemPrompt`의 `## 판단 규칙` 중 kind 줄을 교체하고 capacity/material_kinds 설명을 추가:

```csharp
- kind: 페이지 통째가 완결된 표지(오픈/엔드)면 ""slide"", 중간 슬라이드의 상단 제목 영역이면 ""header"", 헤더를 제외한 본문 배치 틀이면 ""layout"", 본문 위에 얹는 부품(카드·아이콘블록·그래프·표·다이어그램)이면 ""component"". 우선순위: slide → header → layout → component.
- capacity: 이 에셋이 담기 좋은 재료 블록 수를 {min,max}로 (예: 카드 3개면 {min:3,max:3}, 1~2단이면 {min:1,max:2}). 통짜 slide나 단일 header는 생략 가능.
- material_kinds: 담기 좋은 재료 타입 (text/image/chart/table/bullet) 배열.
```

- [ ] **Step 7: 파서 갱신**

`src/TeampptAddin/Services/UnderstandingParser.cs`의 `asset` 초기화에서 `Slots = ...` 뒤(닫는 `}` 전)에 추가:

```csharp
,
                Capacity = o["capacity"] is JObject cap
                    ? new AssetCapacity { Min = cap["min"]?.Value<int>() ?? 0, Max = cap["max"]?.Value<int>() ?? 0 }
                    : null,
                MaterialKinds = StrList(o["material_kinds"])
```

- [ ] **Step 8: 테스트 통과 확인**

공용 비-UAC 명령 재실행. Expected: `UnderstandingParserTest` 4건(기존 2 + 신규 2) PASS.

- [ ] **Step 9: 커밋**

```bash
git add src/TeampptAddin/Models/AssetSchema.cs src/TeampptAddin/Models/HeaderAsset.cs src/TeampptAddin/Services/UnderstandingSchema.cs src/TeampptAddin/Services/UnderstandingParser.cs src/TeampptAddin.Tests/UnderstandingParserTest.cs
git commit -m "feat(ingest): 에셋 kind 4분류(slide/header/layout/component) + capacity·material_kinds"
```

---

## Task 2: 적재 라운드트립 — metadata 저장 + 역매핑

**Files:**
- Modify: `src/TeampptAddin/Services/AssetRowBuilder.cs`
- Modify: `src/TeampptAddin/Services/SupabaseAssetMapper.cs`
- Test: `src/TeampptAddin.Tests/AssetRowBuilderTest.cs`, `src/TeampptAddin.Tests/SupabaseAssetMapperTest.cs`

**Interfaces:**
- Consumes: `HeaderAsset.Capacity`/`MaterialKinds`/`SourceDeck` (Task 1)
- Produces:
  - `AssetRowBuilder.Build(...)` — metadata jsonb에 `capacity`, `material_kinds` 포함
  - `SupabaseAssetMapper.Map(JObject row)` — metadata에서 capacity/material_kinds, row에서 `source_deck`을 읽어 `HeaderAsset.SourceDeck`(=pairing key)에 채움

- [ ] **Step 1: 실패 테스트 — RowBuilder**

`src/TeampptAddin.Tests/AssetRowBuilderTest.cs`의 `U()` 헬퍼에 capacity/material_kinds를 추가하고 새 테스트 작성. `U()`의 `Asset` 초기화에 추가:

```csharp
                Capacity = new AssetCapacity { Min = 3, Max = 3 },
                MaterialKinds = new List<string> { "text", "image" },
```

새 테스트:
```csharp
        [Fact]
        public void Build_Puts_Capacity_And_MaterialKinds_In_Metadata()
        {
            var row = AssetRowBuilder.Build(U(), new float[] { 0.1f }, "t", "p", "th", "d");
            var meta = (JObject)row["metadata"];
            Assert.Equal(3, (int)meta["capacity"]["min"]);
            Assert.Equal("text", (string)meta["material_kinds"][0]);
        }
```

- [ ] **Step 2: 실패 테스트 — Mapper**

`src/TeampptAddin.Tests/SupabaseAssetMapperTest.cs`에 추가:
```csharp
        [Fact]
        public void Map_Reads_Capacity_MaterialKinds_SourceDeck()
        {
            var row = JObject.Parse(@"{
              ""file"":""pptx/a.pptx"",""thumb"":""thumb/a.png"",""name"":""A"",
              ""category"":""레이아웃"",""kind"":""layout"",""scope"":""slide"",
              ""source_deck"":""bundle.pptx"",
              ""metadata"":{ ""capacity"":{""min"":2,""max"":4}, ""material_kinds"":[""text""] }
            }");
            var a = SupabaseAssetMapper.Map(row);
            Assert.Equal(2, a.Capacity.Min);
            Assert.Equal(4, a.Capacity.Max);
            Assert.Equal(new[] { "text" }, a.MaterialKinds.ToArray());
            Assert.Equal("bundle.pptx", a.SourceDeck);
        }
```
(파일 상단에 `using System.Linq;`·`using Newtonsoft.Json.Linq;` 없으면 추가.)

- [ ] **Step 3: 테스트 실패 확인**

비-UAC 빌드 후 `/TestCaseFilter:"FullyQualifiedName~AssetRowBuilderTest|FullyQualifiedName~SupabaseAssetMapperTest"`.
Expected: 신규 2건 FAIL.

- [ ] **Step 4: AssetRowBuilder 구현**

`src/TeampptAddin/Services/AssetRowBuilder.cs`의 `metadata` JObject에 두 줄 추가(slots 뒤):

```csharp
                ["slots"] = JArray.FromObject(a.Slots ?? new List<AssetSlot>()),
                ["capacity"] = a.Capacity != null ? JObject.FromObject(a.Capacity) : null,
                ["material_kinds"] = JArray.FromObject(a.MaterialKinds ?? new List<string>())
```

- [ ] **Step 5: SupabaseAssetMapper 구현**

`src/TeampptAddin/Services/SupabaseAssetMapper.cs`의 `Map`에서 반환 객체에 추가(`Slots = ...` 뒤, `Extra =` 전):

```csharp
                Capacity = meta["capacity"] is JObject cap
                    ? new AssetCapacity { Min = cap["min"]?.Value<int>() ?? 0, Max = cap["max"]?.Value<int>() ?? 0 }
                    : null,
                MaterialKinds = StrList(meta["material_kinds"]),
                SourceDeck = row["source_deck"]?.ToString(),
```

- [ ] **Step 6: 테스트 통과 확인**

Step 3 명령 재실행. Expected: 두 클래스 전체 PASS.

- [ ] **Step 7: 커밋**

```bash
git add src/TeampptAddin/Services/AssetRowBuilder.cs src/TeampptAddin/Services/SupabaseAssetMapper.cs src/TeampptAddin.Tests/AssetRowBuilderTest.cs src/TeampptAddin.Tests/SupabaseAssetMapperTest.cs
git commit -m "feat(ingest): capacity·material_kinds metadata 라운드트립 + source_deck 역매핑"
```

---

## Task 3: match_assets kind 필터 — SQL 갱신 + MatchQuery 오버로드

**Files:**
- Modify: `docs/SUPABASE-SETUP.md` (match_assets 함수 정의)
- Modify: `src/TeampptAddin/Services/MatchQuery.cs`
- Test: `src/TeampptAddin.Tests/MatchQueryTest.cs`

**Interfaces:**
- Consumes: 기존 `MatchQuery.BuildArgs(float[], int)` (변경 없이 유지 — 기존 호출자 `DraftMatchService`·`VectorRecommendService` 호환)
- Produces: `MatchQuery.BuildArgs(float[] queryEmbedding, int matchCount, string filterKind)` — `filter_kind`를 args에 추가(null이면 키 생략). `match_assets` RPC가 `source_deck` 반환.

> **배경:** PostgREST는 JSON body의 인자명으로 함수를 매칭하므로, `match_assets`에 `filter_kind text default null`을 추가해도 기존 2-인자 호출은 그대로 동작(backward compatible). 반환에 `source_deck`을 추가해야 `SupabaseAssetMapper`가 pairing key를 읽는다.

- [ ] **Step 1: 실패 테스트 작성**

`src/TeampptAddin.Tests/MatchQueryTest.cs`에 추가:
```csharp
        [Fact]
        public void BuildArgs_With_Kind_Includes_FilterKind()
        {
            var args = MatchQuery.BuildArgs(new float[] { 0.1f }, 5, "header");
            Assert.Equal("header", (string)args["filter_kind"]);
            Assert.Equal(5, (int)args["match_count"]);
        }

        [Fact]
        public void BuildArgs_Null_Kind_Omits_FilterKind()
        {
            var args = MatchQuery.BuildArgs(new float[] { 0.1f }, 5, null);
            Assert.False(args.ContainsKey("filter_kind"));
        }
```

- [ ] **Step 2: 테스트 실패 확인**

비-UAC 빌드 후 `/TestCaseFilter:"FullyQualifiedName~MatchQueryTest"`. Expected: 신규 2건 컴파일/FAIL.

- [ ] **Step 3: MatchQuery 오버로드 구현**

`src/TeampptAddin/Services/MatchQuery.cs`에 오버로드 추가(기존 `BuildArgs(float[], int)`는 유지). 기존 메서드 body를 새 오버로드에 위임:

```csharp
        public static JObject BuildArgs(float[] queryEmbedding, int matchCount)
            => BuildArgs(queryEmbedding, matchCount, null);

        public static JObject BuildArgs(float[] queryEmbedding, int matchCount, string filterKind)
        {
            var vec = "[" + string.Join(",", queryEmbedding.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";
            var o = new JObject { ["query_embedding"] = vec, ["match_count"] = matchCount };
            if (!string.IsNullOrEmpty(filterKind)) o["filter_kind"] = filterKind;
            return o;
        }
```

- [ ] **Step 4: 테스트 통과 확인**

Step 2 명령 재실행. Expected: `MatchQueryTest` 전체(기존 3 + 신규 2) PASS.

- [ ] **Step 5: SQL 정의 갱신 (문서)**

`docs/SUPABASE-SETUP.md`의 `## 3. 벡터 검색 함수(RPC)` 코드블록을 교체:

```sql
create or replace function match_assets(
  query_embedding vector(768), match_count int, filter_kind text default null)
returns table (
  id uuid, file text, thumb text, name text, category text, kind text,
  scope text, tags text[], use_when text, content_fit text[], metadata jsonb,
  source_deck text, similarity float
) language sql stable as $$
  select a.id, a.file, a.thumb, a.name, a.category, a.kind,
         a.scope, a.tags, a.use_when, a.content_fit, a.metadata,
         a.source_deck,
         1 - (a.embedding <=> query_embedding) as similarity
  from assets a
  where filter_kind is null or a.kind = filter_kind
  order by a.embedding <=> query_embedding
  limit match_count;
$$;
```

같은 파일 `## 완료 확인`에 한 줄 추가: `- [ ] match_assets가 filter_kind 인자 + source_deck 반환을 갖도록 재실행됨.`

- [ ] **Step 6: 사용자에게 SQL 재실행 안내 (수동)**

이 태스크 리뷰 시 사용자에게: "Supabase SQL Editor에서 위 `create or replace function match_assets(...)`를 1회 실행해 주세요. (반환 컬럼·인자 변경이라 재실행 필요)" 라고 알린다. 코드가 SQL을 자동 적용하지 않는다.

- [ ] **Step 7: 커밋**

```bash
git add src/TeampptAddin/Services/MatchQuery.cs src/TeampptAddin.Tests/MatchQueryTest.cs docs/SUPABASE-SETUP.md
git commit -m "feat(match): match_assets kind 필터 인자 + source_deck 반환, MatchQuery 오버로드"
```

---

## Task 4: 번들 재인제스트 (수동 운영)

**Files:** 없음(코드 변경 없음 — 운영 단계). Task 1–3 빌드가 PowerPoint에 로드된 상태여야 한다.

> 기존 DB는 표지류가 `kind="layout"`, 헤더류가 `component`로 적재돼 있을 수 있다. 4분류 + 신규 필드로 다시 인식시킨다. 파일 UNIQUE + upsert(merge-duplicates)라 중복 없음.

- [ ] **Step 1: COM 등록 빌드 확인**

Task 1–3 변경이 반영된 관리자 빌드가 PowerPoint에 로드돼 있어야 인제스트 버튼이 신규 스키마로 동작한다. 필요 시 CLAUDE.md 관리자 MSBuild로 재빌드 후 DLL 타임스탬프·로그 0건 확인.

- [ ] **Step 2: 사용자에게 재인제스트 안내 (수동)**

사용자(관리자 PC)에게: "PowerPoint TEAMPPT 패널 → 인제스트 버튼 → 표지·본문이 섞인 번들 pptx 선택 → 완료까지 대기"를 안내. 진행 중 패널 로그(`prog.Kind`)에 slide/header/layout/component가 찍히는지 본다.

- [ ] **Step 3: 적재 검증 (수동)**

Supabase Table Editor `assets`에서:
- 표지 행 `kind="slide"`, 헤더 행 `kind="header"`인지.
- `metadata`에 `capacity`/`material_kinds`가 채워졌는지.
- `source_deck`이 번들 파일명인지.

- [ ] **Step 4: 보드 갱신 (커밋 없음 / 운영 메모만)**

PROGRESS-BOARD.md의 해당 잎 상태를 갱신(재인제스트 완료 표기). 코드 커밋 없음.

---

## Task 5: 초안 이해 확장 — purpose + neededCombination

**Files:**
- Modify: `src/TeampptAddin/Models/DraftModels.cs`
- Modify: `src/TeampptAddin/Services/DraftUnderstandingSchema.cs`
- Modify: `src/TeampptAddin/Services/DraftUnderstandingParser.cs`
- Test: `src/TeampptAddin.Tests/DraftUnderstandingParserTest.cs`

**Interfaces:**
- Produces:
  - `class NeededCombination { int Slide; int Header; int Layout; int Component; }`
  - `DraftUnderstanding.Purpose` (string), `DraftUnderstanding.NeededCombination` (NeededCombination)
  - `DraftUnderstandingParser.Parse(string, DraftProfile)` — 시그니처 유지, purpose·neededCombination 채움. counts·materials는 COM 사실 유지.

- [ ] **Step 1: 모델 추가**

`src/TeampptAddin/Models/DraftModels.cs`의 `DraftUnderstanding` 클래스 끝(마지막 프로퍼티 뒤)에 추가:

```csharp
        public string Purpose { get; set; } = "";
        public NeededCombination NeededCombination { get; set; } = new NeededCombination();
```

같은 파일에 클래스 추가(`SlotMapping` 위 등 아무 위치):

```csharp
    public class NeededCombination
    {
        public int Slide { get; set; }
        public int Header { get; set; }
        public int Layout { get; set; }
        public int Component { get; set; }
    }
```

- [ ] **Step 2: 실패 테스트 작성**

`src/TeampptAddin.Tests/DraftUnderstandingParserTest.cs`에 추가:

```csharp
        [Fact]
        public void Parses_Purpose_And_NeededCombination_Body()
        {
            var profile = new DraftProfile
            {
                Shapes = { new DraftShape { Id = 1, Kind = "text", Text = "기능", CharCount = 2 } }
            };
            const string llm = @"{
              ""materials"":[{""role"":""title"",""type"":""text"",""sourceShapeId"":1,""emphasis"":""heading""}],
              ""counts"":{""textBlocks"":1,""bullets"":0,""images"":0,""tables"":0,""charts"":0},
              ""layoutShape"":""x"",""designSummary"":""y"",""dominantColors"":[],
              ""matchIntent"":""기능 비교"",""slideKind"":""body"",
              ""purpose"":""3개 핵심 기능을 동등 비교"",
              ""neededCombination"":{""slide"":0,""header"":1,""layout"":1,""component"":3}
            }";
            var u = DraftUnderstandingParser.Parse(llm, profile);
            Assert.Equal("3개 핵심 기능을 동등 비교", u.Purpose);
            Assert.Equal(1, u.NeededCombination.Header);
            Assert.Equal(3, u.NeededCombination.Component);
            Assert.Equal(0, u.NeededCombination.Slide);
        }

        [Fact]
        public void Parses_Cover_NeededCombination_Slide()
        {
            var profile = new DraftProfile();
            const string llm = @"{
              ""materials"":[],""counts"":{""textBlocks"":0,""bullets"":0,""images"":0,""tables"":0,""charts"":0},
              ""layoutShape"":"""",""designSummary"":"""",""dominantColors"":[],
              ""matchIntent"":""표지"",""slideKind"":""cover"",
              ""purpose"":""오프닝 표지"",
              ""neededCombination"":{""slide"":1,""header"":0,""layout"":0,""component"":0}
            }";
            var u = DraftUnderstandingParser.Parse(llm, profile);
            Assert.Equal(1, u.NeededCombination.Slide);
            Assert.Equal("cover", u.SlideKind);
        }
```

- [ ] **Step 3: 테스트 실패 확인**

비-UAC 빌드 후 `/TestCaseFilter:"FullyQualifiedName~DraftUnderstandingParserTest"`. Expected: 신규 2건 FAIL.

- [ ] **Step 4: 스키마 갱신**

`src/TeampptAddin/Services/DraftUnderstandingSchema.cs`의 `BuildResponseSchema()` properties에서 `slideKind` 뒤에 추가:

```csharp
,
                    ["purpose"] = Str(),
                    ["neededCombination"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["slide"] = Int(), ["header"] = Int(),
                            ["layout"] = Int(), ["component"] = Int()
                        },
                        ["required"] = new JArray { "slide", "header", "layout", "component" }
                    }
```

`["required"]` 배열에 `"purpose", "neededCombination"` 추가:
```csharp
                ["required"] = new JArray { "materials", "counts", "layoutShape", "designSummary", "dominantColors", "matchIntent", "slideKind", "purpose", "neededCombination" }
```

- [ ] **Step 5: 프롬프트 갱신**

`BuildSystemPrompt`의 `## 네 일` 마지막(`- slideKind:` 뒤)에 추가:

```csharp
- purpose: 이 슬라이드의 의도·목적 한 문장 (예: '3개 핵심 기능을 동등 비교').
- neededCombination: 필요한 에셋 조합 수. cover/end면 {slide:1, header:0, layout:0, component:0}. body/section이면 {slide:0, header:1, layout:1, component:N} — N은 본문 부품 수(카드·블록 개수, counts·materials 기준). 슬롯 채우기가 아니라 '몇 종류·몇 개가 필요한가' 판단만.
```

- [ ] **Step 6: 파서 갱신**

`src/TeampptAddin/Services/DraftUnderstandingParser.cs`의 반환 객체에서 `SlideKind = ...` 뒤에 추가:

```csharp
,
                Purpose = o["purpose"]?.ToString() ?? "",
                NeededCombination = o["neededCombination"] is JObject nc
                    ? new NeededCombination
                    {
                        Slide = nc["slide"]?.Value<int>() ?? 0,
                        Header = nc["header"]?.Value<int>() ?? 0,
                        Layout = nc["layout"]?.Value<int>() ?? 0,
                        Component = nc["component"]?.Value<int>() ?? 0
                    }
                    : new NeededCombination()
```

- [ ] **Step 7: 테스트 통과 확인**

Step 3 명령 재실행. Expected: `DraftUnderstandingParserTest` 전체 PASS.

- [ ] **Step 8: 커밋**

```bash
git add src/TeampptAddin/Models/DraftModels.cs src/TeampptAddin/Services/DraftUnderstandingSchema.cs src/TeampptAddin/Services/DraftUnderstandingParser.cs src/TeampptAddin.Tests/DraftUnderstandingParserTest.cs
git commit -m "feat(draft): 초안 이해에 purpose·neededCombination 확장"
```

---

## Task 6: 추천 결과 모델 + LLM 조합 응답 파서

**Files:**
- Create: `src/TeampptAddin/Models/RecommendationModels.cs`
- Create: `src/TeampptAddin/Services/CombinationRecommenderSchema.cs`
- Create: `src/TeampptAddin/Services/CombinationRecommenderParser.cs`
- Test: `src/TeampptAddin.Tests/CombinationRecommenderParserTest.cs`

**Interfaces:**
- Consumes: `HeaderAsset` (Task 1), `DraftUnderstanding` (Task 5)
- Produces:
  - `class RecommendedSlot { HeaderAsset Asset; string FitNote; double Confidence; }`
  - `class CombinationRecommendation { string Purpose; string SlideKind; RecommendedSlot Slide; RecommendedSlot Header; RecommendedSlot Layout; List<RecommendedSlot> Components; List<string> Unmet; }`
  - `CombinationRecommenderSchema.BuildResponseSchema()` → JObject, `CombinationRecommenderSchema.BuildSystemPrompt()` → string
  - `CombinationRecommenderParser.Parse(string llmJson, Dictionary<string,List<HeaderAsset>> candidatesByKind)` → `CombinationRecommendation` — 후보 풀에 없는 file은 버림(환각 제거), `unmet` 패스스루.

- [ ] **Step 1: 추천 모델 생성**

`src/TeampptAddin/Models/RecommendationModels.cs`:

```csharp
using System.Collections.Generic;

namespace TeampptAddin
{
    public class RecommendedSlot
    {
        public HeaderAsset Asset { get; set; }
        public string FitNote { get; set; } = "";
        public double Confidence { get; set; }
    }

    public class CombinationRecommendation
    {
        public string Purpose { get; set; } = "";
        public string SlideKind { get; set; } = "";
        public RecommendedSlot Slide { get; set; }                          // cover/end
        public RecommendedSlot Header { get; set; }                         // body/section
        public RecommendedSlot Layout { get; set; }                         // body/section
        public List<RecommendedSlot> Components { get; set; } = new List<RecommendedSlot>();
        public List<string> Unmet { get; set; } = new List<string>();       // 미충족 종류명
    }
}
```

- [ ] **Step 2: 실패 테스트 작성**

`src/TeampptAddin.Tests/CombinationRecommenderParserTest.cs`:

```csharp
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationRecommenderParserTest
    {
        private static Dictionary<string, List<HeaderAsset>> Pool() => new Dictionary<string, List<HeaderAsset>>
        {
            ["header"] = new List<HeaderAsset> { new HeaderAsset { File = "h1.pptx", Name = "헤더1" } },
            ["layout"] = new List<HeaderAsset> { new HeaderAsset { File = "l1.pptx", Name = "레이아웃1" } },
            ["component"] = new List<HeaderAsset>
            {
                new HeaderAsset { File = "c1.pptx", Name = "카드" },
                new HeaderAsset { File = "c2.pptx", Name = "아이콘블록" }
            }
        };

        [Fact]
        public void Maps_Header_Layout_Components_From_Pool()
        {
            const string llm = @"{
              ""header"":{""file"":""h1.pptx"",""fitNote"":""제목"",""confidence"":0.88},
              ""layout"":{""file"":""l1.pptx"",""fitNote"":""3단"",""confidence"":0.82},
              ""components"":[{""file"":""c1.pptx"",""fitNote"":""카드"",""confidence"":0.79}],
              ""unmet"":[]
            }";
            var r = CombinationRecommenderParser.Parse(llm, Pool());
            Assert.Equal("헤더1", r.Header.Asset.Name);
            Assert.Equal(0.88, r.Header.Confidence, 2);
            Assert.Equal("레이아웃1", r.Layout.Asset.Name);
            Assert.Single(r.Components);
            Assert.Equal("카드", r.Components[0].Asset.Name);
        }

        [Fact]
        public void Drops_Hallucinated_File_And_Keeps_Unmet()
        {
            const string llm = @"{
              ""header"":{""file"":""nope.pptx"",""fitNote"":""x"",""confidence"":0.5},
              ""layout"":null,
              ""components"":[],
              ""unmet"":[""layout"",""component""]
            }";
            var r = CombinationRecommenderParser.Parse(llm, Pool());
            Assert.Null(r.Header);   // 환각 file → 버림
            Assert.Null(r.Layout);
            Assert.Empty(r.Components);
            Assert.Contains("layout", r.Unmet);
        }
    }
}
```

- [ ] **Step 3: 테스트 실패 확인**

비-UAC 빌드 후 `/TestCaseFilter:"FullyQualifiedName~CombinationRecommenderParserTest"`. Expected: 컴파일 실패(Parser 없음).

- [ ] **Step 4: 스키마/프롬프트 생성**

`src/TeampptAddin/Services/CombinationRecommenderSchema.cs`:

```csharp
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class CombinationRecommenderSchema
    {
        public static JObject BuildResponseSchema()
        {
            JObject Pick() => new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["file"] = new JObject { ["type"] = "string" },
                    ["fitNote"] = new JObject { ["type"] = "string" },
                    ["confidence"] = new JObject { ["type"] = "number" }
                },
                ["required"] = new JArray { "file", "fitNote", "confidence" }
            };

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["header"] = Pick(),
                    ["layout"] = Pick(),
                    ["components"] = new JObject { ["type"] = "array", ["items"] = Pick() },
                    ["unmet"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } }
                },
                ["required"] = new JArray { "components", "unmet" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 초안에 어울리는 디자인 에셋 '조합'을 고르는 엔진이야. 초안 이해 요약과, 종류별(header/layout/component) 후보 목록을 받아 각 종류에서 가장 잘 맞는 것을 고른다.

## 규칙
- 후보 목록(file)에 있는 것만 고른다. 목록에 없는 file을 지어내지 마라.
- header 1개, layout 1개, component는 필요수량(neededCombination.component)만큼 고른다.
- 우선순위: 재료 양(capacity)·종류(material_kinds) 적합 > 같은 source_deck(일관성) 선호.
- 적합한 후보가 없으면 해당 종류는 비워 두고(null/빈 배열) unmet 배열에 종류명을 넣는다. 욱여넣기 금지.
- fitNote는 왜 골랐는지 짧게(한 문장). 텍스트 내용을 생성하지 마라.
- confidence는 0~1.";
        }
    }
}
```

- [ ] **Step 5: 파서 구현**

`src/TeampptAddin/Services/CombinationRecommenderParser.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class CombinationRecommenderParser
    {
        public static CombinationRecommendation Parse(string llmJson, Dictionary<string, List<HeaderAsset>> candidatesByKind)
        {
            var o = JObject.Parse(llmJson);
            var rec = new CombinationRecommendation();

            rec.Header = PickFrom(o["header"], candidatesByKind, "header");
            rec.Layout = PickFrom(o["layout"], candidatesByKind, "layout");

            foreach (var c in (o["components"] as JArray) ?? new JArray())
            {
                var slot = PickFrom(c, candidatesByKind, "component");
                if (slot != null) rec.Components.Add(slot);
            }

            rec.Unmet = (o["unmet"] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>();
            return rec;
        }

        private static RecommendedSlot PickFrom(JToken pick, Dictionary<string, List<HeaderAsset>> pool, string kind)
        {
            if (pick == null || pick.Type == JTokenType.Null) return null;
            var file = pick["file"]?.ToString();
            if (string.IsNullOrEmpty(file)) return null;
            if (!pool.TryGetValue(kind, out var list)) return null;
            var asset = list.FirstOrDefault(a =>
                string.Equals(a.File, file, System.StringComparison.OrdinalIgnoreCase));
            if (asset == null) return null;   // 환각 제거
            return new RecommendedSlot
            {
                Asset = asset,
                FitNote = pick["fitNote"]?.ToString() ?? "",
                Confidence = pick["confidence"]?.Value<double>() ?? 0
            };
        }
    }
}
```

- [ ] **Step 6: 테스트 통과 확인**

Step 3 명령 재실행. Expected: `CombinationRecommenderParserTest` 2건 PASS.

- [ ] **Step 7: 커밋**

```bash
git add src/TeampptAddin/Models/RecommendationModels.cs src/TeampptAddin/Services/CombinationRecommenderSchema.cs src/TeampptAddin/Services/CombinationRecommenderParser.cs src/TeampptAddin.Tests/CombinationRecommenderParserTest.cs
git commit -m "feat(reco): 추천 결과 모델 + LLM 조합 응답 스키마·파서"
```

---

## Task 7: CombinationCandidateProvider — kind별 벡터 후보 풀

**Files:**
- Create: `src/TeampptAddin/Services/CombinationCandidateProvider.cs`
- Test: `src/TeampptAddin.Tests/CombinationCandidateProviderTest.cs`

**Interfaces:**
- Consumes: `EmbeddingService`, `SupabaseClient`, `MatchQuery.BuildArgs(float[],int,string)` (Task 3), `DraftUnderstanding.NeededCombination` (Task 5), `RecommendationCache`
- Produces:
  - `CombinationCandidateProvider(EmbeddingService embed, SupabaseClient supa)`
  - `Task<Dictionary<string,List<HeaderAsset>>> GetCandidatesAsync(DraftUnderstanding u, int topK = 5)` — 필요한 각 kind마다 벡터 검색, 실패 시 캐시 폴백
  - `static List<string> NeededKinds(NeededCombination nc)` — 순수 헬퍼(테스트 대상): slide>0이면 `["slide"]`, 아니면 header/layout/component 중 수량>0인 것
  - `static Dictionary<string,List<HeaderAsset>> GroupByKind(IEnumerable<HeaderAsset> assets, IEnumerable<string> kinds)` — 순수 헬퍼(캐시 폴백용 클라이언트 분류)

- [ ] **Step 1: 실패 테스트 작성 (순수 헬퍼만)**

`src/TeampptAddin.Tests/CombinationCandidateProviderTest.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationCandidateProviderTest
    {
        [Fact]
        public void NeededKinds_Cover_Returns_Slide_Only()
        {
            var nc = new NeededCombination { Slide = 1 };
            Assert.Equal(new[] { "slide" }, CombinationCandidateProvider.NeededKinds(nc).ToArray());
        }

        [Fact]
        public void NeededKinds_Body_Returns_Header_Layout_Component()
        {
            var nc = new NeededCombination { Header = 1, Layout = 1, Component = 3 };
            var kinds = CombinationCandidateProvider.NeededKinds(nc);
            Assert.Contains("header", kinds);
            Assert.Contains("layout", kinds);
            Assert.Contains("component", kinds);
            Assert.DoesNotContain("slide", kinds);
        }

        [Fact]
        public void GroupByKind_Splits_By_Asset_Kind()
        {
            var assets = new List<HeaderAsset>
            {
                new HeaderAsset { File = "h.pptx", Kind = "header" },
                new HeaderAsset { File = "c1.pptx", Kind = "component" },
                new HeaderAsset { File = "c2.pptx", Kind = "component" }
            };
            var g = CombinationCandidateProvider.GroupByKind(assets, new[] { "header", "component" });
            Assert.Single(g["header"]);
            Assert.Equal(2, g["component"].Count);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

비-UAC 빌드 후 `/TestCaseFilter:"FullyQualifiedName~CombinationCandidateProviderTest"`. Expected: 컴파일 실패(클래스 없음).

- [ ] **Step 3: Provider 구현**

`src/TeampptAddin/Services/CombinationCandidateProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 초안 이해의 neededCombination을 보고, 필요한 각 kind마다 match_assets(kind 필터)로
    /// 후보 풀을 만든다. Supabase 실패 시 RecommendationCache 폴백 후 클라이언트에서 kind 분류.
    /// </summary>
    public class CombinationCandidateProvider
    {
        private readonly EmbeddingService _embed;
        private readonly SupabaseClient _supa;
        private readonly RecommendationCache _cache = new RecommendationCache();

        public CombinationCandidateProvider(EmbeddingService embed, SupabaseClient supa)
        { _embed = embed; _supa = supa; }

        public static List<string> NeededKinds(NeededCombination nc)
        {
            if (nc != null && nc.Slide > 0) return new List<string> { "slide" };
            var kinds = new List<string>();
            if (nc == null) return kinds;
            if (nc.Header > 0) kinds.Add("header");
            if (nc.Layout > 0) kinds.Add("layout");
            if (nc.Component > 0) kinds.Add("component");
            return kinds;
        }

        public static Dictionary<string, List<HeaderAsset>> GroupByKind(
            IEnumerable<HeaderAsset> assets, IEnumerable<string> kinds)
        {
            var list = assets?.ToList() ?? new List<HeaderAsset>();
            var result = new Dictionary<string, List<HeaderAsset>>();
            foreach (var k in kinds)
                result[k] = list.Where(a => string.Equals(a.Kind, k, StringComparison.OrdinalIgnoreCase)).ToList();
            return result;
        }

        public async Task<Dictionary<string, List<HeaderAsset>>> GetCandidatesAsync(DraftUnderstanding u, int topK = 5)
        {
            var kinds = NeededKinds(u.NeededCombination);
            var countsText = string.Join(", ", u.Counts.Select(kv => $"{kv.Key}:{kv.Value}"));
            var query = $"{u.Purpose} | {u.MatchIntent} ({countsText})";

            try
            {
                var vector = await _embed.EmbedAsync(query).ConfigureAwait(false);
                var result = new Dictionary<string, List<HeaderAsset>>();
                var all = new List<HeaderAsset>();
                foreach (var kind in kinds)
                {
                    var rpcJson = await _supa.RpcAsync("match_assets",
                        MatchQuery.BuildArgs(vector, topK, kind)).ConfigureAwait(false);
                    var list = MatchQuery.ParseResults(rpcJson);
                    result[kind] = list;
                    all.AddRange(list);
                    Logger.Log($"[Combo] kind={kind} 후보 {list.Count}개");
                }
                if (all.Count > 0) _cache.Save(all);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Combo] Supabase 실패 → 캐시 폴백: {ex.Message}");
                return GroupByKind(_cache.Load(), kinds);
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Step 2 명령 재실행. Expected: 3건 PASS.

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Services/CombinationCandidateProvider.cs src/TeampptAddin.Tests/CombinationCandidateProviderTest.cs
git commit -m "feat(reco): kind별 벡터 후보 풀 provider (+캐시 폴백 분류)"
```

---

## Task 8: CombinationRecommender — LLM 조합 선택 + cover/end 단축

**Files:**
- Create: `src/TeampptAddin/Services/CombinationRecommender.cs`
- Test: `src/TeampptAddin.Tests/CombinationRecommenderTest.cs`

**Interfaces:**
- Consumes: `GeminiAiService.GenerateJsonAsync`, `CombinationRecommenderSchema`/`Parser` (Task 6), `CombinationCandidateProvider` 결과(dict)
- Produces:
  - `CombinationRecommender(GeminiAiService gemini)`
  - `Task<CombinationRecommendation> RecommendAsync(DraftUnderstanding u, Dictionary<string,List<HeaderAsset>> candidatesByKind)`
  - `static CombinationRecommendation PickSlideOnly(DraftUnderstanding u, List<HeaderAsset> slideCandidates)` — 순수 헬퍼(테스트 대상): cover/end는 LLM 없이 top1 slide 추천, 후보 0개면 unmet=["slide"]
  - `static string BuildUserText(DraftUnderstanding u, Dictionary<string,List<HeaderAsset>> pool)` — 순수 헬퍼(LLM 입력 문자열, 테스트로 후보 file이 포함되는지 확인)

- [ ] **Step 1: 실패 테스트 작성 (순수 부분만)**

`src/TeampptAddin.Tests/CombinationRecommenderTest.cs`:

```csharp
using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationRecommenderTest
    {
        [Fact]
        public void PickSlideOnly_Returns_Top_Candidate()
        {
            var u = new DraftUnderstanding { SlideKind = "cover", Purpose = "오프닝" };
            var slides = new List<HeaderAsset>
            {
                new HeaderAsset { File = "s1.pptx", Name = "표지A" },
                new HeaderAsset { File = "s2.pptx", Name = "표지B" }
            };
            var r = CombinationRecommender.PickSlideOnly(u, slides);
            Assert.Equal("표지A", r.Slide.Asset.Name);
            Assert.Empty(r.Unmet);
        }

        [Fact]
        public void PickSlideOnly_Empty_Marks_Unmet()
        {
            var u = new DraftUnderstanding { SlideKind = "end", Purpose = "마무리" };
            var r = CombinationRecommender.PickSlideOnly(u, new List<HeaderAsset>());
            Assert.Null(r.Slide);
            Assert.Contains("slide", r.Unmet);
        }

        [Fact]
        public void BuildUserText_Lists_Candidate_Files_By_Kind()
        {
            var u = new DraftUnderstanding
            {
                Purpose = "기능 비교", SlideKind = "body",
                NeededCombination = new NeededCombination { Header = 1, Layout = 1, Component = 2 }
            };
            var pool = new Dictionary<string, List<HeaderAsset>>
            {
                ["header"] = new List<HeaderAsset> { new HeaderAsset { File = "h1.pptx", Name = "헤더1" } },
                ["component"] = new List<HeaderAsset> { new HeaderAsset { File = "c1.pptx", Name = "카드" } }
            };
            var text = CombinationRecommender.BuildUserText(u, pool);
            Assert.Contains("h1.pptx", text);
            Assert.Contains("c1.pptx", text);
            Assert.Contains("기능 비교", text);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

비-UAC 빌드 후 `/TestCaseFilter:"FullyQualifiedName~CombinationRecommenderTest"`. Expected: 컴파일 실패.

- [ ] **Step 3: Recommender 구현**

`src/TeampptAddin/Services/CombinationRecommender.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 후보 풀 위에서 LLM이 조합(header/layout/component)을 1회 호출로 고른다.
    /// cover/end(neededCombination.slide>0)는 LLM 없이 top1 slide를 추천(단축).
    /// 사실(후보 목록)은 벡터, 판단(선택)은 LLM. 텍스트 생성 금지.
    /// </summary>
    public class CombinationRecommender
    {
        private readonly GeminiAiService _gemini;
        public CombinationRecommender(GeminiAiService gemini) { _gemini = gemini; }

        public async Task<CombinationRecommendation> RecommendAsync(
            DraftUnderstanding u, Dictionary<string, List<HeaderAsset>> candidatesByKind)
        {
            // cover/end → 통짜 slide 단축
            if (candidatesByKind.ContainsKey("slide"))
                return PickSlideOnly(u, candidatesByKind["slide"]);

            var json = await _gemini.GenerateJsonAsync(
                CombinationRecommenderSchema.BuildSystemPrompt(),
                BuildUserText(u, candidatesByKind),
                null,
                CombinationRecommenderSchema.BuildResponseSchema()).ConfigureAwait(false);

            var rec = CombinationRecommenderParser.Parse(json, candidatesByKind);
            rec.Purpose = u.Purpose;
            rec.SlideKind = u.SlideKind;
            return rec;
        }

        public static CombinationRecommendation PickSlideOnly(DraftUnderstanding u, List<HeaderAsset> slideCandidates)
        {
            var rec = new CombinationRecommendation { Purpose = u.Purpose, SlideKind = u.SlideKind };
            var top = slideCandidates?.FirstOrDefault();
            if (top == null) { rec.Unmet.Add("slide"); return rec; }
            rec.Slide = new RecommendedSlot { Asset = top, FitNote = "표지 통짜", Confidence = 1.0 };
            return rec;
        }

        public static string BuildUserText(DraftUnderstanding u, Dictionary<string, List<HeaderAsset>> pool)
        {
            var sb = new StringBuilder();
            var nc = u.NeededCombination ?? new NeededCombination();
            sb.AppendLine($"초안 의도(purpose): {u.Purpose}");
            sb.AppendLine($"매칭 의도(matchIntent): {u.MatchIntent}");
            sb.AppendLine($"필요 조합: header {nc.Header}, layout {nc.Layout}, component {nc.Component}");
            sb.AppendLine($"재료 개수: {string.Join(", ", u.Counts.Select(kv => $"{kv.Key}:{kv.Value}"))}");
            foreach (var kind in pool.Keys)
            {
                sb.AppendLine($"\n[{kind} 후보]");
                foreach (var a in pool[kind])
                {
                    var cap = a.Capacity != null ? $" cap={a.Capacity.Min}-{a.Capacity.Max}" : "";
                    var mk = a.MaterialKinds != null && a.MaterialKinds.Count > 0
                        ? " mk=" + string.Join("/", a.MaterialKinds) : "";
                    sb.AppendLine($"- file={a.File} name={a.Name} deck={a.SourceDeck}{cap}{mk} :: {a.UseWhen}");
                }
            }
            return sb.ToString();
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Step 2 명령 재실행. Expected: 3건 PASS.

- [ ] **Step 5: 커밋**

```bash
git add src/TeampptAddin/Services/CombinationRecommender.cs src/TeampptAddin.Tests/CombinationRecommenderTest.cs
git commit -m "feat(reco): LLM 조합 선택 + cover/end slide 단축 recommender"
```

---

## Task 9: RecommendationService — 오케스트레이터 (COM + 네트워크)

**Files:**
- Create: `src/TeampptAddin/Services/RecommendationService.cs`
- (검증) 관리자 빌드 — COM 로드 경로 포함

**Interfaces:**
- Consumes: `DraftSlideReader.ReadCurrentSlide()`, `SlideCaptureService.CaptureCurrentSlide()`, `DraftUnderstandingService` (Task 5), `CombinationCandidateProvider` (Task 7), `CombinationRecommender` (Task 8)
- Produces:
  - `RecommendationService(string supabaseUrl, string anonKey, string geminiKey)`
  - `Task<CombinationRecommendation> RunAsync(Action<string> progress)` — 슬라이드를 건드리지 않고 추천만 반환

> COM(읽기)과 LLM 호출이 섞이므로 UI(STA) 디스패처 스레드에서 호출. `RedesignService` 패턴을 따르되 내부 await에 ConfigureAwait(false)를 쓰지 않는다(COM 연속 실행을 UI 스레드에 유지).

- [ ] **Step 1: 서비스 구현**

`src/TeampptAddin/Services/RecommendationService.cs`:

```csharp
using System;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 에셋 조합 추천 오케스트레이터(추천까지만 — 슬라이드 비파괴).
    /// 읽기(COM 사실) → 이해(LLM) → kind별 벡터 후보 → LLM 조합 선택 → 추천 반환.
    /// 슬라이드를 배치·수정하지 않는다(배치는 다음 스펙).
    /// </summary>
    public class RecommendationService
    {
        private readonly GeminiAiService _gemini;
        private readonly DraftUnderstandingService _understand;
        private readonly CombinationCandidateProvider _candidates;
        private readonly CombinationRecommender _recommender;

        public RecommendationService(string supabaseUrl, string anonKey, string geminiKey)
        {
            _gemini = new GeminiAiService(geminiKey);
            _understand = new DraftUnderstandingService(_gemini);
            _candidates = new CombinationCandidateProvider(
                new EmbeddingService(geminiKey), new SupabaseClient(supabaseUrl, anonKey));
            _recommender = new CombinationRecommender(_gemini);
        }

        public async Task<CombinationRecommendation> RunAsync(Action<string> progress)
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
            return rec;
        }
    }
}
```

- [ ] **Step 2: 관리자 빌드 + 검증**

CLAUDE.md 관리자 MSBuild 명령 실행. 직후:
```bash
stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll   # 1분 이내
tail -5 c:/Projects/teamppt-addin/build.log                                          # 오류 0
```
Expected: 타임스탬프 갱신 + 오류 0건. (UI 미배선이라 동작 검증은 Task 10에서.)

- [ ] **Step 3: 커밋**

```bash
git add src/TeampptAddin/Services/RecommendationService.cs
git commit -m "feat(reco): 조합 추천 오케스트레이터 RecommendationService (비파괴·추천만)"
```

---

## Task 10: AssetPanel 추천 카드 UI + 배선

**Files:**
- Modify: `src/TeampptAddin/UI/Wpf/AssetPanel.cs`
- Modify: `src/TeampptAddin/UI/TaskPaneHost.cs`
- (검증) 관리자 빌드 + PowerPoint 수동 확인

**Interfaces:**
- Consumes: `RecommendationService.RunAsync` (Task 9), `CombinationRecommendation`/`RecommendedSlot` (Task 6)
- Produces: 리디자인 바 클릭 → 추천 실행 → 종류별(헤더/레이아웃/컴포넌트, 또는 통짜 표지) 카드 + 미충족 표시 + 비활성 "이 조합으로 배치 ▶(다음 단계)" 버튼.

> 직전 단일에셋 흐름(`_redesign`/`RunRedesignAsync`/`ShowRedesignCards`/`OnRedesignCardChosen`)은 이번 경로에서 호출하지 않는다. `RedesignService` 코드는 삭제하지 않고 배선만 추천 경로로 바꾼다.

- [ ] **Step 1: 패널에 추천 서비스 필드 추가**

`src/TeampptAddin/UI/Wpf/AssetPanel.cs`의 필드 선언부(`private RedesignService _redesign;` 근처, 약 57행)에 추가:

```csharp
        private RecommendationService _recommend;
```

- [ ] **Step 2: InitAi 시그니처 확장**

`InitAi`(약 2429행)에 파라미터 추가하고 대입:

```csharp
        public void InitAi(IAiService aiService, StyleConfig styles, RemoteAssetCache remoteCache = null, RedesignService redesign = null, RecommendationService recommend = null)
        {
            _aiService = aiService;
            _styleConfig = styles;
            _remoteCache = remoteCache;
            _redesign = redesign;
            _recommend = recommend;
            PopulateStylePanel();
        }
```

- [ ] **Step 3: 리디자인 바 클릭을 추천 경로로 교체**

`RedesignBarClick`(약 584행)의 호출을 교체:

```csharp
        private async void RedesignBarClick(object sender, MouseButtonEventArgs e)
        {
            await RunRecommendationAsync();
        }
```

- [ ] **Step 4: 추천 실행 메서드 추가**

`RunRedesignAsync` 메서드 **뒤**에 새 메서드 추가:

```csharp
        private async Task RunRecommendationAsync()
        {
            if (_redesignRunning) return;
            if (_recommend == null)
            {
                AddAiBubble("추천은 Supabase·Gemini 설정이 있어야 동작해요.");
                return;
            }

            _redesignRunning = true;
            _redesignBar.IsEnabled = false;
            _redesignBar.Opacity = 0.6;
            if (_emptyState != null && _emptyState.Visibility == Visibility.Visible)
                _emptyState.Visibility = Visibility.Collapsed;

            try
            {
                AddAiBubble("현재 초안에 어울리는 에셋 조합을 찾아볼게요.");
                var rec = await _recommend.RunAsync(msg => Dispatcher.Invoke(() => AddAiBubble(msg)));
                ShowRecommendation(rec);
            }
            catch (Exception ex)
            {
                AddAiBubble($"추천 중 오류: {ex.Message}");
                Logger.Log($"[Reco] 실패: {ex}");
            }
            finally
            {
                _redesignRunning = false;
                _redesignBar.IsEnabled = true;
                _redesignBar.Opacity = 1;
                _chatScroll.ScrollToBottom();
            }
        }

        private void ShowRecommendation(CombinationRecommendation rec)
        {
            if (rec == null) { AddAiBubble("추천 결과가 없어요."); return; }

            AddAiBubble(string.IsNullOrEmpty(rec.Purpose)
                ? "추천 조합이에요."
                : $"초안 분석: \"{rec.Purpose}\" ({rec.SlideKind})");

            _chatStack.Children.Add(new TextBlock
            {
                Text = "추천 조합",
                FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub,
                Margin = new Thickness(12, 8, 0, 2)
            });

            if (rec.Slide != null)
                _chatStack.Children.Add(BuildRecoCard("표지", rec.Slide));
            if (rec.Header != null)
                _chatStack.Children.Add(BuildRecoCard("헤더", rec.Header));
            if (rec.Layout != null)
                _chatStack.Children.Add(BuildRecoCard("레이아웃", rec.Layout));
            for (int i = 0; i < rec.Components.Count; i++)
                _chatStack.Children.Add(BuildRecoCard($"컴포넌트 {i + 1}", rec.Components[i]));

            if (rec.Unmet != null && rec.Unmet.Count > 0)
                _chatStack.Children.Add(new TextBlock
                {
                    Text = $"미충족: {string.Join(", ", rec.Unmet)} (적합한 에셋 없음)",
                    FontSize = 10, Foreground = ThemeResources.TextSub,
                    Margin = new Thickness(14, 4, 12, 2)
                });

            _chatStack.Children.Add(BuildPlaceArrangeButton());
            _chatScroll.ScrollToBottom();
        }

        private Border BuildRecoCard(string slotLabel, RecommendedSlot slot)
        {
            var label = new TextBlock
            {
                Text = slotLabel,
                FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextSub,
                Width = 64, VerticalAlignment = VerticalAlignment.Center
            };
            var name = new TextBlock
            {
                Text = slot.Asset?.Name ?? slot.Asset?.File ?? "에셋",
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = ThemeResources.TextMain, FontFamily = ThemeResources.FontBase,
                TextWrapping = TextWrapping.Wrap
            };
            var note = new TextBlock
            {
                Text = $"{slot.FitNote}  ·  {(int)(slot.Confidence * 100)}%",
                FontSize = 10, Foreground = ThemeResources.TextSub,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var textCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textCol.Children.Add(name);
            textCol.Children.Add(note);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(label);
            row.Children.Add(textCol);

            return new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 4, 12, 4),
                Padding = new Thickness(10, 8, 10, 8),
                Child = row
            };
        }

        private Border BuildPlaceArrangeButton()
        {
            return new Border
            {
                Background = ThemeResources.BgChip,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 8, 12, 8),
                Padding = new Thickness(12, 8, 12, 8),
                Opacity = 0.5,
                Cursor = Cursors.Arrow,
                Child = new TextBlock
                {
                    Text = "이 조합으로 배치 ▶ (다음 단계)",
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeResources.TextSub, FontFamily = ThemeResources.FontBase,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
        }
```

- [ ] **Step 5: TaskPaneHost 배선**

`src/TeampptAddin/UI/TaskPaneHost.cs`의 Supabase 설정 분기(약 196–198행)에서 `redesign = new RedesignService(...)` 뒤에 추가:

```csharp
                redesign = new RedesignService(supaUrl, supaAnon, gemini);
                recommend = new RecommendationService(supaUrl, supaAnon, gemini);
```

그 위 변수 선언부에 `RedesignService redesign = null;`과 같은 스코프에 `RecommendationService recommend = null;`을 추가하고, `InitAi` 호출(약 208행)을 교체:

```csharp
            _wpfPanel.InitAi(ai, styles, _remoteCache, redesign, recommend);
```

(약 191–198행을 Read로 확인해 `redesign` 선언 위치에 맞춰 `recommend` 선언을 둔다.)

- [ ] **Step 6: 관리자 빌드 + 검증**

CLAUDE.md 관리자 MSBuild 실행 후:
```bash
stat -c '%y' c:/Projects/teamppt-addin/src/TeampptAddin/bin/Debug/TeampptAddin.dll
tail -5 c:/Projects/teamppt-addin/build.log
```
Expected: 타임스탬프 갱신(1분 이내) + 오류 0건.

- [ ] **Step 7: 수동 동작 확인**

PowerPoint에서 본문 초안 슬라이드 선택 → AI 탭 → "✨ AI 리디자인" 클릭. 기대:
- 진행 버블(초안 읽는 중→이해→후보→조합) 표시.
- "추천 조합" 아래 헤더/레이아웃/컴포넌트 카드가 종류별로 분리되어 표시.
- cover/end 슬라이드에서는 "표지" 카드 1장.
- 슬라이드 자체는 변경되지 않음(추천만).
- "이 조합으로 배치 ▶ (다음 단계)" 버튼은 비활성(흐림).

- [ ] **Step 8: 커밋**

```bash
git add src/TeampptAddin/UI/Wpf/AssetPanel.cs src/TeampptAddin/UI/TaskPaneHost.cs
git commit -m "feat(ui): 리디자인 바를 조합 추천 표시로 교체 (종류별 카드+미충족+배치 플레이스홀더)"
```

---

## Task 11: PROGRESS-BOARD 갱신 + 다음 스펙 예고

**Files:**
- Modify: `PROGRESS-BOARD.md`

- [ ] **Step 1: 보드의 잎 테이블 갱신**

`🍃 잎` 테이블에서 완료된 항목(스키마·재인제스트·이해확장·추천엔진·UI)을 ✅로, "검증 후 다음 스펙(배치 = 빈 템플릿)"을 🔵 다음으로 교체. CLAUDE.md 규칙대로 끝난 세부 잎만 갈아끼우고 상위 골격(숲·나무)은 유지.

- [ ] **Step 2: 커밋**

```bash
git add PROGRESS-BOARD.md
git commit -m "docs: 에셋 조합 추천 1단계(추천) 구현 완료 — 보드 갱신"
```

---

## Self-Review (작성자 체크리스트 결과)

**Spec coverage:**
- 4분류 taxonomy(slide/header/layout/component) → Task 1 ✓
- 신규 필드 capacity/material_kinds/pairing_key → Task 1·2 (pairing_key = source_deck 재사용으로 구현, 별도 컬럼/LLM 필드 없음 ✓)
- 번들 재인제스트 → Task 4(수동) ✓
- 초안 이해 purpose·neededCombination → Task 5 ✓
- 추천 엔진 Ⓑ: 분기→kind별 벡터 후보→LLM 조합 선택 → Task 7(후보)·8(조합)·6(파서) ✓
- cover/end 통짜 slide 단축 → Task 8 `PickSlideOnly` ✓
- match_assets kind 필터 + Supabase 실패 시 RecommendationCache 폴백 → Task 3·7 ✓
- 미충족 슬롯 표시·욱여넣기 금지 → Task 6 파서 unmet + Task 8 프롬프트 ✓
- UI 종류별 카드 + 배치 버튼 비활성 → Task 10 ✓
- 비파괴(슬라이드 안 건드림), RedesignApplier 등 미호출 → Task 9·10 (배선만 교체) ✓

**Placeholder scan:** 모든 코드 스텝에 실제 코드 포함. "적절한 에러처리" 류 없음. ✓

**Type consistency:** `CombinationRecommendation`/`RecommendedSlot`/`NeededCombination`/`AssetCapacity` 명칭이 Task 1·5·6·8·10에서 일관. `MatchQuery.BuildArgs(float[],int,string)`·`GetCandidatesAsync`·`PickSlideOnly`·`BuildUserText` 시그니처가 호출부와 일치. `HeaderAsset.SourceDeck`(=pairing key)·`Capacity`·`MaterialKinds` 일관. ✓

**참고(범위 밖, 다음 스펙):** 추천 조합 배치(영역 합성), 조립 완성도, 재료 이식, slide 통짜 삽입 정교화 — 이번 플랜에서 제외.
