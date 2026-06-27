using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class MaterialFitScorerTest
    {
        private static HeaderAsset Asset(double sim, AssetCapacity cap = null) => new HeaderAsset
        {
            File = "a.pptx",
            Capacity = cap,
            Extra = sim >= 0 ? new Dictionary<string, JToken> { ["similarity"] = sim } : null
        };

        [Fact]
        public void Averages_Similarity_With_Neutral_Capacity()
        {
            var rec = new CombinationRecommendation
            {
                Header = new RecommendedSlot { Asset = Asset(0.8) },
                Layout = new RecommendedSlot { Asset = Asset(0.6) }
            };
            var r = MaterialFitScorer.Score(rec, new DraftUnderstanding());
            Assert.Equal(0.70, r.SimilarityAvg, 2);
            Assert.Equal(1.0, r.CapacityScore, 2);
            Assert.Equal(82, r.Score);   // round(100*(0.6*0.7 + 0.4*1.0))
        }

        [Fact]
        public void Penalizes_Capacity_Mismatch()
        {
            var rec = new CombinationRecommendation
            {
                Layout = new RecommendedSlot { Asset = Asset(0.5, new AssetCapacity { Min = 2, Max = 2 }) }
            };
            var u = new DraftUnderstanding { NeededCombination = new NeededCombination { Component = 5 } };
            var r = MaterialFitScorer.Score(rec, u);
            Assert.Equal(0.0, r.CapacityScore, 2);   // dist=3, denom=max(2,1)=2 → max(0,1-1.5)=0
            Assert.Equal(30, r.Score);               // round(100*(0.6*0.5 + 0.4*0))
        }

        [Fact]
        public void No_Similarity_Anywhere_Uses_Neutral_Half()
        {
            var rec = new CombinationRecommendation { Header = new RecommendedSlot { Asset = Asset(-1) } };
            var r = MaterialFitScorer.Score(rec, new DraftUnderstanding());
            Assert.Equal(0.5, r.SimilarityAvg, 2);
            Assert.Equal(70, r.Score);   // round(100*(0.6*0.5 + 0.4*1.0))
        }
    }
}
