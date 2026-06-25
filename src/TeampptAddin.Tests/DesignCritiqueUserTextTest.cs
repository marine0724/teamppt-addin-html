using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DesignCritiqueUserTextTest
    {
        [Fact]
        public void UserText_Includes_MaterialFit_Score()
        {
            var u = new DraftUnderstanding { Purpose = "3개 기능 비교", Reasoning = "본문 비교형입니다" };
            var rec = new CombinationRecommendation();
            var mf = new MaterialFitResult { Score = 81, SimilarityAvg = 0.79, Note = "유사도 0.79, 용량 맞음" };
            var text = DesignCritiqueService.BuildUserText(u, rec, new List<string> { "header 5개 (유사도 0.82~0.71)" }, mf);
            Assert.Contains("재료적합", text);
            Assert.Contains("81", text);
        }
    }
}
