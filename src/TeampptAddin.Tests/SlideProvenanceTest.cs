using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class SlideProvenanceTest
    {
        [Fact]
        public void Format_Then_Parse_RoundTrips()
        {
            var slots = new List<RecommendedSlot>
            {
                new RecommendedSlot { Asset = new HeaderAsset {
                    Kind = "header", Name = "공통헤더",
                    Extra = new Dictionary<string, JToken> { ["remote_file"] = "assets/h.pptx" } } },
                new RecommendedSlot { Asset = new HeaderAsset {
                    Kind = "layout", Name = "2단", File = "local-l.pptx" } }
            };

            var json = SlideProvenance.Format(slots);
            var parsed = SlideProvenance.Parse(json);

            Assert.Equal(2, parsed.Count);
            Assert.Equal("header", parsed[0].Kind);
            Assert.Equal("공통헤더", parsed[0].Name);
            Assert.Equal("assets/h.pptx", parsed[0].RemoteFile);   // Extra["remote_file"] 우선
            Assert.Equal("layout", parsed[1].Kind);
            Assert.Equal("local-l.pptx", parsed[1].RemoteFile);     // remote_file 없으면 File 폴백
        }

        [Fact]
        public void Format_SkipsNullAssets()
        {
            var slots = new List<RecommendedSlot>
            {
                new RecommendedSlot { Asset = null },
                new RecommendedSlot { Asset = new HeaderAsset { Kind = "layout", Name = "L" } }
            };
            var parsed = SlideProvenance.Parse(SlideProvenance.Format(slots));
            Assert.Single(parsed);
            Assert.Equal("layout", parsed[0].Kind);
        }

        [Fact]
        public void Parse_NullOrGarbage_ReturnsEmpty()
        {
            Assert.Empty(SlideProvenance.Parse(null));
            Assert.Empty(SlideProvenance.Parse(""));
            Assert.Empty(SlideProvenance.Parse("not json {{{"));
        }
    }
}
