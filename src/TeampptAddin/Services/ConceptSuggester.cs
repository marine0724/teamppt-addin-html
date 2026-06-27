using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 덱 구조 요약 + 용도 + 느낌을 Gemini 저가 텍스트 1회 호출로 받아
    /// 서로 구별되는 DesignConcept 3개를 생성한다(디자인-온리, 텍스트 내용 불변).
    /// 적용(검색 가중·색/폰트 override)은 소비자(Phase 3)가 담당.
    /// </summary>
    public class ConceptSuggester
    {
        private readonly IAiService _gemini;
        public ConceptSuggester(IAiService gemini) { _gemini = gemini; }

        public static string BuildUserText(DeckStructure structure, string usage, string feeling)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"용도: {usage}");
            sb.AppendLine($"원하는 느낌: {feeling}");
            sb.AppendLine("[덱 구조]");
            foreach (var line in DeckStructureFormatter.ToSummaryLines(structure))
                sb.AppendLine(line);
            return sb.ToString();
        }

        public async Task<List<DesignConcept>> SuggestAsync(DeckStructure structure, string usage, string feeling)
        {
            var json = await _gemini.GenerateJsonAsync(
                ConceptSuggesterSchema.BuildSystemPrompt(),
                BuildUserText(structure, usage, feeling),
                (string)null,
                ConceptSuggesterSchema.BuildResponseSchema(),
                temperature: 0.7,        // 3안 간 다양성 위해 약간 높임
                thinkingBudget: 512).ConfigureAwait(false);
            Logger.Log("[ConceptSuggester] raw↓ " + json);
            return ConceptSuggesterParser.Parse(json);
        }
    }
}
