using System;
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>덱 구조 + 본문 패턴 → 박스 계획. 표지→[공통헤더]→본문패턴들→엔드, toc/section은 위치대로 slide-box. 토큰0.</summary>
    public static class DeckBoxPlanner
    {
        public static List<BoxPlan> Plan(DeckStructure structure, List<BodyPattern> patterns)
        {
            var boxes = new List<BoxPlan>();
            var slides = structure?.Slides ?? new List<DeckSlideStructure>();
            var pats = (patterns ?? new List<BodyPattern>()).OrderBy(p => p.RepresentativeIndex).ToList();
            bool bodyInserted = false;

            foreach (var s in slides.OrderBy(x => x.Index))
            {
                if (string.Equals(s.Kind, "body", StringComparison.OrdinalIgnoreCase))
                {
                    if (bodyInserted) continue;
                    bodyInserted = true;

                    var allBodyIdx = pats.SelectMany(p => p.SlideIndexes).OrderBy(i => i).ToList();
                    boxes.Add(new BoxPlan
                    {
                        BoxKind = "header",
                        Label = "공통 헤더",
                        CoveredSlideIndexes = allBodyIdx,
                        RepresentativeIndex = pats.Count > 0 ? pats[0].RepresentativeIndex : (int?)null
                    });
                    foreach (var p in pats)
                        boxes.Add(new BoxPlan
                        {
                            BoxKind = "body",
                            Label = $"본문 패턴 ({p.SlideIndexes.Count}장)",
                            CoveredSlideIndexes = p.SlideIndexes.ToList(),
                            RepresentativeIndex = p.RepresentativeIndex,
                            Signature = p.Signature
                        });
                }
                else
                {
                    boxes.Add(new BoxPlan
                    {
                        BoxKind = (s.Kind ?? "").ToLowerInvariant(),
                        Label = string.IsNullOrEmpty(s.Label) ? s.Kind : s.Label,
                        CoveredSlideIndexes = new List<int> { s.Index },
                        RepresentativeIndex = s.Index
                    });
                }
            }
            return boxes;
        }
    }
}
