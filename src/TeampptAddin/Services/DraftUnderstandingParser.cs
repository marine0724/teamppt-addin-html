using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DraftUnderstandingParser
    {
        public static DraftUnderstanding Parse(string llmJson, DraftProfile profile)
        {
            var o = JObject.Parse(llmJson);
            var byId = profile.Shapes.ToDictionary(s => s.Id);

            var materials = new List<DraftMaterial>();
            foreach (var m in (o["materials"] as JArray) ?? new JArray())
            {
                var id = m["sourceShapeId"]?.Value<int>() ?? -1;
                if (!byId.TryGetValue(id, out var shape)) continue;   // 환각 도형 제거
                materials.Add(new DraftMaterial
                {
                    Role = m["role"]?.ToString(),
                    Type = m["type"]?.ToString() ?? shape.Kind,
                    Emphasis = m["emphasis"]?.ToString(),
                    SourceShapeId = id,
                    Text = shape.Text,                 // COM 사실
                    CharCount = shape.CharCount,        // COM 사실
                    BulletCount = shape.BulletCount,    // COM 사실
                    Level = shape.MaxLevel
                });
            }

            var counts = new Dictionary<string, int>();
            if (o["counts"] is JObject c)
                foreach (var p in c.Properties())
                    counts[p.Name] = p.Value.Value<int>();

            return new DraftUnderstanding
            {
                Materials = materials,
                Counts = counts,
                LayoutShape = o["layoutShape"]?.ToString() ?? "",
                DesignSummary = o["designSummary"]?.ToString() ?? "",
                DominantColors = (o["dominantColors"] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>(),
                MatchIntent = o["matchIntent"]?.ToString() ?? "",
                SlideKind = o["slideKind"]?.ToString() ?? "",
                Purpose = o["purpose"]?.ToString() ?? "",
                Reasoning = o["reasoning"]?.ToString() ?? "",
                NeededCombination = o["neededCombination"] is JObject nc
                    ? new NeededCombination
                    {
                        Slide = nc["slide"]?.Value<int>() ?? 0,
                        Header = nc["header"]?.Value<int>() ?? 0,
                        Layout = nc["layout"]?.Value<int>() ?? 0,
                        Component = nc["component"]?.Value<int>() ?? 0
                    }
                    : new NeededCombination()
            };
        }
    }
}
