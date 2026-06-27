// src/TeampptAddin.Tests/ConceptFitScorerTest.cs
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class ConceptFitScorerTest
    {
        private static DesignConcept Concept() => new DesignConcept
        {
            StyleTags = new List<string> { "minimal", "trust" },
            Colors = new Dictionary<string, string> { ["main"] = "#111", ["text"] = "#222" },
            Fonts = new Dictionary<string, string> { ["heading"] = "Noto" }
        };

        private static HeaderAsset Asset(List<string> tags, List<string> colorRoles, List<string> fontRoles)
            => new HeaderAsset
            {
                Tags = tags,
                Colors = colorRoles.Select(r => new AssetColor { Role = r }).ToList(),
                Fonts = fontRoles.Select(r => new AssetFont { Role = r }).ToList()
            };

        [Fact]
        public void Full_Cover_Scores_100()
        {
            var a = Asset(new List<string> { "minimal", "trust" },
                new List<string> { "main", "text" }, new List<string> { "heading" });
            Assert.Equal(100, ConceptFitScorer.Score(a, Concept()).Score);
        }

        [Fact]
        public void Half_Tags_Full_Roles_Scores_75()
        {
            var a = Asset(new List<string> { "minimal" },
                new List<string> { "main", "text" }, new List<string> { "heading" });
            // 0.5*0.5 + 0.25*1 + 0.25*1 = 0.75
            Assert.Equal(75, ConceptFitScorer.Score(a, Concept()).Score);
        }

        [Fact]
        public void No_Overlap_Scores_0()
        {
            var a = Asset(new List<string> { "loud" },
                new List<string> { "accent" }, new List<string> { "body" });
            Assert.Equal(0, ConceptFitScorer.Score(a, Concept()).Score);
        }

        [Fact]
        public void Empty_Concept_StyleTags_Is_Neutral_Half()
        {
            var concept = new DesignConcept
            {
                StyleTags = new List<string>(),
                Colors = new Dictionary<string, string> { ["main"] = "#111" },
                Fonts = new Dictionary<string, string> { ["heading"] = "Noto" }
            };
            var a = Asset(new List<string>(), new List<string> { "main" }, new List<string> { "heading" });
            // tag 중립 0.5 → 0.5*0.5 + 0.25*1 + 0.25*1 = 0.75
            Assert.Equal(75, ConceptFitScorer.Score(a, concept).Score);
        }

        [Fact]
        public void No_Assets_Scores_0()
        {
            Assert.Equal(0, ConceptFitScorer.Score(new CombinationRecommendation(), Concept()).Score);
        }

        [Fact]
        public void Recommendation_Overload_Unions_Across_Slots()
        {
            var rec = new CombinationRecommendation
            {
                Header = new RecommendedSlot { Asset = Asset(new List<string> { "minimal" },
                    new List<string> { "main" }, new List<string>()) },
                Layout = new RecommendedSlot { Asset = Asset(new List<string> { "trust" },
                    new List<string> { "text" }, new List<string> { "heading" }) }
            };
            // union tags {minimal,trust}=1, colors {main,text}=1, fonts {heading}=1 → 100
            Assert.Equal(100, ConceptFitScorer.Score(rec, Concept()).Score);
        }
    }
}
