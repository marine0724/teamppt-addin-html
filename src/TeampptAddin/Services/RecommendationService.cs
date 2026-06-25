using System;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 에셋 조합 추천 오케스트레이터(추천까지만 — 슬라이드 비파괴).
    /// 읽기(COM 사실) → 이해(LLM) → kind별 벡터 후보 → LLM 조합 선택 → 추천 반환.
    /// 슬라이드를 배치·수정하지 않는다(배치는 다음 스펙).
    /// </summary>
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

        public async Task<CombinationRecommendation> RunAsync(Action<string> progress)
        {
            progress("초안 읽는 중…");
            var profile = DraftSlideReader.ReadCurrentSlide();
            if (profile == null) throw new InvalidOperationException("활성 슬라이드를 찾을 수 없습니다.");

            var png = SlideCaptureService.CaptureCurrentSlide()?.PngPath;

            progress("초안 이해하는 중…");
            var u = await _understand.UnderstandAsync(profile, png);

            progress("어울리는 에셋 후보 찾는 중…");
            var pool = await _candidates.GetCandidatesAsync(u);

            progress("조합 고르는 중…");
            var rec = await _recommender.RecommendAsync(u, pool);
            return rec;
        }
    }
}
