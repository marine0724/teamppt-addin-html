using System.Collections.Generic;

namespace TeampptAddin
{
    public class DesignCritique
    {
        public int Score { get; set; }
        public Dictionary<string, int> DimensionScores { get; set; } = new Dictionary<string, int>();
        public string Verdict { get; set; } = "";
        public string Bottleneck { get; set; } = "";
        public string Suggestion { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }
}
