using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeampptAddin
{
    public class RecommendationService
    {
        private readonly GeminiAiService _gemini;
        private readonly DraftUnderstandingService _understand;
        private readonly CombinationCandidateProvider _candidates;
        private readonly CombinationRecommender _recommender;

        public RecommendationService(string supabaseUrl, string anonKey, string geminiKey)
        {
            _gemini = new GeminiAiService(geminiKey);
            _understand = new DraftUnderstandingService(_gemini);
            var supa = new SupabaseClient(supabaseUrl, anonKey);
            _candidates = new CombinationCandidateProvider(
                new EmbeddingService(geminiKey), supa, new RemoteAssetCache(supabaseUrl, anonKey));
            _recommender = new CombinationRecommender(_gemini);
        }

        public async Task<RecommendationResult> RunAsync(Action<string> progress, Action<string> narrate)
        {
            progress("초안 읽는 중…");
            var profile = DraftSlideReader.ReadCurrentSlide();
            if (profile == null) throw new InvalidOperationException("활성 슬라이드를 찾을 수 없습니다.");

            var png = SlideCaptureService.CaptureCurrentSlide()?.PngPath;

            progress("초안 이해하는 중…");
            var u = await _understand.UnderstandAsync(profile, png);
            if (!string.IsNullOrEmpty(u.Reasoning)) narrate(u.Reasoning);

            progress("어울리는 에셋 후보 찾는 중…");
            var pool = await _candidates.GetCandidatesAsync(u);
            if (_candidates.LastRetrieveLines.Count > 0)
                narrate("후보를 추렸어요 — " + string.Join(", ", _candidates.LastRetrieveLines));

            progress("조합 고르는 중…");
            var rec = await _recommender.RecommendAsync(u, pool);
            if (!string.IsNullOrEmpty(rec.Reasoning)) narrate(rec.Reasoning);

            var trace = new RecommendationTrace
            {
                UnderstandReasoning = u.Reasoning,
                RetrieveLines = _candidates.LastRetrieveLines,
                ComposeReasoning = rec.Reasoning,
                Unmet = rec.Unmet
            };
            foreach (var line in trace.ToReadableLines()) Logger.Log("[Trace] " + line);

            return new RecommendationResult
            {
                Recommendation = rec, Trace = trace, DraftPngPath = png, Understanding = u
            };
        }

        public async Task<DesignCritique> CritiqueAsync(string resultPng, RecommendationResult prior)
        {
            var critic = new DesignCritiqueService(_gemini);
            var c = await critic.CritiqueAsync(
                resultPng, prior.DraftPngPath, prior.Understanding, prior.Recommendation, prior.Trace.RetrieveLines);
            prior.Trace.Critique = c;
            foreach (var line in prior.Trace.ToReadableLines()) Logger.Log("[Trace] " + line);
            return c;
        }
    }
}
