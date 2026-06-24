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
