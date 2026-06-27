using Xunit;

namespace TeampptAddin.Tests
{
    public class ConceptSuggesterParserTest
    {
        [Fact]
        public void Parses_Three_Concepts_With_Ids_Tags_Colors_Fonts()
        {
            const string json = @"{
              ""concepts"": [
                { ""name"":""신뢰 블루"", ""styleTags"":[""trust"",""corporate""],
                  ""colors"":[{""role"":""main"",""value"":""#1D4ED8""},{""role"":""text"",""value"":""#0F172A""}],
                  ""fonts"":[{""role"":""heading"",""family"":""Pretendard""}] },
                { ""name"":""모던 미니멀"", ""styleTags"":[""minimal""],
                  ""colors"":[{""role"":""main"",""value"":""#111827""}],
                  ""fonts"":[{""role"":""heading"",""family"":""Noto Sans KR""}] },
                { ""name"":""웜 그레이"", ""styleTags"":[""warm""],
                  ""colors"":[{""role"":""main"",""value"":""#92400E""}],
                  ""fonts"":[{""role"":""heading"",""family"":""Gowun Dodum""}] }
              ]
            }";
            var list = ConceptSuggesterParser.Parse(json);
            Assert.Equal(3, list.Count);
            Assert.Equal("c1", list[0].Id);
            Assert.Equal("c3", list[2].Id);
            Assert.Equal("신뢰 블루", list[0].Name);
            Assert.Contains("trust", list[0].StyleTags);
            Assert.Equal("#1D4ED8", list[0].Colors["main"]);
            Assert.Equal("#0F172A", list[0].Colors["text"]);
            Assert.Equal("Pretendard", list[0].Fonts["heading"]);
        }

        [Fact]
        public void Drops_Concept_With_Blank_Name_And_Reassigns_Ids()
        {
            const string json = @"{
              ""concepts"": [
                { ""name"":""있음"", ""styleTags"":[], ""colors"":[], ""fonts"":[] },
                { ""name"":"""",   ""styleTags"":[], ""colors"":[], ""fonts"":[] }
              ]
            }";
            var list = ConceptSuggesterParser.Parse(json);
            Assert.Single(list);
            Assert.Equal("있음", list[0].Name);
            Assert.Equal("c1", list[0].Id);
        }
    }
}
