using System.Collections.Generic;
using System.IO;
using Xunit;

namespace TeampptAddin.Tests
{
    public class RecommendationCacheTest
    {
        [Fact]
        public void Save_Then_Load_Roundtrips()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "rc_" + System.Guid.NewGuid() + ".json");
            try
            {
                var cache = new RecommendationCache(tmp);
                cache.Save(new List<HeaderAsset> { new HeaderAsset { Name = "A", File = "a.pptx", Kind = "layout" } });
                var loaded = cache.Load();
                Assert.Single(loaded);
                Assert.Equal("A", loaded[0].Name);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }

        [Fact]
        public void Load_Missing_File_Returns_Empty()
        {
            var cache = new RecommendationCache(Path.Combine(Path.GetTempPath(), "nope_" + System.Guid.NewGuid() + ".json"));
            Assert.Empty(cache.Load());
        }
    }
}
