using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 초안 이해의 neededCombination을 보고, 필요한 각 kind마다 match_assets(kind 필터)로
    /// 후보 풀을 만든다. Supabase 실패 시 RecommendationCache 폴백 후 클라이언트에서 kind 분류.
    /// </summary>
    public class CombinationCandidateProvider
    {
        private readonly EmbeddingService _embed;
        private readonly SupabaseClient _supa;
        private readonly RemoteAssetCache _thumbs;
        private readonly RecommendationCache _cache = new RecommendationCache();
        public List<string> LastRetrieveLines { get; } = new List<string>();

        public CombinationCandidateProvider(EmbeddingService embed, SupabaseClient supa, RemoteAssetCache thumbs)
        { _embed = embed; _supa = supa; _thumbs = thumbs; }

        public static List<string> NeededKinds(NeededCombination nc)
        {
            if (nc != null && nc.Slide > 0) return new List<string> { "slide" };
            var kinds = new List<string>();
            if (nc == null) return kinds;
            if (nc.Header > 0) kinds.Add("header");
            if (nc.Layout > 0) kinds.Add("layout");
            if (nc.Component > 0) kinds.Add("component");
            return kinds;
        }

        public static Dictionary<string, List<HeaderAsset>> GroupByKind(
            IEnumerable<HeaderAsset> assets, IEnumerable<string> kinds)
        {
            var list = assets?.ToList() ?? new List<HeaderAsset>();
            var result = new Dictionary<string, List<HeaderAsset>>();
            foreach (var k in kinds)
                result[k] = list.Where(a => string.Equals(a.Kind, k, StringComparison.OrdinalIgnoreCase)).ToList();
            return result;
        }

        public async Task<Dictionary<string, List<HeaderAsset>>> GetCandidatesAsync(DraftUnderstanding u, int topK = 5)
        {
            var kinds = NeededKinds(u.NeededCombination);
            var countsText = string.Join(", ", u.Counts.Select(kv => $"{kv.Key}:{kv.Value}"));
            var query = $"{u.Purpose} | {u.MatchIntent} ({countsText})";

            try
            {
                LastRetrieveLines.Clear();
                var vector = await _embed.EmbedAsync(query).ConfigureAwait(false);
                var result = new Dictionary<string, List<HeaderAsset>>();
                var all = new List<HeaderAsset>();
                foreach (var kind in kinds)
                {
                    var rpcJson = await _supa.RpcAsync("match_assets",
                        MatchQuery.BuildArgs(vector, topK, kind)).ConfigureAwait(false);
                    var list = MatchQuery.ParseResults(rpcJson);
                    result[kind] = list;
                    all.AddRange(list);
                    Logger.Log($"[Combo] kind={kind} 후보 {list.Count}개");
                    var sims = list
                        .Where(a => a.Extra != null && a.Extra.ContainsKey("similarity"))
                        .Select(a => (double)a.Extra["similarity"])
                        .ToList();
                    var simText = sims.Count > 0 ? $" (유사도 {sims.Max():F2}~{sims.Min():F2})" : "";
                    LastRetrieveLines.Add($"{kind} {list.Count}개{simText}");
                    foreach (var a in list)
                    {
                        if (a.Extra != null && a.Extra.TryGetValue("remote_thumb", out var rt) && _thumbs != null)
                        {
                            try { a.Extra["local_thumb"] = await _thumbs.GetThumbAsync(rt.ToString()).ConfigureAwait(false); }
                            catch (Exception tex) { Logger.Log($"[Combo] 썸네일 실패 {a.File}: {tex.Message}"); }
                        }
                    }
                }
                if (all.Count > 0) _cache.Save(all);
                return result;
            }
            catch (Exception ex)
            {
                Logger.Log($"[Combo] Supabase 실패 → 캐시 폴백: {ex.Message}");
                return GroupByKind(_cache.Load(), kinds);
            }
        }
    }
}
