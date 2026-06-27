using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>responseSchema(JObject) type을 소문자 표준 JSON Schema로 정규화(프롬프트 임베딩용). 입력 비파괴.</summary>
    public static class GlmSchema
    {
        public static JObject Normalize(JObject schema)
        {
            var clone = (JObject)schema.DeepClone();
            Walk(clone);
            return clone;
        }

        private static void Walk(JToken node)
        {
            if (node is JObject obj)
            {
                var t = obj["type"];
                if (t != null && t.Type == JTokenType.String)
                    obj["type"] = t.ToString().ToLowerInvariant();
                foreach (var prop in obj.Properties())
                    Walk(prop.Value);
            }
            else if (node is JArray arr)
            {
                foreach (var item in arr)
                    Walk(item);
            }
        }
    }
}
