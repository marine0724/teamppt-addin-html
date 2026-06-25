using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DesignCritiqueParser
    {
        public static DesignCritique Parse(string json)
        {
            var o = JObject.Parse(json);
            var c = new DesignCritique
            {
                Score = o["score"]?.Value<int>() ?? 0,
                Verdict = o["verdict"]?.ToString() ?? "",
                Bottleneck = o["bottleneck"]?.ToString() ?? "",
                Suggestion = o["suggestion"]?.ToString() ?? "",
                Reasoning = o["reasoning"]?.ToString() ?? ""
            };
            if (o["dimensionScores"] is JObject dim)
                foreach (var p in dim.Properties())
                    c.DimensionScores[p.Name] = p.Value?.Value<int>() ?? 0;
            return c;
        }
    }
}
