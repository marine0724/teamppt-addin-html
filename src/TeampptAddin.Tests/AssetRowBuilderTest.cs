using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class AssetRowBuilderTest
    {
        private static AssetUnderstanding U() => new AssetUnderstanding
        {
            Asset = new HeaderAsset
            {
                Name = "연도강조 표지", Kind = "layout", Category = "표지", Scope = "slide",
                UseWhen = "연도 강조", Tags = new List<string> { "표지" },
                ContentFit = new List<string> { "표지" },
                Colors = new List<AssetColor> { new AssetColor { Role = "main", Value = "#1A2B4C", Locked = false } },
                Slots = new List<AssetSlot> { new AssetSlot { Name = "title", Type = "text", PerSlide = true } }
            },
            ExampleIntents = new List<string> { "IR 표지" }
        };

        [Fact]
        public void Build_Maps_Columns_And_Vector_String()
        {
            var row = AssetRowBuilder.Build(U(), new float[] { 0.1f, 0.2f }, "embed text",
                "pptx/표지_01.pptx", "thumb/표지_01.png", "bundle.pptx");

            Assert.Equal("연도강조 표지", (string)row["name"]);
            Assert.Equal("layout", (string)row["kind"]);
            Assert.Equal("표지", (string)row["category"]);
            Assert.Equal("pptx/표지_01.pptx", (string)row["file"]);
            Assert.Equal("thumb/표지_01.png", (string)row["thumb"]);
            Assert.Equal("[0.1,0.2]", (string)row["embedding"]);
            Assert.Equal("embed text", (string)row["embed_text"]);
        }

        [Fact]
        public void Build_Puts_Structure_In_Metadata()
        {
            var row = AssetRowBuilder.Build(U(), new float[] { 0.1f }, "t", "p", "th", "d");
            var meta = (JObject)row["metadata"];
            Assert.NotNull(meta["colors"]);
            Assert.NotNull(meta["slots"]);
        }
    }
}
