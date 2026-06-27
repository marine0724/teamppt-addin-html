using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationCandidateProviderQueryTest
    {
        private static DraftUnderstanding U() => new DraftUnderstanding
        {
            Purpose = "P",
            MatchIntent = "M",
            Counts = new Dictionary<string, int> { ["text"] = 2 }
        };

        [Fact]
        public void BuildQuery_Without_Concept_Is_Unchanged()   // Route A 회귀 가드
        {
            Assert.Equal("P | M (text:2)", CombinationCandidateProvider.BuildQuery(U(), null));
        }

        [Fact]
        public void BuildQuery_With_StyleTags_Appends_Style()
        {
            var concept = new DesignConcept { StyleTags = new List<string> { "minimal", "trust" } };
            Assert.Equal("P | M | 스타일:minimal,trust (text:2)",
                CombinationCandidateProvider.BuildQuery(U(), concept));
        }

        [Fact]
        public void BuildQuery_Empty_StyleTags_Is_Unchanged()
        {
            var concept = new DesignConcept { StyleTags = new List<string>() };
            Assert.Equal("P | M (text:2)", CombinationCandidateProvider.BuildQuery(U(), concept));
        }
    }
}
