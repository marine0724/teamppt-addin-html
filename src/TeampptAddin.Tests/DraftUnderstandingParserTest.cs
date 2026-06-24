using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftUnderstandingParserTest
    {
        [Fact]
        public void Overwrites_Text_From_Profile_Not_Llm()
        {
            var profile = new DraftProfile
            {
                Shapes = { new DraftShape { Id = 3, Kind = "text", Text = "진짜 제목", CharCount = 4, BulletCount = 1 } }
            };
            const string llm = @"{
              ""materials"": [ { ""role"": ""title"", ""type"": ""text"", ""sourceShapeId"": 3, ""emphasis"": ""heading"" } ],
              ""counts"": { ""textBlocks"":1, ""bullets"":1, ""images"":0, ""tables"":0, ""charts"":0 },
              ""layoutShape"":""x"", ""designSummary"":""y"", ""dominantColors"":[""#000""],
              ""matchIntent"":""제목 슬라이드"", ""slideKind"":""body""
            }";

            var u = DraftUnderstandingParser.Parse(llm, profile);

            Assert.Single(u.Materials);
            Assert.Equal("title", u.Materials[0].Role);          // LLM
            Assert.Equal("진짜 제목", u.Materials[0].Text);        // COM 덮어씀
            Assert.Equal(4, u.Materials[0].CharCount);            // COM 덮어씀
            Assert.Equal("제목 슬라이드", u.MatchIntent);
        }

        [Fact]
        public void Drops_Material_With_Unknown_ShapeId()
        {
            var profile = new DraftProfile();   // 도형 없음
            const string llm = @"{
              ""materials"": [ { ""role"":""title"", ""type"":""text"", ""sourceShapeId"":99, ""emphasis"":""heading"" } ],
              ""counts"": { ""textBlocks"":0,""bullets"":0,""images"":0,""tables"":0,""charts"":0 },
              ""layoutShape"":"""", ""designSummary"":"""", ""dominantColors"":[], ""matchIntent"":""x"", ""slideKind"":""body""
            }";

            var u = DraftUnderstandingParser.Parse(llm, profile);
            Assert.Empty(u.Materials);
        }
    }
}
