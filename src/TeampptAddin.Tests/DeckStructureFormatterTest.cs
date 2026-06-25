using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckStructureFormatterTest
    {
        [Fact]
        public void Groups_Cover_Body_End_With_Total()
        {
            var d = new DeckStructure
            {
                TotalCount = 5,
                Slides = new List<DeckSlideStructure>
                {
                    new DeckSlideStructure { Index = 1, Kind = "cover", Label = "표지" },
                    new DeckSlideStructure { Index = 2, Kind = "toc", Label = "목차" },
                    new DeckSlideStructure { Index = 3, Kind = "body", Label = "회사소개" },
                    new DeckSlideStructure { Index = 4, Kind = "body", Label = "장점 3단" },
                    new DeckSlideStructure { Index = 5, Kind = "end", Label = "마무리" }
                }
            };
            var lines = DeckStructureFormatter.ToSummaryLines(d);
            Assert.Equal("1. 표지", lines[0]);
            Assert.Equal("2. 본문 (3장)", lines[1]);
            Assert.Equal("   - 목차", lines[2]);
            Assert.Equal("   - 회사소개", lines[3]);
            Assert.Equal("   - 장점 3단", lines[4]);
            Assert.Equal("3. 엔드", lines[5]);
            Assert.Equal("총 슬라이드 → 5장", lines[6]);
        }
    }
}
