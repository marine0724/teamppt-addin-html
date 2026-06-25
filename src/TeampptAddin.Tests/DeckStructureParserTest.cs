using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckStructureParserTest
    {
        [Fact]
        public void Parses_Slides_Drops_Hallucinated_Index_Sets_Total()
        {
            const string json = @"{
              ""slides"": [
                {""index"":1,""kind"":""cover"",""label"":""표지""},
                {""index"":2,""kind"":""toc"",""label"":""목차""},
                {""index"":3,""kind"":""body"",""label"":""회사소개""},
                {""index"":99,""kind"":""body"",""label"":""환각""}
              ]
            }";
            var d = DeckStructureParser.Parse(json, 3);
            Assert.Equal(3, d.TotalCount);
            Assert.Equal(3, d.Slides.Count);              // index 99 dropped (>slideCount)
            Assert.Equal("목차", d.Slides.Single(s => s.Index == 2).Label);
            Assert.Equal("cover", d.Slides.Single(s => s.Index == 1).Kind);
        }

        [Fact]
        public void BuildUserText_Lists_Each_Slide()
        {
            var slides = new System.Collections.Generic.List<DraftProfile>
            {
                new DraftProfile { SlideIndex = 1, Shapes = { new DraftShape { Kind = "text", Text = "회사 소개" } } },
                new DraftProfile { SlideIndex = 2, Shapes = { new DraftShape { Kind = "image" } } }
            };
            var t = DeckStructureService.BuildUserText(slides);
            Assert.Contains("슬라이드 1:", t);
            Assert.Contains("회사 소개", t);
            Assert.Contains("슬라이드 2:", t);
        }
    }
}
