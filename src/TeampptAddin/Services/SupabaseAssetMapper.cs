using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class SupabaseAssetMapper
    {
        public static HeaderAsset Map(JObject row)
        {
            var meta = row["metadata"] as JObject ?? new JObject();
            var remoteFile = row["file"]?.ToString() ?? "";
            var remoteThumb = row["thumb"]?.ToString() ?? "";

            return new HeaderAsset
            {
                SchemaVersion = 2,
                File = Path.GetFileName(remoteFile),
                Name = row["name"]?.ToString(),
                Kind = row["kind"]?.ToString() ?? "component",
                Category = row["category"]?.ToString(),
                Scope = row["scope"]?.ToString() ?? "slide",
                UseWhen = row["use_when"]?.ToString(),
                Tags = StrList(row["tags"]),
                ContentFit = StrList(row["content_fit"]),
                Colors = (meta["colors"] as JArray)?.Select(c => new AssetColor
                {
                    Role = c["role"]?.ToString(), Value = c["value"]?.ToString(),
                    Locked = c["locked"]?.Value<bool>() ?? false
                }).ToList() ?? new List<AssetColor>(),
                Fonts = (meta["fonts"] as JArray)?.Select(f => new AssetFont
                {
                    Role = f["role"]?.ToString(), Family = f["family"]?.ToString(), Weight = f["weight"]?.ToString()
                }).ToList() ?? new List<AssetFont>(),
                Slots = (meta["slots"] as JArray)?.Select(s => new AssetSlot
                {
                    Name = s["name"]?.ToString(), Type = s["type"]?.ToString(),
                    PerSlide = s["perSlide"]?.Value<bool>() ?? false
                }).ToList() ?? new List<AssetSlot>(),
                Capacity = meta["capacity"] is JObject cap
                    ? new AssetCapacity { Min = cap["min"]?.Value<int>() ?? 0, Max = cap["max"]?.Value<int>() ?? 0 }
                    : null,
                MaterialKinds = StrList(meta["material_kinds"]),
                SourceDeck = row["source_deck"]?.ToString(),
                Extra = new Dictionary<string, JToken>
                {
                    ["remote_file"] = remoteFile,
                    ["remote_thumb"] = remoteThumb
                }
            };
        }

        private static List<string> StrList(JToken t) =>
            (t as JArray)?.Select(x => x.ToString()).ToList() ?? new List<string>();
    }
}
