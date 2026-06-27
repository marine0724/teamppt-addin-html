// src/TeampptAddin/Services/BodyPatternClusterer.cs
using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>본문 DraftProfile들을 도형 시그니처(kind 멀티셋 버킷 + 대략 열 수)로 묶는다. 토큰0.</summary>
    public static class BodyPatternClusterer
    {
        public static List<BodyPattern> Cluster(List<DraftProfile> bodyProfiles)
        {
            var profiles = bodyProfiles ?? new List<DraftProfile>();
            var order = new List<BodyPattern>();
            var bySig = new Dictionary<string, BodyPattern>();
            foreach (var p in profiles)
            {
                var sig = Signature(p);
                if (!bySig.TryGetValue(sig, out var pat))
                {
                    pat = new BodyPattern { Signature = sig };
                    bySig[sig] = pat;
                    order.Add(pat);
                }
                pat.SlideIndexes.Add(p.SlideIndex);
            }
            foreach (var pat in order)
            {
                pat.SlideIndexes = pat.SlideIndexes.OrderBy(i => i).ToList();
                pat.RepresentativeIndex = pat.SlideIndexes.FirstOrDefault();
            }
            return order.OrderBy(g => g.RepresentativeIndex).ToList();
        }

        public static string Signature(DraftProfile p)
        {
            int t = 0, i = 0, b = 0, c = 0;
            foreach (var s in p.Shapes ?? new List<DraftShape>())
            {
                switch (s.Kind)
                {
                    case "text": t++; break;
                    case "image": i++; break;
                    case "table": b++; break;
                    case "chart": c++; break;
                }
            }
            return $"t{Bucket(t)}i{Bucket(i)}b{Bucket(b)}c{Bucket(c)}|col{EstimateColumns(p)}";
        }

        private static string Bucket(int n) => n >= 4 ? "4+" : n.ToString();

        /// <summary>도형 가로 중심을 1/3 밴드로 나눠 점유 밴드 수 = 대략 열 수.</summary>
        public static int EstimateColumns(DraftProfile p)
        {
            float w = p.SlideWidth > 0 ? p.SlideWidth : 1f;
            var thirds = new HashSet<int>();
            foreach (var s in p.Shapes ?? new List<DraftShape>())
            {
                if (s.Width <= 0) continue;
                float center = (s.Left + s.Width / 2f) / w;
                int third = center < 1f / 3f ? 0 : center < 2f / 3f ? 1 : 2;
                thirds.Add(third);
            }
            return thirds.Count;
        }
    }
}
