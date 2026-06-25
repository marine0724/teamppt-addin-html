using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class RecommendationTraceTest
    {
        [Fact]
        public void ToReadableLines_Includes_Each_Stage()
        {
            var t = new RecommendationTrace
            {
                UnderstandReasoning = "본문, 비전 설명형",
                RetrieveLines = new List<string> { "header 5(sim 0.70~0.72)" },
                ComposeReasoning = "layout 선택, component 미충족",
                Unmet = new List<string> { "component" },
                Critique = new DesignCritique
                {
                    Score = 78, Verdict = "여백 답답", Bottleneck = "에셋",
                    Suggestion = "차트 외 컴포넌트 확보", Reasoning = "위계는 좋음"
                }
            };

            var text = string.Join("\n", t.ToReadableLines());

            Assert.Contains("본문, 비전 설명형", text);
            Assert.Contains("header 5(sim 0.70~0.72)", text);
            Assert.Contains("component", text);
            Assert.Contains("78", text);
            Assert.Contains("에셋", text);
        }

        [Fact]
        public void ToReadableLines_Omits_Critique_When_Null()
        {
            var t = new RecommendationTrace { UnderstandReasoning = "x", ComposeReasoning = "y" };
            var lines = t.ToReadableLines();
            Assert.DoesNotContain(lines, l => l.Contains("검수"));
        }
    }
}
