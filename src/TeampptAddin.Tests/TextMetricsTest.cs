using Xunit;

namespace TeampptAddin.Tests
{
    public class TextMetricsTest
    {
        [Fact]
        public void CharCount_Excludes_Whitespace()
            => Assert.Equal(5, TextMetrics.CharCount("a b\nc d e"));

        [Fact]
        public void BulletCount_Counts_NonEmpty_Paragraphs()
            => Assert.Equal(2, TextMetrics.BulletCount(new[] { "first", "", "  ", "second" }));

        [Fact]
        public void MaxLevel_Returns_Highest()
            => Assert.Equal(2, TextMetrics.MaxLevel(new[] { 0, 1, 2, 1 }));

        [Fact]
        public void MaxLevel_Empty_Is_Zero()
            => Assert.Equal(0, TextMetrics.MaxLevel(new int[0]));
    }
}
