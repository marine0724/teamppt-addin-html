using System.Collections.Generic;

namespace TeampptAddin
{
    public class DesignCritique
    {
        public int Score { get; set; }
        public int MaterialFit { get; set; }      // 0-100, 계산값(벡터유사도+capacity) — LLM 아님
        public int DesignConcept { get; set; }    // 0-100, dimensionScores 합(비전 채점)
        public Dictionary<string, int> DimensionScores { get; set; } = new Dictionary<string, int>();
        public string Verdict { get; set; } = "";
        public string Bottleneck { get; set; } = "";
        public string Suggestion { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }
}
