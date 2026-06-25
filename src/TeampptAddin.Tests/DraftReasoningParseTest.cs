using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftReasoningParseTest
    {
        [Fact]
        public void Parses_Reasoning_Field()
        {
            const string json = @"{
              ""materials"":[], ""counts"":{""textBlocks"":1,""bullets"":0,""images"":0,""tables"":0,""charts"":0},
              ""layoutShape"":""title-top"", ""designSummary"":""x"", ""dominantColors"":[],
              ""matchIntent"":""비전 설명"", ""slideKind"":""body"", ""purpose"":""비전 제시"",
              ""neededCombination"":{""slide"":0,""header"":1,""layout"":1,""component"":2},
              ""reasoning"":""좌측 텍스트 우측 시각이 필요해 header+layout 조합으로 판단""
            }";
            var u = DraftUnderstandingParser.Parse(json, new DraftProfile());
            Assert.Equal("좌측 텍스트 우측 시각이 필요해 header+layout 조합으로 판단", u.Reasoning);
        }
    }
}
