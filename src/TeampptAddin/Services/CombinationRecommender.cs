using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// 후보 풀 위에서 LLM이 조합(header/layout/component)을 1회 호출로 고른다.
    /// cover/end(neededCombination.slide>0)는 LLM 없이 top1 slide를 추천(단축).
    /// 사실(후보 목록)은 벡터, 판단(선택)은 LLM. 텍스트 생성 금지.
    /// </summary>
    public class CombinationRecommender
    {
        private readonly IAiService _gemini;
        public CombinationRecommender(IAiService gemini) { _gemini = gemini; }

        public async Task<CombinationRecommendation> RecommendAsync(
            DraftUnderstanding u, Dictionary<string, List<HeaderAsset>> candidatesByKind)
        {
            // cover/end → 통짜 slide 단축
            if (candidatesByKind.ContainsKey("slide"))
                return PickSlideOnly(u, candidatesByKind["slide"]);

            var userText = BuildUserText(u, candidatesByKind);
            Logger.Log("[Reco] userText↓\r\n" + userText);

            var thumbs = CollectThumbs(candidatesByKind, 3);
            var json = await _gemini.GenerateJsonAsync(
                CombinationRecommenderSchema.BuildSystemPrompt(),
                userText, thumbs,
                CombinationRecommenderSchema.BuildResponseSchema(), thinkingBudget: 768).ConfigureAwait(false);

            Logger.Log("[Reco] raw↓ " + json);
            var rec = CombinationRecommenderParser.Parse(json, candidatesByKind);
            rec.Purpose = u.Purpose;
            rec.SlideKind = u.SlideKind;
            return rec;
        }

        public static CombinationRecommendation PickSlideOnly(DraftUnderstanding u, List<HeaderAsset> slideCandidates)
        {
            var rec = new CombinationRecommendation { Purpose = u.Purpose, SlideKind = u.SlideKind };
            var top = slideCandidates?.FirstOrDefault();
            if (top == null) { rec.Unmet.Add("slide"); return rec; }
            rec.Slide = new RecommendedSlot { Asset = top, FitNote = "표지 통짜", Confidence = 1.0 };
            return rec;
        }

        public static List<string> CollectThumbs(Dictionary<string, List<HeaderAsset>> pool, int thumbTopK)
        {
            var paths = new List<string>();
            foreach (var kind in pool.Keys)
                foreach (var a in pool[kind].Take(thumbTopK))
                    if (a.Extra != null && a.Extra.TryGetValue("local_thumb", out var lt) && !string.IsNullOrEmpty(lt?.ToString()))
                        paths.Add(lt.ToString());
            return paths;
        }

        public static string BuildUserText(DraftUnderstanding u, Dictionary<string, List<HeaderAsset>> pool)
        {
            var sb = new StringBuilder();
            var nc = u.NeededCombination ?? new NeededCombination();
            sb.AppendLine($"초안 의도(purpose): {u.Purpose}");
            sb.AppendLine($"매칭 의도(matchIntent): {u.MatchIntent}");
            sb.AppendLine($"필요 조합: header {nc.Header}, layout {nc.Layout}, component {nc.Component}");
            sb.AppendLine($"재료 개수: {string.Join(", ", u.Counts.Select(kv => $"{kv.Key}:{kv.Value}"))}");
            int thumbIdx = 1;
            foreach (var kind in pool.Keys)
            {
                sb.AppendLine($"\n[{kind} 후보]");
                int i = 0;
                foreach (var a in pool[kind])
                {
                    var cap = a.Capacity != null ? $" cap={a.Capacity.Min}-{a.Capacity.Max}" : "";
                    var mk = a.MaterialKinds != null && a.MaterialKinds.Count > 0
                        ? " mk=" + string.Join("/", a.MaterialKinds) : "";
                    var thumbTag = (i < 3 && a.Extra != null && a.Extra.ContainsKey("local_thumb"))
                        ? $"[썸네일 {thumbIdx++}] " : "";
                    sb.AppendLine($"- {thumbTag}file={a.File} name={a.Name} deck={a.SourceDeck}{cap}{mk} :: {a.UseWhen}");
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
