using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>
    /// 생성 LLM provider 선택 + 키. 시작 시 api-keys.json에서 1회 로딩.
    /// provider="gemini"로 바꾸면 전체가 Gemini로 즉시 복귀.
    /// </summary>
    public static class AiConfig
    {
        public static string Provider = "glm";
        public static string GlmKey = "";
        public static string GeminiKey = "";

        // provider가 glm이고 키가 실제로 있을 때만 GLM 사용(안전 폴백)
        public static bool UseGlm =>
            string.Equals(Provider, "glm", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(GlmKey);

        public static void Load(string assetsDir)
        {
            try
            {
                var path = Path.Combine(assetsDir, "api-keys.json");
                LoadFromJson(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Logger.Log($"[AiConfig] 로딩 실패(기본값 유지): {ex.Message}");
            }
        }

        public static void LoadFromJson(string json)
        {
            var obj = JObject.Parse(json);
            Provider = obj["provider"]?.ToString() ?? "glm";
            GlmKey = obj["glm"]?.ToString() ?? "";
            GeminiKey = obj["gemini"]?.ToString() ?? "";
            Logger.Log($"[AiConfig] provider={Provider}, glmKey={(string.IsNullOrEmpty(GlmKey) ? "없음" : "있음")}, useGlm={UseGlm}");
        }
    }
}
