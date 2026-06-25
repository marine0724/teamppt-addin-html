using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class CombinationRecommenderParserTest
    {
        private static Dictionary<string, List<HeaderAsset>> Pool() => new Dictionary<string, List<HeaderAsset>>
        {
            ["header"] = new List<HeaderAsset> { new HeaderAsset { File = "h1.pptx", Name = "헤더1" } },
            ["layout"] = new List<HeaderAsset> { new HeaderAsset { File = "l1.pptx", Name = "레이아웃1" } },
            ["component"] = new List<HeaderAsset>
            {
                new HeaderAsset { File = "c1.pptx", Name = "카드" },
                new HeaderAsset { File = "c2.pptx", Name = "아이콘블록" }
            }
        };

        [Fact]
        public void Maps_Header_Layout_Components_From_Pool()
        {
            const string llm = @"{
              ""header"":{""file"":""h1.pptx"",""fitNote"":""제목"",""confidence"":0.88},
              ""layout"":{""file"":""l1.pptx"",""fitNote"":""3단"",""confidence"":0.82},
              ""components"":[{""file"":""c1.pptx"",""fitNote"":""카드"",""confidence"":0.79}],
              ""unmet"":[],
              ""reasoning"":""본문 항목 나열이라 좌측번호 레이아웃 선택""
            }";
            var r = CombinationRecommenderParser.Parse(llm, Pool());
            Assert.Equal("헤더1", r.Header.Asset.Name);
            Assert.Equal(0.88, r.Header.Confidence, 2);
            Assert.Equal("레이아웃1", r.Layout.Asset.Name);
            Assert.Single(r.Components);
            Assert.Equal("카드", r.Components[0].Asset.Name);
            Assert.Equal("본문 항목 나열이라 좌측번호 레이아웃 선택", r.Reasoning);
        }

        [Fact]
        public void Drops_Hallucinated_File_And_Keeps_Unmet()
        {
            const string llm = @"{
              ""header"":{""file"":""nope.pptx"",""fitNote"":""x"",""confidence"":0.5},
              ""layout"":null,
              ""components"":[],
              ""unmet"":[""layout"",""component""]
            }";
            var r = CombinationRecommenderParser.Parse(llm, Pool());
            Assert.Null(r.Header);   // 환각 file → 버림
            Assert.Null(r.Layout);
            Assert.Empty(r.Components);
            Assert.Contains("layout", r.Unmet);
        }
    }
}
