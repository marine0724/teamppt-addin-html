// src/TeampptAddin/Services/ConceptFitScorer.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>컨셉적합(계산값, 토큰0): 에셋 Tags ∩ concept.StyleTags 커버 + 색/폰트 Role 커버 가중합.</summary>
    public static class ConceptFitScorer
    {
        public static ConceptFitResult Score(CombinationRecommendation rec, DesignConcept concept)
        {
            var assets = new List<HeaderAsset>();
            if (rec?.Slide?.Asset != null) assets.Add(rec.Slide.Asset);
            if (rec?.Header?.Asset != null) assets.Add(rec.Header.Asset);
            if (rec?.Layout?.Asset != null) assets.Add(rec.Layout.Asset);
            if (rec?.Components != null)
                assets.AddRange(rec.Components.Where(s => s?.Asset != null).Select(s => s.Asset));
            return ScoreAssets(assets, concept);
        }

        public static ConceptFitResult Score(HeaderAsset asset, DesignConcept concept)
            => ScoreAssets(asset != null ? new List<HeaderAsset> { asset } : new List<HeaderAsset>(), concept);

        private static ConceptFitResult ScoreAssets(List<HeaderAsset> assets, DesignConcept concept)
        {
            if (assets == null || assets.Count == 0)
                return new ConceptFitResult { Score = 0, Note = "선택 에셋 없음" };

            var tags = Union(assets.SelectMany(a => a.Tags ?? new List<string>()));
            var colorRoles = Union(assets.SelectMany(a => (a.Colors ?? new List<AssetColor>()).Select(c => c.Role)));
            var fontRoles = Union(assets.SelectMany(a => (a.Fonts ?? new List<AssetFont>()).Select(f => f.Role)));

            double tagScore = Cover(concept?.StyleTags, tags);
            double colorScore = Cover(concept?.Colors?.Keys, colorRoles);
            double fontScore = Cover(concept?.Fonts?.Keys, fontRoles);

            double weighted = 0.5 * tagScore + 0.25 * colorScore + 0.25 * fontScore;
            int score = Math.Max(0, Math.Min(100, (int)Math.Round(100.0 * weighted)));
            return new ConceptFitResult
            {
                Score = score,
                Note = $"스타일 {tagScore:P0}, 색 {colorScore:P0}, 폰트 {fontScore:P0}"
            };
        }

        private static HashSet<string> Union(IEnumerable<string> values)
            => new HashSet<string>((values ?? Enumerable.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant()));

        private static double Cover(IEnumerable<string> needed, HashSet<string> have)
        {
            var need = (needed ?? Enumerable.Empty<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim().ToLowerInvariant())
                .Distinct().ToList();
            if (need.Count == 0) return 0.5;   // 신호 없음 → 중립
            int matched = need.Count(n => have.Contains(n));
            return (double)matched / need.Count;
        }
    }
}
