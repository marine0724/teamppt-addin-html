using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class ConceptSuggesterUserTextTest
    {
        [Fact]
        public void BuildUserText_Includes_Usage_Feeling_And_Structure_Label()
        {
            var structure = new DeckStructure
            {
                TotalCount = 3,
                Slides = new List<DeckSlideStructure>
                {
                    new DeckSlideStructure { Index = 1, Kind = "cover", Label = "표지" },
                    new DeckSlideStructure { Index = 2, Kind = "body",  Label = "회사소개" },
                    new DeckSlideStructure { Index = 3, Kind = "end",   Label = "마무리" }
                }
            };
            var t = ConceptSuggester.BuildUserText(structure, "투자유치", "신뢰감");
            Assert.Contains("투자유치", t);
            Assert.Contains("신뢰감", t);
            Assert.Contains("회사소개", t);   // 구조 라벨이 입력으로 흘러들어감(DeckStructureFormatter 경유)
        }
    }
}
