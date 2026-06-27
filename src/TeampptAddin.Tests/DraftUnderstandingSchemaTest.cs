using Xunit;

namespace TeampptAddin.Tests
{
    public class DraftUnderstandingSchemaTest
    {
        [Fact]
        public void Schema_Has_Materials_And_MatchIntent()
        {
            var s = DraftUnderstandingSchema.BuildResponseSchema();
            var props = s["properties"];
            Assert.NotNull(props["materials"]);
            Assert.NotNull(props["matchIntent"]);
            Assert.NotNull(props["counts"]);
            Assert.NotNull(props["slideKind"]);
        }

        [Fact]
        public void SystemPrompt_Mentions_RoleJudgment()
        {
            var p = DraftUnderstandingSchema.BuildSystemPrompt();
            Assert.Contains("역할", p);
        }
    }
}
