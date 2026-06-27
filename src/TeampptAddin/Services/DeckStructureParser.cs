using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DeckStructureParser
    {
        public static DeckStructure Parse(string json, int slideCount)
        {
            var d = new DeckStructure { TotalCount = slideCount };
            var o = JObject.Parse(json);
            foreach (var s in (o["slides"] as JArray) ?? new JArray())
            {
                int idx = s["index"]?.Value<int>() ?? -1;
                if (idx < 1 || idx > slideCount) continue;   // 환각 인덱스 제거
                d.Slides.Add(new DeckSlideStructure
                {
                    Index = idx,
                    Kind = s["kind"]?.ToString() ?? "body",
                    Label = s["label"]?.ToString() ?? ""
                });
            }
            return d;
        }
    }
}
