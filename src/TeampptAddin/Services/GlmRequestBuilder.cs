using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>z.ai chat/completions 요청 바디 빌더(순수). 이미지 유무로 모델 자동 선택.</summary>
    public static class GlmRequestBuilder
    {
        public const string TextModel = "glm-4.7-flash";
        public const string VisionModel = "glm-4.6v-flash";

        public static JObject Build(
            string systemPrompt, string userText, IList<string> imageBase64,
            JObject responseSchema, double temperature, int thinkingBudget)
        {
            bool hasImages = imageBase64 != null && imageBase64.Count > 0;
            var example = GlmSchema.ToExample(responseSchema);

            var sys = new StringBuilder(systemPrompt);
            sys.AppendLine();
            sys.AppendLine("Reply ONLY with JSON matching this exact structure:");
            sys.AppendLine(example.ToString(Formatting.Indented));

            JToken userContent;
            if (hasImages)
            {
                var arr = new JArray { new JObject { ["type"] = "text", ["text"] = userText } };
                foreach (var b64 in imageBase64)
                    arr.Add(new JObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JObject { ["url"] = $"data:image/png;base64,{b64}" }
                    });
                userContent = arr;
            }
            else
            {
                userContent = userText;
            }

            return new JObject
            {
                ["model"] = hasImages ? VisionModel : TextModel,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = sys.ToString() },
                    new JObject { ["role"] = "user", ["content"] = userContent }
                },
                ["temperature"] = temperature,
                // z.ai docs: response_format은 json_object만 지원. 구조 강제는 위 system 프롬프트의 스키마로.
                ["response_format"] = new JObject { ["type"] = "json_object" },
                ["thinking"] = new JObject { ["type"] = thinkingBudget > 0 ? "enabled" : "disabled" }
            };
        }
    }
}
