using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 덱 구조 1단 분석: 슬라이드 텍스트 요약(사실)을 Gemini 저가 1회 호출로 kind/label 판정.
    /// 이미지 없이 텍스트만(D1 — 비용이 장수에 폭증 안 하게).
    /// </summary>
    public class DeckStructureService
    {
        private readonly GeminiAiService _gemini;
        public DeckStructureService(GeminiAiService gemini) { _gemini = gemini; }

        public static string BuildUserText(List<DraftProfile> slides)
        {
            var sb = new StringBuilder();
            foreach (var p in slides ?? new List<DraftProfile>())
            {
                var text = string.Join(" / ", p.Shapes.Where(s => s.Kind == "text").Select(s => s.Text))
                    .Replace("\r", " ").Replace("\n", " ").Trim();
                if (text.Length > 160) text = text.Substring(0, 160);
                int imgs = p.Shapes.Count(s => s.Kind == "image");
                sb.AppendLine($"슬라이드 {p.SlideIndex}: \"{text}\" (이미지 {imgs}개, 도형 {p.Shapes.Count}개)");
            }
            return sb.ToString();
        }

        public async Task<DeckStructure> AnalyzeAsync(List<DraftProfile> slides)
        {
            var json = await _gemini.GenerateJsonAsync(
                DeckStructureSchema.BuildSystemPrompt(), BuildUserText(slides), (string)null,
                DeckStructureSchema.BuildResponseSchema(), thinkingBudget: 512).ConfigureAwait(false);
            Logger.Log("[DeckStructure] raw↓ " + json);
            return DeckStructureParser.Parse(json, slides?.Count ?? 0);
        }
    }
}
