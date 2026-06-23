using Xunit;

namespace TeampptAddin.Tests
{
    public class SlideDiagnosisSchemaTest
    {
        [Fact]
        public void ResponseSchema_Has_Message_And_Questions()
        {
            var schema = SlideDiagnosisSchema.BuildResponseSchema();
            var props = schema["properties"];
            Assert.NotNull(props["message"]);
            Assert.NotNull(props["questions"]);
            Assert.Equal("array", props["questions"]["type"].ToString());
        }

        [Fact]
        public void SystemPrompt_Mentions_Diagnosis_And_Three_Questions()
        {
            var p = SlideDiagnosisSchema.BuildSystemPrompt();
            Assert.Contains("개선", p);
            Assert.Contains("3", p);
        }
    }
}
