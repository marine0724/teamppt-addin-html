namespace TeampptAddin
{
    /// <summary>생성 LLM 서비스의 단일 생성 지점. provider 스위치는 여기 한 곳.</summary>
    public static class AiServiceFactory
    {
        public static IAiService CreateGenerative()
        {
            if (AiConfig.UseGlm)
            {
                Logger.Log("[AiFactory] GLM-Flash provider 사용");
                return new GlmAiService(AiConfig.GlmKey);
            }
            Logger.Log("[AiFactory] Gemini provider 사용");
            return new GeminiAiService(AiConfig.GeminiKey);
        }
    }
}
