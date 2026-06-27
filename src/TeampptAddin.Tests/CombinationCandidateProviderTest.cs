using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationCandidateProviderTest
    {
        [Fact]
        public void NeededKinds_Cover_Returns_Slide_Only()
        {
            var nc = new NeededCombination { Slide = 1 };
            Assert.Equal(new[] { "slide" }, CombinationCandidateProvider.NeededKinds(nc).ToArray());
        }

        [Fact]
        public void NeededKinds_Body_Returns_Header_Layout_Component()
        {
            var nc = new NeededCombination { Header = 1, Layout = 1, Component = 3 };
            var kinds = CombinationCandidateProvider.NeededKinds(nc);
            Assert.Contains("header", kinds);
            Assert.Contains("layout", kinds);
            Assert.Contains("component", kinds);
            Assert.DoesNotContain("slide", kinds);
        }

        [Fact]
        public void GroupByKind_Splits_By_Asset_Kind()
        {
            var assets = new List<HeaderAsset>
            {
                new HeaderAsset { File = "h.pptx", Kind = "header" },
                new HeaderAsset { File = "c1.pptx", Kind = "component" },
                new HeaderAsset { File = "c2.pptx", Kind = "component" }
            };
            var g = CombinationCandidateProvider.GroupByKind(assets, new[] { "header", "component" });
            Assert.Single(g["header"]);
            Assert.Equal(2, g["component"].Count);
        }
    }
}
