using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationRecommenderTest
    {
        [Fact]
        public void PickSlideOnly_Returns_Top_Candidate()
        {
            var u = new DraftUnderstanding { SlideKind = "cover", Purpose = "오프닝" };
            var slides = new List<HeaderAsset>
            {
                new HeaderAsset { File = "s1.pptx", Name = "표지A" },
                new HeaderAsset { File = "s2.pptx", Name = "표지B" }
            };
            var r = CombinationRecommender.PickSlideOnly(u, slides);
            Assert.Equal("표지A", r.Slide.Asset.Name);
            Assert.Empty(r.Unmet);
        }

        [Fact]
        public void PickSlideOnly_Empty_Marks_Unmet()
        {
            var u = new DraftUnderstanding { SlideKind = "end", Purpose = "마무리" };
            var r = CombinationRecommender.PickSlideOnly(u, new List<HeaderAsset>());
            Assert.Null(r.Slide);
            Assert.Contains("slide", r.Unmet);
        }

        [Fact]
        public void BuildUserText_Lists_Candidate_Files_By_Kind()
        {
            var u = new DraftUnderstanding
            {
                Purpose = "기능 비교", SlideKind = "body",
                NeededCombination = new NeededCombination { Header = 1, Layout = 1, Component = 2 }
            };
            var pool = new Dictionary<string, List<HeaderAsset>>
            {
                ["header"] = new List<HeaderAsset> { new HeaderAsset { File = "h1.pptx", Name = "헤더1" } },
                ["component"] = new List<HeaderAsset> { new HeaderAsset { File = "c1.pptx", Name = "카드" } }
            };
            var text = CombinationRecommender.BuildUserText(u, pool);
            Assert.Contains("h1.pptx", text);
            Assert.Contains("c1.pptx", text);
            Assert.Contains("기능 비교", text);
        }
    }
}
