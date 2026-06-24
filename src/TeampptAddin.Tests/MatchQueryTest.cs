using Xunit;

namespace TeampptAddin.Tests
{
    public class MatchQueryTest
    {
        [Fact]
        public void BuildArgs_Formats_Vector_And_Count()
        {
            var args = MatchQuery.BuildArgs(new float[] { 0.1f, 0.2f }, 8);
            Assert.Equal("[0.1,0.2]", args["query_embedding"].ToString());
            Assert.Equal(8, (int)args["match_count"]);
        }

        [Fact]
        public void ParseResults_Maps_Each_Row()
        {
            var json = @"[
              {""file"":""pptx/a.pptx"",""thumb"":""thumb/a.png"",""name"":""A"",""category"":""표지"",""kind"":""layout"",""metadata"":{},""similarity"":0.9},
              {""file"":""pptx/b.pptx"",""thumb"":""thumb/b.png"",""name"":""B"",""category"":""목차"",""kind"":""component"",""metadata"":{},""similarity"":0.7}
            ]";
            var list = MatchQuery.ParseResults(json);
            Assert.Equal(2, list.Count);
            Assert.Equal("A", list[0].Name);
            Assert.Equal("b.pptx", list[1].File);
        }

        [Fact]
        public void BuildArgs_With_Kind_Includes_FilterKind()
        {
            var args = MatchQuery.BuildArgs(new float[] { 0.1f }, 5, "header");
            Assert.Equal("header", (string)args["filter_kind"]);
            Assert.Equal(5, (int)args["match_count"]);
        }

        [Fact]
        public void BuildArgs_Null_Kind_Omits_FilterKind()
        {
            var args = MatchQuery.BuildArgs(new float[] { 0.1f }, 5, null);
            Assert.False(args.ContainsKey("filter_kind"));
        }

        [Fact]
        public void ParseResults_Empty_Returns_Empty()
        {
            Assert.Empty(MatchQuery.ParseResults("[]"));
        }
    }
}
