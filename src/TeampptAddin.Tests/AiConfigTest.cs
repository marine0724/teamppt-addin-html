using Xunit;

namespace TeampptAddin.Tests
{
    public class AiConfigTest
    {
        [Fact]
        public void DefaultsToGlm_WhenProviderMissing()
        {
            AiConfig.LoadFromJson("{\"gemini\":\"g-key\",\"glm\":\"z-key\"}");
            Assert.Equal("glm", AiConfig.Provider);
            Assert.True(AiConfig.UseGlm);
            Assert.Equal("z-key", AiConfig.GlmKey);
            Assert.Equal("g-key", AiConfig.GeminiKey);
        }

        [Fact]
        public void RespectsExplicitGeminiProvider()
        {
            AiConfig.LoadFromJson("{\"provider\":\"gemini\",\"gemini\":\"g\",\"glm\":\"z\"}");
            Assert.False(AiConfig.UseGlm);
        }

        [Fact]
        public void FallsBackToGemini_WhenGlmKeyEmpty()
        {
            AiConfig.LoadFromJson("{\"provider\":\"glm\",\"gemini\":\"g\"}");
            Assert.Equal("glm", AiConfig.Provider);
            Assert.False(AiConfig.UseGlm); // 키 없으면 GLM 안 씀
        }
    }
}
