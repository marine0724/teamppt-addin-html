namespace TeampptAddin
{
    /// <summary>생성 LLM 서비스의 단일 생성 지점. provider 스위치는 여기 한 곳.</summary>
    public static class AiServiceFactory
    {
        public static IAiService CreateGenerative()
        {
            // Task 5에서 GlmAiService 분기 추가. 지금은 Gemini만.
            return new GeminiAiService(AiConfig.GeminiKey);
        }
    }
}
