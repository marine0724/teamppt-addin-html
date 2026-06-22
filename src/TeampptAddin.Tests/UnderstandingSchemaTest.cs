using Newtonsoft.Json.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class UnderstandingSchemaTest
    {
        [Fact]
        public void Schema_Requires_Core_Fields()
        {
            var schema = UnderstandingSchema.BuildResponseSchema();
            var required = (JArray)schema["required"];
            Assert.Contains("name", required.ToObject<string[]>());
            Assert.Contains("kind", required.ToObject<string[]>());
            Assert.Contains("slots", required.ToObject<string[]>());
            Assert.Contains("colors", required.ToObject<string[]>());
            Assert.Contains("example_intents", required.ToObject<string[]>());
        }

        [Fact]
        public void Schema_Kind_Is_Constrained_Enum()
        {
            var schema = UnderstandingSchema.BuildResponseSchema();
            var kindEnum = (JArray)schema["properties"]["kind"]["enum"];
            Assert.Equal(2, kindEnum.Count);
            Assert.Contains("layout", kindEnum.ToObject<string[]>());
            Assert.Contains("component", kindEnum.ToObject<string[]>());
        }

        [Fact]
        public void SystemPrompt_Includes_Category_Hint()
        {
            var prompt = UnderstandingSchema.BuildSystemPrompt("표지");
            Assert.Contains("표지", prompt);
        }
    }
}
