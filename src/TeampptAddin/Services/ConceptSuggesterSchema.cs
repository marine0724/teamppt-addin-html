using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class ConceptSuggesterSchema
    {
        public static JObject BuildResponseSchema()
        {
            var roleColor = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["role"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "main", "accent", "text", "bg" } },
                    ["value"] = new JObject { ["type"] = "string" }   // #RRGGBB
                },
                ["required"] = new JArray { "role", "value" }
            };
            var roleFont = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["role"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "heading", "body" } },
                    ["family"] = new JObject { ["type"] = "string" }
                },
                ["required"] = new JArray { "role", "family" }
            };
            var concept = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["name"] = new JObject { ["type"] = "string" },
                    ["styleTags"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" } },
                    ["colors"] = new JObject { ["type"] = "array", ["items"] = roleColor },
                    ["fonts"] = new JObject { ["type"] = "array", ["items"] = roleFont }
                },
                ["required"] = new JArray { "name", "styleTags", "colors", "fonts" }
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["concepts"] = new JObject { ["type"] = "array", ["items"] = concept } },
                ["required"] = new JArray { "concepts" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 덱 '디자인 컨셉'을 제안하는 컨설턴트야. 덱 구조 요약 + 사용 용도 + 원하는 느낌을 받아, 서로 뚜렷이 구별되는 디자인 방향 3개를 제안해.
## 절대 제약
- 디자인(색·폰트·무드)만 제안한다. 내용을 새로 기획하거나 텍스트를 바꾸지 마라.
## 출력 규칙
- 정확히 3개. 셋은 같은 용도·느낌을 만족하되 팔레트·타이포 personality가 서로 달라야 한다(예: 진중 블루 / 모던 미니멀 / 웜 톤).
- name: 컨셉 이름 2~8자 한국어.
- styleTags: 영문 소문자 키워드 2~4개(나중 검색 가중용. 예: trust, corporate, minimal, warm, bold).
- colors: 역할별 HEX. role은 main/accent/text/bg 중에서. 최소 main·text 포함. value는 #RRGGBB.
- fonts: 역할별 글꼴. role은 heading/body. 최소 heading 포함. 한글 지원 글꼴 위주(예: Pretendard, Noto Sans KR, Gowun Dodum).
- 가독성 유지(text는 어둡게, bg는 밝게).";
        }
    }
}
