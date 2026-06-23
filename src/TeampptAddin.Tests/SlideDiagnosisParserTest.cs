using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class SlideDiagnosisParserTest
    {
        [Fact]
        public void Parse_Reads_Message_And_Questions()
        {
            var json = @"{""message"":""대비가 약합니다."",""questions"":[""제목을 키울까?"",""색을 바꿀까?"",""여백을 줄일까?""]}";
            var d = SlideDiagnosisParser.Parse(json);
            Assert.Equal("대비가 약합니다.", d.Message);
            Assert.Equal(3, d.SuggestedQuestions.Count);
            Assert.Equal("제목을 키울까?", d.SuggestedQuestions[0]);
        }

        [Fact]
        public void Parse_Missing_Fields_Yields_Empty_Defaults()
        {
            var d = SlideDiagnosisParser.Parse("{}");
            Assert.Equal("", d.Message);
            Assert.Empty(d.SuggestedQuestions);
        }

        [Fact]
        public void Parse_Caps_Questions_At_Three()
        {
            var json = @"{""message"":""x"",""questions"":[""1"",""2"",""3"",""4"",""5""]}";
            var d = SlideDiagnosisParser.Parse(json);
            Assert.Equal(3, d.SuggestedQuestions.Count);
        }
    }
}
