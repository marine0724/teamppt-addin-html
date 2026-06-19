using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public static class GeminiPromptBuilder
    {
        public static string BuildSystemPrompt(
            List<CatalogEntry> catalog,
            IEnumerable<StylePalette> palettes,
            IEnumerable<StyleFont> fonts)
        {
            var catalogJson = JsonConvert.SerializeObject(catalog, Formatting.Indented);

            var paletteSummaries = palettes.Select(p => new
            {
                p.Id, p.Name, p.Mood, p.UseWhen
            });
            var palettesJson = JsonConvert.SerializeObject(paletteSummaries, Formatting.Indented);

            var fontSummaries = fonts.Select(f => new
            {
                f.Name, f.Mood, f.UseWhen
            });
            var fontsJson = JsonConvert.SerializeObject(fontSummaries, Formatting.Indented);

            return $@"너는 PPT 디자인 어시스턴트야. 사용자의 의도에 가장 적합한 에셋과 스타일을 추천해.

## 에셋 카탈로그
{catalogJson}

## 팔레트 목록
{palettesJson}

## 폰트 목록
{fontsJson}

## 응답 규칙
1. 사용자 의도에 가장 적합한 에셋 1~3개를 추천해.
2. 각 에셋 추천에 이유를 달아.
3. 가장 어울리는 팔레트 1개와 폰트 1개도 추천해.
4. 반드시 아래 JSON 형식으로만 응답해. 다른 텍스트는 포함하지 마.

```json
{{
  ""message"": ""추천 설명 메시지 (한국어, 1~2문장)"",
  ""assets"": [
    {{ ""file"": ""header_N.pptx"", ""reason"": ""추천 이유"" }}
  ],
  ""palette"": {{ ""id"": ""팔레트id"", ""reason"": ""추천 이유"" }},
  ""font"": {{ ""name"": ""폰트이름"", ""reason"": ""추천 이유"" }}
}}
```";
        }

        public static string BuildUserPrompt(string userIntent)
        {
            return userIntent;
        }
    }
}
