using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TeampptAddin
{
    /// <summary>
    /// 초안 슬라이드(DraftProfile + 렌더 PNG)를 Gemini Flash 멀티모달 1회 호출로 이해한다.
    /// LLM은 역할 판단만; 사실(텍스트·개수)은 파서가 COM 값으로 덮어쓴다.
    /// </summary>
    public class DraftUnderstandingService
    {
        private readonly GeminiAiService _gemini;
        public DraftUnderstandingService(GeminiAiService gemini) { _gemini = gemini; }

        public async Task<DraftUnderstanding> UnderstandAsync(DraftProfile profile, string pngPath)
        {
            var userText = "도형 목록(JSON):\n" + JsonConvert.SerializeObject(profile.Shapes);
            var json = await _gemini.GenerateJsonAsync(
                DraftUnderstandingSchema.BuildSystemPrompt(),
                userText, pngPath,
                DraftUnderstandingSchema.BuildResponseSchema(), thinkingBudget: 768).ConfigureAwait(false);
            return DraftUnderstandingParser.Parse(json, profile);
        }
    }
}
