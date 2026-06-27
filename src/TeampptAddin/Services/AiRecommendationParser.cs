using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class AiRecommendationParser
    {
        public static AiRecommendation Parse(
            string json,
            List<HeaderAsset> assets,
            List<StylePalette> palettes,
            List<StyleFont> fonts)
        {
            var obj = JObject.Parse(json);
            var message = obj["message"]?.ToString() ?? "";

            var assetSuggestions = new List<AssetSuggestion>();
            var assetArray = obj["assets"] as JArray;
            if (assetArray != null)
            {
                foreach (var item in assetArray)
                {
                    var file = item["file"]?.ToString();
                    var reason = item["reason"]?.ToString() ?? "";
                    var match = assets.FirstOrDefault(a =>
                        string.Equals(a.File, file, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        assetSuggestions.Add(new AssetSuggestion { Asset = match, Reason = reason });
                }
            }

            StylePalette matchedPalette = null;
            string paletteReason = "";
            var paletteObj = obj["palette"];
            if (paletteObj != null && paletteObj.Type != JTokenType.Null)
            {
                var pid = paletteObj["id"]?.ToString();
                paletteReason = paletteObj["reason"]?.ToString() ?? "";
                matchedPalette = palettes.FirstOrDefault(p =>
                    string.Equals(p.Id, pid, StringComparison.OrdinalIgnoreCase));
            }

            StyleFont matchedFont = null;
            string fontReason = "";
            var fontObj = obj["font"];
            if (fontObj != null && fontObj.Type != JTokenType.Null)
            {
                var fname = fontObj["name"]?.ToString();
                fontReason = fontObj["reason"]?.ToString() ?? "";
                matchedFont = fonts.FirstOrDefault(f =>
                    string.Equals(f.Name, fname, StringComparison.OrdinalIgnoreCase));
            }

            return new AiRecommendation
            {
                Message = message,
                Assets = assetSuggestions,
                Style = new StyleSuggestion
                {
                    Palette = matchedPalette,
                    Font = matchedFont,
                    Reason = string.Join("; ", new[] { paletteReason, fontReason }
                        .Where(r => !string.IsNullOrEmpty(r)))
                }
            };
        }
    }
}
