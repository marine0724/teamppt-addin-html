using Xunit;

namespace TeampptAddin.Tests
{
    public class MatchQuerySimilarityTest
    {
        [Fact]
        public void ParseResults_Sets_Similarity_In_Extra()
        {
            const string json = @"[
              {""name"":""A"",""kind"":""header"",""file"":""a.pptx"",""thumb"":""a.png"",""similarity"":0.71},
              {""name"":""B"",""kind"":""header"",""file"":""b.pptx"",""thumb"":""b.png"",""similarity"":0.62}
            ]";
            var list = MatchQuery.ParseResults(json);
            Assert.Equal(2, list.Count);
            Assert.True(list[0].Extra.ContainsKey("similarity"));
            Assert.Equal(0.71, (double)list[0].Extra["similarity"], 3);
            Assert.Equal(0.62, (double)list[1].Extra["similarity"], 3);
        }
    }
}
