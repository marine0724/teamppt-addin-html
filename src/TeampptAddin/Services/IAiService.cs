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

        Task<SlideDiagnosis> DiagnoseSlideAsync(string pngPath);
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

        public Task<SlideDiagnosis> DiagnoseSlideAsync(string pngPath)
        {
            return Task.FromResult(new SlideDiagnosis
            {
                Message = "제목 대비가 약해 한눈에 안 들어와요. 본문 여백이 좌우로 치우쳤고, 색이 3개를 넘어 산만합니다.",
                SuggestedQuestions = new System.Collections.Generic.List<string>
                {
                    "제목을 어떻게 키우면 좋을까?",
                    "색을 몇 개로 줄이면 좋을까?",
                    "여백을 어떻게 맞추지?"
                }
            });
        }
    }
}
