using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeampptAddin
{
    public class DesignCritiqueService
    {
        private readonly GeminiAiService _gemini;
        public DesignCritiqueService(GeminiAiService gemini) { _gemini = gemini; }

        public static string BuildUserText(
            DraftUnderstanding u, CombinationRecommendation rec, List<string> retrieveLines, MaterialFitResult materialFit)
        {
            return
                $"초안 의도(purpose): {u?.Purpose}\n" +
                $"초안 이해 요약: {u?.Reasoning}\n" +
                $"검색 유사도: {string.Join(" / ", retrieveLines ?? new List<string>())}\n" +
                $"재료적합 점수(계산값): {materialFit?.Score ?? 0} ({materialFit?.Note})\n" +
                $"적용된 조합: header={rec?.Header?.Asset?.Name}, layout={rec?.Layout?.Asset?.Name}, " +
                $"components={rec?.Components?.Count ?? 0}, 미충족={string.Join("/", rec?.Unmet ?? new List<string>())}\n" +
                "첫 이미지=배치 결과 슬라이드, 둘째 이미지=초안. 결과를 채점하라.";
        }

        public async Task<DesignCritique> CritiqueAsync(
            string resultPngPath, string draftPngPath, DraftUnderstanding u,
            CombinationRecommendation rec, List<string> retrieveLines, MaterialFitResult materialFit)
        {
            var userText = BuildUserText(u, rec, retrieveLines, materialFit);

            var imgs = new List<string>();
            if (!string.IsNullOrEmpty(resultPngPath)) imgs.Add(resultPngPath);
            if (!string.IsNullOrEmpty(draftPngPath)) imgs.Add(draftPngPath);

            var json = await _gemini.GenerateJsonAsync(
                DesignCritiqueSchema.BuildSystemPrompt(), userText, imgs,
                DesignCritiqueSchema.BuildResponseSchema(), thinkingBudget: 2048).ConfigureAwait(false);
            Logger.Log("[Critique] raw↓ " + json);
            var c = DesignCritiqueParser.Parse(json);
            c.MaterialFit = materialFit?.Score ?? 0;
            return c;
        }
    }
}
