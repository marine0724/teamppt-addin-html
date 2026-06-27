using System;
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public class MaterialFitResult
    {
        public int Score { get; set; }            // 0-100
        public double SimilarityAvg { get; set; } // 0-1
        public double CapacityScore { get; set; } // 0-1
        public string Note { get; set; } = "";
    }

    /// <summary>
    /// 재료적합 점수(계산값, LLM 아님). 선택된 조합의 벡터유사도 평균 + 레이아웃 capacity 대비
    /// 필요 블록수 적합을 합산. 사실=벡터/계산 원칙: 토큰 0.
    /// </summary>
    public static class MaterialFitScorer
    {
        public static MaterialFitResult Score(CombinationRecommendation rec, DraftUnderstanding u)
        {
            var slots = new List<RecommendedSlot>();
            if (rec?.Slide != null) slots.Add(rec.Slide);
            if (rec?.Header != null) slots.Add(rec.Header);
            if (rec?.Layout != null) slots.Add(rec.Layout);
            if (rec?.Components != null) slots.AddRange(rec.Components);

            var sims = slots.Select(s => Sim(s?.Asset)).Where(v => v >= 0).ToList();
            double simAvg = sims.Count > 0 ? sims.Average() : 0.5;

            double capScore = 1.0;
            string capNote = "용량 제약 없음";
            var cap = rec?.Layout?.Asset?.Capacity;
            int need = u?.NeededCombination?.Component ?? 0;
            if (cap != null && need > 0)
            {
                if (need >= cap.Min && need <= cap.Max) { capScore = 1.0; capNote = $"용량 {cap.Min}-{cap.Max}, 블록 {need} — 맞음"; }
                else
                {
                    int dist = need < cap.Min ? cap.Min - need : need - cap.Max;
                    capScore = Math.Max(0.0, 1.0 - (double)dist / Math.Max(cap.Max, 1));
                    capNote = $"용량 {cap.Min}-{cap.Max}, 블록 {need} — 어긋남";
                }
            }

            int score = (int)Math.Round(100.0 * (0.6 * simAvg + 0.4 * capScore));
            score = Math.Max(0, Math.Min(100, score));
            return new MaterialFitResult
            {
                Score = score,
                SimilarityAvg = simAvg,
                CapacityScore = capScore,
                Note = $"유사도 {simAvg:F2}, {capNote}"
            };
        }

        private static double Sim(HeaderAsset a)
        {
            if (a?.Extra != null && a.Extra.TryGetValue("similarity", out var s) && s != null)
                return (double)s;
            return -1;   // 신호 없음
        }
    }
}
