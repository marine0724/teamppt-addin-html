using System.Collections.Generic;

namespace TeampptAddin
{
    public class RecommendedSlot
    {
        public HeaderAsset Asset { get; set; }
        public string FitNote { get; set; } = "";
        public double Confidence { get; set; }
    }

    public class RecommendationResult
    {
        public CombinationRecommendation Recommendation { get; set; }
        public RecommendationTrace Trace { get; set; }
        public string DraftPngPath { get; set; }
        public DraftUnderstanding Understanding { get; set; }
    }

    public class CombinationRecommendation
    {
        public string Purpose { get; set; } = "";
        public string SlideKind { get; set; } = "";
        public RecommendedSlot Slide { get; set; }                          // cover/end
        public RecommendedSlot Header { get; set; }                         // body/section
        public RecommendedSlot Layout { get; set; }                         // body/section
        public List<RecommendedSlot> Components { get; set; } = new List<RecommendedSlot>();
        public List<string> Unmet { get; set; } = new List<string>();       // 미충족 종류명
        public string Reasoning { get; set; } = "";
    }
}
