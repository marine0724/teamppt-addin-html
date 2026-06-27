using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class SlotMapSchema
    {
        public static JObject BuildResponseSchema()
        {
            var mapping = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["draftShapeId"] = new JObject { ["type"] = "integer" },
                    ["assetShapeId"] = new JObject { ["type"] = "string" },
                    ["fitNote"] = new JObject { ["type"] = "string" },
                    ["confidence"] = new JObject { ["type"] = "number" }
                },
                ["required"] = new JArray { "draftShapeId", "assetShapeId", "confidence" }
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["mappings"] = new JObject { ["type"] = "array", ["items"] = mapping } },
                ["required"] = new JArray { "mappings" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 초안 재료를 디자인 에셋의 빈 자리에 배정하는 엔진이야.
입력: 초안 재료 목록(id, 역할, 타입, 글자수)과 에셋의 실제 도형 목록(id, 종류, 위치, 샘플텍스트).
할 일: 각 초안 재료를 역할·타입·개수가 맞는 에셋 도형에 배정(draftShapeId→assetShapeId).
규칙: 텍스트 내용은 만들지 마라. 적합한 자리가 없으면 배정하지 말고 남겨라(욱여넣기 금지). confidence는 0~1.";
        }
    }
}
