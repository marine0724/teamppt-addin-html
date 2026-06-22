using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class MatchQuery
    {
        public static JObject BuildArgs(float[] queryEmbedding, int matchCount)
        {
            var vec = "[" + string.Join(",", queryEmbedding.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";
            return new JObject { ["query_embedding"] = vec, ["match_count"] = matchCount };
        }

        public static List<HeaderAsset> ParseResults(string rpcJson)
        {
            var arr = JArray.Parse(rpcJson);
            var result = new List<HeaderAsset>();
            foreach (var row in arr.OfType<JObject>())
            {
                var sim = row["similarity"]?.Value<double>() ?? 0;
                Logger.Log($"[Match] {row["name"]} sim={sim:F3}");
                result.Add(SupabaseAssetMapper.Map(row));
            }
            return result;
        }
    }
}
