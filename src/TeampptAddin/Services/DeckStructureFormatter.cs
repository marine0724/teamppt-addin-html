using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>덱 구조를 구조 요약 박스 라인으로 변환(순수). UI는 이 라인을 렌더만 한다.</summary>
    public static class DeckStructureFormatter
    {
        public static List<string> ToSummaryLines(DeckStructure d)
        {
            var lines = new List<string>();
            if (d == null) return lines;
            var ordered = d.Slides.OrderBy(s => s.Index).ToList();
            var covers = ordered.Where(s => s.Kind == "cover").ToList();
            var ends = ordered.Where(s => s.Kind == "end").ToList();
            var body = ordered.Where(s => s.Kind != "cover" && s.Kind != "end").ToList();

            int n = 1;
            if (covers.Count > 0) lines.Add($"{n++}. 표지");
            if (body.Count > 0)
            {
                lines.Add($"{n++}. 본문 ({body.Count}장)");
                foreach (var s in body)
                    lines.Add($"   - {(string.IsNullOrEmpty(s.Label) ? s.Kind : s.Label)}");
            }
            if (ends.Count > 0) lines.Add($"{n++}. 엔드");
            lines.Add($"총 슬라이드 → {d.TotalCount}장");
            return lines;
        }
    }
}
