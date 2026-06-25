using System.Collections.Generic;

namespace TeampptAddin
{
    public class RecommendationTrace
    {
        public string UnderstandReasoning { get; set; } = "";
        public List<string> RetrieveLines { get; set; } = new List<string>();
        public string ComposeReasoning { get; set; } = "";
        public List<string> Unmet { get; set; } = new List<string>();
        public DesignCritique Critique { get; set; }

        public List<string> ToReadableLines()
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(UnderstandReasoning))
                lines.Add("① 이해: " + UnderstandReasoning);
            foreach (var r in RetrieveLines)
                lines.Add("② 검색: " + r);
            if (!string.IsNullOrEmpty(ComposeReasoning))
                lines.Add("③ 구성: " + ComposeReasoning);
            if (Unmet != null && Unmet.Count > 0)
                lines.Add("   미충족: " + string.Join(", ", Unmet));
            if (Critique != null)
            {
                lines.Add($"⑤ 검수: {Critique.Score}점 — {Critique.Verdict}");
                if (!string.IsNullOrEmpty(Critique.Bottleneck))
                    lines.Add("   병목: " + Critique.Bottleneck + " · " + Critique.Suggestion);
            }
            return lines;
        }
    }
}
