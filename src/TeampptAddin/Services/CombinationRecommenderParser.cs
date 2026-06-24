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
