using Xunit;

namespace TeampptAddin.Tests
{
    public class SlotMapParserTest
    {
        [Fact]
        public void Computes_Overflow_And_Empty()
        {
            const string llm = @"{ ""mappings"": [
                { ""draftShapeId"":1, ""assetShapeId"":""a1"", ""fitNote"":""제목"", ""confidence"":0.9 }
            ] }";
            var r = SlotMapParser.Parse(llm, new[] { 1, 2 }, new[] { "a1", "a2" });

            Assert.Single(r.Mappings);
            Assert.Equal(1, r.Mappings[0].DraftShapeId);
            Assert.Contains(2, r.Overflow);      // 배정 안된 초안 도형
            Assert.Contains("a2", r.Empty);      // 빈 에셋 슬롯
        }

        [Fact]
        public void Ignores_Mapping_With_Unknown_Ids()
        {
            const string llm = @"{ ""mappings"": [
                { ""draftShapeId"":99, ""assetShapeId"":""zz"", ""confidence"":0.5 }
            ] }";
            var r = SlotMapParser.Parse(llm, new[] { 1 }, new[] { "a1" });
            Assert.Empty(r.Mappings);
            Assert.Contains(1, r.Overflow);
            Assert.Contains("a1", r.Empty);
        }
    }
}
