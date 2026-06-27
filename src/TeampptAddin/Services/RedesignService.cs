using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeampptAddin
{
    /// <summary>
    /// Route B 오케스트레이터: 읽기→이해→매칭→Top2→매핑→비파괴 적용→썸네일.
    /// COM(읽기·적용)과 LLM 호출이 섞이므로 UI(STA) 디스패처 스레드에서 호출해야 한다.
    /// 따라서 내부 await에 ConfigureAwait(false)를 쓰지 않는다(COM 연속 실행을 UI 스레드에 유지).
    /// </summary>
    public class RedesignService
    {
        private readonly IAiService _gemini;
        private readonly DraftUnderstandingService _understand;
        private readonly DraftMatchService _match;
        private readonly SlotMapper _mapper;
        private readonly RemoteAssetCache _assetCache;

        public RedesignService(string supabaseUrl, string anonKey, string geminiKey)
        {
            _gemini = AiServiceFactory.CreateGenerative();
            _understand = new DraftUnderstandingService(_gemini);
            _match = new DraftMatchService(new EmbeddingService(geminiKey), new SupabaseClient(supabaseUrl, anonKey));
            _mapper = new SlotMapper(_gemini);
            _assetCache = new RemoteAssetCache(supabaseUrl, anonKey);
        }

        public async Task<List<RedesignPreview>> RunAsync(Action<string> progress)
        {
            progress("초안 읽는 중…");
            var profile = DraftSlideReader.ReadCurrentSlide();
            if (profile == null) throw new InvalidOperationException("활성 슬라이드를 찾을 수 없습니다.");

            var png = SlideCaptureService.CaptureCurrentSlide()?.PngPath;

            progress("초안 이해하는 중…");
            var u = await _understand.UnderstandAsync(profile, png);

            progress("맞는 에셋 찾는 중…");
            var candidates = await _match.FindCandidatesAsync(u);
            var top2 = candidates.Take(2).ToList();
            if (top2.Count == 0) throw new InvalidOperationException("매칭되는 에셋 후보가 없습니다.");

            progress("시안 만드는 중…");
            var previews = new List<RedesignPreview>();
            foreach (var asset in top2)
            {
                var pptx = await _assetCache.GetPptxAsync(asset.File);
                var preview = await RedesignApplier.BuildPreviewAsync(
                    profile.SlideIndex, profile, u, asset, pptx, _mapper);
                previews.Add(preview);
            }
            return previews;
        }

        /// <summary>선택분만 남기고 나머지 복제 슬라이드를 삭제. 인덱스 밀림 무관하게 SlideID로 식별.</summary>
        public void Commit(RedesignPreview chosen, List<RedesignPreview> all)
        {
            var pres = Globals.Application.ActivePresentation;
            foreach (var p in all)
            {
                if (p == chosen) continue;
                try { pres.Slides.FindBySlideID(p.SlideId).Delete(); }
                catch (Exception ex) { Logger.Log($"[Redesign] 복제 삭제 실패 id={p.SlideId}: {ex.Message}"); }
            }
        }
    }
}
