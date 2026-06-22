using Newtonsoft.Json.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class SupabaseAssetMapperTest
    {
        private const string Row = @"{
          ""file"":""pptx/표지_01.pptx"", ""thumb"":""thumb/표지_01.png"",
          ""name"":""연도강조 표지"", ""category"":""표지"", ""kind"":""layout"", ""scope"":""slide"",
          ""tags"":[""표지"",""연도""], ""use_when"":""연도 강조"", ""content_fit"":[""표지""],
          ""metadata"":{ ""colors"":[{""role"":""main"",""value"":""#1A2B4C"",""locked"":false}],
                         ""fonts"":[{""role"":""heading"",""family"":""Pretendard""}],
                         ""slots"":[{""name"":""title"",""type"":""text"",""perSlide"":true}] },
          ""similarity"":0.83 }";

        [Fact]
        public void Map_Core_Fields_And_Filename()
        {
            var a = SupabaseAssetMapper.Map(JObject.Parse(Row));
            Assert.Equal("연도강조 표지", a.Name);
            Assert.Equal("layout", a.Kind);
            Assert.Equal("표지", a.Category);
            Assert.Equal("표지_01.pptx", a.File);
        }

        [Fact]
        public void Map_Restores_Metadata_Structures()
        {
            var a = SupabaseAssetMapper.Map(JObject.Parse(Row));
            Assert.Equal("#1A2B4C", a.Colors[0].Value);
            Assert.Equal("Pretendard", a.Fonts[0].Family);
            Assert.Equal("title", a.Slots[0].Name);
        }

        [Fact]
        public void Map_Keeps_Remote_Paths_In_Extra()
        {
            var a = SupabaseAssetMapper.Map(JObject.Parse(Row));
            Assert.Equal("pptx/표지_01.pptx", a.Extra["remote_file"].ToString());
            Assert.Equal("thumb/표지_01.png", a.Extra["remote_thumb"].ToString());
        }
    }
}
