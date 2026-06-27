using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>responseSchema(JObject) type을 소문자 표준 JSON Schema로 정규화(프롬프트 임베딩용). 입력 비파괴.</summary>
    public static class GlmSchema
    {
        public static JObject ToExample(JObject schema)
        {
            return (JObject)ConvertNode(schema);
        }

        private static JToken ConvertNode(JToken schemaNode)
        {
            if (!(schemaNode is JObject obj)) return new JValue("<unknown>");

            var type = obj["type"]?.ToString()?.ToLowerInvariant() ?? "object";

            var enumArr = obj["enum"] as JArray;
            if (enumArr != null && enumArr.Count > 0)
                return new JValue("<" + string.Join("|", enumArr.Select(e => e.ToString())) + ">");

            switch (type)
            {
                case "string":
                    var desc = obj["description"]?.ToString();
                    return new JValue(string.IsNullOrEmpty(desc) ? "<string>" : $"<{desc}>");

                case "integer":
                case "number":
                    return new JValue(0);

                case "boolean":
                    return new JValue(false);

                case "array":
                    var items = obj["items"];
                    var arr = new JArray();
                    if (items != null) arr.Add(ConvertNode(items));
                    return arr;

                case "object":
                default:
                    var result = new JObject();
                    var props = obj["properties"] as JObject;
                    if (props != null)
                    {
                        foreach (var prop in props.Properties())
                            result[prop.Name] = ConvertNode(prop.Value);
                    }
                    return result;
            }
        }

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
