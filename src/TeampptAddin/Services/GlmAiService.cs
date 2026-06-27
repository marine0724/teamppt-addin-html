using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    /// <summary>
    /// z.ai GLM-Flash provider. 텍스트=glm-4.7-flash(무료).
    /// 비전은 Gemini 위임 — 무료 티어 RPM 제한으로 연속 비전 호출 시 429 폭주.
    /// </summary>
    public class GlmAiService : IAiService
    {
        private const string Endpoint = "https://api.z.ai/api/paas/v4/chat/completions";
        private static readonly HttpClient Http = new HttpClient();
        private readonly string _apiKey;

        private GeminiAiService _vision;
        private GeminiAiService Vision => _vision ?? (_vision = new GeminiAiService(AiConfig.GeminiKey));

        public GlmAiService(string apiKey) { _apiKey = apiKey; }

        public Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, string pngPathOrNull,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
        {
            var imgs = pngPathOrNull == null ? new string[0] : new[] { pngPathOrNull };
            return GenerateJsonAsync(systemPrompt, userText, imgs, responseSchema, temperature, thinkingBudget);
        }

        public async Task<string> GenerateJsonAsync(
            string systemPrompt, string userText, IEnumerable<string> pngPaths,
            JObject responseSchema, double temperature = 0.4, int thinkingBudget = 0)
        {
            var paths = (pngPaths ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrEmpty(p) && File.Exists(p))
                .ToList();

            if (paths.Count > 0)
            {
                Logger.Log($"[GLM] 비전 호출 → Gemini 위임 (이미지 {paths.Count}장)");
                return await Vision.GenerateJsonAsync(
                    systemPrompt, userText, paths, responseSchema, temperature, thinkingBudget).ConfigureAwait(false);
            }

            var body = GlmRequestBuilder.Build(systemPrompt, userText, null, responseSchema, temperature, thinkingBudget);
            var resp = await PostAsync(body, "text").ConfigureAwait(false);
            return ExtractContent(resp);
        }

        public async Task<AiRecommendation> RecommendAsync(
            string userIntent, IEnumerable<HeaderAsset> assets,
            IEnumerable<StylePalette> palettes, IEnumerable<StyleFont> fonts)
        {
            var assetList = assets.ToList();
            var paletteList = palettes.ToList();
            var fontList = fonts.ToList();
            var catalog = CatalogBuilder.Build(assetList);

            var sys = GeminiPromptBuilder.BuildSystemPrompt(catalog, paletteList, fontList);
            var user = GeminiPromptBuilder.BuildUserPrompt(userIntent);

            var json = await GenerateJsonAsync(
                sys, user, (string)null, GeminiPromptBuilder.BuildResponseSchema(), 0.7, 0).ConfigureAwait(false);
            return AiRecommendationParser.Parse(json, assetList, paletteList, fontList);
        }

        public async Task<SlideDiagnosis> DiagnoseSlideAsync(string pngPath)
        {
            var json = await GenerateJsonAsync(
                SlideDiagnosisSchema.BuildSystemPrompt(),
                "이 슬라이드를 개선점 중심으로 진단해줘.",
                pngPath,
                SlideDiagnosisSchema.BuildResponseSchema(), 0.6, 0).ConfigureAwait(false);
            return SlideDiagnosisParser.Parse(json);
        }

        private async Task<string> PostAsync(JObject body, string tag)
        {
            var bodyString = body.ToString(Formatting.None);
            const int maxAttempts = 4; // 무료 티어 레이트리밋 흡수 위해 1회 추가
            HttpResponseMessage response = null;
            string respBody = null;

            Logger.Log($"[GLM] {tag} 호출, model={body["model"]}, keyLen={_apiKey.Length}");

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var content = new StringContent(bodyString, Encoding.UTF8, "application/json");
                var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                response = await Http.SendAsync(req).ConfigureAwait(false);
                respBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Logger.Log($"[GLM] attempt {attempt}: HTTP {(int)response.StatusCode}");
                if (response.IsSuccessStatusCode) { LogUsage(respBody); return respBody; }

                var status = (int)response.StatusCode;
                bool transient = status == 503 || status == 429 || status == 500;
                if (transient && attempt < maxAttempts)
                {
                    var delayMs = 800 * (1 << (attempt - 1)); // 800,1600,3200ms
                    Logger.Log($"[GLM] 일시 오류 {status}, {delayMs}ms 후 재시도 ({attempt}/{maxAttempts})");
                    await Task.Delay(delayMs).ConfigureAwait(false);
                    continue;
                }
                throw new HttpRequestException($"GLM API 오류 ({status}): {respBody}");
            }
            throw new InvalidOperationException("GLM 재시도 소진.");
        }

        public static string ExtractContent(string responseBody)
        {
            var root = JObject.Parse(responseBody);
            var text = root["choices"]?[0]?["message"]?["content"]?.ToString();
            if (string.IsNullOrEmpty(text))
                throw new InvalidOperationException($"GLM 응답에 content 없음: {responseBody}");
            return text;
        }

        private static void LogUsage(string responseBody)
        {
            try
            {
                var u = JObject.Parse(responseBody)["usage"];
                if (u == null) return;
                Logger.Log($"[GLM] 토큰: input={u["prompt_tokens"]}, output={u["completion_tokens"]}, total={u["total_tokens"]}");
            }
            catch { }
        }
    }
}
