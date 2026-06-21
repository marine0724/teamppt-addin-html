using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class IngestPlannerTest
    {
        [Fact]
        public void Plan_Expands_Each_Slide_To_One_Item_With_PerCategory_Sequence()
        {
            var sections = new List<SectionInfo>
            {
                new SectionInfo { Name = "표지", FirstSlideIndex = 1, SlideCount = 2 },
                new SectionInfo { Name = "목차", FirstSlideIndex = 3, SlideCount = 1 },
            };

            var items = IngestPlanner.Plan(sections);

            Assert.Equal(3, items.Count);

            Assert.Equal(1, items[0].SourceSlideIndex);
            Assert.Equal("표지", items[0].Category);
            Assert.Equal("표지_01", items[0].AssetId);
            Assert.Equal("표지_01.pptx", items[0].PptxFileName);
            Assert.Equal("표지_01.png", items[0].ThumbFileName);

            Assert.Equal(2, items[1].SourceSlideIndex);
            Assert.Equal("표지_02", items[1].AssetId);

            Assert.Equal(3, items[2].SourceSlideIndex);
            Assert.Equal("목차", items[2].Category);
            Assert.Equal("목차_01", items[2].AssetId);
        }

        [Fact]
        public void Plan_Empty_Sections_Returns_Empty()
        {
            Assert.Empty(IngestPlanner.Plan(new List<SectionInfo>()));
        }
    }
}
