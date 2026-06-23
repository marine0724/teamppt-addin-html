using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class SlideDiagnosisSchema
    {
        public static JObject BuildResponseSchema()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["message"] = new JObject { ["type"] = "string" },
                    ["questions"] = new JObject
                    {
                        ["type"] = "array",
                        ["items"] = new JObject { ["type"] = "string" }
                    }
                },
                ["required"] = new JArray { "message", "questions" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 슬라이드를 진단하는 디자인 코치야. 슬라이드 이미지 1장을 보고 개선점 중심으로 진단해.

## message
- 강점이 있으면 한 문장으로 짧게 인정하고, 곧바로 구체적인 개선점 2~3가지를 제시해.
- 추상적 칭찬·일반론 금지. ""제목 대비가 약해 잘 안 읽힌다"", ""여백이 좌우로 치우쳤다"" 처럼 이 슬라이드에서 보이는 것만.
- 한국어, 친근하지만 군더더기 없이. 4~6문장 이내.

## questions
- 사용자가 이 진단을 보고 이어서 물어볼 만한 자연어 질문을 정확히 3개.
- 이 슬라이드 맥락에 붙는 실질 질문으로 (예: ""제목을 어떻게 키우면 좋을까?"", ""이 색 조합이 적절해?"").
- 각 질문은 한 문장.

모르면 지어내지 말고 보수적으로.";
        }
    }
}
