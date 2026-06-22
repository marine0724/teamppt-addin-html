using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class AssetRowBuilder
    {
        public static JObject Build(AssetUnderstanding u, float[] embedding, string embedText,
            string filePath, string thumbPath, string sourceDeck)
        {
            var a = u.Asset;
            var metadata = new JObject
            {
                ["colors"] = JArray.FromObject(a.Colors ?? new List<AssetColor>()),
                ["fonts"] = JArray.FromObject(a.Fonts ?? new List<AssetFont>()),
                ["slots"] = JArray.FromObject(a.Slots ?? new List<AssetSlot>())
            };

            var vec = "[" + string.Join(",", embedding.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";

            return new JObject
            {
                ["file"] = filePath,
                ["thumb"] = thumbPath,
                ["name"] = a.Name,
                ["category"] = a.Category,
                ["kind"] = a.Kind,
                ["scope"] = a.Scope,
                ["tags"] = JArray.FromObject(a.Tags ?? new List<string>()),
                ["use_when"] = a.UseWhen,
                ["content_fit"] = JArray.FromObject(a.ContentFit ?? new List<string>()),
                ["metadata"] = metadata,
                ["embed_text"] = embedText,
                ["embedding"] = vec,
                ["source_deck"] = sourceDeck
            };
        }
    }
}
