using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    public interface IAiService
    {
        Task<AiRecommendation> RecommendAsync(
            string userIntent,
            IEnumerable<HeaderAsset> assets,
            IEnumerable<StylePalette> palettes,
            IEnumerable<StyleFont> fonts);
    }

    public class MockAiService : IAiService
    {
        public Task<AiRecommendation> RecommendAsync(
            string userIntent,
            IEnumerable<HeaderAsset> assets,
            IEnumerable<StylePalette> palettes,
            IEnumerable<StyleFont> fonts)
        {
            var result = new AiRecommendation
            {
                Message = $"'{userIntent}'에 맞는 에셋을 추천해요.",
                Assets = assets.Take(3).Select(a => new AssetSuggestion
                {
                    Asset = a,
                    Reason = a.UseWhen
                }).ToList(),
                Style = new StyleSuggestion
                {
                    Palette = palettes.FirstOrDefault(),
                    Font = fonts.FirstOrDefault(),
                    Reason = "기본 스타일"
                }
            };
            return Task.FromResult(result);
        }
    }
}
