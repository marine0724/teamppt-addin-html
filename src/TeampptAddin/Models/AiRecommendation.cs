using System.Collections.Generic;

namespace TeampptAddin
{
    public class AssetSuggestion
    {
        public HeaderAsset Asset { get; set; }
        public string Reason { get; set; }
    }

    public class StyleSuggestion
    {
        public StylePalette Palette { get; set; }
        public StyleFont Font { get; set; }
        public string Reason { get; set; }
    }

    public class AiRecommendation
    {
        public List<AssetSuggestion> Assets { get; set; }
        public StyleSuggestion Style { get; set; }
        public string Message { get; set; }
    }
}
