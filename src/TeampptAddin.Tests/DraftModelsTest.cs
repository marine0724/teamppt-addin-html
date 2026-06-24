using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftModelsTest
    {
        [Fact]
        public void DraftProfile_Defaults_NonNull_Collections()
        {
            var p = new DraftProfile();
            Assert.NotNull(p.Shapes);
            Assert.Empty(p.Shapes);
        }

        [Fact]
        public void DraftUnderstanding_Defaults_NonNull()
        {
            var u = new DraftUnderstanding();
            Assert.NotNull(u.Materials);
            Assert.NotNull(u.Counts);
            Assert.Equal("", u.MatchIntent);
        }
    }
}
