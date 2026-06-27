using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>LLM JSON → DesignConcept 리스트(순수). 빈 name 컨셉은 드롭, 생존 순서로 id(c1,c2,...) 부여.</summary>
    public static class ConceptSuggesterParser
    {
        public static List<DesignConcept> Parse(string json)
        {
            var result = new List<DesignConcept>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            var o = JObject.Parse(json);
            int n = 1;
            foreach (var c in (o["concepts"] as JArray) ?? new JArray())
            {
                var name = c["name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;   // 환각/빈 컨셉 드롭

                var concept = new DesignConcept
                {
                    Id = "c" + n,
                    Name = name.Trim(),
                    StyleTags = new List<string>(),
                    Colors = new Dictionary<string, string>(),
                    Fonts = new Dictionary<string, string>()
                };

                foreach (var t in (c["styleTags"] as JArray) ?? new JArray())
                {
                    var tag = t?.ToString();
                    if (!string.IsNullOrWhiteSpace(tag)) concept.StyleTags.Add(tag.Trim());
                }
                foreach (var col in (c["colors"] as JArray) ?? new JArray())
                {
                    var role = col["role"]?.ToString();
                    var val = col["value"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(val)
                        && !concept.Colors.ContainsKey(role)) concept.Colors[role] = val.Trim();
                }
                foreach (var fo in (c["fonts"] as JArray) ?? new JArray())
                {
                    var role = fo["role"]?.ToString();
                    var fam = fo["family"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(role) && !string.IsNullOrWhiteSpace(fam)
                        && !concept.Fonts.ContainsKey(role)) concept.Fonts[role] = fam.Trim();
                }

                result.Add(concept);
                n++;
            }
            return result;
        }
    }
}
