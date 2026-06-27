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

        [Fact]
        public void Parses_Purpose_And_NeededCombination_Body()
        {
            var profile = new DraftProfile
            {
                Shapes = { new DraftShape { Id = 1, Kind = "text", Text = "기능", CharCount = 2 } }
            };
            const string llm = @"{
              ""materials"":[{""role"":""title"",""type"":""text"",""sourceShapeId"":1,""emphasis"":""heading""}],
              ""counts"":{""textBlocks"":1,""bullets"":0,""images"":0,""tables"":0,""charts"":0},
              ""layoutShape"":""x"",""designSummary"":""y"",""dominantColors"":[],
              ""matchIntent"":""기능 비교"",""slideKind"":""body"",
              ""purpose"":""3개 핵심 기능을 동등 비교"",
              ""neededCombination"":{""slide"":0,""header"":1,""layout"":1,""component"":3}
            }";
            var u = DraftUnderstandingParser.Parse(llm, profile);
            Assert.Equal("3개 핵심 기능을 동등 비교", u.Purpose);
            Assert.Equal(1, u.NeededCombination.Header);
            Assert.Equal(3, u.NeededCombination.Component);
            Assert.Equal(0, u.NeededCombination.Slide);
        }

        [Fact]
        public void Parses_Cover_NeededCombination_Slide()
        {
            var profile = new DraftProfile();
            const string llm = @"{
              ""materials"":[],""counts"":{""textBlocks"":0,""bullets"":0,""images"":0,""tables"":0,""charts"":0},
              ""layoutShape"":"""",""designSummary"":"""",""dominantColors"":[],
              ""matchIntent"":""표지"",""slideKind"":""cover"",
              ""purpose"":""오프닝 표지"",
              ""neededCombination"":{""slide"":1,""header"":0,""layout"":0,""component"":0}
            }";
            var u = DraftUnderstandingParser.Parse(llm, profile);
            Assert.Equal(1, u.NeededCombination.Slide);
            Assert.Equal("cover", u.SlideKind);
        }
    }
}
