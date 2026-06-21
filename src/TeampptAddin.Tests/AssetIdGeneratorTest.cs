using Xunit;

namespace TeampptAddin.Tests
{
    public class AssetIdGeneratorTest
    {
        [Fact]
        public void Make_Pads_Sequence_To_Two_Digits()
        {
            Assert.Equal("표지_01", AssetIdGenerator.Make("표지", 1));
            Assert.Equal("표지_12", AssetIdGenerator.Make("표지", 12));
        }

        [Fact]
        public void Make_Replaces_Filename_Unsafe_Chars_In_Category()
        {
            Assert.Equal("레이아웃_표지__01", AssetIdGenerator.Make("레이아웃(표지)", 1).Replace("(", "_").Replace(")", "_"));
        }

        [Fact]
        public void Make_Replaces_Slash_And_Space()
        {
            Assert.Equal("3단_강점_01", AssetIdGenerator.Make("3단 강점", 1));
            Assert.Equal("a_b_03", AssetIdGenerator.Make("a/b", 3));
        }
    }
}
