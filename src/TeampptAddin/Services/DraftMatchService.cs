using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 초안 이해(matchIntent + counts)를 임베딩 → match_assets RPC로 후보 에셋을 찾는다.
    /// VectorRecommendService의 매칭 부분과 동일 패턴. 실패 시 RecommendationCache 폴백.
    /// </summary>
    public class DraftMatchService
    {
        private readonly EmbeddingService _embed;
        private readonly SupabaseClient _supa;
        private readonly RecommendationCache _cache = new RecommendationCache();

        public DraftMatchService(EmbeddingService embed, SupabaseClient supa) { _embed = embed; _supa = supa; }

        public async Task<List<HeaderAsset>> FindCandidatesAsync(DraftUnderstanding u, int topN = 8)
        {
            var countsText = string.Join(", ", u.Counts.Select(kv => $"{kv.Key}:{kv.Value}"));
            var query = $"{u.MatchIntent} ({countsText})";
            try
            {
                var vector = await _embed.EmbedAsync(query).ConfigureAwait(false);
                var rpcJson = await _supa.RpcAsync("match_assets", MatchQuery.BuildArgs(vector, topN)).ConfigureAwait(false);
                var candidates = MatchQuery.ParseResults(rpcJson);
                if (candidates.Count > 0) _cache.Save(candidates);
                Logger.Log($"[DraftMatch] 후보 {candidates.Count}개 — query='{query}'");
                return candidates;
            }
            catch (Exception ex)
            {
                Logger.Log($"[DraftMatch] Supabase 실패 → 캐시 폴백: {ex.Message}");
                return _cache.Load();
            }
        }
    }
}
