using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class MatchQuery
    {
        public static JObject BuildArgs(float[] queryEmbedding, int matchCount)
            => BuildArgs(queryEmbedding, matchCount, null);

        public static JObject BuildArgs(float[] queryEmbedding, int matchCount, string filterKind)
        {
            var vec = "[" + string.Join(",", queryEmbedding.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";
            var o = new JObject { ["query_embedding"] = vec, ["match_count"] = matchCount };
            if (!string.IsNullOrEmpty(filterKind)) o["filter_kind"] = filterKind;
            return o;
        }

        public static List<HeaderAsset> ParseResults(string rpcJson)
        {
            var arr = JArray.Parse(rpcJson);
            var result = new List<HeaderAsset>();
            foreach (var row in arr.OfType<JObject>())
            {
                var sim = row["similarity"]?.Value<double>() ?? 0;
                Logger.Log($"[Match] {row["name"]} sim={sim:F3}");
                var asset = SupabaseAssetMapper.Map(row);
                if (asset.Extra == null) asset.Extra = new Dictionary<string, JToken>();
                asset.Extra["similarity"] = sim;
                result.Add(asset);
            }
            return result;
        }
    }
}
