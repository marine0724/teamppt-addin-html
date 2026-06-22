using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class EmbedTextBuilderTest
    {
        private static AssetUnderstanding Sample()
        {
            return new AssetUnderstanding
            {
                Asset = new HeaderAsset
                {
                    Name = "연도강조 표지",
                    Category = "표지",
                    UseWhen = "연도를 강조할 때",
                    ContentFit = new List<string> { "표지", "연도 강조" },
                    Tags = new List<string> { "표지", "연도" },
                    Colors = new List<AssetColor> { new AssetColor { Role = "main", Value = "#1A2B4C" } }
                },
                ExampleIntents = new List<string> { "투자 유치 IR 표지" }
            };
        }

        [Fact]
        public void Build_Includes_Search_Relevant_Fields()
        {
            var text = EmbedTextBuilder.Build(Sample());
            Assert.Contains("연도강조 표지", text);
            Assert.Contains("표지", text);
            Assert.Contains("연도를 강조할 때", text);
            Assert.Contains("투자 유치 IR 표지", text);
        }

        [Fact]
        public void Build_Excludes_Insertion_Only_Data()
        {
            var text = EmbedTextBuilder.Build(Sample());
            Assert.DoesNotContain("#1A2B4C", text);
        }
    }
}
