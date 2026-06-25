using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TeampptAddin
{
    /// <summary>
    /// 초안 재료 ↔ 삽입된 에셋 실제 도형을 Gemini로 매핑한다(역할·타입·개수 배정만, 텍스트 생성 금지).
    /// overflow/empty 계산은 SlotMapParser가 담당.
    /// </summary>
    public class SlotMapper
    {
        private readonly GeminiAiService _gemini;
        public SlotMapper(GeminiAiService gemini) { _gemini = gemini; }

        public async Task<MappingResult> MapAsync(DraftUnderstanding u, List<AssetShapeInfo> assetShapes)
        {
            var userText =
                "초안 재료:\n" + JsonConvert.SerializeObject(u.Materials) +
                "\n\n에셋 도형:\n" + JsonConvert.SerializeObject(assetShapes);
            var json = await _gemini.GenerateJsonAsync(
                SlotMapSchema.BuildSystemPrompt(), userText, (string)null,
                SlotMapSchema.BuildResponseSchema(), 0.2).ConfigureAwait(false);
            return SlotMapParser.Parse(json,
                u.Materials.Select(m => m.SourceShapeId),
                assetShapes.Select(a => a.Id));
        }
    }
}
