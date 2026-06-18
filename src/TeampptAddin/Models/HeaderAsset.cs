using System.Collections.Generic;
using Newtonsoft.Json;

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
    }
}
