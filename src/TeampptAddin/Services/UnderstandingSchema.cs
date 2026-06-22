using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class UnderstandingSchema
    {
        public static JObject BuildResponseSchema()
        {
            JObject StrArray() => new JObject
            {
                ["type"] = "array",
                ["items"] = new JObject { ["type"] = "string" }
            };

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["name"] = new JObject { ["type"] = "string" },
                    ["kind"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray { "layout", "component" }
                    },
                    ["use_when"] = new JObject { ["type"] = "string" },
                    ["content_fit"] = StrArray(),
                    ["tags"] = StrArray(),
                    ["example_intents"] = StrArray(),
                    ["slots"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject
                            {
                                ["name"] = new JObject { ["type"] = "string" },
                                ["type"] = new JObject
                                {
                                    ["type"] = "string",
                                    ["enum"] = new JArray { "text", "image", "chart", "table" }
                                },
                                ["perSlide"] = new JObject { ["type"] = "boolean" }
                            },
                            ["required"] = new JArray { "name", "type", "perSlide" }
                        }
                    },
                    ["colors"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject
                            {
                                ["role"] = new JObject { ["type"] = "string" },
                                ["value"] = new JObject { ["type"] = "string" },
                                ["locked"] = new JObject { ["type"] = "boolean" }
                            },
                            ["required"] = new JArray { "role", "value", "locked" }
                        }
                    },
                    ["fonts"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject
                            {
                                ["role"] = new JObject { ["type"] = "string" },
                                ["family"] = new JObject { ["type"] = "string" },
                                ["weight"] = new JObject { ["type"] = "string" }
                            },
                            ["required"] = new JArray { "role", "family" }
                        }
                    }
                },
                ["required"] = new JArray
                {
                    "name", "kind", "use_when", "content_fit",
                    "tags", "example_intents", "slots", "colors", "fonts"
                }
            };
        }

        public static string BuildSystemPrompt(string category)
        {
            return $@"너는 PPT 에셋을 분석하는 디자인 인제스트 엔진이야. 슬라이드 이미지 1장과 섹션명 힌트를 보고, 이 에셋을 미래에 검색·재사용하기 위한 구조화 메타데이터를 생성해.

## 섹션명(카테고리 힌트)
""{category}""

## 판단 규칙
- kind: 슬라이드 페이지 전체 틀이면 ""layout"", 틀 위에 얹는 부품(그래프/표/다이어그램 등)이면 ""component"".
- name: 이 에셋을 한눈에 구분할 짧은 한국어 이름 (예: ""우측정렬 연도강조 표지"").
- use_when: 어떤 상황에서 쓰면 좋은지 한 문장.
- content_fit: 들어가기 좋은 콘텐츠 종류 2~4개.
- tags: 검색용 키워드 3~8개.
- example_intents: 사용자가 이 에셋을 찾을 때 칠 법한 자연어 의도 문장 3~5개 (예: ""투자 유치 IR 표지"", ""회사 소개 첫 장""). 검색 임베딩 품질의 핵심이니 다양하게.
- slots: 사용자 글/이미지가 들어갈 이름 붙은 빈자리. 위치·크기·폰트 단서로 title/subtitle/body/image1 등 추론. type은 text|image|chart|table. 슬라이드마다 내용이 바뀌면 perSlide=true.
- colors: 핵심 색을 역할(main/sub1/sub2/text/accent 등)+hex로. 로고/브랜드 고정색은 locked=true.
- fonts: 보이는 폰트를 역할(heading/body 등)+family로.
- 모르면 지어내지 말고 빈 배열/보수적 값으로.";
        }
    }
}
