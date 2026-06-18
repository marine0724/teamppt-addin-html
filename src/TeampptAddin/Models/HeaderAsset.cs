using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public class HeaderAsset
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("content_fit")]
        public List<string> ContentFit { get; set; }

        [JsonProperty("use_when")]
        public string UseWhen { get; set; }

        [JsonProperty("grid_columns")]
        public int GridColumns { get; set; } = 1;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        [JsonProperty("colors")]
        public AssetColors Colors { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JToken> Extra { get; set; }
    }

    public class AssetColors
    {
        [JsonProperty("main")]
        public string Main { get; set; }

        [JsonProperty("sub1")]
        public string Sub1 { get; set; }

        [JsonProperty("sub2")]
        public string Sub2 { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JToken> Extra { get; set; }
    }
}
