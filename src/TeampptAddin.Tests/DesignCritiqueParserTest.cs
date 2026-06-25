using Xunit;

namespace TeampptAddin.Tests
{
    public class DesignCritiqueParserTest
    {
        [Fact]
        public void Parses_Score_Dimensions_Bottleneck()
        {
            const string json = @"{
              ""score"":78,
              ""dimensionScores"":{""정렬"":18,""여백"":9,""위계"":18,""색"":12,""타이포"":8,""의도부합"":13},
              ""verdict"":""위계는 좋으나 여백이 답답"",
              ""bottleneck"":""에셋"",
              ""suggestion"":""차트 외 컴포넌트 에셋 확보"",
              ""reasoning"":""원본 에셋만큼은 나오지만 종류가 차트뿐""
            }";
            var c = DesignCritiqueParser.Parse(json);
            Assert.Equal(78, c.Score);
            Assert.Equal(18, c.DimensionScores["위계"]);
            Assert.Equal("에셋", c.Bottleneck);
            Assert.Equal("차트 외 컴포넌트 에셋 확보", c.Suggestion);
            Assert.Contains("차트", c.Reasoning);
        }

        [Fact]
        public void Parses_New_Bottleneck_Categories()
        {
            var c = DesignCritiqueParser.Parse(@"{""score"":70,""bottleneck"":""에셋부족""}");
            Assert.Equal("에셋부족", c.Bottleneck);
        }

        [Fact]
        public void Defaults_When_Fields_Missing()
        {
            var c = DesignCritiqueParser.Parse(@"{""score"":50}");
            Assert.Equal(50, c.Score);
            Assert.Equal("", c.Bottleneck);
            Assert.NotNull(c.DimensionScores);
        }

        [Fact]
        public void DesignConcept_Is_Sum_Of_Dimension_Scores()
        {
            const string json = @"{
              ""score"": 80,
              ""dimensionScores"": {""정렬"":18,""여백"":12,""위계"":16,""색"":12,""타이포"":8,""의도부합"":16},
              ""verdict"": ""실무급"",
              ""bottleneck"": ""에셋품질"",
              ""suggestion"": ""대비 강화"",
              ""reasoning"": ""괜찮습니다""
            }";
            var c = DesignCritiqueParser.Parse(json);
            Assert.Equal(82, c.DesignConcept);   // 18+12+16+12+8+16
            Assert.Equal(0, c.MaterialFit);      // 파서는 0; 서비스가 Task 3에서 주입
            Assert.Equal("에셋품질", c.Bottleneck);
        }
    }
}
