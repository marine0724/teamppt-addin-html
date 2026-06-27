// src/TeampptAddin/Services/DeckRecommendationOrchestrator.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// Phase 3 통합 브레인: 덱 구조 + 선택 컨셉 → 박스별(표지·공통헤더·본문패턴·목차·간지·엔드) 추천 + 두 배지.
    /// 멀티모달·LLM 호출은 본문 패턴 수(2~4)에만 비례. 비파괴(원본 ReadOnly).
    /// </summary>
    public class DeckRecommendationOrchestrator
    {
        private readonly DraftUnderstandingService _understand;
        private readonly CombinationCandidateProvider _candidates;
        private readonly CombinationRecommender _recommender;

        public DeckRecommendationOrchestrator(string supabaseUrl, string anonKey, string geminiKey)
        {
            var gemini = new GeminiAiService(geminiKey);
            _understand = new DraftUnderstandingService(gemini);
            var supa = new SupabaseClient(supabaseUrl, anonKey);
            _candidates = new CombinationCandidateProvider(
                new EmbeddingService(geminiKey), supa, new RemoteAssetCache(supabaseUrl, anonKey));
            _recommender = new CombinationRecommender(gemini);
        }

        public async Task<DeckRecommendation> RecommendDeckAsync(
            string deckPath,
            List<DraftProfile> profiles,
            DeckStructure structure,
            DesignConcept concept,
            Action<string> progress)
        {
            progress?.Invoke("본문 패턴을 묶고 있어요…");
            var bodyIdx = new HashSet<int>(structure.Slides
                .Where(s => string.Equals(s.Kind, "body", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Index));
            var bodyProfiles = profiles.Where(p => bodyIdx.Contains(p.SlideIndex)).ToList();
            var patterns = BodyPatternClusterer.Cluster(bodyProfiles);
            var plans = DeckBoxPlanner.Plan(structure, patterns);

            // 1) 대표 본문 장 PNG 내보내기(COM — 첫 await 전, UI STA 동기)
            progress?.Invoke("대표 장을 이미지로 추출하는 중…");
            var pngMap = DeckSlideImageExporter.Export(deckPath, patterns.Select(p => p.RepresentativeIndex));

            // 2) 본문 패턴 understanding을 먼저 모두 계산(멀티모달 1회/패턴)
            var byProfile = profiles.GroupBy(p => p.SlideIndex).ToDictionary(g => g.Key, g => g.First());
            var patternU = new Dictionary<string, DraftUnderstanding>();
            foreach (var pat in patterns)
            {
                progress?.Invoke($"본문 패턴 이해 중… (대표 {pat.RepresentativeIndex}장)");
                byProfile.TryGetValue(pat.RepresentativeIndex, out var repProfile);
                pngMap.TryGetValue(pat.RepresentativeIndex, out var png);   // 없으면 null → 텍스트-only
                var u = await _understand.UnderstandAsync(repProfile, png);
                patternU[pat.Signature] = u;
            }

            // 3) 공통 header = 첫 본문 패턴 understanding 재사용 → header 후보 top1(LLM0)
            RecommendedSlot commonHeader = null;
            DraftUnderstanding firstU = null;
            if (patterns.Count > 0 && patternU.TryGetValue(patterns[0].Signature, out firstU))
            {
                progress?.Invoke("공통 헤더를 고르는 중…");
                var headerPool = await _candidates.GetCandidatesAsync(firstU, concept);
                var top = (headerPool.TryGetValue("header", out var hs) ? hs : new List<HeaderAsset>()).FirstOrDefault();
                if (top != null)
                    commonHeader = new RecommendedSlot { Asset = top, FitNote = "공통 헤더", Confidence = Sim(top) };
            }

            // 4) 디스플레이 순서(plans)대로 박스 조립
            var boxes = new List<BoxRecommendation>();
            foreach (var plan in plans)
            {
                CombinationRecommendation rec;
                DraftUnderstanding uForFit;

                if (plan.BoxKind == "body")
                {
                    var pat = patterns.First(p => p.Signature == plan.Signature);
                    var u = patternU[pat.Signature];
                    if (u.NeededCombination == null) u.NeededCombination = new NeededCombination();
                    u.NeededCombination.Header = 0;   // 공통 header는 별도 1회 → 본문 후보에서 제외
                    progress?.Invoke($"{plan.Label} 조합을 고르는 중…");
                    var pool = await _candidates.GetCandidatesAsync(u, concept);
                    rec = await _recommender.RecommendAsync(u, pool);
                    uForFit = u;
                }
                else if (plan.BoxKind == "header")
                {
                    rec = new CombinationRecommendation();
                    if (commonHeader != null) rec.Header = commonHeader;
                    else rec.Unmet.Add("header");
                    uForFit = firstU ?? new DraftUnderstanding();
                }
                else
                {
                    // slide-box: cover/toc/section/end → synthetic understanding → slide 후보 → PickSlideOnly(LLM0)
                    var synth = new DraftUnderstanding
                    {
                        Purpose = plan.Label,
                        MatchIntent = plan.Label,
                        SlideKind = plan.BoxKind,
                        NeededCombination = new NeededCombination { Slide = 1 }
                    };
                    var pool = await _candidates.GetCandidatesAsync(synth, concept);
                    var slideList = pool.TryGetValue("slide", out var sl) ? sl : new List<HeaderAsset>();
                    rec = CombinationRecommender.PickSlideOnly(synth, slideList);
                    uForFit = synth;
                }

                boxes.Add(new BoxRecommendation
                {
                    Plan = plan,
                    Recommendation = rec,
                    MaterialFit = MaterialFitScorer.Score(rec, uForFit),
                    ConceptFit = ConceptFitScorer.Score(rec, concept)
                });
            }

            progress?.Invoke("추천을 정리했어요.");
            return new DeckRecommendation { Boxes = boxes, Concept = concept };
        }

        private static double Sim(HeaderAsset a)
        {
            if (a?.Extra != null && a.Extra.TryGetValue("similarity", out var s) && s != null)
                return (double)s;
            return 1.0;
        }
    }
}
