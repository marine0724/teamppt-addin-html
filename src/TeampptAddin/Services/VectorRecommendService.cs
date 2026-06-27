using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public class VectorRecommendService : IAiService
    {
        private const int TopN = 8;
        private readonly EmbeddingService _embed;
        private readonly SupabaseClient _supa;
        private readonly GeminiAiService _selector;
        private readonly RecommendationCache _cache = new RecommendationCache();

        public VectorRecommendService(string supabaseUrl, string anonKey, string geminiKey)
        {
            _embed = new EmbeddingService(geminiKey);
            _supa = new SupabaseClient(supabaseUrl, anonKey);
            _selector = new GeminiAiService(geminiKey);
        }

        public async Task<AiRecommendation> RecommendAsync(
            string userIntent,
            IEnumerable<HeaderAsset> assets,
            IEnumerable<StylePalette> palettes,
            IEnumerable<StyleFont> fonts)
        {
            List<HeaderAsset> candidates;
            try
            {
                var vector = await _embed.EmbedAsync(userIntent).ConfigureAwait(false);
                var rpcJson = await _supa.RpcAsync("match_assets", MatchQuery.BuildArgs(vector, TopN)).ConfigureAwait(false);
                candidates = MatchQuery.ParseResults(rpcJson);
                if (candidates.Count > 0) _cache.Save(candidates);
                Logger.Log($"[VectorRec] 후보 {candidates.Count}개");
            }
            catch (Exception ex)
            {
                Logger.Log($"[VectorRec] Supabase 실패 → 폴백: {ex.Message}");
                candidates = _cache.Load();
                if (candidates.Count == 0) candidates = (assets ?? Enumerable.Empty<HeaderAsset>()).ToList();
            }

            return await _selector.RecommendAsync(userIntent, candidates, palettes, fonts).ConfigureAwait(false);
        }

        public Task<SlideDiagnosis> DiagnoseSlideAsync(string pngPath)
            => _selector.DiagnoseSlideAsync(pngPath);

        public Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, string pngPathOrNull,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
            => _selector.GenerateJsonAsync(systemPrompt, userText, pngPathOrNull, responseSchema, temperature, thinkingBudget);

        public Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, IEnumerable<string> pngPaths,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
            => _selector.GenerateJsonAsync(systemPrompt, userText, pngPaths, responseSchema, temperature, thinkingBudget);
    }
}
